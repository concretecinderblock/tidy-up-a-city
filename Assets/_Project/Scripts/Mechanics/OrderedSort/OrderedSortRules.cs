using System;
using System.Collections.Generic;
using System.Globalization;

namespace TidyUp.Mechanics.OrderedSort
{
    public enum SortDirection
    {
        Ascending = 0,
        Descending = 1
    }

    /// <summary>Where a rejected item should go instead, relative to the slot the player tried.</summary>
    public enum OrderHint
    {
        None = 0,
        Here = 1,
        FurtherLeft = 2,
        FurtherRight = 3,
        ShelfAbove = 4,
        ShelfBelow = 5
    }

    /// <summary>
    /// Ordered sort (family B) logic: sort keys (alphabetical, numeric or size) and direction hints.
    /// The ShelfGenerator uses Sort to give each slot its exact item; the Interactor uses Direction to
    /// turn a rejection into "further left / shelf above". Pure C#, EditMode-testable.
    /// </summary>
    public static class OrderedSortRules
    {
        /// <summary>
        /// Numeric when both keys are numbers ("2" before "10"), otherwise culture-invariant text,
        /// ignoring case and accents ("Brontë" sorts with "Bronte").
        /// </summary>
        public static int CompareKeys(string a, string b)
        {
            a ??= string.Empty;
            b ??= string.Empty;
            if (double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                return x.CompareTo(y);
            }
            return string.Compare(a, b, CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
        }

        /// <summary>Stable sort by key. Equal keys keep their input order.</summary>
        public static List<T> Sort<T>(IEnumerable<T> items, Func<T, string> key, SortDirection direction)
        {
            var indexed = new List<KeyValuePair<int, T>>();
            int i = 0;
            foreach (T item in items) indexed.Add(new KeyValuePair<int, T>(i++, item));

            indexed.Sort((p, q) =>
            {
                int c = CompareKeys(key(p.Value), key(q.Value));
                if (direction == SortDirection.Descending) c = -c;
                return c != 0 ? c : p.Key.CompareTo(q.Key);
            });

            var result = new List<T>(indexed.Count);
            foreach (KeyValuePair<int, T> pair in indexed) result.Add(pair.Value);
            return result;
        }

        /// <summary>Direction from the tried slot to the right one. Slots are in reading order (rows top-down).</summary>
        public static OrderHint Direction(int fromIndex, int targetIndex, int columns)
        {
            if (fromIndex < 0 || targetIndex < 0 || columns <= 0) return OrderHint.None;
            if (fromIndex == targetIndex) return OrderHint.Here;
            int fromRow = fromIndex / columns;
            int targetRow = targetIndex / columns;
            if (targetRow < fromRow) return OrderHint.ShelfAbove;
            if (targetRow > fromRow) return OrderHint.ShelfBelow;
            return targetIndex < fromIndex ? OrderHint.FurtherLeft : OrderHint.FurtherRight;
        }

        public static string Describe(string itemName, OrderHint hint)
        {
            switch (hint)
            {
                case OrderHint.FurtherLeft: return $"{itemName} goes further left.";
                case OrderHint.FurtherRight: return $"{itemName} goes further right.";
                case OrderHint.ShelfAbove: return $"{itemName} belongs on the shelf above.";
                case OrderHint.ShelfBelow: return $"{itemName} belongs on the shelf below.";
                default: return $"{itemName} doesn't go here.";
            }
        }
    }
}
