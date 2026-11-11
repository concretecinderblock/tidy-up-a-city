using System;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Mechanics.ShelfSort;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// Tracks how tidy a location is and owns its checklist ("Bakery 412/600 · Bread ✓ · Pastries 80%").
    /// Built for thousands of slots: each slot change updates the counts in O(1), and ProgressChanged fires
    /// at most once per frame (the Muddle moving 5,000 items, or a run of 40 placements, is one update).
    /// </summary>
    public sealed class LocationProgress : MonoBehaviour
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private SlotGroup[] _groups = Array.Empty<SlotGroup>();

        private readonly List<ItemSlot> _slots = new List<ItemSlot>();
        private readonly List<TidyItem> _items = new List<TidyItem>();
        private readonly Dictionary<ItemSlot, int> _slotIndex = new Dictionary<ItemSlot, int>();
        private bool[] _filled = Array.Empty<bool>();
        private Checklist _checklist;
        private bool _counted;
        private bool _dirty;
        private bool _wasComplete;

        public LocationDefinition Location => _location;
        public SlotGroup[] Groups => _groups;
        /// <summary>Every slot in the location, in a stable order.</summary>
        public IReadOnlyList<ItemSlot> Slots => _slots;
        /// <summary>Every item that belongs to this location (from the tidy state).</summary>
        public IReadOnlyList<TidyItem> Items => _items;
        public Checklist Checklist => _checklist;
        public int Placed => _checklist?.Placed ?? 0;
        public int Total => _checklist?.Total ?? 0;
        public float CompletionRatio => Total == 0 ? 0f : (float)Placed / Total;
        public bool IsComplete => Total > 0 && Placed == Total;

        /// <summary>Raised (at most once per frame) when slots changed.</summary>
        public event Action<LocationProgress> ProgressChanged;

        /// <summary>Raised once when the location reaches 100%.</summary>
        public event Action<LocationProgress> Completed;

        private void Awake()
        {
            var labels = new List<string>();
            for (int g = 0; g < _groups.Length; g++)
            {
                if (_groups[g] == null) continue;
                ItemSlot[] slots = _groups[g].Slots;
                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] == null) continue;
                    _slotIndex[slots[s]] = _slots.Count;
                    _slots.Add(slots[s]);
                    labels.Add(slots[s].Label);
                    if (slots[s].StartOccupant != null) _items.Add(slots[s].StartOccupant);
                }
            }
            _filled = new bool[_slots.Count];
            _checklist = new Checklist(labels);
        }

        private void OnEnable()
        {
            for (int i = 0; i < _groups.Length; i++)
            {
                if (_groups[i] != null) _groups[i].Changed += HandleGroupChanged;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _groups.Length; i++)
            {
                if (_groups[i] != null) _groups[i].Changed -= HandleGroupChanged;
            }
        }

        private void Start()
        {
            EnsureCounted();
            _wasComplete = IsComplete;
            ProgressChanged?.Invoke(this);
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            ProgressChanged?.Invoke(this);
            bool complete = IsComplete;
            if (complete && !_wasComplete) Completed?.Invoke(this);
            _wasComplete = complete;
        }

        private void HandleGroupChanged(SlotGroup group, ItemSlot slot)
        {
            // Before the first full count, a change just means "count everything later".
            if (!_counted || !_slotIndex.TryGetValue(slot, out int index))
            {
                _dirty = true;
                return;
            }
            bool now = !slot.IsEmpty;
            _checklist.SetFilled(index, _filled[index], now);
            _filled[index] = now;
            _dirty = true;
        }

        private void EnsureCounted()
        {
            if (_counted) return;
            for (int i = 0; i < _slots.Count; i++) _filled[i] = !_slots[i].IsEmpty;
            _checklist.Recount(_filled);
            _counted = true;
        }

        internal void EditorConfigure(LocationDefinition location, SlotGroup[] groups)
        {
            _location = location;
            _groups = groups ?? Array.Empty<SlotGroup>();
        }
    }
}
