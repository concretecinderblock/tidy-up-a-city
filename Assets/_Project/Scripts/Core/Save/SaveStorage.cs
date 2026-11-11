using System;
using System.IO;
using UnityEngine;

namespace TidyUp.Core.Save
{
    /// <summary>
    /// Reads and writes save slots as JSON files ("slot1.json" …) in one folder. Writes go to a temp file first
    /// and then replace the slot, so a crash mid-save never corrupts an existing save. Plain C# with an
    /// injectable folder, so it's EditMode-testable.
    /// </summary>
    public sealed class SaveStorage
    {
        public const int SlotCount = 3;

        private readonly string _directory;

        public SaveStorage(string directory)
        {
            _directory = directory;
        }

        /// <summary>The game's save folder: persistentDataPath/Saves.</summary>
        public static SaveStorage Default => new SaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));

        public string Directory => _directory;

        public string PathFor(int slot) => Path.Combine(_directory, $"slot{slot}.json");

        public bool Exists(int slot) => IsValidSlot(slot) && File.Exists(PathFor(slot));

        public void Write(int slot, SaveData data)
        {
            if (!IsValidSlot(slot)) throw new ArgumentOutOfRangeException(nameof(slot));
            if (data == null) throw new ArgumentNullException(nameof(data));
            System.IO.Directory.CreateDirectory(_directory);

            string path = PathFor(slot);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        /// <summary>Reads a slot. Missing, unreadable or newer-version files return false (error explains why).</summary>
        public bool TryRead(int slot, out SaveData data, out string error)
        {
            data = null;
            error = null;
            if (!Exists(slot))
            {
                error = "Empty slot.";
                return false;
            }
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
            }
            catch (Exception e)
            {
                error = "This save can't be read: " + e.Message;
                return false;
            }
            if (data == null)
            {
                error = "This save is empty or damaged.";
                return false;
            }
            if (data.version > SaveData.CurrentVersion)
            {
                error = "This save is from a newer version of the game.";
                data = null;
                return false;
            }
            data.locations ??= new System.Collections.Generic.List<LocationSave>();
            data.memories ??= new System.Collections.Generic.List<string>();
            return true;
        }

        public bool Delete(int slot)
        {
            if (!Exists(slot)) return false;
            File.Delete(PathFor(slot));
            return true;
        }

        /// <summary>The slot with the most recent save, or -1.</summary>
        public int MostRecentSlot()
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (!TryRead(slot, out SaveData data, out _)) continue;
                if (best < 0 || data.SavedAt > bestTime)
                {
                    best = slot;
                    bestTime = data.SavedAt;
                }
            }
            return best;
        }

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= SlotCount;
    }
}
