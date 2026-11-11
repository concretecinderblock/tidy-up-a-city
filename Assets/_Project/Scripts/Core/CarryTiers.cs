namespace TidyUp.Core
{
    /// <summary>
    /// Carry upgrades (CLAUDE.md section 5): hands 10 → basket 20 → trolley 40 → satchel unlimited, earned by
    /// the total number of items placed across the city. Derived from progress, so nothing extra is saved.
    /// </summary>
    public static class CarryTiers
    {
        public readonly struct Tier
        {
            public readonly string Name;
            public readonly int Capacity;
            public readonly int PlacedNeeded;

            public Tier(string name, int capacity, int placedNeeded)
            {
                Name = name;
                Capacity = capacity;
                PlacedNeeded = placedNeeded;
            }

            public bool Unlimited => Capacity == CarryInventory<object>.Unlimited;
        }

        public static readonly Tier[] All =
        {
            new Tier("Hands", 10, 0),
            new Tier("Basket", 20, 100),
            new Tier("Trolley", 40, 500),
            new Tier("Satchel", CarryInventory<object>.Unlimited, 2000),
        };

        /// <summary>Index of the best tier earned with this many items placed.</summary>
        public static int IndexFor(int totalPlaced)
        {
            int index = 0;
            for (int i = 0; i < All.Length; i++)
            {
                if (totalPlaced >= All[i].PlacedNeeded) index = i;
            }
            return index;
        }

        public static Tier For(int totalPlaced) => All[IndexFor(totalPlaced)];

        /// <summary>Items still to place for the next tier, or 0 at the top.</summary>
        public static int ToNext(int totalPlaced)
        {
            int index = IndexFor(totalPlaced);
            return index + 1 < All.Length ? All[index + 1].PlacedNeeded - totalPlaced : 0;
        }
    }
}
