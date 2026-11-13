using System;
using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Items;
using TidyUp.Locations;
using TidyUp.Mechanics.ShelfSort;
using UnityEngine;

namespace TidyUp.Muddle
{
    /// <summary>
    /// Generates the mess from the tidy state: takes items out of their slots and scatters each one on its own
    /// over whatever surface lies under the scatter areas (floor, rugs, tables, counters, never on fixtures).
    /// Items rest kinematic (no physics) until the player touches them, so thousands cost nothing to simulate,
    /// and a spatial grid keeps them from landing inside each other. Seeded: same seed and layout = same mess
    /// (CLAUDE.md section 4). Scatter areas are trigger BoxColliders on the Ignore Raycast layer.
    /// </summary>
    public sealed class MuddleScatterer : MonoBehaviour
    {
        [SerializeField] private LocationDefinition _location;
        [Tooltip("Session-store key; empty = the location id. Rooms of a mega location use \"location/zone\".")]
        [SerializeField] private string _stateKey;
        [Tooltip("Used when no LocationDefinition is set.")]
        [SerializeField] private int _seed = 1;
        [Range(0f, 1f)]
        [SerializeField] private float _muddleFraction = 1f;
        [SerializeField] private bool _scatterOnStart = true;
        [SerializeField] private SlotGroup[] _groups = Array.Empty<SlotGroup>();
        [SerializeField] private BoxCollider[] _scatterAreas = Array.Empty<BoxCollider>();
        [SerializeField] private LayerMask _surfaceMask = ~(1 << 2); // everything except Ignore Raycast
        [Tooltip("Areas the mess stays out of (decorations, ovens): world bounds, checked in XZ.")]
        [SerializeField] private Bounds[] _keepClear = Array.Empty<Bounds>();

        private const int Attempts = 12;
        private readonly ScatterGrid _grid = new ScatterGrid(0.5f);

        private string StateKey => !string.IsNullOrEmpty(_stateKey) ? _stateKey : _location != null ? _location.Id : null;
        // Each room of a mega location gets its own mess from the location seed.
        public int Seed => _location != null ? _location.MuddleSeed + (string.IsNullOrEmpty(_stateKey) ? 0 : Hash(_stateKey)) : _seed;

        private static int Hash(string s)
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fff;
        }
        public float MuddleFraction => _location != null ? _location.MuddleFraction : _muddleFraction;

        /// <summary>True once the mess exists; progress saved before this would be the tidy layout.</summary>
        public bool HasScattered { get; private set; }

        private void Start()
        {
            if (_scatterOnStart) Scatter(Seed);
        }

        /// <summary>Scatters slotted items. Deterministic for a given seed and tidy layout.</summary>
        public void Scatter(int seed)
        {
            if (_scatterAreas.Length == 0)
            {
                Debug.LogWarning($"{nameof(MuddleScatterer)} on '{name}' has no scatter areas.", this);
                HasScattered = true;
                return;
            }

            var random = new System.Random(seed);
            _grid.Clear();
            List<ItemSlot> slots = CollectSlots();
            float fraction = MuddleFraction;
            // Returning to a location: slots the player already filled stay filled.
            bool[] saved = StateKey != null ? LocationStateStore.Get(StateKey) : null;
            if (saved != null && saved.Length != slots.Count) saved = null; // layout changed since: ignore

            // Decide everything first (one roll per slot keeps the choice stable), then shuffle so kinds mix.
            var muddled = new List<TidyItem>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                double roll = random.NextDouble();
                if (slots[i].IsEmpty || roll >= fraction || (saved != null && saved[i])) continue;
                muddled.Add(slots[i].Occupant);
            }
            Shuffle(muddled, random);

            foreach (TidyItem item in muddled)
            {
                Shape(item, out Quaternion lie, out Vector2 footprint);
                float radius = Mathf.Max(footprint.x, footprint.y) * 0.5f;
                Vector3 point = FreeSpot(random, radius);
                Quaternion tilt = Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 8f, 0f, ((float)random.NextDouble() - 0.5f) * 8f);
                Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * tilt * lie;
                item.SetLooseResting(point, rotation);
                RestOn(item, point.y);
            }
            HasScattered = true;
        }

        /// <summary>
        /// The item's own size (mesh bounds, so a shelf's rotation doesn't matter), how it lies on the floor, and
        /// its footprint there. Tall or slab-like things tip over so their thinnest side ends up vertical: a book
        /// lies flat on its cover, a bottle or candle rolls onto its side.
        /// </summary>
        private static void Shape(TidyItem item, out Quaternion lie, out Vector2 footprint)
        {
            MeshFilter filter = item.GetComponentInChildren<MeshFilter>();
            Vector3 size = filter != null && filter.sharedMesh != null
                ? Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale)
                : Vector3.one * 0.1f;
            lie = Quaternion.identity;
            footprint = new Vector2(size.x, size.z);
            if (!ScatterRules.ShouldLieDown(size)) return;
            if (size.x <= size.z)
            {
                lie = Quaternion.Euler(0f, 0f, 90f);
                footprint = new Vector2(size.y, size.z);
            }
            else
            {
                lie = Quaternion.Euler(90f, 0f, 0f);
                footprint = new Vector2(size.x, size.y);
            }
        }

        /// <summary>
        /// A spot on a surface (not on a fixture) where an item of this radius doesn't overlap earlier items.
        /// Tries a few seeded candidates and keeps the clearest, so it's deterministic and never stalls.
        /// </summary>
        private Vector3 FreeSpot(System.Random random, float radius)
        {
            Vector3 best = Vector3.zero;
            float bestClearance = float.NegativeInfinity;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                BoxCollider area = _scatterAreas[random.Next(_scatterAreas.Length)];
                if (!TryPointInArea(area, (float)random.NextDouble(), (float)random.NextDouble(), out Vector3 point)) continue;
                float clearance = _grid.Clearance(point, radius);
                if (clearance > bestClearance)
                {
                    best = point;
                    bestClearance = clearance;
                }
                if (clearance >= 0.02f) break; // free: take it
            }
            if (float.IsNegativeInfinity(bestClearance)) best = FallbackPoint();
            _grid.Add(best, radius);
            return best;
        }

        private Vector3 FallbackPoint()
        {
            BoxCollider area = _scatterAreas[0];
            return area.transform.TransformPoint(area.center - Vector3.up * area.size.y * 0.5f);
        }

        private bool TryPointInArea(BoxCollider area, float u, float v, out Vector3 point)
        {
            Transform t = area.transform;
            Vector3 half = area.size * 0.5f;
            Vector3 local = area.center + new Vector3(Mathf.Lerp(-half.x, half.x, u), half.y, Mathf.Lerp(-half.z, half.z, v));
            Vector3 top = t.TransformPoint(local);
            float castLength = area.size.y * t.lossyScale.y + 0.1f;
            int mask = TidyLayers.WithoutItems(_surfaceMask); // never land on other items
            if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, castLength, mask, QueryTriggerInteraction.Ignore)
                && hit.normal.y > 0.8f
                && hit.collider.GetComponentInParent<SlotGroup>() == null // not on or inside a fixture
                && !InKeepClear(hit.point))                                // not inside a decoration
            {
                point = hit.point;
                return true;
            }
            point = default;
            return false;
        }


        private bool InKeepClear(Vector3 p)
        {
            for (int i = 0; i < _keepClear.Length; i++)
            {
                Bounds b = _keepClear[i];
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && p.y <= b.max.y) return true;
            }
            return false;
        }

        internal void EditorSetKeepClear(Bounds[] keepClear) => _keepClear = keepClear ?? Array.Empty<Bounds>();
        /// <summary>Moves the item up or down so its visual bottom sits on the given height.</summary>
        private static void RestOn(TidyItem item, float surfaceY)
        {
            Renderer renderer = item.GetComponentInChildren<MeshRenderer>();
            if (renderer == null) return;
            Bounds b = renderer.bounds;
            item.MoveTo(item.transform.position + Vector3.up * (surfaceY + 0.002f - b.min.y), item.transform.rotation);
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private List<ItemSlot> CollectSlots()
        {
            var result = new List<ItemSlot>(256);
            for (int g = 0; g < _groups.Length; g++)
            {
                if (_groups[g] == null) continue;
                ItemSlot[] slots = _groups[g].Slots;
                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] != null) result.Add(slots[s]);
                }
            }
            return result;
        }

        internal void EditorConfigure(LocationDefinition location, SlotGroup[] groups, BoxCollider[] scatterAreas, string stateKey = null)
        {
            _stateKey = stateKey;
            _location = location;
            _groups = groups ?? Array.Empty<SlotGroup>();
            _scatterAreas = scatterAreas ?? Array.Empty<BoxCollider>();
        }
    }
}
