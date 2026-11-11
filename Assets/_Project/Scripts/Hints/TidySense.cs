using System;
using System.Collections.Generic;
using TidyUp.Core;
using TidyUp.Items;
using TidyUp.Locations;
using UnityEngine;

namespace TidyUp.Hints
{
    /// <summary>
    /// "Tidy Sense": on a cooldown, briefly highlights the nearest useful target.
    /// Holding items: the nearest empty slot that fits the selected item (or any carried item).
    /// Empty hands: the nearest loose item.
    /// </summary>
    public sealed class TidySense : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _origin;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private LocationProgress _progress;
        [SerializeField] private HintService _hints;
        [SerializeField] private HintBeacon _beacon;
        [SerializeField] private FeedbackService _feedback;
        [SerializeField] private float _cooldown = 8f;
        [SerializeField] private float _highlightDuration = 3f;

        private float _readyTime;
        private int _shownSeconds = -1;

        public bool IsReady => Time.time >= _readyTime;
        public float CooldownRemaining => Mathf.Max(0f, _readyTime - Time.time);
        // Follows the active location (City setup: locations load and unload around the player). On the street: unavailable.
        private LocationProgress Progress => _progress != null ? _progress : (GameServices.ActiveLocation != null ? GameServices.ActiveLocation.Progress : null);
        private HintService Hints => _hints != null ? _hints : (GameServices.ActiveLocation != null ? GameServices.ActiveLocation.Hints : null);
        public bool IsUnlocked => Progress != null && (Hints == null || Hints.TidySenseUnlocked);

        /// <summary>Raised when the HUD should redraw (ready/locked state or whole seconds left changed).</summary>
        public event Action<TidySense> StatusChanged;

        /// <summary>What the pulse found, e.g. "Croissant → Pastries".</summary>
        public event Action<string> Message;

        private void Start() => StatusChanged?.Invoke(this);

        private void Update()
        {
            if (_input != null && _input.TidySensePressed) TryPulse();

            int seconds = IsUnlocked ? Mathf.CeilToInt(CooldownRemaining) : -2;
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                StatusChanged?.Invoke(this);
            }
        }

        public bool TryPulse()
        {
            if (!IsUnlocked || !IsReady) return false;
            if (Progress == null || _origin == null) return false;

            Vector3 from = _origin.position;
            CarryInventory<TidyItem> inventory = _carry.Inventory;
            string message;
            Vector3 target;

            if (!inventory.IsEmpty)
            {
                TidyItem item = inventory.Selected;
                ItemSlot slot = NearestSlotFor(item, from);
                for (int i = 0; slot == null && i < inventory.Count; i++)
                {
                    item = inventory.Items[i];
                    slot = NearestSlotFor(item, from);
                }
                if (slot == null)
                {
                    Message?.Invoke("Nothing here needs what you're carrying.");
                    return false;
                }
                target = slot.SnapPoint.position;
                message = string.IsNullOrEmpty(slot.Label) ? item.DisplayName : $"{item.DisplayName} → {slot.Label}";
            }
            else
            {
                TidyItem item = NearestLooseItem(from);
                if (item == null)
                {
                    Message?.Invoke("Everything here is in its place.");
                    return false;
                }
                target = item.transform.position;
                message = item.DisplayName;
            }

            _readyTime = Time.time + _cooldown;
            if (_beacon != null) _beacon.Show(target, _highlightDuration);
            _feedback?.PlayHint();
            Message?.Invoke("Tidy Sense: " + message);
            StatusChanged?.Invoke(this);
            return true;
        }

        private ItemSlot NearestSlotFor(TidyItem item, Vector3 from)
        {
            if (item == null) return null;
            IReadOnlyList<ItemSlot> slots = Progress.Slots;
            ItemSlot best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].CanAccept(item)) continue;
                float sqr = (slots[i].SnapPoint.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = slots[i];
                }
            }
            return best;
        }

        private TidyItem NearestLooseItem(Vector3 from)
        {
            IReadOnlyList<TidyItem> items = Progress.Items;
            TidyItem best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null || items[i].State != ItemState.Loose) continue;
                float sqr = (items[i].transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = items[i];
                }
            }
            return best;
        }

        internal void EditorConfigure(PlayerInputReader input, Transform origin, PlayerCarry carry, LocationProgress progress,
            HintService hints, HintBeacon beacon, FeedbackService feedback)
        {
            _input = input;
            _origin = origin;
            _carry = carry;
            _progress = progress;
            _hints = hints;
            _beacon = beacon;
            _feedback = feedback;
        }
    }
}
