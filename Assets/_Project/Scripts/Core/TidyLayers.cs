using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Physics layers. Items live on their own layer so the player walks through Muddle piles instead of
    /// bumping into hundreds of them, and so the Muddle never stacks one pile on another.
    /// </summary>
    public static class TidyLayers
    {
        public const string ItemsName = "Items";
        private static int _items = -2;

        /// <summary>Index of the Items layer, or -1 if the project doesn't define it.</summary>
        public static int Items => _items == -2 ? (_items = LayerMask.NameToLayer(ItemsName)) : _items;

        /// <summary>Mask with the Items layer removed.</summary>
        public static int WithoutItems(int mask) => Items >= 0 ? mask & ~(1 << Items) : mask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _items = -2;
    }
}
