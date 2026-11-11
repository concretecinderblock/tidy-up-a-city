using System;
using UnityEngine;

namespace TidyUp.Items
{
    /// <summary>
    /// Rules for one placeable slot. Serialized inline on each ItemSlot (thousands of slots per
    /// location, so not one ScriptableObject per slot).
    /// </summary>
    [Serializable]
    public sealed class SlotDefinition
    {
        [Tooltip("Item slot types this slot accepts (any match). Empty = only Exact Item Id fits.")]
        [SerializeField] private string[] _acceptedSlotTypes = Array.Empty<string>();

        [Tooltip("Optional: only this exact item id fits.")]
        [SerializeField] private string _exactItemId = string.Empty;

        [Tooltip("Optional order index for ordered sorts (family B). -1 = unordered.")]
        [SerializeField] private int _orderIndex = -1;

        [Tooltip("Degrees. Within this, placement counts as 'perfect alignment'. 0 = no precision bonus.")]
        [SerializeField] private float _rotationTolerance;

        [Tooltip("Human-readable label shown when looking at an empty slot, e.g. 'Baguettes'.")]
        [SerializeField] private string _label = string.Empty;

        public string[] AcceptedSlotTypes => _acceptedSlotTypes;
        public string ExactItemId => _exactItemId;
        public int OrderIndex => _orderIndex;
        public float RotationTolerance => _rotationTolerance;
        public string Label => _label;

        public SlotDefinition() { }

        public SlotDefinition(string[] acceptedSlotTypes, string exactItemId = "", int orderIndex = -1,
            float rotationTolerance = 0f, string label = "")
        {
            _acceptedSlotTypes = acceptedSlotTypes ?? Array.Empty<string>();
            _exactItemId = exactItemId ?? string.Empty;
            _orderIndex = orderIndex;
            _rotationTolerance = rotationTolerance;
            _label = label ?? string.Empty;
        }
    }
}
