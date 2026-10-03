using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum RoomShape
    {
        Rectangle,
        LShape,
        TShape,
        Cross,
        Circle,
        Composite,
        PillaredHall,
        Ruined,
        /// <summary>A rectangle with its corners cut off.</summary>
        Octagon,
        /// <summary>A cloister: a ring of floor around a solid core.</summary>
        Ring,
        /// <summary>A long room ending in a half circle (a chapel's apse).</summary>
        Apse,
        /// <summary>A square turned 45 degrees.</summary>
        Diamond,
    }

    /// <summary>A room's floor plan in its own w x h box (index = x + y * W, y = 0 south).</summary>
    public sealed class ShapeMask
    {
        public int W, H;
        public bool[] Floor;
        public bool[] Pillar;
        public RoomShape Shape;

        public ShapeMask(int w, int h)
        {
            W = w;
            H = h;
            Floor = new bool[w * h];
            Pillar = new bool[w * h];
        }

        public int FloorCount
        {
            get
            {
                int n = 0;
                foreach (bool f in Floor)
                    if (f)
                        n++;
                return n;
            }
        }

        public bool Used(int i) => Floor[i] || Pillar[i];
    }

    /// <summary>
    /// Generates room floor plans: rectangles, L / T / cross shapes (randomly rotated), circles and ellipses,
    /// composites of overlapping rectangles, pillared halls, ruined (eroded) rooms, octagons, cloisters (a ring around a
    /// solid core), apses (a room ending in a half circle) and diamonds. Every plan is 4-connected.
    /// </summary>
    public static class RoomShapes
    {
        public static int MinSide(RoomShape shape)
        {
            switch (shape)
            {
                case RoomShape.Rectangle: return 3;
                case RoomShape.PillaredHall: return 8;
                case RoomShape.Ruined: return 4;
                case RoomShape.Octagon: return 6;
                case RoomShape.Ring: return 9;
                case RoomShape.Apse: return 6;
                case RoomShape.Diamond: return 7;
                default: return 5;
            }
        }

        public static ShapeMask Generate(RoomShape shape, int w, int h, DungeonRandom rng, float erosion = 0.35f)
        {
            w = Mathf.Max(3, w);
            h = Mathf.Max(3, h);
            if (Mathf.Min(w, h) < MinSide(shape))
                shape = RoomShape.Rectangle;

            ShapeMask m;
            switch (shape)
            {
                case RoomShape.LShape: m = LShape(w, h, rng); break;
                case RoomShape.TShape: m = Rotated(w, h, rng, (ww, hh) => TShape(ww, hh, rng)); break;
                case RoomShape.Cross: m = Cross(w, h, rng); break;
                case RoomShape.Circle: m = Circle(w, h); break;
                case RoomShape.Composite: m = Composite(w, h, rng); break;
                case RoomShape.PillaredHall: m = PillaredHall(w, h, rng); break;
                case RoomShape.Ruined: m = Ruined(w, h, rng, erosion); break;
                case RoomShape.Octagon: m = Octagon(w, h); break;
                case RoomShape.Ring: m = Ring(w, h, rng); break;
                case RoomShape.Apse: m = Rotated(w, h, rng, Apse); break;
                case RoomShape.Diamond: m = Diamond(w, h); break;
                default: m = Rect(w, h); break;
            }
            KeepLargestComponent(m);
            if (shape == RoomShape.Ring)
                RestoreCore(m);
            if (m.FloorCount < 4)
                m = Rect(w, h);
            m.Shape = shape;
            return m;
        }

        private static ShapeMask Rect(int w, int h)
        {
            var m = new ShapeMask(w, h);
            for (int i = 0; i < m.Floor.Length; i++)
                m.Floor[i] = true;
            return m;
        }

        private static ShapeMask LShape(int w, int h, DungeonRandom rng)
        {
            ShapeMask m = Rect(w, h);
            int cutW = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.35f, 0.6f)), 1, w - 2);
            int cutH = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.35f, 0.6f)), 1, h - 2);
            int corner = rng.Range(0, 4);
            int x0 = (corner & 1) == 0 ? 0 : w - cutW;
            int y0 = (corner & 2) == 0 ? 0 : h - cutH;
            for (int y = y0; y < y0 + cutH; y++)
                for (int x = x0; x < x0 + cutW; x++)
                    m.Floor[x + y * w] = false;
            return m;
        }

        private static ShapeMask TShape(int w, int h, DungeonRandom rng)
        {
            var m = new ShapeMask(w, h);
            int barH = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.3f, 0.45f)), 2, h - 2);
            int stemW = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.3f, 0.5f)), 2, w);
            int stemX = (w - stemW) / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    m.Floor[x + y * w] = y >= h - barH || (x >= stemX && x < stemX + stemW);
            return m;
        }

        private static ShapeMask Cross(int w, int h, DungeonRandom rng)
        {
            var m = new ShapeMask(w, h);
            int barH = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.38f, 0.55f)), 2, h);
            int barW = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.38f, 0.55f)), 2, w);
            int bx = (w - barW) / 2, by = (h - barH) / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    m.Floor[x + y * w] = (y >= by && y < by + barH) || (x >= bx && x < bx + barW);
            return m;
        }

        private static ShapeMask Circle(int w, int h)
        {
            var m = new ShapeMask(w, h);
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float rx = w * 0.5f, ry = h * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    m.Floor[x + y * w] = dx * dx + dy * dy <= 1.05f;
                }
            }
            return m;
        }

        private static ShapeMask Composite(int w, int h, DungeonRandom rng)
        {
            var m = new ShapeMask(w, h);
            int bw = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.55f, 0.85f)), 3, w);
            int bh = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.55f, 0.85f)), 3, h);
            FillRect(m, rng.Range(0, w - bw + 1), rng.Range(0, h - bh + 1), bw, bh);
            int extra = rng.Range(1, 3);
            for (int e = 0; e < extra; e++)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    int rw = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.35f, 0.7f)), 2, w);
                    int rh = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.35f, 0.7f)), 2, h);
                    int rx = rng.Range(0, w - rw + 1), ry = rng.Range(0, h - rh + 1);
                    int overlap = 0;
                    for (int y = ry; y < ry + rh; y++)
                        for (int x = rx; x < rx + rw; x++)
                            if (m.Floor[x + y * w])
                                overlap++;
                    if (overlap < 2)
                        continue;
                    FillRect(m, rx, ry, rw, rh);
                    break;
                }
            }
            return m;
        }

        private static ShapeMask PillaredHall(int w, int h, DungeonRandom rng)
        {
            ShapeMask m = Rect(w, h);
            int step = rng.Range(3, 5);
            int margin = 2;
            for (int y = margin; y < h - margin; y += step)
            {
                for (int x = margin; x < w - margin; x += step)
                {
                    int i = x + y * w;
                    m.Floor[i] = false;
                    m.Pillar[i] = true;
                }
            }
            return m;
        }

        private static ShapeMask Ruined(int w, int h, DungeonRandom rng, float erosion)
        {
            ShapeMask m = rng.Chance(0.5f) ? Rect(w, h) : Composite(w, h, rng);
            int before = m.FloorCount;
            for (int pass = 0; pass < 2; pass++)
            {
                var remove = new List<int>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = x + y * w;
                        if (!m.Floor[i])
                            continue;
                        bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                                    !m.Floor[i - 1] || !m.Floor[i + 1] || !m.Floor[i - w] || !m.Floor[i + w];
                        if (edge && rng.Chance(erosion * 0.6f))
                            remove.Add(i);
                    }
                }
                foreach (int i in remove)
                    m.Floor[i] = false;
            }
            // A few collapsed blocks inside.
            for (int y = 2; y < h - 2; y++)
            {
                for (int x = 2; x < w - 2; x++)
                {
                    int i = x + y * w;
                    if (m.Floor[i] && rng.Chance(erosion * 0.04f))
                    {
                        m.Floor[i] = false;
                        m.Pillar[i] = true;
                    }
                }
            }
            KeepLargestComponent(m);
            if (m.FloorCount < before / 2)
                return Rect(w, h);
            return m;
        }

        private static ShapeMask Octagon(int w, int h)
        {
            var m = new ShapeMask(w, h);
            int cut = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(w, h) * 0.29f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int rx = w - 1 - x, ry = h - 1 - y;
                    m.Floor[x + y * w] = x + y >= cut && rx + y >= cut && x + ry >= cut && rx + ry >= cut;
                }
            return m;
        }

        private static ShapeMask Diamond(int w, int h)
        {
            var m = new ShapeMask(w, h);
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float rx = w * 0.5f, ry = h * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    m.Floor[x + y * w] = Mathf.Abs(x - cx) / rx + Mathf.Abs(y - cy) / ry <= 1.05f;
            return m;
        }

        /// <summary>A half circle on the north end of a rectangle (rotated by the caller).</summary>
        private static ShapeMask Apse(int w, int h)
        {
            var m = new ShapeMask(w, h);
            float r = w * 0.5f;
            float cx = (w - 1) * 0.5f;
            float start = h - r;   // where the round end begins
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y < start)
                    {
                        m.Floor[x + y * w] = true;
                        continue;
                    }
                    float dx = (x - cx) / r, dy = (y - start + 0.5f) / r;
                    m.Floor[x + y * w] = dx * dx + dy * dy <= 1.05f;
                }
            }
            return m;
        }

        /// <summary>A cloister: floor all round a solid core (the core is pillar - kept solid, never carved).</summary>
        private static ShapeMask Ring(int w, int h, DungeonRandom rng)
        {
            ShapeMask m = rng.Chance(0.5f) ? Octagon(w, h) : Rect(w, h);
            int coreW = Mathf.Clamp(Mathf.RoundToInt(w * rng.Range(0.3f, 0.42f)), 2, w - 6);
            int coreH = Mathf.Clamp(Mathf.RoundToInt(h * rng.Range(0.3f, 0.42f)), 2, h - 6);
            int x0 = (w - coreW) / 2, y0 = (h - coreH) / 2;
            for (int y = y0; y < y0 + coreH; y++)
                for (int x = x0; x < x0 + coreW; x++)
                {
                    m.Floor[x + y * w] = false;
                    m.Pillar[x + y * w] = true;
                }
            return m;
        }

        /// <summary>The cloister's core stays solid pillar even where it no longer touches floor.</summary>
        private static void RestoreCore(ShapeMask m)
        {
            int n = m.Floor.Length;
            var core = new bool[n];
            for (int i = 0; i < n; i++)
                core[i] = !m.Floor[i];
            // Only cells enclosed by floor on both axes are core (the outside of the room stays empty).
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int i = x + y * m.W;
                    if (!core[i])
                        continue;
                    bool l = false, r = false, d = false, u = false;
                    for (int k = x - 1; k >= 0 && !l; k--) l = m.Floor[k + y * m.W];
                    for (int k = x + 1; k < m.W && !r; k++) r = m.Floor[k + y * m.W];
                    for (int k = y - 1; k >= 0 && !d; k--) d = m.Floor[x + k * m.W];
                    for (int k = y + 1; k < m.H && !u; k++) u = m.Floor[x + k * m.W];
                    if (l && r && d && u)
                        m.Pillar[i] = true;
                }
            }
        }

        private static void FillRect(ShapeMask m, int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    m.Floor[x + y * m.W] = true;
        }

        /// <summary>Builds a shape in a random rotation, keeping the requested bounding box.</summary>
        private static ShapeMask Rotated(int w, int h, DungeonRandom rng, System.Func<int, int, ShapeMask> build)
        {
            int turns = rng.Range(0, 4);
            bool swap = (turns & 1) == 1;
            ShapeMask m = swap ? build(h, w) : build(w, h);
            for (int t = 0; t < turns; t++)
                m = RotateCW(m);
            return m;
        }

        private static ShapeMask RotateCW(ShapeMask m)
        {
            var r = new ShapeMask(m.H, m.W);
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    // (x, y) -> (y, W - 1 - x)
                    int nx = y, ny = m.W - 1 - x;
                    r.Floor[nx + ny * r.W] = m.Floor[x + y * m.W];
                    r.Pillar[nx + ny * r.W] = m.Pillar[x + y * m.W];
                }
            }
            return r;
        }

        /// <summary>Keeps only the largest 4-connected group of floor cells (pillars inside it stay).</summary>
        public static void KeepLargestComponent(ShapeMask m)
        {
            int n = m.Floor.Length;
            var label = new int[n];
            for (int i = 0; i < n; i++)
                label[i] = -1;
            int best = -1, bestSize = 0, next = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (!m.Floor[s] || label[s] >= 0)
                    continue;
                int size = 0;
                label[s] = next;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    size++;
                    int x = c % m.W, y = c / m.W;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Dir4Util.DX[d], ny = y + Dir4Util.DY[d];
                        if (nx < 0 || ny < 0 || nx >= m.W || ny >= m.H)
                            continue;
                        int ni = nx + ny * m.W;
                        if (m.Floor[ni] && label[ni] < 0)
                        {
                            label[ni] = next;
                            stack.Push(ni);
                        }
                    }
                }
                if (size > bestSize)
                {
                    bestSize = size;
                    best = next;
                }
                next++;
            }
            for (int i = 0; i < n; i++)
                if (m.Floor[i] && label[i] != best)
                    m.Floor[i] = false;
            // Pillars must stay surrounded by floor: drop pillars that lost all floor neighbours.
            for (int i = 0; i < n; i++)
            {
                if (!m.Pillar[i])
                    continue;
                int x = i % m.W, y = i / m.W;
                bool touching = false;
                for (int d = 0; d < 4 && !touching; d++)
                {
                    int nx = x + Dir4Util.DX[d], ny = y + Dir4Util.DY[d];
                    touching = nx >= 0 && ny >= 0 && nx < m.W && ny < m.H && m.Floor[nx + ny * m.W];
                }
                if (!touching)
                    m.Pillar[i] = false;
            }
        }
    }
}
