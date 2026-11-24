using System;
using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Items;
using TidyUp.Locations;
using UnityEngine;

namespace TidyUp.Hints
{
    /// <summary>
    /// Owns which hint tools are unlocked (GDD: ghost outlines, Tidy Sense and checklists unlock over
    /// time; signs are always on) and builds the ghost silhouettes for a location.
    /// For the vertical slice everything starts unlocked.
    /// </summary>
    [DefaultExecutionOrder(-50)] // build ghosts before the MuddleScatterer moves items (it runs in Start)
    public sealed class HintService : MonoBehaviour
    {
        [SerializeField] private LocationProgress _progress;
        [SerializeField] private Material _ghostMaterial;

        [Header("Unlocks")]
        [SerializeField] private bool _ghostsUnlocked = true;
        [SerializeField] private bool _tidySenseUnlocked = true;
        [SerializeField] private bool _checklistUnlocked = true;

        public bool GhostsUnlocked => _ghostsUnlocked;
        public bool TidySenseUnlocked => _tidySenseUnlocked;
        public bool ChecklistUnlocked => _checklistUnlocked;

        /// <summary>Raised when any unlock flag changes.</summary>
        public event Action<HintService> UnlocksChanged;

        [Header("Ghosts")]
        [Tooltip("Ghosts only show within this distance of the player (thousands of slots, few on screen).")]
        [SerializeField] private float _ghostRadius = 4f;

        private readonly List<ItemSlot> _ghostSlots = new List<ItemSlot>();

        private void Awake()
        {
            if (_progress == null) return;
            // LocationProgress collects slots in its own Awake; read the groups directly so Awake order doesn't matter.
            var groups = _progress.Groups;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g] == null) continue;
                ItemSlot[] slots = groups[g].Slots;
                for (int s = 0; s < slots.Length; s++)
                {
                    if (slots[s] == null) continue;
                    slots[s].CaptureGhost(_ghostMaterial);
                    slots[s].SetGhostAllowed(_ghostsUnlocked);
                    if (slots[s].HasGhost) _ghostSlots.Add(slots[s]);
                }
            }
        }

        private Vector3[] _ghostPositions;
        private int _ghostCursor;
        private const int GhostChecksPerFrame = 600;

        /// <summary>
        /// Shows ghosts only near the player. Slots don't move, so their positions are cached once, and a slice of
        /// slots is checked each frame (round robin) instead of every slot at once: thousands of slots per room
        /// never land on a single frame.
        /// </summary>
        private void Update()
        {
            int count = _ghostSlots.Count;
            if (count == 0) return;
            Transform player = GameServices.Player;
            if (player == null) return;
            if (_ghostPositions == null || _ghostPositions.Length != count)
            {
                _ghostPositions = new Vector3[count];
                for (int i = 0; i < count; i++) _ghostPositions[i] = _ghostSlots[i].SnapPoint.position;
            }
            Vector3 p = player.position;
            float r2 = _ghostRadius * _ghostRadius;
            int checks = Mathf.Min(count, GhostChecksPerFrame);
            for (int n = 0; n < checks; n++)
            {
                if (_ghostCursor >= count) _ghostCursor = 0;
                int i = _ghostCursor++;
                _ghostSlots[i].SetGhostNear((_ghostPositions[i] - p).sqrMagnitude <= r2);
            }
        }

        public void SetGhostsUnlocked(bool unlocked)
        {
            if (_ghostsUnlocked == unlocked) return;
            _ghostsUnlocked = unlocked;
            if (_progress != null)
            {
                var slots = _progress.Slots;
                for (int i = 0; i < slots.Count; i++) slots[i].SetGhostAllowed(unlocked);
            }
            UnlocksChanged?.Invoke(this);
        }

        public void SetTidySenseUnlocked(bool unlocked)
        {
            if (_tidySenseUnlocked == unlocked) return;
            _tidySenseUnlocked = unlocked;
            UnlocksChanged?.Invoke(this);
        }

        public void SetChecklistUnlocked(bool unlocked)
        {
            if (_checklistUnlocked == unlocked) return;
            _checklistUnlocked = unlocked;
            UnlocksChanged?.Invoke(this);
        }

        internal void EditorConfigure(LocationProgress progress, Material ghostMaterial)
        {
            _progress = progress;
            _ghostMaterial = ghostMaterial;
        }
    }
}
