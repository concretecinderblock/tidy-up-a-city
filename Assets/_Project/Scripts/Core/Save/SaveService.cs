using System;
using System.Collections.Generic;
using TidyUp.Locations;
using TidyUp.Story;
using UnityEngine;

namespace TidyUp.Core.Save
{
    /// <summary>
    /// Lives in the City. Applies the pending new game / loaded save when the City starts, saves the current
    /// game to a slot, and autosaves into the current slot whenever the player steps back onto the street
    /// or finishes a location. Loading a save reloads the City scene, so every load starts from a clean scene.
    /// </summary>
    [DefaultExecutionOrder(-150)] // after PlayerServices (-200), before gameplay
    public sealed class SaveService : MonoBehaviour
    {
        [SerializeField] private MemoryService _memories;
        [SerializeField] private LocationLoader _loader;
        [SerializeField] private string _mainMenuScene = "MainMenu";

        private SaveStorage _storage; // lazy: persistentDataPath may not be read from a field initializer
        private LocationProgress _watchedProgress;

        public SaveStorage Storage => _storage ??= SaveStorage.Default;
        public int CurrentSlot => SaveSession.CurrentSlot;
        public string MainMenuScene => _mainMenuScene;

        /// <summary>Raised after a successful save (slot, autosave?).</summary>
        public event Action<int, bool> Saved;

        private void Start()
        {
            ApplyPending();
            GameServices.ActiveLocationChanged += HandleActiveLocationChanged;
            if (_loader != null) _loader.ReturnedToStreet += Autosave; // after the player is back outside
        }

        private void OnDestroy()
        {
            GameServices.ActiveLocationChanged -= HandleActiveLocationChanged;
            if (_loader != null) _loader.ReturnedToStreet -= Autosave;
            WatchProgress(null);
        }

        private void Update()
        {
            if (Time.timeScale > 0f) SaveSession.PlayTimeSeconds += Time.unscaledDeltaTime;
        }

        // ───────────── Applying ─────────────

        private void ApplyPending()
        {
            if (SaveSession.PendingNewGame)
            {
                LocationStateStore.Clear();
                SaveSession.ClearPending();
                SaveToSlot(SaveSession.CurrentSlot, autosave: true); // so Continue works straight away
                return;
            }

            SaveData data = SaveSession.PendingLoad;
            SaveSession.ClearPending();
            if (data == null) return;

            var state = new List<KeyValuePair<string, bool[]>>();
            foreach (LocationSave location in data.locations)
            {
                if (location.filled == null || location.filled.Length == 0) continue; // not visited
                state.Add(new KeyValuePair<string, bool[]>(location.id, location.filled));
            }
            LocationStateStore.Restore(state);
            var cleaned = new List<KeyValuePair<string, bool[]>>();
            foreach (LocationSave location in data.locations)
            {
                if (location.cleaned != null && location.cleaned.Length > 0) cleaned.Add(new KeyValuePair<string, bool[]>(location.id, location.cleaned));
            }
            LocationStateStore.RestoreCleaned(cleaned);
            if (_memories != null) _memories.Journal.Load(data.memories);

            if (data.hasStreetPosition && GameServices.PlayerController != null)
            {
                GameServices.PlayerController.Teleport(data.streetPosition, data.streetYaw);
            }

            // Saved inside a location: walk back in.
            if (!string.IsNullOrEmpty(data.currentLocationId) && _loader != null)
            {
                foreach (LocationDoor door in FindDoors())
                {
                    if (door.Location != null && door.Location.Id == data.currentLocationId)
                    {
                        _loader.Enter(door.Location, door.ReturnPoint);
                        break;
                    }
                }
            }
        }

        private LocationDoor[] FindDoors() => _loader != null ? _loader.GetDoors() : Array.Empty<LocationDoor>();

        // ───────────── Saving ─────────────

        public SaveData Capture()
        {
            var data = new SaveData
            {
                savedAtUtc = DateTime.UtcNow.ToString("o"),
                playTimeSeconds = SaveSession.PlayTimeSeconds,
            };
            // Every location on the street, in street order, with names for the menu; unvisited ones stay empty.
            List<KeyValuePair<string, bool[]>> visited = LocationStateStore.Snapshot();
            if (_loader != null)
            {
                foreach (LocationDefinition location in _loader.Locations)
                {
                    if (location == null) continue;
                    data.locations.Add(new LocationSave { id = location.Id, name = location.DisplayName, filled = FilledFor(visited, location.Id) });
                }
            }
            foreach (KeyValuePair<string, bool[]> pair in visited)
            {
                if (data.FindLocation(pair.Key) == null) data.locations.Add(new LocationSave { id = pair.Key, name = pair.Key, filled = pair.Value });
            }
            foreach (KeyValuePair<string, bool[]> pair in LocationStateStore.CleanedSnapshot())
            {
                LocationSave location = data.FindLocation(pair.Key);
                if (location == null) data.locations.Add(location = new LocationSave { id = pair.Key, name = pair.Key });
                location.cleaned = pair.Value;
            }
            if (_memories != null) data.memories.AddRange(_memories.Journal.CollectedIds);

            LocationRuntime active = GameServices.ActiveLocation;
            if (active != null && active.Location != null)
            {
                data.currentLocationId = active.Location.Id;
            }
            else if (GameServices.Player != null && (_loader == null || !_loader.IsInside))
            {
                data.hasStreetPosition = true;
                data.streetPosition = GameServices.Player.position;
                data.streetYaw = GameServices.Player.eulerAngles.y;
            }
            return data;
        }

        private static bool[] FilledFor(List<KeyValuePair<string, bool[]>> visited, string id)
        {
            foreach (KeyValuePair<string, bool[]> pair in visited) if (pair.Key == id) return pair.Value;
            return Array.Empty<bool>();
        }

        public bool SaveToSlot(int slot, bool autosave = false)
        {
            if (!SaveStorage.IsValidSlot(slot)) return false;
            try
            {
                Storage.Write(slot, Capture());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Tidy Up] Saving to slot {slot} failed: {e}", this);
                GameServices.Announce("Saving didn't work. Your game is still running.");
                return false;
            }
            SaveSession.CurrentSlot = slot;
            Saved?.Invoke(slot, autosave);
            if (!autosave) GameServices.Announce($"Saved to slot {slot}.");
            return true;
        }

        /// <summary>Autosave into the current slot, if this game has one.</summary>
        public void Autosave()
        {
            if (SaveStorage.IsValidSlot(SaveSession.CurrentSlot)) SaveToSlot(SaveSession.CurrentSlot, autosave: true);
        }

        /// <summary>Loads a slot by reloading the City scene with that save pending.</summary>
        public bool LoadSlot(int slot, out string error)
        {
            if (!Storage.TryRead(slot, out SaveData data, out error)) return false;
            SaveSession.RequestLoad(slot, data);
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
            return true;
        }

        private void HandleActiveLocationChanged(LocationRuntime location)
        {
            WatchProgress(location != null ? location.Progress : null);
        }

        private void WatchProgress(LocationProgress progress)
        {
            if (_watchedProgress != null) _watchedProgress.Completed -= HandleLocationCompleted;
            _watchedProgress = progress;
            if (_watchedProgress != null) _watchedProgress.Completed += HandleLocationCompleted;
        }

        private void HandleLocationCompleted(LocationProgress progress) => Autosave();

        internal void EditorConfigure(MemoryService memories, LocationLoader loader, string mainMenuScene)
        {
            _memories = memories;
            _loader = loader;
            _mainMenuScene = mainMenuScene;
        }
    }
}
