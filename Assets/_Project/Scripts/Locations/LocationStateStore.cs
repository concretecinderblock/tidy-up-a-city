using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// Which slots are filled, per location, for this session. Locations unload when the player leaves, so
    /// this is what brings their progress back (CLAUDE.md: save the seed plus progress, not transforms).
    /// Plain C#; the save system will persist it later.
    /// </summary>
    public static class LocationStateStore
    {
        private static readonly Dictionary<string, bool[]> Filled = new Dictionary<string, bool[]>();

        public static void Save(string locationId, IReadOnlyList<bool> filled)
        {
            if (string.IsNullOrEmpty(locationId) || filled == null) return;
            if (!Filled.TryGetValue(locationId, out bool[] stored) || stored.Length != filled.Count)
            {
                stored = new bool[filled.Count];
                Filled[locationId] = stored;
            }
            for (int i = 0; i < filled.Count; i++) stored[i] = filled[i];
        }

        /// <summary>The saved filled-slot flags, or null if the location hasn't been visited.</summary>
        public static bool[] Get(string locationId)
        {
            return !string.IsNullOrEmpty(locationId) && Filled.TryGetValue(locationId, out bool[] stored) ? stored : null;
        }

        public static int PlacedCount(string locationId)
        {
            bool[] stored = Get(locationId);
            if (stored == null) return 0;
            int count = 0;
            for (int i = 0; i < stored.Length; i++) if (stored[i]) count++;
            return count;
        }

        public static void Clear()
        {
            Filled.Clear();
            Cleaned.Clear();
        }

        // ───────────── Cleaning (family K): which fixtures and spots are clean, per location ─────────────

        private static readonly Dictionary<string, bool[]> Cleaned = new Dictionary<string, bool[]>();

        public static void SaveCleaned(string locationId, IReadOnlyList<bool> cleaned)
        {
            if (string.IsNullOrEmpty(locationId) || cleaned == null) return;
            var copy = new bool[cleaned.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = cleaned[i];
            Cleaned[locationId] = copy;
        }

        /// <summary>The saved clean flags, or null if nothing was recorded for the location.</summary>
        public static bool[] GetCleaned(string locationId)
        {
            return !string.IsNullOrEmpty(locationId) && Cleaned.TryGetValue(locationId, out bool[] stored) ? stored : null;
        }

        public static List<KeyValuePair<string, bool[]>> CleanedSnapshot()
        {
            var result = new List<KeyValuePair<string, bool[]>>(Cleaned.Count);
            foreach (KeyValuePair<string, bool[]> pair in Cleaned) result.Add(new KeyValuePair<string, bool[]>(pair.Key, (bool[])pair.Value.Clone()));
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        /// <summary>Replaces the cleaned flags (for loading).</summary>
        public static void RestoreCleaned(IEnumerable<KeyValuePair<string, bool[]>> state)
        {
            Cleaned.Clear();
            if (state == null) return;
            foreach (KeyValuePair<string, bool[]> pair in state)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Length == 0) continue;
                Cleaned[pair.Key] = (bool[])pair.Value.Clone();
            }
        }

        /// <summary>Copy of every visited location's flags (for saving).</summary>
        public static List<KeyValuePair<string, bool[]>> Snapshot()
        {
            var result = new List<KeyValuePair<string, bool[]>>(Filled.Count);
            foreach (KeyValuePair<string, bool[]> pair in Filled)
            {
                result.Add(new KeyValuePair<string, bool[]>(pair.Key, (bool[])pair.Value.Clone()));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        /// <summary>Replaces everything with the given state (for loading).</summary>
        public static void Restore(IEnumerable<KeyValuePair<string, bool[]>> state)
        {
            Filled.Clear();
            if (state == null) return;
            foreach (KeyValuePair<string, bool[]> pair in state)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null) continue;
                Filled[pair.Key] = (bool[])pair.Value.Clone();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();
    }
}
