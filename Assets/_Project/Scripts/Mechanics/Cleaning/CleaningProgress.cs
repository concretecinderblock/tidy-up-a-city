using System;
using TidyUp.Locations;
using UnityEngine;

namespace TidyUp.Mechanics.Cleaning
{
    /// <summary>
    /// A location's cleaning goals (family K): restores which cleanables are clean from the session store,
    /// saves every change back, and feeds the checklist line "Cleaned 8/20".
    /// </summary>
    public sealed class CleaningProgress : MonoBehaviour
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private Cleanable[] _cleanables = Array.Empty<Cleanable>();

        private bool[] _clean = Array.Empty<bool>();

        public Cleanable[] Cleanables => _cleanables;
        public int Total => _cleanables.Length;
        public int Cleaned => CleaningRules.CountTrue(_clean);
        public bool IsComplete => Total > 0 && Cleaned == Total;

        /// <summary>Raised when anything gets cleaner (for the HUD).</summary>
        public event Action<CleaningProgress> ProgressChanged;

        private void Awake()
        {
            _clean = new bool[_cleanables.Length];
            bool[] saved = _location != null ? LocationStateStore.GetCleaned(_location.Id) : null;
            if (saved != null && saved.Length != _cleanables.Length) saved = null; // layout changed since: start dusty
            for (int i = 0; i < _cleanables.Length; i++)
            {
                if (_cleanables[i] == null) continue;
                _clean[i] = saved != null && saved[i];
                _cleanables[i].SetCleanSilently(_clean[i]);
            }
        }

        private void OnEnable()
        {
            foreach (Cleanable c in _cleanables) if (c != null) c.Changed += HandleChanged;
        }

        private void OnDisable()
        {
            foreach (Cleanable c in _cleanables) if (c != null) c.Changed -= HandleChanged;
        }

        private void HandleChanged(Cleanable cleanable)
        {
            int index = Array.IndexOf(_cleanables, cleanable);
            if (index < 0) return;
            bool wasClean = _clean[index];
            _clean[index] = cleanable.IsClean;
            if (_clean[index] != wasClean && _location != null) LocationStateStore.SaveCleaned(_location.Id, _clean);
            ProgressChanged?.Invoke(this);
        }

        internal void EditorConfigure(LocationDefinition location, Cleanable[] cleanables)
        {
            _location = location;
            _cleanables = cleanables ?? Array.Empty<Cleanable>();
        }
    }
}
