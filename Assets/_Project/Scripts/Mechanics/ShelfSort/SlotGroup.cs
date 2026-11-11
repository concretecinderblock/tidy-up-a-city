using System;
using TidyUp.Items;
using UnityEngine;

namespace TidyUp.Mechanics.ShelfSort
{
    /// <summary>
    /// A set of slots that fill together (a shelf, rack, display case). Drives the rising placement
    /// tone and per-fixture checklist entries. Built by the ShelfGenerator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlotGroup : MonoBehaviour
    {
        [SerializeField] private string _label = "Shelf";
        [SerializeField] private ItemSlot[] _slots = Array.Empty<ItemSlot>();
        [Tooltip("Ordered sort (family B): each slot takes one exact item; rejections hint left/right/above/below.")]
        [SerializeField] private bool _ordered;
        [Tooltip("Slots per shelf row, for ordered-sort direction hints.")]
        [SerializeField] private int _columns = 1;

        private int _filledCount = -1;

        public string Label => _label;
        public ItemSlot[] Slots => _slots;
        public int Count => _slots.Length;
        public int FilledCount
        {
            get
            {
                if (_filledCount < 0) Recount();
                return _filledCount;
            }
        }
        public float FillRatio => _slots.Length == 0 ? 0f : (float)FilledCount / _slots.Length;
        public bool IsComplete => _slots.Length > 0 && FilledCount == _slots.Length;

        /// <summary>Raised whenever a slot in this group gains or loses an item.</summary>
        public event Action<SlotGroup, ItemSlot> Changed;

        private void Awake()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) _slots[i].OccupantChanged += HandleSlotChanged;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null) _slots[i].OccupantChanged -= HandleSlotChanged;
            }
        }

        public bool Contains(ItemSlot slot) => Array.IndexOf(_slots, slot) >= 0;
        public bool IsOrdered => _ordered;
        public int Columns => _columns;
        public int IndexOf(ItemSlot slot) => Array.IndexOf(_slots, slot);

        /// <summary>Index of the slot reserved for this exact item id, or -1.</summary>
        public int IndexOfExactItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return -1;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].Definition.ExactItemId == itemId) return i;
            }
            return -1;
        }

        private void HandleSlotChanged(ItemSlot slot)
        {
            // O(1): every occupant change fires exactly one event (filled ↔ empty), so adjust the count.
            if (_filledCount >= 0) _filledCount = Mathf.Clamp(_filledCount + (slot.IsEmpty ? -1 : 1), 0, _slots.Length);
            Changed?.Invoke(this, slot);
        }

        private void Recount()
        {
            int filled = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && !_slots[i].IsEmpty) filled++;
            }
            _filledCount = filled;
        }

        internal void EditorConfigure(string label, ItemSlot[] slots, bool ordered = false, int columns = 1)
        {
            _label = label;
            _slots = slots ?? Array.Empty<ItemSlot>();
            _ordered = ordered;
            _columns = Math.Max(1, columns);
        }
    }
}
