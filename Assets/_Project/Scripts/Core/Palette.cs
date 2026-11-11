using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>The art bible's palette (Docs/ArtBible.md). New colours in code come from here.</summary>
    public static class Palette
    {
        public static readonly Color HoneyOak = Hex("A0784F");
        public static readonly Color CreamPlaster = Hex("EADBC0");
        public static readonly Color Walnut = Hex("6B4A32");
        public static readonly Color Sage = Hex("6E8F6A");
        public static readonly Color Terracotta = Hex("C9714D");
        public static readonly Color WarmStone = Hex("B8AE9C");
        public static readonly Color CoolSlate = Hex("6B7A8F");
        public static readonly Color Amber = Hex("F2B134");
        public static readonly Color Ink = Hex("3D2A1C");
        public static readonly Color Cream = Hex("FFF4DF");

        // Location tints (same warm family).
        public static readonly Color BakeryTeal = Hex("3A8C8A");
        public static readonly Color LibraryGreen = Hex("3E6650");
        public static readonly Color MagicLilac = Hex("C9B5DE");
        public static readonly Color MagicPlum = Hex("5B3A73");

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
