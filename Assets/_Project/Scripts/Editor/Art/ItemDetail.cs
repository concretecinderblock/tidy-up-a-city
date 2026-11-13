using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Art step 4: the detail that makes items read as real things (Docs/ArtBible.md).
    /// Books: a cloth cover tinted per kind, plus one shared "detail" material whose atlas holds gold-foil cover
    /// designs, spine bands and the cream page block (foil is metallic, pages matte, everything else cut out).
    /// Glass for potion bottles, a crust texture for baked goods.
    /// Atlas cells (2×2): 0 framed cover with a diamond emblem, 1 cover with corner flourishes and a round crest,
    /// 2 spine bands, 3 pages.
    /// </summary>
    public static class ItemDetail
    {
        private const string Folder = ProceduralTextures.Folder;
        private const int AtlasSize = 512;

        public static Vector2 CellMin(int cell) => new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
        public const float CellSize = 0.5f;

        // ───────────── Materials ─────────────

        /// <summary>Shared by every book: foil designs, spine bands and pages (one draw per book for all of it).</summary>
        public static Material BookDetailMaterial()
        {
            Material m = GreyboxAssets.Lit("Book_Detail", Color.white);
            m.SetTexture("_BaseMap", Atlas());
            m.SetTexture("_MetallicGlossMap", MetalMap());
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Smoothness", 1f); // smoothness comes from the map's alpha
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Book cloth in the kind's colour: fine weave, edges rubbed lighter with age.</summary>
        public static Material BookCover(Color color)
        {
            Material m = GreyboxAssets.Lit("Book_" + ColorUtility.ToHtmlStringRGB(color), color);
            m.SetTexture("_BaseMap", Cloth());
            m.SetTextureOffset("_BaseMap", new Vector2(0f, 0.5f)); // box UVs are centred: put the worn edges on the cover edges
            m.SetFloat("_Smoothness", 0.28f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Clear, slightly tinted glass for bottles (transparent, shared by every bottle).</summary>
        public static Material Glass()
        {
            Material m = GreyboxAssets.Lit("Item_Glass", new Color(0.8f, 0.9f, 0.92f, 0.32f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.86f);
            m.SetFloat("_BlendModePreserveSpecular", 1f); // full reflections on clear glass: that is what makes it read as a bottle
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Greyscale crust (tinted by the item's colour): baked mottling and flour dusting.</summary>
        public static Texture2D Crust() => Generate("Item_Crust", 256, false, (u, v) =>
        {
            float bake = Mathf.PerlinNoise(u * 9f + 3f, v * 9f + 7f) * 0.6f + Mathf.PerlinNoise(u * 31f, v * 31f) * 0.4f;
            float shade = Mathf.Lerp(0.72f, 1.02f, bake);
            float flour = Mathf.PerlinNoise(u * 70f + 11f, v * 70f + 5f);
            if (flour > 0.74f) shade = Mathf.Lerp(shade, 1.35f, (flour - 0.74f) * 3f); // pale flour flecks (clamped below)
            return new Color(shade, shade * 0.98f, shade * 0.95f, 1f);
        });

        // ───────────── Textures ─────────────

        private static Texture2D Cloth() => Generate("Book_Cloth", 256, false, (u, v) =>
        {
            float warp = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 90f);
            float weft = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 90f);
            float weave = 0.9f + 0.05f * warp * weft + (Mathf.PerlinNoise(u * 12f, v * 12f) - 0.5f) * 0.08f;
            // One cover per 0..1 tile: rub the edges and corners lighter, like handled books.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float wear = Mathf.SmoothStep(0.06f, 0f, edge) * 0.25f;
            float g = weave + wear;
            return new Color(g, g, g, 1f);
        });

        private static Texture2D Atlas() => Generate("Book_Detail_Atlas", AtlasSize, false, (u, v) =>
        {
            int cell = (u >= 0.5f ? 1 : 0) + (v >= 0.5f ? 2 : 0);
            float cu = Mathf.Repeat(u * 2f, 1f), cv = Mathf.Repeat(v * 2f, 1f);
            Color foil = new Color(0.86f, 0.68f, 0.36f, 1f);
            switch (cell)
            {
                case 0: return Mask(CoverA(cu, cv), foil);
                case 1: return Mask(CoverB(cu, cv), foil);
                case 2: return Mask(Spine(cu, cv), foil);
                default:
                {
                    // Page block: cream with fine page lines.
                    float lines = 0.96f + 0.04f * Mathf.Sin(cv * Mathf.PI * 2f * 60f);
                    return new Color(0.94f * lines, 0.89f * lines, 0.77f * lines, 1f);
                }
            }
        });

        /// <summary>R = metallic, A = smoothness. Foil is polished metal; pages are matte paper.</summary>
        private static Texture2D MetalMap() => Generate("Book_Detail_Metal", AtlasSize, true, (u, v) =>
        {
            bool pages = u >= 0.5f && v >= 0.5f;
            return pages ? new Color(0f, 0f, 0f, 0.1f) : new Color(0.9f, 0f, 0f, 0.72f);
        });

        private static Color Mask(float a, Color foil)
        {
            Color c = foil;
            c.a = a > 0.5f ? 1f : 0f;
            return c;
        }

        private static float Line(float d, float w) => d < w ? 1f : 0f;

        /// <summary>Double frame, a diamond emblem, two title lines of "lettering".</summary>
        private static float CoverA(float u, float v)
        {
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float a = Mathf.Max(Line(Mathf.Abs(edge - 0.08f), 0.012f), Line(Mathf.Abs(edge - 0.115f), 0.006f));
            float dx = Mathf.Abs(u - 0.5f), dy = Mathf.Abs(v - 0.42f);
            a = Mathf.Max(a, Line(Mathf.Abs(dx + dy * 0.8f - 0.13f), 0.012f));            // diamond outline
            a = Mathf.Max(a, dx + dy * 0.8f < 0.05f ? 1f : 0f);                          // its heart
            a = Mathf.Max(a, Lettering(u, v, 0.72f, 0.3f, 0.7f));
            a = Mathf.Max(a, Lettering(u, v, 0.64f, 0.36f, 0.64f));
            return a;
        }

        /// <summary>Corner flourishes, a round crest with a star, one big title line.</summary>
        private static float CoverB(float u, float v)
        {
            float a = 0f;
            foreach (Vector2 c in new[] { new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.12f), new Vector2(0.12f, 0.88f), new Vector2(0.88f, 0.88f) })
            {
                float r = Vector2.Distance(new Vector2(u, v), c);
                a = Mathf.Max(a, Line(Mathf.Abs(r - 0.06f), 0.01f));
                a = Mathf.Max(a, r < 0.022f ? 1f : 0f);
            }
            float cr = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.45f));
            a = Mathf.Max(a, Line(Mathf.Abs(cr - 0.17f), 0.012f));
            a = Mathf.Max(a, Line(Mathf.Abs(cr - 0.14f), 0.005f));
            float sx = Mathf.Abs(u - 0.5f), sy = Mathf.Abs(v - 0.45f);
            a = Mathf.Max(a, Mathf.Min(sx * 4f + sy, sy * 4f + sx) < 0.09f ? 1f : 0f);     // four-pointed star
            a = Mathf.Max(a, Lettering(u, v, 0.75f, 0.24f, 0.76f));
            return a;
        }

        /// <summary>Spine: gold bands near the top and bottom, a title panel in between.</summary>
        private static float Spine(float u, float v)
        {
            float a = 0f;
            foreach (float y in new[] { 0.08f, 0.14f, 0.86f, 0.92f }) a = Mathf.Max(a, Line(Mathf.Abs(v - y), 0.012f));
            float panel = Mathf.Max(Mathf.Abs(u - 0.5f) / 0.32f, Mathf.Abs(v - 0.62f) / 0.12f);
            a = Mathf.Max(a, Line(Mathf.Abs(panel - 1f), 0.08f));
            // Little vertical "letters" in the panel.
            if (panel < 0.85f && Mathf.Repeat(v * 40f, 1f) < 0.55f && Mathf.Abs(u - 0.5f) < 0.12f) a = 1f;
            return a;
        }

        /// <summary>A line of fake lettering: short blocks with gaps, centred between x0 and x1 at height y.</summary>
        private static float Lettering(float u, float v, float y, float x0, float x1)
        {
            if (Mathf.Abs(v - y) > 0.018f || u < x0 || u > x1) return 0f;
            float t = (u - x0) / (x1 - x0) * 14f;
            int letter = Mathf.FloorToInt(t);
            float inLetter = t - letter;
            bool space = Mathf.PerlinNoise(letter * 0.77f, y * 10f) > 0.68f;
            return !space && inLetter > 0.15f && inLetter < 0.85f ? 1f : 0f;
        }

        // ───────────── Writing textures ─────────────

        private static Texture2D Generate(string name, int size, bool linear, System.Func<float, float, Color> pixel)
        {
            string path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Folder);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = pixel((x + 0.5f) / size, (y + 0.5f) / size);
                pixels[y * size + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.sRGBTexture = !linear;
                importer.alphaIsTransparency = !linear;
                bool atlas = name.StartsWith("Book_Detail");
                importer.wrapMode = atlas ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                if (atlas && !linear)
                {
                    // Keep the cut-out foil from thinning away in the distance.
                    importer.mipMapsPreserveCoverage = true;
                    importer.alphaTestReferenceValue = 0.5f;
                }
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
