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
        // Rich interiors (art step 2)
        Parquet,
        Wainscot,
        Damask,
        Brick,
        /// <summary>Full colour (navy coffers, gold stars and frames): tint white.</summary>
        StarPanels,
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

            WritePng(AlbedoPath(set), set == TextureSet.StarPanels ? StarPanelsColour() : ToAlbedo(albedo));
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
                case TextureSet.Damask: return 1.5f;
                case TextureSet.Parquet: return 3f;
                case TextureSet.StarPanels: return 3f;
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
                case TextureSet.Parquet: Parquet(u, v, out albedo, out height); break;
                case TextureSet.Wainscot: Wainscot(u, v, out albedo, out height); break;
                case TextureSet.Damask: Damask(u, v, out albedo, out height); break;
                case TextureSet.Brick: Brick(u, v, out albedo, out height); break;
                case TextureSet.StarPanels: StarPanels(u, v, out _, out height, out albedo); break;
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

        // ───────────── Rich interiors (art step 2) ─────────────

        /// <summary>
        /// Basket-weave parquet: a 4×4 grid of squares, each of four boards, alternating direction like a
        /// checkerboard. Per-board tint, grain along each board, fine dark seams.
        /// </summary>
        private static void Parquet(float u, float v, out float albedo, out float height)
        {
            const int cells = 4, boards = 4;
            float cu = u * cells, cv = v * cells;
            int cx = Mathf.FloorToInt(cu), cy = Mathf.FloorToInt(cv);
            float fu = cu - cx, fv = cv - cy;
            bool horizontal = ((cx + cy) & 1) == 0;
            float across = horizontal ? fv : fu;
            float along = horizontal ? fu : fv;
            int board = Mathf.Min(boards - 1, Mathf.FloorToInt(across * boards));
            float inBoard = across * boards - board;
            int id = (cx * 31 + cy * 17) * 7 + board;

            float grain = GrainStripes(along * 1.5f + id * 0.37f, inBoard * 0.6f + Fbm(u * 6f + board, v * 6f, 6, 2) * 0.1f, 14f);
            float seam = Mathf.Min(Edge(Mathf.Min(inBoard, 1f - inBoard), 0.05f),
                                   Mathf.Min(Edge(Mathf.Min(fu, 1f - fu), 0.012f), Edge(Mathf.Min(fv, 1f - fv), 0.012f)));
            albedo = 0.78f + (Hash(id, 5) - 0.5f) * 0.2f + grain * 0.045f;
            albedo *= Mathf.Lerp(0.45f, 1f, seam);
            height = (0.55f + grain * 0.03f) * Mathf.Lerp(0.25f, 1f, seam);
        }

        /// <summary>
        /// One raised wooden panel per tile: flat stiles and rails, a dark groove, a bevel up to the raised field.
        /// The material offsets this by half a tile so a panel sits centred in the wall band.
        /// </summary>
        private static void Wainscot(float u, float v, out float albedo, out float height)
        {
            const float frame = 0.13f, groove = 0.018f, bevel = 0.07f;
            float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float grain = GrainStripes(v * 2f, u * 0.5f + Fbm(u * 4f, v * 4f, 4, 2) * 0.08f, 12f);
            if (d < frame)
            {
                height = 0.62f;
                albedo = 0.8f + grain * 0.035f;
            }
            else if (d < frame + groove)
            {
                height = 0.3f;
                albedo = 0.55f;
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, (d - frame - groove) / bevel);
                height = Mathf.Lerp(0.32f, 0.72f, t);
                albedo = Mathf.Lerp(0.68f, 0.84f, t) + grain * 0.035f;
            }
        }

        /// <summary>
        /// Damask wallpaper: a mirrored flourish in the middle of each tile and a smaller one on the corners
        /// (half-drop), on faint stripes. Two quiet tones: it reads as pattern, not noise.
        /// </summary>
        private static void Damask(float u, float v, out float albedo, out float height)
        {
            float big = DamaskMotif((u - 0.5f) * 2.2f, (v - 0.5f) * 2.2f);
            float cu = Mathf.Repeat(u + 0.5f, 1f) - 0.5f, cv = Mathf.Repeat(v + 0.5f, 1f) - 0.5f;
            float small = DamaskMotif(cu * 3.6f, cv * 3.6f);
            float motif = Mathf.Max(big, small * 0.85f);
            float stripe = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 2f * 8f);
            albedo = 0.66f + stripe * 0.02f + motif * 0.3f + (Fbm(u * 8f, v * 8f, 8, 2) - 0.5f) * 0.03f;
            height = 0.5f + motif * 0.08f;
        }

        /// <summary>A symmetric flourish: petals around a heart, a leaf above and below, mirrored left/right.</summary>
        private static float DamaskMotif(float x, float y)
        {
            x = Mathf.Abs(x);
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float petals = Mathf.SmoothStep(0.02f, -0.02f, r - (0.32f + 0.1f * Mathf.Cos(a * 6f)));
            float heart = Mathf.SmoothStep(0.02f, -0.02f, r - 0.12f);
            float leafTop = Mathf.SmoothStep(0.03f, -0.03f, Mathf.Sqrt(x * x * 9f + (y - 0.62f) * (y - 0.62f) * 2.2f) - 0.28f);
            float leafBottom = Mathf.SmoothStep(0.03f, -0.03f, Mathf.Sqrt(x * x * 9f + (y + 0.62f) * (y + 0.62f) * 2.2f) - 0.28f);
            float curl = Mathf.SmoothStep(0.025f, 0f, Mathf.Abs(Mathf.Sqrt((x - 0.42f) * (x - 0.42f) + y * y) - 0.16f)) * (x > 0.3f ? 1f : 0f);
            float ring = Mathf.SmoothStep(0.02f, 0f, Mathf.Abs(r - 0.22f)) * 0.6f;
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(petals - heart * 0.5f + ring, Mathf.Max(leafTop, leafBottom)), curl));
        }

        /// <summary>Running-bond brick: 14 courses, 4 bricks each, mortar joints, per-brick tone and speckle.</summary>
        private static void Brick(float u, float v, out float albedo, out float height)
        {
            const int courses = 14, perCourse = 4;
            float cv = v * courses;
            int row = Mathf.FloorToInt(cv);
            float inRow = cv - row;
            float cu = u * perCourse + ((row & 1) == 0 ? 0f : 0.5f);
            int col = Mathf.FloorToInt(cu);
            float inCol = cu - col;
            int id = row * 13 + ((col % perCourse) + perCourse) % perCourse;

            float mortar = Mathf.Min(Edge(Mathf.Min(inRow, 1f - inRow), 0.1f), Edge(Mathf.Min(inCol, 1f - inCol), 0.03f));
            float speckle = Fbm(u * 32f, v * 32f, 32, 2);
            float tone = 0.62f + Hash(id, 9) * 0.3f + (speckle - 0.5f) * 0.12f;
            albedo = Mathf.Lerp(0.9f, tone, mortar); // light mortar, varied bricks
            height = Mathf.Lerp(0.2f, 0.62f + (speckle - 0.5f) * 0.08f, mortar);
        }

        /// <summary>
        /// Coffered ceiling panel: gilded frame, a navy field scattered with little gold stars. Colour is baked
        /// in (StarPanelsColour); albedo here is only used to shade the height map.
        /// </summary>
        private static void StarPanels(float u, float v, out Color colour, out float height, out float albedo)
        {
            const float frame = 0.07f, lip = 0.02f;
            float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            Color navy = new Color(0.13f, 0.18f, 0.38f);
            Color gold = new Color(0.86f, 0.7f, 0.38f);
            float cloud = Fbm(u * 4f, v * 4f, 4, 3);

            if (d < frame)
            {
                // Gilded frame with a little worn variation.
                colour = gold * (0.92f + Fbm(u * 16f, v * 16f, 16, 2) * 0.1f);
                height = 0.7f;
                albedo = 0.8f;
                return;
            }
            if (d < frame + lip)
            {
                colour = gold * 0.55f;
                height = 0.45f;
                albedo = 0.5f;
                return;
            }

            colour = navy * (0.85f + cloud * 0.3f);
            height = 0.4f;
            albedo = 0.3f;

            // Stars on a jittered 6×6 grid (not every cell), four-pointed, with a soft glow.
            const int grid = 7;
            float gu = u * grid, gv = v * grid;
            int gx = Mathf.FloorToInt(gu), gy = Mathf.FloorToInt(gv);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
            {
                int sx = gx + ox, sy = gy + oy;
                int wx = ((sx % grid) + grid) % grid, wy = ((sy % grid) + grid) % grid;
                int cell = wx * 37 + wy * 101;
                if (Hash(cell, 3) < 0.2f) continue;
                float px = sx + 0.2f + Hash(cell, 5) * 0.6f, py = sy + 0.2f + Hash(cell, 7) * 0.6f;
                float dx = Mathf.Abs(gu - px), dy = Mathf.Abs(gv - py);
                float size = 0.11f + Hash(cell, 11) * 0.08f;
                float star = Mathf.Max(Mathf.SmoothStep(size, 0f, dx * 3f + dy), Mathf.SmoothStep(size, 0f, dy * 3f + dx));
                float glow = Mathf.SmoothStep(size * 2.5f, 0f, Mathf.Sqrt(dx * dx + dy * dy)) * 0.25f;
                float s = Mathf.Clamp01(star + glow);
                if (s <= 0f) continue;
                colour = Color.Lerp(colour, gold * 1.1f, s);
                height = Mathf.Max(height, 0.4f + star * 0.15f);
            }
        }

        private static Color32[] StarPanelsColour()
        {
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                StarPanels((float)x / Size, (float)y / Size, out Color c, out _, out _);
                pixels[y * Size + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
            }
            return pixels;
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
