using System.Collections.Generic;
using UnityEngine;

namespace TidyUp.Items
{
    /// <summary>Pure slot validation logic. No scene dependencies, so it's EditMode-testable.</summary>
    public static class SlotRules
    {
        public static bool Accepts(SlotDefinition slot, ItemDefinition item)
        {
            if (item == null) return false;
            return Accepts(slot, item.Id, item.SlotTypes);
        }

        public static bool Accepts(SlotDefinition slot, string itemId, IReadOnlyList<string> itemSlotTypes)
        {
            if (slot == null || string.IsNullOrEmpty(itemId)) return false;

            bool hasExact = !string.IsNullOrEmpty(slot.ExactItemId);
            if (hasExact && slot.ExactItemId != itemId) return false;

            string[] accepted = slot.AcceptedSlotTypes;
            if (accepted == null || accepted.Length == 0) return hasExact;
            if (itemSlotTypes == null) return false;

            for (int a = 0; a < accepted.Length; a++)
            {
                for (int i = 0; i < itemSlotTypes.Count; i++)
                {
                    if (accepted[a] == itemSlotTypes[i]) return true;
                }
            }
            return false;
        }

        /// <summary>True when the item rotation is within the slot's precision tolerance.</summary>
        public static bool IsPerfectAlignment(SlotDefinition slot, Quaternion slotRotation, Quaternion itemRotation)
        {
            if (slot == null || slot.RotationTolerance <= 0f) return false;
            return Quaternion.Angle(slotRotation, itemRotation) <= slot.RotationTolerance;
        }
    }
}
