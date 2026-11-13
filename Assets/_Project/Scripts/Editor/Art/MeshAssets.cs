using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TidyUp.Editor.Art
{
    /// <summary>
    /// Saves generated meshes as assets and reuses them: one asset per distinct rounded-box size, one per
    /// item. Re-saving an existing asset keeps its GUID, so scenes and prefabs stay linked.
    /// </summary>
    public static class MeshAssets
    {
        public const string Folder = "Assets/_Project/Art/Meshes/Generated";
        private const int Version = 1; // bump when generator output changes, to rebuild cached boxes
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Rounded box of this size (sizes rounded to the millimetre to share meshes).</summary>
        public static Mesh RoundedBox(Vector3 size, float radius = -1f)
        {
            if (radius < 0f) radius = DefaultRadius(size);
            string key = $"Box_v{Version}_{Mm(size.x)}x{Mm(size.y)}x{Mm(size.z)}_r{Mm(radius)}";
            return GetOrCreate(key, () => MeshGen.RoundedBox(size, radius, radius > 0.02f ? 3 : 2), rebuild: false);
        }

        /// <summary>Soft edges that read as "made", scaled to the object, never more than 3 cm.</summary>
        public static float DefaultRadius(Vector3 size)
        {
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            return Mathf.Min(0.03f, smallest * 0.25f);
        }

        public static Mesh GetOrCreate(string key, Func<Mesh> make, bool rebuild)
        {
            if (!rebuild && Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            string path = $"{Folder}/{key}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && !rebuild)
            {
                Cache[key] = existing;
                return existing;
            }

            EditorFolders.Ensure(Folder);
            Mesh mesh = make();
            mesh.name = key;
            if (existing != null)
            {
                // Copy through the Mesh API, not CopySerialized: CopySerialized updates the asset's data but not the
                // copy already on the GPU, so the old shape kept rendering (missing book foil, bottles without
                // potion) until the Editor reloaded the asset. The asset keeps its GUID either way.
                CopyInto(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            Cache[key] = mesh;
            return mesh;
        }

        private static int Mm(float metres) => Mathf.RoundToInt(metres * 1000f);

        private static void CopyInto(Mesh source, Mesh target)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.SetVertices(source.vertices);
            if (source.normals.Length == source.vertexCount) target.SetNormals(source.normals);
            if (source.tangents.Length == source.vertexCount) target.SetTangents(source.tangents);
            if (source.uv.Length == source.vertexCount) target.SetUVs(0, source.uv);
            if (source.colors.Length == source.vertexCount) target.SetColors(source.colors);
            target.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) target.SetTriangles(source.GetTriangles(i), i, false);
            target.RecalculateBounds();
        }
    }
}
