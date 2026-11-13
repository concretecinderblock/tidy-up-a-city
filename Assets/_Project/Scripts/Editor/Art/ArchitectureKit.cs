using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Art step 3: the pieces that make a box into a room (Docs/ArtBible.md). Chandeliers, wall sconces,
    /// pilasters, crown mouldings, framed paintings and maps, ornate bookcase tops and the Library's gallery.
    /// Everything is generated; multi-part pieces are merged into one mesh (submesh per material) so a room
    /// full of them stays cheap. Convention for wall pieces: local -Z faces into the room.
    /// </summary>
    public static class ArchitectureKit
    {
        // ───────────── Materials ─────────────

        public static Material Brass()
        {
            Material m = GreyboxAssets.Lit("Arch_Brass", new Color(0.78f, 0.6f, 0.3f));
            m.SetFloat("_Metallic", 0.85f);
            m.SetFloat("_Smoothness", 0.62f);
            return m;
        }

        public static Material Gilt()
        {
            Material m = GreyboxAssets.Lit("Arch_Gilt", new Color(0.82f, 0.64f, 0.34f));
            m.SetFloat("_Metallic", 0.7f);
            m.SetFloat("_Smoothness", 0.5f);
            return m;
        }

        private static Material Wax() => GreyboxAssets.Lit("Arch_CandleWax", new Color(0.96f, 0.9f, 0.78f));
        private static Material Flame() => GreyboxAssets.Emissive("Arch_CandleFlame", new Color(1f, 0.85f, 0.55f), new Color(1f, 0.62f, 0.28f), 5f);

        // ───────────── Mesh helpers ─────────────

        private struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public int Material;
        }

        private static Part P(Mesh mesh, Matrix4x4 m, int material) => new Part { Mesh = mesh, Matrix = m, Material = material };
        private static Part P(Mesh mesh, Vector3 position, int material) => P(mesh, Matrix4x4.Translate(position), material);
        private static Vector2 V(float r, float h) => new Vector2(r, h);

        /// <summary>Merges parts into one mesh with one submesh per material, saved under key (cached).</summary>
        private static Mesh Assemble(string key, List<Part> parts, int materialCount)
        {
            return MeshAssets.GetOrCreate(key, () =>
            {
                var perMaterial = new Mesh[materialCount];
                for (int m = 0; m < materialCount; m++)
                {
                    var combine = new List<CombineInstance>();
                    foreach (Part part in parts) if (part.Material == m) combine.Add(new CombineInstance { mesh = part.Mesh, transform = part.Matrix });
                    perMaterial[m] = new Mesh { indexFormat = IndexFormat.UInt32 };
                    perMaterial[m].CombineMeshes(combine.ToArray(), true, true);
                }
                var instances = new CombineInstance[materialCount];
                for (int m = 0; m < materialCount; m++) instances[m] = new CombineInstance { mesh = perMaterial[m], transform = Matrix4x4.identity };
                var mesh = new Mesh { name = key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(instances, false, false);
                mesh.RecalculateBounds();
                foreach (Mesh pm in perMaterial) Object.DestroyImmediate(pm);
                return mesh;
            }, rebuild: true);
        }

        private static GameObject Place(string name, Transform parent, Mesh mesh, Material[] materials, bool castShadows, bool isStatic = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (isStatic) GreyboxAssets.MarkStatic(go);
            return go;
        }

        private static Vector2[] Circle(float ringRadius, float tubeRadius, int steps, float y)
        {
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / steps;
                points[i] = new Vector2(ringRadius + tubeRadius * Mathf.Cos(a), y + tubeRadius * Mathf.Sin(a));
            }
            return points;
        }

        // ───────────── Chandelier ─────────────

        /// <summary>
        /// A brass chandelier hanging from the ceiling: chain, turned body, a ring of six candles with glowing
        /// flames, and a hidden downlight underneath (named "Light" so the mood tunes it like any lamp).
        /// root is placed at floor level below the attachment point.
        /// </summary>
        public static Transform Chandelier(string name, Transform parent, Vector3 floorPosition, float ceiling)
        {
            float drop = ceiling > 5f ? 2.3f : 1.05f;  // tall rooms: hang lower so the light reaches the floor
            float bodyY = ceiling - drop;              // bottom of the body
            int dropCm = Mathf.RoundToInt(drop * 100f), ceilCm = Mathf.RoundToInt(ceiling * 100f);
            const float ring = 0.36f;
            const int candles = 6;

            var parts = new List<Part>();
            // 0 brass, 1 wax, 2 flame
            parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.012f, 0f), V(0.012f, drop - 0.48f), V(0f, drop - 0.48f) }, 8, "Chain"), new Vector3(0f, bodyY + 0.48f, 0f), 0));
            parts.Add(P(MeshGen.Lathe(new[] { V(0f, -0.06f), V(0.11f, -0.06f), V(0.09f, -0.02f), V(0.03f, 0f), V(0f, 0f) }, 16, "Canopy"), new Vector3(0f, ceiling, 0f), 0));
            parts.Add(P(MeshGen.Lathe(new[]
            {
                V(0f, -0.08f), V(0.025f, -0.08f), V(0.05f, -0.02f), V(0.075f, 0.04f), V(0.06f, 0.12f), V(0.12f, 0.2f), V(0.13f, 0.24f),
                V(0.07f, 0.3f), V(0.045f, 0.38f), V(0.03f, 0.48f), V(0f, 0.48f),
            }, 18, "Body"), new Vector3(0f, bodyY, 0f), 0));
            parts.Add(P(MeshGen.Lathe(Circle(ring, 0.016f, 8, 0f), 28, "Ring"), new Vector3(0f, bodyY + 0.21f, 0f), 0));
            for (int i = 0; i < candles; i++)
            {
                float a = i * Mathf.PI * 2f / candles;
                var at = new Vector3(Mathf.Cos(a) * ring, bodyY + 0.21f, Mathf.Sin(a) * ring);
                // A little S-curved arm from the body to the cup.
                var arm = Matrix4x4.TRS(new Vector3(at.x * 0.5f, at.y - 0.01f, at.z * 0.5f), // from the body out to the cup
                    Quaternion.LookRotation(new Vector3(at.x, 0f, at.z)), new Vector3(0.014f, 0.014f, ring));
                parts.Add(P(MeshGen.RoundedBox(Vector3.one, 0.3f, 1), arm, 0));
                parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.035f, 0f), V(0.05f, 0.035f), V(0.045f, 0.04f), V(0f, 0.04f) }, 12, "Cup"), at, 0));
                parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.017f, 0f), V(0.017f, 0.11f), V(0.013f, 0.12f), V(0f, 0.12f) }, 10, "Candle"), at + new Vector3(0f, 0.035f, 0f), 1));
                parts.Add(P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.012f, 0.012f), V(0.014f, 0.025f), V(0.008f, 0.045f), V(0f, 0.06f) }, 8, "Flame"), at + new Vector3(0f, 0.16f, 0f), 2));
            }

            Mesh mesh = Assemble($"Chandelier_v1_{ceilCm}_{dropCm}", parts, 3);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(floorPosition.x, 0f, floorPosition.z);
            Place("Chandelier", root, mesh, new[] { Brass(), Wax(), Flame() }, castShadows: false);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, bodyY - 0.12f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point; // the mood turns it into a wide downlight
            light.color = new Color(1f, 0.82f, 0.6f);
            light.intensity = 3f;
            light.range = 6f;
            return root;
        }

        // ───────────── Wall sconce ─────────────

        /// <summary>A brass wall sconce with one candle and a small warm light that washes the wall.</summary>
        public static void Sconce(Transform parent, Vector3 wallPoint, Quaternion facing)
        {
            var parts = new List<Part>
            {
                P(MeshGen.RoundedBox(new Vector3(0.1f, 0.18f, 0.02f), 0.008f, 2), new Vector3(0f, 0f, -0.01f), 0),
                P(MeshGen.RoundedBox(new Vector3(0.02f, 0.02f, 0.14f), 0.008f, 1), new Vector3(0f, -0.03f, -0.08f), 0),
                P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.03f, 0f), V(0.045f, 0.03f), V(0.04f, 0.035f), V(0f, 0.035f) }, 12, "Cup"), new Vector3(0f, -0.04f, -0.15f), 0),
                P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.016f, 0f), V(0.016f, 0.1f), V(0.012f, 0.11f), V(0f, 0.11f) }, 10, "Candle"), new Vector3(0f, -0.01f, -0.15f), 1),
                P(MeshGen.Lathe(new[] { V(0f, 0f), V(0.011f, 0.011f), V(0.013f, 0.023f), V(0.007f, 0.042f), V(0f, 0.055f) }, 8, "Flame"), new Vector3(0f, 0.11f, -0.15f), 2),
            };
            Mesh mesh = Assemble("Sconce_v1", parts, 3);
            var root = new GameObject("Sconce").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(wallPoint, facing);
            Place("Sconce Mesh", root, mesh, new[] { Brass(), Wax(), Flame() }, castShadows: false);

            var lightGo = new GameObject("Sconce Light"); // not "Light": the mood leaves sconces as soft point lights
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.15f, -0.22f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.52f);
            light.intensity = 1.6f;
            light.range = 3.2f;
            light.shadows = LightShadows.None;
        }

        // ───────────── Pilaster ─────────────

        /// <summary>A flat column against the wall: base, fluted-looking shaft, moulded capital.</summary>
        public static void Pilaster(Transform parent, Vector3 wallBase, Quaternion facing, float height, Material wood, Material trim)
        {
            var root = new GameObject("Pilaster").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(wallBase, facing);
            const float w = 0.26f, d = 0.05f;
            Mark(GreyboxAssets.Box("Base", root, new Vector3(0f, 0.09f, -0.035f), new Vector3(w + 0.08f, 0.18f, 0.07f), trim, keepCollider: false));
            Mark(GreyboxAssets.Box("Shaft", root, new Vector3(0f, height * 0.5f, -d * 0.5f), new Vector3(w, height - 0.3f, d), wood, keepCollider: false));
            // Two shallow "flutes" (darker strips) read as fluting at a distance.
            Mark(GreyboxAssets.Box("Flute L", root, new Vector3(-0.06f, height * 0.5f, -d - 0.004f), new Vector3(0.03f, height - 0.55f, 0.008f), trim, keepCollider: false));
            Mark(GreyboxAssets.Box("Flute R", root, new Vector3(0.06f, height * 0.5f, -d - 0.004f), new Vector3(0.03f, height - 0.55f, 0.008f), trim, keepCollider: false));
            var capital = Place("Capital", root, MeshAssets.GetOrCreate("Capital_v1", () => MeshGen.Extrude(CapitalProfile, w + 0.1f, "Capital"), rebuild: false),
                new[] { trim }, castShadows: true);
            capital.transform.localPosition = new Vector3(0f, height - 0.16f, 0f);
        }

        private static readonly Vector2[] CapitalProfile =
        {
            V(0f, 0f), V(0.05f, 0f), V(0.055f, 0.02f), V(0.07f, 0.05f), V(0.09f, 0.09f), V(0.1f, 0.12f), V(0.1f, 0.16f), V(0f, 0.16f),
        };

        private static readonly Vector2[] CrownProfile =
        {
            V(0f, 0f), V(0.025f, 0f), V(0.03f, 0.015f), V(0.05f, 0.035f), V(0.08f, 0.06f), V(0.11f, 0.1f), V(0.13f, 0.13f),
            V(0.14f, 0.15f), V(0.16f, 0.165f), V(0.17f, 0.19f), V(0.17f, 0.22f), V(0f, 0.22f),
        };

        /// <summary>A run of crown moulding along the top of a wall (bottom at y, length centred on position).</summary>
        public static void CrownRun(Transform parent, Vector3 position, Quaternion facing, float length, Material material)
        {
            int cm = Mathf.RoundToInt(length * 100f);
            Mesh mesh = MeshAssets.GetOrCreate($"Crown_v1_{cm}", () => MeshGen.Extrude(CrownProfile, length, "Crown"), rebuild: false);
            GameObject go = Place("Crown Moulding", parent, mesh, new[] { material }, castShadows: true);
            go.transform.SetPositionAndRotation(position, facing);
        }

        /// <summary>Ornate top and plinth for a bookcase: a crown moulding across the front and down both sides.</summary>
        public static void BookcaseCrown(Transform frame, float width, float depth, float topY, Material material)
        {
            int wcm = Mathf.RoundToInt(width * 100f), dcm = Mathf.RoundToInt(depth * 100f);
            Vector2[] small = Scale(CrownProfile, 0.55f);
            Mesh front = MeshAssets.GetOrCreate($"CaseCrown_v1_{wcm}", () => MeshGen.Extrude(small, width + 0.18f, "CaseCrown"), rebuild: false);
            Mesh side = MeshAssets.GetOrCreate($"CaseCrown_v1_{dcm}", () => MeshGen.Extrude(small, depth + 0.09f, "CaseCrown"), rebuild: false);
            GameObject f = Place("Crown Front", frame, front, new[] { material }, castShadows: true);
            f.transform.localPosition = new Vector3(0f, topY, -depth * 0.5f);
            foreach (int sign in new[] { -1, 1 })
            {
                GameObject s = Place("Crown Side", frame, side, new[] { material }, castShadows: true);
                s.transform.localPosition = new Vector3(sign * width * 0.5f, topY, 0.045f);
                s.transform.localRotation = Quaternion.Euler(0f, sign * 90f, 0f);
            }
        }

        private static Vector2[] Scale(Vector2[] profile, float s)
        {
            var result = new Vector2[profile.Length];
            for (int i = 0; i < profile.Length; i++) result[i] = profile[i] * s;
            return result;
        }

        private static void Mark(GameObject go) => GreyboxAssets.MarkStatic(go);

        // ───────────── Framed pictures ─────────────

        public enum Picture { Landscape, Map, NightSky }

        /// <summary>A gilded frame with a generated picture (painted landscape, parchment city map, starry night).</summary>
        public static void Painting(Transform parent, Vector3 centre, Quaternion facing, Vector2 size, Picture picture, int seed)
        {
            var root = new GameObject($"Painting {picture}").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(centre, facing);
            Material gilt = Gilt();
            const float frame = 0.07f, depth = 0.05f;
            Mark(GreyboxAssets.Box("Frame Top", root, new Vector3(0f, size.y * 0.5f + frame * 0.5f, -depth * 0.5f), new Vector3(size.x + frame * 2f, frame, depth), gilt, keepCollider: false));
            Mark(GreyboxAssets.Box("Frame Bottom", root, new Vector3(0f, -size.y * 0.5f - frame * 0.5f, -depth * 0.5f), new Vector3(size.x + frame * 2f, frame, depth), gilt, keepCollider: false));
            Mark(GreyboxAssets.Box("Frame L", root, new Vector3(-size.x * 0.5f - frame * 0.5f, 0f, -depth * 0.5f), new Vector3(frame, size.y, depth), gilt, keepCollider: false));
            Mark(GreyboxAssets.Box("Frame R", root, new Vector3(size.x * 0.5f + frame * 0.5f, 0f, -depth * 0.5f), new Vector3(frame, size.y, depth), gilt, keepCollider: false));

            Texture2D texture = PictureTexture(picture, seed);
            Material canvas = GreyboxAssets.Lit($"Picture_{picture}_{seed}", Color.white);
            canvas.SetTexture("_BaseMap", texture);
            canvas.SetFloat("_Smoothness", picture == Picture.Map ? 0.1f : 0.25f);
            EditorUtility.SetDirty(canvas);
            GameObject quad = Place("Canvas", root, WallQuad(), new[] { canvas }, castShadows: false);
            quad.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static Mesh WallQuad() => MeshAssets.GetOrCreate("Quad_Wall_v1", () =>
        {
            var mesh = new Mesh { name = "Quad_Wall_v1" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }, rebuild: false);

        private static Texture2D PictureTexture(Picture picture, int seed)
        {
            string path = $"{ProceduralTextures.Folder}/Picture_{picture}_{seed}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int w = 384, h = 288;
            var pixels = new Color32[w * h];
            var random = new System.Random(seed);
            float ox = (float)random.NextDouble() * 100f, oy = (float)random.NextDouble() * 100f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                Color c = picture == Picture.Map ? MapPixel(u, v, ox, oy) : picture == Picture.NightSky ? NightPixel(u, v, ox, oy) : LandscapePixel(u, v, ox, oy);
                pixels[y * w + x] = c;
            }
            Directory.CreateDirectory(ProceduralTextures.Folder);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static float Step(float edge, float x) => x >= edge ? 1f : 0f;

        private static float Brush(float u, float v, float ox, float oy) =>
            (Mathf.PerlinNoise(u * 40f + ox, v * 6f + oy) - 0.5f) * 0.06f + (Mathf.PerlinNoise(u * 9f + oy, v * 9f + ox) - 0.5f) * 0.05f;

        /// <summary>Golden-hour landscape: sky, sun, three ranges of hills, a few trees, brushy texture.</summary>
        private static Color LandscapePixel(float u, float v, float ox, float oy)
        {
            Color sky = Color.Lerp(new Color(0.98f, 0.86f, 0.62f), new Color(0.55f, 0.66f, 0.82f), Mathf.SmoothStep(0.45f, 1f, v));
            float sun = Mathf.SmoothStep(0.09f, 0.07f, Vector2.Distance(new Vector2(u, v * 0.75f), new Vector2(0.68f, 0.52f)));
            Color c = Color.Lerp(sky, new Color(1f, 0.95f, 0.8f), sun);
            float far = 0.48f + (Mathf.PerlinNoise(u * 3f + ox, 0.5f) - 0.5f) * 0.18f;
            float mid = 0.36f + (Mathf.PerlinNoise(u * 4f + oy, 2.5f) - 0.5f) * 0.2f;
            float near = 0.2f + (Mathf.PerlinNoise(u * 5f + ox, 5.5f) - 0.5f) * 0.16f;
            if (v < far) c = Color.Lerp(new Color(0.55f, 0.62f, 0.66f), c, 0.25f);
            if (v < mid) c = new Color(0.42f, 0.55f, 0.36f);
            if (v < near) c = new Color(0.3f, 0.45f, 0.24f);
            float tree = Mathf.PerlinNoise(u * 22f + oy, v * 22f + ox);
            if (v < mid + 0.05f && v > near && tree > 0.72f) c = new Color(0.18f, 0.3f, 0.16f);
            c *= 1f + Brush(u, v, ox, oy);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            return c * Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0f, 0.08f, edge)); // aged varnish darkens the edges
        }

        /// <summary>Parchment city map: stained paper, ink streets, a river, blocks, border and compass rose.</summary>
        private static Color MapPixel(float u, float v, float ox, float oy)
        {
            float stain = Mathf.PerlinNoise(u * 5f + ox, v * 5f + oy);
            Color paper = Color.Lerp(new Color(0.9f, 0.8f, 0.6f), new Color(0.78f, 0.64f, 0.42f), stain * 0.6f);
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            paper *= Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 0.12f, edge));
            Color ink = new Color(0.32f, 0.2f, 0.12f);
            float a = 0f;
            // Double border.
            a = Mathf.Max(a, Mathf.SmoothStep(0.006f, 0f, Mathf.Abs(edge - 0.04f)));
            a = Mathf.Max(a, Mathf.SmoothStep(0.004f, 0f, Mathf.Abs(edge - 0.055f)) * 0.8f);
            if (edge > 0.06f)
            {
                // Streets: a wobbly grid.
                float su = u * 9f + (Mathf.PerlinNoise(v * 3f + ox, 1f) - 0.5f) * 0.8f;
                float sv = v * 7f + (Mathf.PerlinNoise(u * 3f + oy, 2f) - 0.5f) * 0.8f;
                float street = Mathf.Min(Mathf.Abs(su - Mathf.Round(su)), Mathf.Abs(sv - Mathf.Round(sv)));
                a = Mathf.Max(a, Mathf.SmoothStep(0.05f, 0.02f, street) * 0.75f);
                // Little block hatching.
                if (street > 0.12f && Mathf.PerlinNoise(Mathf.Floor(su) * 3.1f + ox, Mathf.Floor(sv) * 2.7f) > 0.55f)
                    a = Mathf.Max(a, 0.18f + 0.12f * Step(0.5f, Mathf.Repeat((u + v) * 120f, 1f)));
                // A river.
                float river = Mathf.Abs(v - (0.35f + Mathf.Sin(u * 6f + ox) * 0.08f));
                if (river < 0.025f) return Color.Lerp(paper, new Color(0.42f, 0.5f, 0.52f), 0.7f);
                // Compass rose, bottom right.
                Vector2 p = new Vector2((u - 0.84f) * 1.33f, v - 0.2f);
                float r = p.magnitude;
                a = Mathf.Max(a, Mathf.SmoothStep(0.005f, 0f, Mathf.Abs(r - 0.07f)));
                float star = Mathf.Min(Mathf.Abs(p.x) * 6f + Mathf.Abs(p.y), Mathf.Abs(p.y) * 6f + Mathf.Abs(p.x));
                if (r < 0.1f) a = Mathf.Max(a, Mathf.SmoothStep(0.06f, 0.05f, star));
            }
            return Color.Lerp(paper, ink, Mathf.Clamp01(a));
        }

        /// <summary>A starry night over rooftops (for the magic shop).</summary>
        private static Color NightPixel(float u, float v, float ox, float oy)
        {
            Color c = Color.Lerp(new Color(0.12f, 0.12f, 0.3f), new Color(0.05f, 0.05f, 0.16f), v);
            float swirl = Mathf.PerlinNoise(u * 4f + ox + Mathf.Sin(v * 8f) * 0.4f, v * 4f + oy);
            c = Color.Lerp(c, new Color(0.25f, 0.3f, 0.6f), swirl * 0.35f);
            float star = Mathf.PerlinNoise(u * 80f + ox, v * 80f + oy);
            if (star > 0.86f && v > 0.35f) c = Color.Lerp(c, new Color(1f, 0.92f, 0.7f), (star - 0.86f) * 8f);
            float moon = Vector2.Distance(new Vector2(u, v * 0.75f), new Vector2(0.25f, 0.58f));
            if (moon < 0.06f) c = new Color(0.98f, 0.94f, 0.78f);
            float roofs = 0.22f + Mathf.Abs(Mathf.Sin(u * 18f + ox)) * 0.06f + Step(0.6f, Mathf.PerlinNoise(u * 12f, 3f)) * 0.08f;
            if (v < roofs) c = new Color(0.06f, 0.05f, 0.1f);
            if (v < roofs && Mathf.PerlinNoise(u * 60f + oy, v * 60f) > 0.8f) c = new Color(0.95f, 0.75f, 0.35f); // lit windows
            return c * (1f + Brush(u, v, ox, oy));
        }

        // ───────────── Gallery (Library) ─────────────

        /// <summary>
        /// A balcony running round three walls at deckY: deck with a moulded fascia, brackets underneath, a turned
        /// balustrade, and full-height decorative bookcases (filler books merged per case) against the walls.
        /// Decor only: the player stays on the ground floor.
        /// </summary>
        public static void Gallery(Transform room, Vector2 size, float wall, float deckY, float wallHeight, Material wood, Material trim, Material[] bookMaterials)
        {
            var root = new GameObject("Gallery").transform;
            root.SetParent(room, false);
            const float depth = 1.4f, deckH = 0.24f;
            float ix = size.x * 0.5f - wall * 0.5f, iz = size.y * 0.5f - wall * 0.5f;
            float sideLength = size.y - wall - depth;          // east/west runs, from the north deck to the south wall
            float sideCentreZ = -depth * 0.5f;

            // Decks.
            Mark(GreyboxAssets.Box("Deck N", root, new Vector3(0f, deckY + deckH * 0.5f, iz - depth * 0.5f), new Vector3(size.x - wall, deckH, depth), wood));
            Mark(GreyboxAssets.Box("Deck W", root, new Vector3(-ix + depth * 0.5f, deckY + deckH * 0.5f, sideCentreZ), new Vector3(depth, deckH, sideLength), wood));
            Mark(GreyboxAssets.Box("Deck E", root, new Vector3(ix - depth * 0.5f, deckY + deckH * 0.5f, sideCentreZ), new Vector3(depth, deckH, sideLength), wood));

            // Runs along the open edges: (centre, facing toward the room, length).
            var runs = new List<(Vector3 centre, Quaternion facing, float length)>
            {
                (new Vector3(0f, 0f, iz - depth), Quaternion.identity, size.x - wall - 2f * depth),
                (new Vector3(-ix + depth, 0f, sideCentreZ - 0f), Quaternion.Euler(0f, -90f, 0f), sideLength),
                (new Vector3(ix - depth, 0f, sideCentreZ), Quaternion.Euler(0f, 90f, 0f), sideLength),
            };

            Mesh baluster = MeshAssets.GetOrCreate("Baluster_v1", () => MeshGen.Lathe(new[]
            {
                V(0f, 0f), V(0.035f, 0f), V(0.035f, 0.05f), V(0.022f, 0.08f), V(0.02f, 0.2f), V(0.038f, 0.36f), V(0.04f, 0.42f),
                V(0.022f, 0.55f), V(0.018f, 0.7f), V(0.03f, 0.76f), V(0.032f, 0.8f), V(0f, 0.8f),
            }, 10, "Baluster"), rebuild: false);
            foreach (var run in runs)
            {
                var edge = new GameObject("Gallery Edge").transform;
                edge.SetParent(root, false);
                edge.SetPositionAndRotation(run.centre, run.facing);
                int lcm = Mathf.RoundToInt(run.length * 100f);
                // Fascia moulding under the deck edge (faces the room).
                GameObject fascia = Place("Fascia", edge, MeshAssets.GetOrCreate($"Fascia_v1_{lcm}", () =>
                    MeshGen.Extrude(Scale(CrownProfile, 1.1f), run.length, "Fascia"), rebuild: false), new[] { trim }, castShadows: true);
                fascia.transform.localPosition = new Vector3(0f, deckY - 0.01f, 0f);
                // Rails.
                Mark(GreyboxAssets.Box("Bottom Rail", edge, new Vector3(0f, deckY + deckH + 0.04f, 0.07f), new Vector3(run.length, 0.08f, 0.1f), wood, keepCollider: false));
                Mark(GreyboxAssets.Box("Hand Rail", edge, new Vector3(0f, deckY + deckH + 0.94f, 0.07f), new Vector3(run.length, 0.08f, 0.14f), wood, keepCollider: false));
                // Balusters merged into one mesh per run.
                var parts = new List<Part>();
                int count = Mathf.FloorToInt(run.length / 0.17f);
                for (int i = 0; i <= count; i++)
                {
                    float x = -run.length * 0.5f + run.length * i / Mathf.Max(1, count);
                    parts.Add(P(baluster, new Vector3(x, deckY + deckH + 0.08f, 0.07f), 0));
                }
                Place("Balusters", edge, Assemble($"Balusters_v1_{lcm}_{Mathf.RoundToInt(deckY * 100f)}", parts, 1), new[] { trim }, castShadows: true);
                // Brackets under the deck every ~2 m.
                int brackets = Mathf.Max(2, Mathf.RoundToInt(run.length / 2f));
                for (int i = 0; i <= brackets; i++)
                {
                    float x = -run.length * 0.5f + run.length * i / brackets;
                    Mark(GreyboxAssets.Box("Bracket", edge, new Vector3(x, deckY - 0.22f, 0.25f), new Vector3(0.12f, 0.36f, 0.5f), wood, keepCollider: false));
                }
            }

            // Decorative full-height bookcases along the walls up on the gallery.
            float caseBase = deckY + deckH, caseHeight = wallHeight - caseBase - 0.35f;
            var walls = new List<(Vector3 start, Vector3 along, Quaternion facing, float length)>
            {
                (new Vector3(-ix + depth, caseBase, iz - 0.2f), Vector3.right, Quaternion.identity, size.x - wall - 2f * depth),
                (new Vector3(-ix + 0.2f, caseBase, iz - depth), Vector3.back, Quaternion.Euler(0f, -90f, 0f), sideLength - 0.2f),
                (new Vector3(ix - 0.2f, caseBase, iz - depth), Vector3.back, Quaternion.Euler(0f, 90f, 0f), sideLength - 0.2f),
            };
            int caseIndex = 0;
            foreach (var w in walls)
            {
                int cases = Mathf.Max(1, Mathf.FloorToInt(w.length / 2.5f));
                float caseWidth = w.length / cases;
                for (int i = 0; i < cases; i++)
                {
                    Vector3 centre = w.start + w.along * (caseWidth * (i + 0.5f));
                    DecorativeBookcase(root, centre, w.facing, caseWidth - 0.08f, caseHeight, wood, trim, bookMaterials, 1000 + caseIndex++);
                }
            }
        }

        /// <summary>A full bookcase that is only scenery: frame, shelves and filler books merged into one mesh.</summary>
        public static void DecorativeBookcase(Transform parent, Vector3 floorCentre, Quaternion facing, float width, float height,
            Material wood, Material trim, Material[] bookMaterials, int seed)
        {
            var root = new GameObject("Gallery Bookcase").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(floorCentre, facing);
            const float depth = 0.36f, board = 0.03f, panel = 0.04f;
            int shelves = Mathf.Max(3, Mathf.FloorToInt((height - 0.2f) / 0.36f));
            float rowH = (height - 0.2f) / shelves;

            var frame = new List<Part>();
            Mesh unit = MeshGen.RoundedBox(Vector3.one, 0.04f, 1);
            Part Box(Vector3 c, Vector3 s, int m) => P(unit, Matrix4x4.TRS(c, Quaternion.identity, s), m);
            frame.Add(Box(new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, 0.02f), 0));
            frame.Add(Box(new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(panel, height, depth), 0));
            frame.Add(Box(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(panel, height, depth), 0));
            frame.Add(Box(new Vector3(0f, 0.08f, 0f), new Vector3(width, 0.16f, depth), 0));
            for (int s = 0; s <= shelves; s++) frame.Add(Box(new Vector3(0f, 0.16f + s * rowH + board * 0.5f, 0f), new Vector3(width - panel, board, depth), 0));

            // Books: seeded widths, heights, colours and the odd lean; one submesh per colour.
            var random = new System.Random(seed);
            var books = new List<Part>();
            for (int s = 0; s < shelves; s++)
            {
                float floorY = 0.16f + s * rowH + board;
                float x = -width * 0.5f + panel;
                float end = width * 0.5f - panel;
                while (true)
                {
                    float w = Mathf.Lerp(0.025f, 0.05f, (float)random.NextDouble());
                    if (x + w > end) break;
                    float h = (rowH - board) * Mathf.Lerp(0.62f, 0.92f, (float)random.NextDouble());
                    int colour = random.Next(bookMaterials.Length);
                    float lean = random.NextDouble() < 0.05 ? 8f : 0f;
                    books.Add(P(unit, Matrix4x4.TRS(new Vector3(x + w * 0.5f, floorY + h * 0.5f, 0.02f), Quaternion.Euler(0f, 0f, lean),
                        new Vector3(w * 0.94f, h, depth * 0.78f)), 1 + colour));
                    x += w;
                    if (random.NextDouble() < 0.04) x += 0.05f;
                }
            }
            var all = new List<Part>(frame);
            all.AddRange(books);
            var materials = new Material[1 + bookMaterials.Length];
            materials[0] = wood;
            for (int i = 0; i < bookMaterials.Length; i++) materials[i + 1] = bookMaterials[i];
            Place("Bookcase", root, Assemble($"DecoCase_v1_{seed}_{Mathf.RoundToInt(width * 100f)}_{Mathf.RoundToInt(height * 100f)}", all, materials.Length),
                materials, castShadows: true);
            BookcaseCrown(root, width, depth, height, trim);
        }
    }
}
