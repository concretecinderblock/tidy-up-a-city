using System;
using System.Collections.Generic;
using UnityEngine;

namespace TidyUp.Core.Save
{
    /// <summary>
    /// One save file. Seed-plus-progress (CLAUDE.md section 4): which slots are filled per location and which
    /// memories were found, never item transforms. Loose items re-scatter from each location's seed.
    /// JsonUtility-friendly (public fields, lists, no dictionaries).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        /// <summary>UTC, ISO 8601 ("o" format).</summary>
        public string savedAtUtc;
        public float playTimeSeconds;
        /// <summary>Location the player was inside, or empty when on the street.</summary>
        public string currentLocationId = string.Empty;
        public Vector3 streetPosition;
        public float streetYaw;
        public bool hasStreetPosition;
        public List<LocationSave> locations = new List<LocationSave>();
        public List<string> memories = new List<string>();

        public LocationSave FindLocation(string id)
        {
            for (int i = 0; i < locations.Count; i++)
            {
                if (locations[i].id == id) return locations[i];
            }
            return null;
        }

        public int TotalPlaced
        {
            get
            {
                int sum = 0;
                foreach (LocationSave l in locations) sum += l.PlacedCount;
                return sum;
            }
        }

        public DateTime SavedAt => DateTime.TryParse(savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t) ? t : DateTime.MinValue;
    }

    [Serializable]
    public sealed class LocationSave
    {
        public string id;
        /// <summary>Display name at save time, so menus can describe a save without loading the game.</summary>
        public string name;
        /// <summary>Filled flag per slot. Empty = not visited yet.</summary>
        public bool[] filled = Array.Empty<bool>();
        /// <summary>Clean flag per cleanable fixture or spot (family K). Empty = nothing recorded.</summary>
        public bool[] cleaned = Array.Empty<bool>();

        public int PlacedCount
        {
            get
            {
                int count = 0;
                if (filled == null) return 0;
                for (int i = 0; i < filled.Length; i++) if (filled[i]) count++;
                return count;
            }
        }
    }
}
