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
        while (true)
        {
            Job job;
            lock (gate)
            {
                while (queue.Count == 0 && !stopping)
                    Monitor.Wait(gate);
                if (stopping)
                    return;
                job = TakeNext();
            }

            if (job == null || (job.Token != null && job.Token.IsCancelled))
                continue;

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

    /// <summary>Removes and returns the most urgent job (dropping cancelled ones on the way). Called under the lock.</summary>
    private Job TakeNext()
    {
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
            Monitor.PulseAll(gate);
        }
    }
}
