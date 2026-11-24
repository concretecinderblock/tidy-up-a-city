using TidyUp.Items;
using TidyUp.Mechanics.LivingItems;
using TidyUp.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TidyUp.Locations
{
    /// <summary>
    /// Static batching for location scenes loaded at runtime. Unity batches static geometry for scenes built into
    /// the player, not for scenes loaded later through Addressables (and never in the Editor), so every shelf board,
    /// panel, basket and dust film was its own draw call: thousands per room. Combining the fixed parts of a scene
    /// once, while the screen is faded, cuts that to a handful of batches.
    /// Only roots with nothing that moves are combined: items, living items, the memory, restoration stages,
    /// particles and animated things are left alone.
    /// </summary>
    public static class SceneBatcher
    {
        public static int Batch(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return 0;
            // One combine call for the whole scene, so parts of different fixtures that share a material end up in
            // the same batch (combining per fixture would still leave hundreds of batches).
            var parts = new System.Collections.Generic.List<GameObject>(4096);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!CanBatch(root)) continue;
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.isPartOfStaticBatch) continue;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                    parts.Add(renderer.gameObject);
                }
            }
            if (parts.Count == 0) return 0;
            var batchRoot = new GameObject("Static Batch Root");
            SceneManager.MoveGameObjectToScene(batchRoot, scene);
            StaticBatchingUtility.Combine(parts.ToArray(), batchRoot);
            return parts.Count;
        }

        private static bool CanBatch(GameObject root)
        {
            return root.GetComponentInChildren<TidyItem>(true) == null
                && root.GetComponentInChildren<LivingItem>(true) == null
                && root.GetComponentInChildren<MemoryFragment>(true) == null
                && root.GetComponentInChildren<RestorationStage>(true) == null
                && root.GetComponentInChildren<ParticleSystem>(true) == null
                && root.GetComponentInChildren<Animator>(true) == null;
        }
    }
}
