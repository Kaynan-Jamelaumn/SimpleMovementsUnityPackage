using System;

namespace ProceduralCommon
{
    /// <summary>
    /// A min-heap of items keyed by a float priority. Items with equal priority come out in the order they were
    /// pushed, so searches built on it (A*, Dijkstra, flood fills) are deterministic.
    /// Plain C#: safe to use on worker threads.
    /// </summary>
    public sealed class BinaryHeap<T>
    {
        private struct Node
        {
            public T Item;
            public float Priority;
            public long Order;
        }

        private Node[] nodes;
        private int count;
        private long order;

        public BinaryHeap(int capacity = 64)
        {
            nodes = new Node[Math.Max(4, capacity)];
        }

        public int Count => count;

        public void Clear()
        {
            count = 0;
            order = 0;
        }

        public void Push(T item, float priority)
        {
            if (count == nodes.Length)
                Array.Resize(ref nodes, nodes.Length * 2);

            var node = new Node { Item = item, Priority = priority, Order = order++ };
            int i = count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (!Less(node, nodes[parent]))
                    break;
                nodes[i] = nodes[parent];
                i = parent;
            }
            nodes[i] = node;
        }

        public float PeekPriority()
        {
            if (count == 0)
                throw new InvalidOperationException("The heap is empty.");
            return nodes[0].Priority;
        }

        public T Pop()
        {
            return Pop(out _);
        }

        public T Pop(out float priority)
        {
            if (count == 0)
                throw new InvalidOperationException("The heap is empty.");

            Node top = nodes[0];
            priority = top.Priority;
            Node last = nodes[--count];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= count)
                    break;
                if (child + 1 < count && Less(nodes[child + 1], nodes[child]))
                    child++;
                if (!Less(nodes[child], last))
                    break;
                nodes[i] = nodes[child];
                i = child;
            }
            if (count > 0)
                nodes[i] = last;
            nodes[count] = default;
            return top.Item;
        }

        private static bool Less(in Node a, in Node b)
        {
            return a.Priority < b.Priority || (a.Priority == b.Priority && a.Order < b.Order);
        }
    }
}
