using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Editor
{
    /// <summary>Shared grey-box materials, created once and reused (SRP Batcher friendly).</summary>
    internal static class GreyboxAssets
    {
        public const string MaterialFolder = "Assets/_Project/Art/Greybox/Materials";

        public static Material Lit(string name, Color color)
        {
            EditorFolders.Ensure(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            // Plain (untextured) unless Textured() sets a pattern right after.
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            material.SetTextureScale("_BaseMap", Vector2.one);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Additive unlit particle material for sparkles.</summary>
        public static Material AdditiveParticle(string name, Color color)
        {
            EditorFolders.Ensure(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 2f);   // Additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Lit material with HDR emission (lit lamp bulbs, glowing signs).</summary>
        public static Material Emissive(string name, Color baseColor, Color emission, float intensity)
        {
            Material material = Lit(name, baseColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Alpha-blended unlit material (ghost silhouettes).</summary>
        public static Material TransparentUnlit(string name, Color color)
        {
            EditorFolders.Ensure(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader) { name = name, enableInstancing = true };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);   // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Textured lit material: a generated pattern (albedo + normal map) tinted by a palette colour.
        /// tileMetres: how many metres one texture repeat covers (meshes carry UVs in metres).
        /// </summary>
        public static Material Textured(string name, Color color, Art.TextureSet pattern, float tileMetres, float smoothness = 0.15f, float normalStrength = 1f)
        {
            Material material = Lit(name, color);
            material.SetTexture("_BaseMap", Art.ProceduralTextures.Albedo(pattern));
            material.SetTexture("_BumpMap", Art.ProceduralTextures.Normal(pattern));
            material.SetFloat("_BumpScale", normalStrength);
            material.EnableKeyword("_NORMALMAP");
            float scale = 1f / Mathf.Max(0.01f, tileMetres);
            material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A box with softly rounded edges (shared mesh per size, UVs in metres so textures keep their scale).
        /// Scale stays 1: the mesh carries the size. Optional BoxCollider of the same size.
        /// </summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool keepCollider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = Art.MeshAssets.RoundedBox(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (keepCollider) go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }
}
