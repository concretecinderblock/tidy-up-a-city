using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Mega locations keep thousands of small items around: beyond a few rooms they're specks, but each is still
    /// a draw call. This tells the player camera to skip drawing the Items layer past a distance (Unity's per-layer
    /// cull distances: no per-frame work of our own). Shelves, walls and decor still draw as normal.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ItemDrawDistance : MonoBehaviour
    {
        [Tooltip("Metres. Items further away than this aren't drawn (about one and a half rooms).")]
        [SerializeField] private float _distance = 30f;

        private void OnEnable() => Apply();

        private void Apply()
        {
            int layer = TidyLayers.Items;
            if (layer < 0) return;
            var cam = GetComponent<Camera>();
            float[] distances = cam.layerCullDistances;
            if (distances == null || distances.Length != 32) distances = new float[32];
            distances[layer] = _distance;
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true; // by distance, not by depth, so turning your head doesn't pop items
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Apply();
        }
    }
}
