namespace TidyUp.Items
{
    /// <summary>The ten reusable mechanic families (CLAUDE.md section 6).</summary>
    public enum MechanicFamily
    {
        ShelfSort = 0,       // A
        OrderedSort = 1,     // B
        Reassembly = 2,      // C
        PatternGradient = 3, // D
        ReferenceMatch = 4,  // E
        Pairing = 5,         // F
        DressFit = 6,        // G
        Orders = 7,          // H
        LivingItems = 8,     // I
        ShadowBoards = 9     // J
    }

    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Oddity = 3 // Muddle oddities (collectibles)
    }
}
