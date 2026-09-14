using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Cancels queued or finished-but-not-yet-applied terrain work, e.g. when the chunk it belongs to is
/// unloaded before its data arrives. Safe to read from any thread.
/// </summary>
public sealed class WorkToken
{
    private volatile bool cancelled;

    public bool IsCancelled => cancelled;

    public void Cancel()
    {
        cancelled = true;
    }
}

/// <summary>
/// A small fixed set of background threads that run terrain work (chunk heights, meshes, object placement),
/// instead of starting a new thread for every request. Each job carries a priority - normally its chunk's
/// distance to the viewer - that is re-evaluated every time a thread picks its next job, so the chunks
/// next to the player are generated first even while far ones are still waiting, and keep being picked
/// first as the player moves. A job whose <see cref="WorkToken"/> was cancelled while it waited (its
/// chunk was unloaded) is dropped instead of run.
/// </summary>
public sealed class TerrainWorkerPool : IDisposable
{
    private sealed class Job
    {
        public Action Work;
        public Func<float> Priority;
        public WorkToken Token;
        public long Order;
    }

    // The pool whose worker is running on this thread (null on any other thread), and the priority of the job it runs.
    [ThreadStatic] private static TerrainWorkerPool current;
    [ThreadStatic] private static float currentPriority;

    // For() calls with items nobody has taken yet (guarded by gate).
    private readonly List<Batch> openBatches = new List<Batch>();

    private readonly List<Job> queue = new List<Job>();
    private readonly object gate = new object();
    private readonly Thread[] threads;
    private bool stopping;
    private long order;

    /// <param name="threadCount">Worker threads (at least 1).</param>
    public TerrainWorkerPool(int threadCount, string name = "Terrain Worker")
    {
        threads = new Thread[Mathf.Max(1, threadCount)];
        for (int i = 0; i < threads.Length; i++)
        {
            // Background: never keeps the game (or the editor leaving Play mode) from shutting down.
            threads[i] = new Thread(Run)
            {
                IsBackground = true,
                Name = name + " " + (i + 1),
                Priority = System.Threading.ThreadPriority.BelowNormal,
            };
            threads[i].Start();
        }
    }

    /// <summary>A sensible thread count for this machine: all cores but one (the main thread's), at most 8.</summary>
    public static int DefaultThreadCount => Mathf.Clamp(Environment.ProcessorCount - 1, 1, 8);

    public int ThreadCount => threads.Length;

    /// <summary>The pool whose worker thread this is, or null when called from any other thread.</summary>
    public static TerrainWorkerPool Current => current;

    /// <summary>
    /// Runs <paramref name="body"/> for every index from 0 to <paramref name="count"/> - 1 and returns when all are
    /// done, shared with this pool's other workers: before starting a new job, and between the items of their own
    /// For calls, workers take items of the most urgent For in progress (by its job's priority, e.g. the chunk
    /// nearest the player). So the chunk under the player gets every thread as soon as they can help, instead of
    /// one each - and with nothing more urgent around, everything runs as before, without any extra threads.
    /// <paramref name="body"/> must be safe to run for different indices at once. The first exception it throws is
    /// rethrown here once the other indices are done. Called from any other thread, it simply runs them in order.
    /// </summary>
    public void For(int count, Action<int> body)
    {
        if (count <= 0)
            return;
        if (current != this || threads.Length < 2)
        {
            for (int i = 0; i < count; i++)
                body(i);
            return;
        }

        var batch = new Batch(count, body, currentPriority);
        lock (gate)
        {
            openBatches.Add(batch);
            Monitor.PulseAll(gate);
        }

        // Work on the most urgent open batch (this one, unless another is more urgent) until this one's items are all taken.
        while (batch.HasOpenItems)
            (MostUrgentBatch(batch) ?? batch).RunOne();
        batch.Wait();
        if (batch.Error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(batch.Error).Throw();
    }

    /// <summary>The open batch to help first: the lowest priority value (<paramref name="own"/> on a tie); null when none is listed.</summary>
    private Batch MostUrgentBatch(Batch own)
    {
        lock (gate)
        {
            Batch best = OpenBatchUnderLock();
            return best != null && own.HasOpenItems && own.Priority <= best.Priority ? own : best;
        }
    }

    /// <summary>The shared state of one <see cref="For"/> call.</summary>
    private sealed class Batch
    {
        public readonly float Priority;
        private readonly int count;
        private readonly Action<int> body;
        private int next = -1;
        private int done;
        public Exception Error;

        public Batch(int count, Action<int> body, float priority)
        {
            this.count = count;
            this.body = body;
            Priority = priority;
        }

        public bool HasOpenItems => Volatile.Read(ref next) + 1 < count;

        /// <summary>Takes and runs one item, if any is left.</summary>
        public void RunOne()
        {
            int index = Interlocked.Increment(ref next);
            if (index >= count)
                return;
            try
            {
                body(index);
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref Error, e, null);
            }

            if (Interlocked.Increment(ref done) == count)
            {
                lock (this)
                    Monitor.PulseAll(this);
            }
        }

        /// <summary>Waits until every item has run (all taken, some maybe still running on other workers).</summary>
        public void Wait()
        {
            lock (this)
            {
                while (Volatile.Read(ref done) < count)
                    Monitor.Wait(this);
            }
        }
    }

    /// <summary>Jobs waiting for a thread (not counting the ones running).</summary>
    public int PendingCount
    {
        get
        {
            lock (gate)
                return queue.Count;
        }
    }

    /// <summary>
    /// Queues <paramref name="work"/>. Lower <paramref name="priority"/> values run first (null = 0); jobs with
    /// equal priority run in the order they were queued. The priority function is called from worker threads
    /// while the queue is locked, so it must be quick and thread-safe (plain arithmetic on values that
    /// may change, like the viewer position, is fine).
    /// </summary>
    public void Enqueue(Action work, Func<float> priority = null, WorkToken token = null)
    {
        if (work == null)
            return;

        lock (gate)
        {
            if (stopping)
                return;
            queue.Add(new Job { Work = work, Priority = priority, Token = token, Order = order++ });
            Monitor.Pulse(gate);
        }
    }

    private void Run()
    {
        current = this;
        while (true)
        {
            Job job = null;
            Batch help = null;
            float priority = 0f;
            lock (gate)
            {
                while (true)
                {
                    if (stopping)
                        return;
                    // A chunk already being worked on finishes before a new one starts.
                    help = OpenBatchUnderLock();
                    if (help != null)
                        break;
                    if (queue.Count > 0)
                    {
                        job = TakeNext(out priority);
                        if (job != null)
                            break;
                        continue;
                    }
                    Monitor.Wait(gate);
                }
            }

            if (help != null)
            {
                help.RunOne();
                continue;
            }

            if (job.Token != null && job.Token.IsCancelled)
                continue;

            currentPriority = priority;
            try
            {
                job.Work();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>The most urgent batch with items left (dropping finished ones); null when none. Called under the lock.</summary>
    private Batch OpenBatchUnderLock()
    {
        Batch best = null;
        for (int i = openBatches.Count - 1; i >= 0; i--)
        {
            Batch batch = openBatches[i];
            if (!batch.HasOpenItems)
            {
                openBatches.RemoveAt(i);
                continue;
            }
            if (best == null || batch.Priority < best.Priority)
                best = batch;
        }
        return best;
    }

    /// <summary>Removes and returns the most urgent job (dropping cancelled ones on the way). Called under the lock.</summary>
    private Job TakeNext(out float priorityOfNext)
    {
        priorityOfNext = 0f;
        int best = -1;
        float bestPriority = float.MaxValue;
        long bestOrder = long.MaxValue;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            Job job = queue[i];
            if (job.Token != null && job.Token.IsCancelled)
            {
                RemoveAt(i);
                if (best == queue.Count)
                    best = i;   // the swapped-in job was the best so far
                continue;
            }

            float priority = 0f;
            if (job.Priority != null)
            {
                try
                {
                    priority = job.Priority();
                }
                catch (Exception)
                {
                    priority = float.MaxValue;
                }
            }

            if (priority < bestPriority || (priority == bestPriority && job.Order < bestOrder))
            {
                best = i;
                bestPriority = priority;
                bestOrder = job.Order;
            }
        }

        if (best < 0)
            return null;
        Job next = queue[best];
        RemoveAt(best);
        priorityOfNext = bestPriority;
        return next;
    }

    private void RemoveAt(int index)
    {
        int last = queue.Count - 1;
        queue[index] = queue[last];
        queue.RemoveAt(last);
    }

    /// <summary>Stops the threads once their current job ends; queued jobs are discarded.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            stopping = true;
            queue.Clear();
            openBatches.Clear();
            Monitor.PulseAll(gate);
        }
    }
}
