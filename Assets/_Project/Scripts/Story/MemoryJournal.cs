using System;
using System.Collections.Generic;

namespace TidyUp.Story
{
    /// <summary>
    /// The memories and clues the player has collected, in the order found. Plain C# so it can be saved
    /// (ids only) and unit-tested. Persistence comes with the save system.
    /// </summary>
    public sealed class MemoryJournal
    {
        private readonly List<string> _order = new List<string>();
        private readonly HashSet<string> _ids = new HashSet<string>();

        public int Count => _order.Count;
        public IReadOnlyList<string> CollectedIds => _order;

        /// <summary>Raised with the memory id when a memory is collected for the first time.</summary>
        public event Action<string> Collected;

        public bool Has(string id) => !string.IsNullOrEmpty(id) && _ids.Contains(id);

        /// <summary>Returns true only the first time an id is collected.</summary>
        public bool Collect(string id)
        {
            if (string.IsNullOrEmpty(id) || !_ids.Add(id)) return false;
            _order.Add(id);
            Collected?.Invoke(id);
            return true;
        }

        /// <summary>Restores state from a save (no events).</summary>
        public void Load(IEnumerable<string> ids)
        {
            _order.Clear();
            _ids.Clear();
            if (ids == null) return;
            foreach (string id in ids)
            {
                if (!string.IsNullOrEmpty(id) && _ids.Add(id)) _order.Add(id);
            }
        }
    }
}
