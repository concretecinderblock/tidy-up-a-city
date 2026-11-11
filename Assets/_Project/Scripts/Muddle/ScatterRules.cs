using System.Collections.Generic;
using UnityEngine;

namespace TidyUp.Muddle
{
    /// <summary>Pure scatter helpers (EditMode-testable).</summary>
    public static class ScatterRules
    {
        /// <summary>
        /// Things that would stand awkwardly lie down when scattered: tall things (bottles, candles, novels) and
        /// thin upright slabs (square picture books, board books).
        /// </summary>
        public static bool ShouldLieDown(Vector3 size) =>
            size.y > 1.3f * Mathf.Max(size.x, size.z)
            || (size.y >= Mathf.Max(size.x, size.z) && size.y > 3f * Mathf.Min(size.x, size.z));
    }

    /// <summary>
    /// Circles on a flat grid of cells, for finding free spots among thousands of scattered items in O(1) per
    /// query (only neighbouring cells are checked). Surfaces at different heights don't block each other.
    /// </summary>
    public sealed class ScatterGrid
    {
        private readonly float _cell;
        private readonly Dictionary<long, List<Vector4>> _cells = new Dictionary<long, List<Vector4>>();
        private float _largest;

        public ScatterGrid(float cellSize) => _cell = Mathf.Max(0.05f, cellSize);

        public int Count { get; private set; }

        public void Clear()
        {
            _cells.Clear();
            _largest = 0f;
            Count = 0;
        }

        public void Add(Vector3 centre, float radius)
        {
            long key = Key(Cell(centre.x), Cell(centre.z));
            if (!_cells.TryGetValue(key, out List<Vector4> list)) _cells[key] = list = new List<Vector4>(4);
            list.Add(new Vector4(centre.x, centre.y, centre.z, radius));
            _largest = Mathf.Max(_largest, radius);
            Count++;
        }

        /// <summary>Gap between this circle and the nearest stored one (negative = overlap; +inf when alone).</summary>
        public float Clearance(Vector3 centre, float radius)
        {
            float best = float.PositiveInfinity;
            int reach = Mathf.CeilToInt((radius + _largest) / _cell);
            int cx = Cell(centre.x), cz = Cell(centre.z);
            for (int x = cx - reach; x <= cx + reach; x++)
            for (int z = cz - reach; z <= cz + reach; z++)
            {
                if (!_cells.TryGetValue(Key(x, z), out List<Vector4> list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    Vector4 s = list[i];
                    if (Mathf.Abs(s.y - centre.y) > 0.3f) continue; // a table top doesn't crowd the floor
                    float dx = s.x - centre.x, dz = s.z - centre.z;
                    best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz) - s.w - radius);
                }
            }
            return best;
        }

        private int Cell(float v) => Mathf.FloorToInt(v / _cell);
        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
