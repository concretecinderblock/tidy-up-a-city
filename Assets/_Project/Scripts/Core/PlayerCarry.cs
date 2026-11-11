using System;
using TidyUp.Items;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>Scene-side owner of the player's carried items and their carry tier (hands, basket, …).</summary>
    public sealed class PlayerCarry : MonoBehaviour
    {
        [Tooltip("Starting capacity (hands). Upgrades come from CarryUpgrades.")]
        [SerializeField] private int _capacity = 10;

        private CarryInventory<TidyItem> _inventory;

        public CarryInventory<TidyItem> Inventory => _inventory ??= new CarryInventory<TidyItem>(Mathf.Max(1, _capacity));

        /// <summary>What the player carries with: "Hands", "Basket", "Trolley", "Satchel".</summary>
        public string TierName { get; private set; } = "Hands";

        public bool Unlimited => Inventory.Capacity == CarryInventory<TidyItem>.Unlimited;

        public event Action Changed
        {
            add => Inventory.Changed += value;
            remove => Inventory.Changed -= value;
        }

        public void SetTier(string name, int capacity)
        {
            TierName = name;
            Inventory.SetCapacity(capacity); // raises Changed, so the HUD redraws
        }
    }
}
