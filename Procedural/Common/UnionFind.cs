namespace ProceduralCommon
{
    /// <summary>Disjoint sets with path halving and union by size (for Kruskal's minimum spanning tree and region merging).</summary>
    public sealed class UnionFind
    {
        private readonly int[] parent;
        private readonly int[] size;

        public UnionFind(int count)
        {
            parent = new int[count];
            size = new int[count];
            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }
            Sets = count;
        }

        /// <summary>How many separate sets remain.</summary>
        public int Sets { get; private set; }

        public int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        /// <summary>Joins the sets of a and b. False when they were already joined.</summary>
        public bool Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb)
                return false;
            if (size[ra] < size[rb])
            {
                int t = ra;
                ra = rb;
                rb = t;
            }
            parent[rb] = ra;
            size[ra] += size[rb];
            Sets--;
            return true;
        }

        public bool Connected(int a, int b) => Find(a) == Find(b);
    }
}
