using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Muddle;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Shows what you carry as a stack in your hands, lower right of the view: lightweight copies of the carried
    /// items (their mesh and materials only), laid the way they'd sit in a pile (books flat, bottles on their side),
    /// the selected kind on top. Drawn on the Held layer by an overlay camera, so it never clips into shelves or
    /// walls. Sways a little with movement and looking, and pops when something lands on top.
    /// </summary>
    public sealed class HeldItemsView : MonoBehaviour
    {
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private Transform _camera;
        // Where the stack sits, in camera space (metres): low and to the right, turned so the top cover, spines and
        // page edges show (like holding a pile of books against your side).
        private static readonly Vector3 RestPosition = new Vector3(0.34f, -0.31f, 0.55f);
        private static readonly Vector3 RestRotation = new Vector3(24f, -40f, 0f);
        [SerializeField, Min(1)] private int _maxShown = 10;
        [Tooltip("Large things (sacks, cakes) are shrunk to at most this size so the stack stays in frame.")]
        [SerializeField] private float _maxHeldItemSize = 0.13f;
        private const float MaxStackHeight = 0.22f; // metres in camera space at 0.55 m: the lower right corner only

        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<TidyItem> _order = new List<TidyItem>();
        private Transform _stack;
        private int _shown;
        private Vector3 _sway;
        private Vector3 _swayVelocity;
        private Quaternion _lastCameraRotation;
        private float _pop;
        private Vector3 _heightOffset;

        private void Awake()
        {
            _stack = new GameObject("Held Stack").transform;
            // The held stack renders through its own overlay camera: keep it out of the GPU Resident Drawer, which
            // would draw these proxies with the main camera's instancing (they came out as flat cyan shapes).
            ExcludeFromGpuDriven(_stack.gameObject);
            _stack.SetParent(_camera != null ? _camera : transform, false);
            _stack.localPosition = RestPosition + _heightOffset;
            _stack.localRotation = Quaternion.Euler(RestRotation);
            if (_camera != null) _lastCameraRotation = _camera.rotation;
        }

        private void OnEnable()
        {
            if (_carry != null) _carry.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_carry != null) _carry.Changed -= Rebuild;
        }

        private void LateUpdate()
        {
            if (_stack == null) return;
            bool hide = GameServices.Input != null && (GameServices.Input.MenuOpen || GameServices.Input.GameplayBlocked);
            if (_stack.gameObject.activeSelf == hide) _stack.gameObject.SetActive(!hide && _shown > 0);

            // Sway: the stack lags a little behind turns and bobs while walking, then settles.
            Vector3 target = Vector3.zero;
            if (_camera != null)
            {
                Quaternion delta = Quaternion.Inverse(_lastCameraRotation) * _camera.rotation;
                _lastCameraRotation = _camera.rotation;
                Vector3 euler = delta.eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, euler.y), pitch = Mathf.DeltaAngle(0f, euler.x);
                target = new Vector3(-yaw, pitch, 0f) * 0.0025f;
            }
            PlayerInputReader input = GameServices.Input;
            if (input != null && input.Move.sqrMagnitude > 0.01f)
            {
                float t = Time.time * 9f;
                target += new Vector3(Mathf.Sin(t * 0.5f) * 0.006f, Mathf.Abs(Mathf.Sin(t * 0.5f)) * -0.008f, 0f);
            }
            _sway = Vector3.SmoothDamp(_sway, Vector3.ClampMagnitude(target, 0.05f), ref _swayVelocity, 0.08f);
            _pop = Mathf.MoveTowards(_pop, 0f, Time.deltaTime * 4f);
            float popOffset = Mathf.Sin(_pop * Mathf.PI) * 0.025f;
            _stack.localPosition = RestPosition + _heightOffset + _sway + new Vector3(0f, popOffset, 0f);
        }

        /// <summary>Re-lays the stack from the inventory (only when it changes, never per frame).</summary>
        private void Rebuild()
        {
            if (_stack == null || _carry == null) return;
            CarryInventory<TidyItem> inventory = _carry.Inventory;
            int before = _shown;

            // Selected kind on top: everything else first, then the selected kind; show the top few.
            _order.Clear();
            TidyItem selected = inventory.Selected;
            ItemDefinition selectedKind = selected != null ? selected.Definition : null;
            foreach (TidyItem item in inventory.Items) if (item != null && item.Definition != selectedKind) _order.Add(item);
            foreach (TidyItem item in inventory.Items) if (item != null && item.Definition == selectedKind && item != selected) _order.Add(item);
            if (selected != null) _order.Add(selected);
            // Show as many from the top as fit in a low pile (never up into the view): ten cakes or sacks would
            // otherwise stack higher than the screen.
            int first = _order.Count;
            float pile = 0f;
            for (int i = _order.Count - 1; i >= 0 && _order.Count - i <= _maxShown; i--)
            {
                float h = StackHeight(_order[i]);
                if (first < _order.Count && pile + h > MaxStackHeight) break;
                pile += h;
                first = i;
            }

            int layer = TidyLayers.Held >= 0 ? TidyLayers.Held : 0;
            float y = 0f;
            _shown = 0;
            for (int i = first; i < _order.Count; i++)
            {
                TidyItem item = _order[i];
                MeshFilter source = item.GetComponentInChildren<MeshFilter>(true);
                MeshRenderer sourceRenderer = source != null ? source.GetComponent<MeshRenderer>() : null;
                if (source == null || source.sharedMesh == null || sourceRenderer == null) continue;

                GameObject proxy = Proxy(_shown++, layer);
                proxy.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                proxy.GetComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;

                Vector3 size = source.sharedMesh.bounds.size;
                Quaternion lie = Quaternion.identity;
                float height = size.y;
                if (ScatterRules.ShouldLieDown(size))
                {
                    if (size.x <= size.z) { lie = Quaternion.Euler(0f, 0f, 90f); height = size.x; }
                    else { lie = Quaternion.Euler(90f, 0f, 0f); height = size.z; }
                }
                float scale = Mathf.Min(1f, _maxHeldItemSize / Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
                // A slightly untidy stack: each item turned a little, seeded by its position so it doesn't jitter.
                float yaw = Mathf.Sin(i * 12.9898f) * 9f + (lie == Quaternion.identity ? 0f : 90f);
                Transform t = proxy.transform;
                t.localScale = Vector3.one * scale;
                t.localRotation = Quaternion.Euler(0f, yaw, 0f) * lie;
                t.localPosition = Vector3.zero;
                // Sit the item's visual bottom on the stack so far (the mesh pivot is bottom-centre when upright).
                Bounds b = WorldBounds(proxy, source.sharedMesh);
                t.localPosition = new Vector3(Mathf.Sin(i * 7.31f) * 0.008f, y - b.min.y, Mathf.Cos(i * 3.7f) * 0.006f);
                y += height * scale + 0.002f;
                proxy.SetActive(true);
            }
            for (int i = _shown; i < _pool.Count; i++) _pool[i].SetActive(false);
            _stack.gameObject.SetActive(_shown > 0);
            // The pile grows up from a fixed base low in the view; its height is capped above.
            _heightOffset = new Vector3(0f, -0.06f, 0f);
            _stack.localPosition = RestPosition + _heightOffset;
            if (_shown > before) _pop = 1f;
        }



        // Unity's opt-out from the GPU Resident Drawer (the "Disallow GPU Driven Rendering" component you can add in
        // the Inspector). Its class is internal to scripts, so it's looked up by name; if it's missing, nothing breaks.
        private static readonly System.Type DisallowGpuDriven =
            System.Type.GetType("UnityEngine.Rendering.DisallowGPUDrivenRendering, Unity.RenderPipelines.GPUDriven.Runtime");

        private static void ExcludeFromGpuDriven(GameObject go)
        {
            if (DisallowGpuDriven != null && go.GetComponent(DisallowGpuDriven) == null) go.AddComponent(DisallowGpuDriven);
        }
        /// <summary>How tall one item stands in the pile (lying down if it's long, shrunk if it's big).</summary>
        private float StackHeight(TidyItem item)
        {
            MeshFilter source = item != null ? item.GetComponentInChildren<MeshFilter>(true) : null;
            if (source == null || source.sharedMesh == null) return 0f;
            Vector3 size = source.sharedMesh.bounds.size;
            float height = size.y;
            if (ScatterRules.ShouldLieDown(size)) height = size.x <= size.z ? size.x : size.z;
            float scale = Mathf.Min(1f, _maxHeldItemSize / Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            return height * scale + 0.002f;
        }
        /// <summary>The item's bounds in stack space for its current local rotation and scale.</summary>
        private Bounds WorldBounds(GameObject proxy, Mesh mesh)
        {
            Bounds local = mesh.bounds;
            Transform t = proxy.transform;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                Vector3 p = t.localRotation * Vector3.Scale(corner, t.localScale);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private GameObject Proxy(int index, int layer)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("Held Item");
                go.layer = layer;
                go.transform.SetParent(_stack, false);
                go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                ExcludeFromGpuDriven(go); // drawn by the overlay camera, not GPU-driven
                go.SetActive(false);
                _pool.Add(go);
            }
            return _pool[index];
        }

        internal void EditorConfigure(PlayerCarry carry, Transform camera)
        {
            _carry = carry;
            _camera = camera;
        }
    }
}
