using System;
using System.Collections.Generic;

namespace TidyUp.Locations
{
    /// <summary>
    /// Per-location checklist tally ("Bakery 412/600 · Bread ✓ · Pastries 80%").
    /// Slots are grouped into categories by their label. Plain C#, EditMode-testable.
    /// </summary>
    public sealed class Checklist
    {
        public readonly struct Category
        {
            public readonly string Label;
            public readonly int Placed;
            public readonly int Total;

            public Category(string label, int placed, int total)
            {
                Label = label;
                Placed = placed;
                Total = total;
            }

            public bool IsComplete => Total > 0 && Placed == Total;
            public float Ratio => Total == 0 ? 0f : (float)Placed / Total;
        }

        private readonly string[] _labels;
        private readonly int[] _slotCategory;
        private readonly int[] _placed;
        private readonly int[] _total;
        private int _placedAll;

        /// <param name="slotLabels">One label per slot, in slot order. Empty labels go into "Other".</param>
        public Checklist(IReadOnlyList<string> slotLabels)
        {
            if (slotLabels == null) throw new ArgumentNullException(nameof(slotLabels));
            var labels = new List<string>();
            var indexByLabel = new Dictionary<string, int>();
            _slotCategory = new int[slotLabels.Count];

            for (int i = 0; i < slotLabels.Count; i++)
            {
                string label = string.IsNullOrEmpty(slotLabels[i]) ? "Other" : slotLabels[i];
                if (!indexByLabel.TryGetValue(label, out int category))
                {
                    category = labels.Count;
                    indexByLabel.Add(label, category);
                    labels.Add(label);
                }
                _slotCategory[i] = category;
            }

            _labels = labels.ToArray();
            _placed = new int[_labels.Length];
            _total = new int[_labels.Length];
            for (int i = 0; i < _slotCategory.Length; i++) _total[_slotCategory[i]]++;
        }

        public int SlotCount => _slotCategory.Length;
        public int CategoryCount => _labels.Length;
        public int Placed => _placedAll;
        public int Total => _slotCategory.Length;

        public Category GetCategory(int index) => new Category(_labels[index], _placed[index], _total[index]);

        /// <summary>Incremental update for one slot (O(1)); the caller tracks the slot's previous state.</summary>
        public void SetFilled(int slotIndex, bool wasFilled, bool isFilled)
        {
            if (wasFilled == isFilled || slotIndex < 0 || slotIndex >= _slotCategory.Length) return;
            int delta = isFilled ? 1 : -1;
            _placed[_slotCategory[slotIndex]] += delta;
            _placedAll += delta;
        }

        /// <summary>Recomputes counts. filled[i] says whether slot i holds its item.</summary>
        public void Recount(IReadOnlyList<bool> filled)
        {
            if (filled == null || filled.Count != _slotCategory.Length)
                throw new ArgumentException("Need one entry per slot.", nameof(filled));

            Array.Clear(_placed, 0, _placed.Length);
            _placedAll = 0;
            for (int i = 0; i < _slotCategory.Length; i++)
            {
                if (!filled[i]) continue;
                _placed[_slotCategory[i]]++;
                _placedAll++;
            }
        }
    }
}
