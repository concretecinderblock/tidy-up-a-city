using System;
using System.Collections.Generic;

namespace TidyUp.Core
{
    /// <summary>
    /// Plain C# carry logic: capacity limit and a selected item. Testable without a scene.
    /// Capacity tiers: hands 10, basket 20, trolley 40, magic satchel unlimited (int.MaxValue).
    /// </summary>
    public sealed class CarryInventory<T> where T : class
    {
        public const int Unlimited = int.MaxValue;

        private readonly List<T> _items;
        private int _capacity;
        private int _selectedIndex = -1;

        public CarryInventory(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _items = new List<T>(Math.Min(capacity, 64));
        }

        public int Count => _items.Count;
        public int Capacity => _capacity;
        public bool IsFull => _items.Count >= _capacity;
        public bool IsEmpty => _items.Count == 0;
        public int SelectedIndex => _selectedIndex;
        public T Selected => _selectedIndex >= 0 ? _items[_selectedIndex] : null;
        public IReadOnlyList<T> Items => _items;

        /// <summary>Raised after any change (add, remove, selection, capacity).</summary>
        public event Action Changed;

        public bool TryAdd(T item)
        {
            if (item == null || IsFull || _items.Contains(item)) return false;
            _items.Add(item);
            _selectedIndex = _items.Count - 1; // newest pickup is selected
            Changed?.Invoke();
            return true;
        }

        public bool Remove(T item)
        {
            int index = _items.IndexOf(item);
            if (index < 0) return false;
            _items.RemoveAt(index);

            if (_items.Count == 0) _selectedIndex = -1;
            else if (index < _selectedIndex || _selectedIndex >= _items.Count) _selectedIndex--;
            if (_selectedIndex < 0 && _items.Count > 0) _selectedIndex = 0;

            Changed?.Invoke();
            return true;
        }

        /// <summary>Moves the selection by direction (+1 / -1), wrapping around.</summary>
        public void Cycle(int direction)
        {
            if (_items.Count < 2 || direction == 0) return;
            int count = _items.Count;
            _selectedIndex = ((_selectedIndex + Math.Sign(direction)) % count + count) % count;
            Changed?.Invoke();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _items.Count || index == _selectedIndex) return;
            _selectedIndex = index;
            Changed?.Invoke();
        }

        /// <summary>Upgrades never drop items: capacity can't go below the current count.</summary>
        public void SetCapacity(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = Math.Max(capacity, _items.Count);
            Changed?.Invoke();
        }
    }
}
