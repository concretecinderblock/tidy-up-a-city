using System.IO;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Generated assets for cleaning (family K): a soft dust-smudge texture (alpha blob with grain) and the
    /// floor-smudge material and quad that use it.
    /// </summary>
    public static class DustAssets
    {
        private const string TexturePath = ProceduralTextures.Folder + "/Dust_Smudge.png";
        private const int Size = 256;

        public static Texture2D SmudgeTexture(bool force = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null && !force) return existing;

            Directory.CreateDirectory(ProceduralTextures.Folder);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    // A lumpy blob: radius wobbles with low-frequency noise, edges fade softly.
                    float angleNoise = Mathf.PerlinNoise(u * 1.7f + 3.1f, v * 1.7f + 7.3f);
                    float r = Mathf.Sqrt(u * u + v * v) / Mathf.Lerp(0.55f, 0.95f, angleNoise);
                    float body = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, r));
                    // Grain and streaks so it reads as dust, not a flat stain.
                    float grain = Mathf.PerlinNoise(u * 18f + 11f, v * 18f + 5f);
                    float streak = Mathf.PerlinNoise(u * 3f + 40f, v * 9f + 2f);
                    float alpha = body * Mathf.Lerp(0.45f, 1f, grain) * Mathf.Lerp(0.7f, 1f, streak);
                    float shade = Mathf.Lerp(0.82f, 1.05f, grain);
                    pixels[y * Size + x] = new Color(shade, shade * 0.96f, shade * 0.9f, Mathf.Clamp01(alpha));
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        /// <summary>Pale, soft dust; the Cleanable fades its alpha as it is scrubbed.</summary>
        public static Material SmudgeMaterial()
        {
            Material material = GreyboxAssets.TransparentUnlit("Dust_Smudge", new Color(0.9f, 0.86f, 0.78f, 0.85f));
            material.SetTexture("_BaseMap", SmudgeTexture());
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private const string BunnyPrefabPath = "Assets/_Project/Prefabs/Cleaning/DustBunny.prefab";

        /// <summary>
        /// The dust bunny (cleaning minigame): a soft, fuzzy grey puff with googly eyes, pivot at its feet, a
        /// generous trigger so it's easy to look at. Rebuilt each time so tweaks here always apply.
        /// </summary>
        public static TidyUp.Mechanics.Cleaning.DustBunny DustBunnyPrefab()
        {
            EditorFolders.Ensure("Assets/_Project/Prefabs/Cleaning");
            Mesh body = MeshAssets.GetOrCreate("DustBunny_Body_v1", () =>
            {
                Mesh m = MeshGen.DeformedSphere(p =>
                {
                    // Fuzzy: little tufts all over, flatter underneath.
                    float fuzz = Mathf.PerlinNoise(p.x * 11f + 3.1f, p.y * 11f + p.z * 9f + 7.7f);
                    Vector3 q = p * (0.88f + 0.28f * fuzz);
                    return new Vector3(q.x * 0.14f, (q.y < 0f ? q.y * 0.6f : q.y) * 0.12f + 0.042f, q.z * 0.14f);
                }, 14, 20, "DustBunny");
                return m;
            }, rebuild: false);
            Mesh ball = MeshAssets.GetOrCreate("Sphere_Lo_v1", () => MeshGen.DeformedSphere(p => p, 8, 12, "SphereLo"), rebuild: false);

            Material fluff = GreyboxAssets.Lit("DustBunny_Fluff", new Color(0.74f, 0.71f, 0.68f));
            fluff.SetFloat("_Smoothness", 0f);
            Material white = GreyboxAssets.Lit("DustBunny_EyeWhite", new Color(0.98f, 0.98f, 0.96f));
            white.SetFloat("_Smoothness", 0.6f);
            Material pupil = GreyboxAssets.Lit("DustBunny_Pupil", new Color(0.12f, 0.09f, 0.08f));
            pupil.SetFloat("_Smoothness", 0.8f);

            var root = new GameObject("Dust Bunny");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.06f, 0f);
            trigger.radius = 0.1f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            void Part(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
            {
                var go = new GameObject(name);
                go.transform.SetParent(visual, false);
                go.transform.localPosition = position;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Part("Body", body, fluff, Vector3.zero, Vector3.one);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(side < 0 ? "Eye L" : "Eye R", ball, white, new Vector3(side * 0.028f, 0.075f, 0.056f), Vector3.one * 0.04f);
                Part(side < 0 ? "Pupil L" : "Pupil R", ball, pupil, new Vector3(side * 0.026f, 0.074f, 0.074f), Vector3.one * 0.02f);
            }

            var bunny = root.AddComponent<TidyUp.Mechanics.Cleaning.DustBunny>();
            bunny.EditorConfigure(visual);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BunnyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<TidyUp.Mechanics.Cleaning.DustBunny>();
        }

        /// <summary>A 1×1 m upward-facing quad with 0..1 UVs (scale the transform for size).</summary>
        public static Mesh FloorQuad()
        {
            return MeshAssets.GetOrCreate("Quad_Floor_v1", () =>
            {
                var mesh = new Mesh { name = "Quad_Floor_v1" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
                };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                return mesh;
            }, rebuild: false);
        }
    }
}
