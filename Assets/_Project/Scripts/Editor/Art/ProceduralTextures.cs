using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor.Art
{
    public enum TextureSet
    {
        Planks,
        Wood,
        Plaster,
        Tiles,
        Cobbles,
        RoofTiles,
        Fabric,
        Stone,
    }

    /// <summary>
    /// Generates the tileable storybook textures (albedo + normal map) into Art/Textures/Generated.
    /// Albedo is light and near-neutral so each material tints it with a palette colour; relief comes from
    /// a height field turned into a normal map. Deterministic: same code, same pixels.
    /// </summary>
    public static class ProceduralTextures
    {
        public const string Folder = "Assets/_Project/Art/Textures/Generated";
        private const int Size = 512;

        [MenuItem("Tools/Tidy Up/Art/Generate Textures")]
        public static void GenerateAllMenu()
        {
            GenerateAll(force: true);
            Debug.Log($"[Tidy Up] Generated textures in {Folder}.");
        }

        public static void GenerateAll(bool force)
        {
            foreach (TextureSet set in Enum.GetValues(typeof(TextureSet))) Ensure(set, force);
        }

        public static Texture2D Albedo(TextureSet set)
        {
            Ensure(set, force: false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath(set));
        }

        public static Texture2D Normal(TextureSet set)
        {
            Ensure(set, force: false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath(set));
        }

        private static string AlbedoPath(TextureSet set) => $"{Folder}/{set}_Albedo.png";
        private static string NormalPath(TextureSet set) => $"{Folder}/{set}_Normal.png";

        private static void Ensure(TextureSet set, bool force)
        {
            if (!force && File.Exists(AlbedoPath(set)) && File.Exists(NormalPath(set))) return;
            EditorFolders.Ensure(Folder);

            var albedo = new float[Size * Size];
            var height = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size;
                    float v = (float)y / Size;
                    Sample(set, u, v, out float a, out float h);
                    albedo[y * Size + x] = a;
                    height[y * Size + x] = h;
                }
            }

            WritePng(AlbedoPath(set), ToAlbedo(albedo));
            WritePng(NormalPath(set), ToNormal(height, Strength(set)));
            AssetDatabase.ImportAsset(AlbedoPath(set));
            AssetDatabase.ImportAsset(NormalPath(set));
            ConfigureImporter(AlbedoPath(set), normal: false);
            ConfigureImporter(NormalPath(set), normal: true);
        }

        private static float Strength(TextureSet set)
        {
            switch (set)
            {
                case TextureSet.Plaster: return 2f;
                case TextureSet.Fabric: return 2.5f;
                case TextureSet.Wood: return 1.5f;
                default: return 4f;
            }
        }

        // ───────────── Patterns (u, v in [0,1), tileable) ─────────────

        private static void Sample(TextureSet set, float u, float v, out float albedo, out float height)
        {
            switch (set)
            {
                case TextureSet.Planks: Planks(u, v, out albedo, out height); break;
                case TextureSet.Wood: Wood(u, v, out albedo, out height); break;
                case TextureSet.Plaster: Plaster(u, v, out albedo, out height); break;
                case TextureSet.Tiles: Tiles(u, v, 4, out albedo, out height); break;
                case TextureSet.Cobbles: Cobbles(u, v, out albedo, out height); break;
                case TextureSet.RoofTiles: RoofTiles(u, v, out albedo, out height); break;
                case TextureSet.Fabric: Fabric(u, v, out albedo, out height); break;
                default: StoneBlocks(u, v, out albedo, out height); break;
            }
        }

        /// <summary>Floor boards: 5 boards across, staggered end seams, flowing grain, per-board tint.</summary>
        private static void Planks(float u, float v, out float albedo, out float height)
        {
            const int boards = 5;
            float bv = v * boards;
            int board = Mathf.FloorToInt(bv);
            float across = bv - board;
            float offset = Hash(board, 7) * 0.9f;
            float along = Mathf.Repeat(u + offset, 1f) * 2f; // two board lengths per tile
            float endSeam = Mathf.Min(Mathf.Repeat(along, 1f), 1f - Mathf.Repeat(along, 1f));

            float grain = GrainStripes(u * 6f + board * 3.1f, across + Fbm(u * 4f, v * 4f + board, 4, 3) * 0.15f, 16f);
            float seam = Mathf.Min(Edge(across, 0.035f), Edge(endSeam, 0.012f));
            albedo = 0.80f + (Hash(board, 3) - 0.5f) * 0.16f + grain * 0.035f;
            albedo *= Mathf.Lerp(0.55f, 1f, seam);
            height = 0.5f + grain * 0.03f;
            height *= Mathf.Lerp(0.2f, 1f, seam);
        }

        /// <summary>Continuous wood grain for furniture and shelves.</summary>
        private static void Wood(float u, float v, out float albedo, out float height)
        {
            float grain = GrainStripes(u * 2f, v + Fbm(u * 3f, v * 3f, 3, 3) * 0.08f, 22f);
            albedo = 0.82f + grain * 0.045f + (Fbm(u * 8f, v * 8f, 8, 2) - 0.5f) * 0.06f; // subtle: reads as wood, not stripes
            height = 0.5f + grain * 0.05f;
        }

        /// <summary>Soft hand-trowelled plaster with fine speckles.</summary>
        private static void Plaster(float u, float v, out float albedo, out float height)
        {
            float broad = Fbm(u * 4f, v * 4f, 4, 4);
            float fine = Noise(u * 64f, v * 64f, 64);
            albedo = 0.90f + (broad - 0.5f) * 0.10f + (fine > 0.9f ? -0.05f : 0f);
            height = broad * 0.6f + fine * 0.15f;
        }

        /// <summary>Square floor tiles with light grout and per-tile variation.</summary>
        private static void Tiles(float u, float v, int count, out float albedo, out float height)
        {
            float tu = u * count;
            float tv = v * count;
            int ix = Mathf.FloorToInt(tu);
            int iy = Mathf.FloorToInt(tv);
            float fu = tu - ix;
            float fv = tv - iy;
            float grout = Mathf.Min(Edge(Mathf.Min(fu, 1f - fu), 0.04f), Edge(Mathf.Min(fv, 1f - fv), 0.04f));
            float tile = 0.82f + (Hash(ix * 31 + iy, 5) - 0.5f) * 0.14f + (Fbm(u * 6f, v * 6f, 6, 3) - 0.5f) * 0.06f;
            albedo = Mathf.Lerp(0.97f, tile, grout);
            height = Mathf.Lerp(0.15f, 0.6f + Fbm(u * 10f, v * 10f, 10, 2) * 0.1f, grout);
        }

        /// <summary>Rounded cobblestones from a jittered cell grid (Worley), darker gaps.</summary>
        private static void Cobbles(float u, float v, out float albedo, out float height)
        {
            const int cells = 6;
            Worley(u, v, cells, out float f1, out float f2, out int id);
            float gap = f2 - f1;
            float stone = Mathf.SmoothStep(0f, 1f, gap / 0.12f); // Mathf.SmoothStep(from, to, t), not GLSL smoothstep
            float dome = Mathf.Clamp01(1f - f1 * 1.6f);
            albedo = Mathf.Lerp(0.45f, 0.78f + (Hash(id, 9) - 0.5f) * 0.18f + (Fbm(u * 12f, v * 12f, 12, 2) - 0.5f) * 0.08f, stone);
            height = stone * (0.4f + dome * 0.6f);
        }

        /// <summary>Overlapping rounded roof tiles in staggered rows.</summary>
        private static void RoofTiles(float u, float v, out float albedo, out float height)
        {
            const int rows = 8;
            const int columns = 8;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float fv = rv - row;
            float cu = u * columns + (row % 2) * 0.5f;
            float fu = cu - Mathf.Floor(cu);
            int col = Mathf.FloorToInt(cu);
            float curve = Mathf.Sin(fu * Mathf.PI);              // each tile is a rounded hump
            float overlap = Mathf.SmoothStep(0f, 1f, fv / 0.25f); // shadow where the row above overlaps
            albedo = (0.72f + (Hash(col * 17 + row, 11) - 0.5f) * 0.16f) * Mathf.Lerp(0.6f, 1f, overlap) * Mathf.Lerp(0.85f, 1f, curve);
            height = curve * 0.5f + fv * 0.4f;
        }

        /// <summary>Soft woven fabric for rugs and cushions.</summary>
        private static void Fabric(float u, float v, out float albedo, out float height)
        {
            const float threads = 96f;
            float warp = Mathf.Sin(u * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
            float weft = Mathf.Sin(v * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
            bool over = (Mathf.FloorToInt(u * threads) + Mathf.FloorToInt(v * threads)) % 2 == 0;
            float weave = over ? warp : weft;
            albedo = 0.82f + weave * 0.08f + (Fbm(u * 5f, v * 5f, 5, 3) - 0.5f) * 0.08f;
            height = weave;
        }

        /// <summary>Running-bond stone blocks with mortar (clock tower, foundations).</summary>
        private static void StoneBlocks(float u, float v, out float albedo, out float height)
        {
            const int rows = 6;
            const int columns = 3;
            float rv = v * rows;
            int row = Mathf.FloorToInt(rv);
            float fv = rv - row;
            float cu = u * columns + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(cu);
            float fu = cu - col;
            float mortar = Mathf.Min(Edge(Mathf.Min(fu, 1f - fu), 0.04f), Edge(Mathf.Min(fv, 1f - fv), 0.08f));
            float face = 0.78f + (Hash(col * 13 + row, 2) - 0.5f) * 0.16f + (Fbm(u * 10f, v * 10f, 10, 3) - 0.5f) * 0.12f;
            albedo = Mathf.Lerp(0.62f, face, mortar);
            height = Mathf.Lerp(0.1f, 0.6f + Fbm(u * 16f, v * 16f, 16, 2) * 0.3f, mortar);
        }

        // ───────────── Noise helpers (all periodic over the tile) ─────────────

        private static float GrainStripes(float x, float y, float frequency)
        {
            // x wobbles the stripes; callers pass whole multiples of u so the wobble tiles seamlessly.
            float s = Mathf.Sin((y * frequency + Mathf.Sin(x * Mathf.PI * 2f) * 0.15f) * Mathf.PI * 2f);
            return s * s * 2f - 1f;
        }

        /// <summary>0 at the edge, rising to 1 over width (soft line).</summary>
        private static float Edge(float distance, float width) => Mathf.SmoothStep(0f, 1f, distance / width);

        private static float Hash(int n, int seed)
        {
            unchecked
            {
                uint h = (uint)(n * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Lattice(int x, int y, int period, int seed = 1)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            return Hash(x * 7919 + y, seed);
        }

        /// <summary>Value noise; x, y in lattice units, repeating every period cells.</summary>
        private static float Noise(float x, float y, int period)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Lattice(ix, iy, period);
            float b = Lattice(ix + 1, iy, period);
            float c = Lattice(ix, iy + 1, period);
            float d = Lattice(ix + 1, iy + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal noise. x, y are in units where the base period repeats once per tile.</summary>
        private static float Fbm(float x, float y, int basePeriod, int octaves)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            int period = Mathf.Max(1, basePeriod);
            float scale = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x * scale, y * scale, period) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                scale *= 2f;
                period *= 2;
            }
            return sum / total;
        }

        private static void Worley(float u, float v, int cells, out float f1, out float f2, out int id)
        {
            float x = u * cells;
            float y = v * cells;
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            f1 = f2 = float.MaxValue;
            id = 0;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox;
                    int gy = cy + oy;
                    int wx = ((gx % cells) + cells) % cells;
                    int wy = ((gy % cells) + cells) % cells;
                    float px = gx + 0.15f + Hash(wx * 101 + wy, 21) * 0.7f;
                    float py = gy + 0.15f + Hash(wx * 101 + wy, 37) * 0.7f;
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = wx * 101 + wy;
                    }
                    else if (d < f2) f2 = d;
                }
            }
        }

        // ───────────── Output ─────────────

        private static Color32[] ToAlbedo(float[] values)
        {
            var pixels = new Color32[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                byte g = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i]) * 255f);
                pixels[i] = new Color32(g, g, g, 255);
            }
            return pixels;
        }

        private static Color32[] ToNormal(float[] height, float strength)
        {
            var pixels = new Color32[height.Length];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float l = height[y * Size + (x + Size - 1) % Size];
                    float r = height[y * Size + (x + 1) % Size];
                    float d = height[((y + Size - 1) % Size) * Size + x];
                    float t = height[((y + 1) % Size) * Size + x];
                    var n = new Vector3((l - r) * strength, (d - t) * strength, 1f).normalized;
                    pixels[y * Size + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
                }
            }
            return pixels;
        }

        private static void WritePng(string assetPath, Color32[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void ConfigureImporter(string assetPath, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }
    }
}
