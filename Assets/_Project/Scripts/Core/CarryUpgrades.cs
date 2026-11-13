using System.Collections.Generic;
using TidyUp.Locations;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Gives the player the carry tier they've earned (CarryTiers) from the total items placed across the
    /// city: saved progress for other locations plus the live count of the one they're in. Announces upgrades
    /// as they happen; a loaded game just starts at its tier.
    /// </summary>
    public sealed class CarryUpgrades : MonoBehaviour
    {
        [SerializeField] private PlayerCarry _carry;

        private LocationProgress _watched;
        private int _tierIndex = -1;

        private void OnEnable()
        {
            GameServices.ActiveLocationChanged += Watch;
            Watch(GameServices.ActiveLocation);
        }

        private void OnDisable()
        {
            GameServices.ActiveLocationChanged -= Watch;
            Watch(null);
        }

        private void Start() => Apply(announce: false); // after a save has been applied (SaveService, order -150)

        private void Watch(LocationRuntime location)
        {
            if (_watched != null) _watched.ProgressChanged -= HandleProgress;
            _watched = location != null ? location.Progress : null;
            if (_watched != null) _watched.ProgressChanged += HandleProgress;
        }

        private void HandleProgress(LocationProgress progress) => Apply(announce: _tierIndex >= 0);

        public int TotalPlaced()
        {
            LocationRuntime active = GameServices.ActiveLocation;
            string activeId = active != null && active.Location != null ? active.StateKey : null;
            int total = 0;
            foreach (KeyValuePair<string, bool[]> pair in LocationStateStore.Snapshot())
            {
                if (pair.Key == activeId) continue;
                foreach (bool filled in pair.Value) if (filled) total++;
            }
            if (active != null && active.Progress != null) total += active.Progress.Placed;
            return total;
        }

        private void Apply(bool announce)
        {
            if (_carry == null) return;
            int total = TotalPlaced();
            int index = CarryTiers.IndexFor(total);
            if (index == _tierIndex) return;
            bool upgrade = index > _tierIndex && _tierIndex >= 0;
            _tierIndex = index;
            CarryTiers.Tier tier = CarryTiers.All[index];
            _carry.SetTier(tier.Name, tier.Capacity);
            if (announce && upgrade)
            {
                GameServices.Announce(tier.Unlimited
                    ? $"You found a magic satchel! Carry as much as you like."
                    : $"You found a {tier.Name.ToLowerInvariant()}! You can carry {tier.Capacity} things now.");
                GameServices.Feedback?.PlayRestore(false);
            }
        }

        internal void EditorConfigure(PlayerCarry carry) => _carry = carry;
    }
}
