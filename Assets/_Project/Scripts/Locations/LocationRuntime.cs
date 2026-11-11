using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Hints;
using TidyUp.Items;
using TidyUp.Mechanics.Cleaning;
using TidyUp.Muddle;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// The root of a location scene. Registers itself as the active location (so the player-side HUD and
    /// Tidy Sense follow it), saves progress to the LocationStateStore once the Muddle has scattered, and
    /// sets carried items down by the door when the player leaves (items never leave their location).
    /// </summary>
    public sealed class LocationRuntime : MonoBehaviour
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private LocationProgress _progress;
        [SerializeField] private HintService _hints;
        [SerializeField] private MuddleScatterer _muddle;
        [Tooltip("Where the player appears when entering from the street.")]
        [SerializeField] private Transform _entryPoint;
        [SerializeField] private CleaningProgress _cleaning;
        [Tooltip("Where carried items are set down when the player leaves.")]
        [SerializeField] private Transform _dropPoint;

        private readonly List<bool> _filled = new List<bool>();
        private bool _savedInitialState;

        public LocationDefinition Location => _location;
        public LocationProgress Progress => _progress;
        public HintService Hints => _hints;
        /// <summary>Cleaning goals (family K); null in a location without any.</summary>
        public CleaningProgress Cleaning => _cleaning;
        public Transform EntryPoint => _entryPoint;

        private void OnEnable()
        {
            GameServices.SetActiveLocation(this);
            if (_progress != null) _progress.ProgressChanged += SaveProgress;
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.ProgressChanged -= SaveProgress;
            GameServices.ClearActiveLocation(this);
        }

        private void Update()
        {
            // Record the starting state once the mess exists (a visited location then shows 0/12 on the street).
            // A flag, not enabled = false: disabling would run OnDisable and un-register the active location.
            if (_savedInitialState || _progress == null || _muddle == null || !_muddle.HasScattered) return;
            SaveProgress(_progress);
            _savedInitialState = true;
        }

        private void SaveProgress(LocationProgress progress)
        {
            // Before the Muddle runs, the scene still shows the tidy layout: never save that as progress.
            if (_location == null || (_muddle != null && !_muddle.HasScattered)) return;
            IReadOnlyList<ItemSlot> slots = progress.Slots;
            _filled.Clear();
            for (int i = 0; i < slots.Count; i++) _filled.Add(!slots[i].IsEmpty);
            LocationStateStore.Save(_location.Id, _filled);
        }

        /// <summary>Puts everything the player carries down near the door. Returns how many items.</summary>
        public int ReturnCarriedItems()
        {
            PlayerCarry carry = GameServices.Carry;
            if (carry == null || carry.Inventory.IsEmpty) return 0;

            var items = new List<TidyItem>(carry.Inventory.Items);
            Vector3 origin = _dropPoint != null ? _dropPoint.position : transform.position;
            for (int i = 0; i < items.Count; i++)
            {
                carry.Inventory.Remove(items[i]);
                // A small fan of positions so they don't stack inside each other.
                var offset = new Vector3((i % 5 - 2) * 0.25f, 0.3f + (i / 5) * 0.2f, 0f);
                items[i].SetLoose(origin + offset, Quaternion.identity);
            }
            return items.Count;
        }

        internal void EditorConfigure(LocationDefinition location, LocationProgress progress, HintService hints,
            MuddleScatterer muddle, Transform entryPoint, Transform dropPoint, CleaningProgress cleaning = null)
        {
            _location = location;
            _progress = progress;
            _hints = hints;
            _muddle = muddle;
            _entryPoint = entryPoint;
            _dropPoint = dropPoint;
            _cleaning = cleaning;
        }
    }
}
