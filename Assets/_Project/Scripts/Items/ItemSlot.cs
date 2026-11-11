using System;
using UnityEngine;

namespace TidyUp.Items
{
    /// <summary>
    /// A place where one item belongs. Generators create these and author the tidy state
    /// (start occupant); the MuddleScatterer creates the mess at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemSlot : MonoBehaviour
    {
        [SerializeField] private SlotDefinition _definition = new SlotDefinition();
        [SerializeField] private Transform _snapPoint;
        [Tooltip("Shown while the slot is empty (marker / ghost silhouette hint).")]
        [SerializeField] private GameObject _emptyIndicator;
        [Tooltip("The item that sits here in the tidy state. Scattered at runtime by the MuddleScatterer.")]
        [SerializeField] private TidyItem _startOccupant;

        private TidyItem _occupant;
        private GameObject _ghost;
        private bool _ghostAllowed = true;

        public SlotDefinition Definition => _definition;
        public Transform SnapPoint => _snapPoint != null ? _snapPoint : transform;
        public TidyItem Occupant => _occupant;
        /// <summary>The item that belongs here in the tidy state (null if none was authored).</summary>
        public TidyItem StartOccupant => _startOccupant;
        /// <summary>True when this slot can show a ghost (its tidy-state look was captured).</summary>
        public bool HasGhost => _ghostRecipe != null;
        /// <summary>True when the ghost is currently visible.</summary>
        public bool GhostVisible => _ghost != null && _ghost.activeSelf;
        public bool IsEmpty => _occupant == null;
        public string Label => _definition.Label;

        /// <summary>Raised when the occupant changes.</summary>
        public event Action<ItemSlot> OccupantChanged;

        private void Awake()
        {
            if (_startOccupant != null)
            {
                _occupant = _startOccupant;
                _startOccupant.BindToSlotAtLoad(this);
            }
            RefreshIndicator();
        }

        public bool CanAccept(TidyItem item)
        {
            return item != null && IsEmpty && IsOpen && SlotRules.Accepts(_definition, item.Definition);
        }

        private ISlotGate _gate;

        /// <summary>False while the fixture this slot belongs to is closed (e.g. still dusty).</summary>
        public bool IsOpen => _gate == null || _gate.IsOpen;
        public ISlotGate Gate => _gate;

        /// <summary>Set at runtime by whatever owns the fixture (a Cleanable).</summary>
        public void SetGate(ISlotGate gate)
        {
            _gate = gate;
            RefreshIndicator();
        }

        /// <summary>Call when the gate opens or closes, so markers and ghosts follow.</summary>
        public void RefreshGate() => RefreshIndicator();

        internal void SetOccupant(TidyItem item)
        {
            _occupant = item;
            RefreshIndicator();
            OccupantChanged?.Invoke(this);
        }

        internal void ClearOccupant(TidyItem item)
        {
            if (_occupant != item) return;
            _occupant = null;
            RefreshIndicator();
            OccupantChanged?.Invoke(this);
        }

        private struct GhostPart
        {
            public Mesh Mesh;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private GhostPart[] _ghostRecipe;
        private Material _ghostMaterial;
        private bool _ghostNear;

        /// <summary>
        /// Remembers what the tidy-state item looks like here, for the "ghost silhouette" hint. Cheap (no objects
        /// created): the ghost itself is built the first time the player comes near. Must run while the start
        /// occupant still sits in the slot (before the Muddle scatters it).
        /// </summary>
        public void CaptureGhost(Material ghostMaterial)
        {
            if (_ghostRecipe != null || _startOccupant == null || ghostMaterial == null) return;
            _ghostMaterial = ghostMaterial;
            Transform snap = SnapPoint;
            // Active meshes only: a disguised item's stand-in visual is inactive and must not show in the ghost.
            MeshFilter[] filters = _startOccupant.GetComponentsInChildren<MeshFilter>(false);
            _ghostRecipe = new GhostPart[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                Transform source = filters[i].transform;
                _ghostRecipe[i] = new GhostPart
                {
                    Mesh = filters[i].sharedMesh,
                    Position = snap.InverseTransformPoint(source.position),
                    Rotation = Quaternion.Inverse(snap.rotation) * source.rotation,
                    Scale = source.lossyScale,
                };
            }
        }

        /// <summary>Hint unlocks switch ghosts on or off.</summary>
        public void SetGhostAllowed(bool allowed)
        {
            if (_ghostAllowed == allowed) return;
            _ghostAllowed = allowed;
            RefreshIndicator();
        }

        /// <summary>Ghosts only show near the player (HintService decides); far ones cost nothing.</summary>
        public void SetGhostNear(bool near)
        {
            if (_ghostNear == near) return;
            _ghostNear = near;
            RefreshIndicator();
        }

        private void BuildGhost()
        {
            Transform snap = SnapPoint;
            _ghost = new GameObject("Ghost");
            _ghost.transform.SetParent(snap, false);
            foreach (GhostPart part in _ghostRecipe)
            {
                var go = new GameObject("Ghost Part");
                go.transform.SetParent(_ghost.transform, false);
                go.transform.localPosition = part.Position;
                go.transform.localRotation = part.Rotation;
                go.transform.localScale = part.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                // Every submesh (book pages, bottle cork …) gets the ghost material.
                var materials = new Material[Mathf.Max(1, part.Mesh != null ? part.Mesh.subMeshCount : 1)];
                for (int m = 0; m < materials.Length; m++) materials[m] = _ghostMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void RefreshIndicator()
        {
            // A closed (dusty) slot hides its marker and ghost: the fixture asks for cleaning first.
            bool empty = _occupant == null && IsOpen;
            if (_emptyIndicator != null) _emptyIndicator.SetActive(empty);
            bool showGhost = empty && _ghostAllowed && _ghostNear && _ghostRecipe != null;
            if (showGhost && _ghost == null) BuildGhost();
            if (_ghost != null) _ghost.SetActive(showGhost);
        }

        internal void EditorConfigure(SlotDefinition definition, Transform snapPoint, GameObject emptyIndicator)
        {
            _definition = definition;
            _snapPoint = snapPoint;
            _emptyIndicator = emptyIndicator;
        }

        internal void EditorSetStartOccupant(TidyItem item) => _startOccupant = item;
    }
}
