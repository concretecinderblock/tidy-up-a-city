using System;
using System.Collections.Generic;
using TidyUp.Items;
using TidyUp.Mechanics.ShelfSort;
using UnityEngine;

namespace TidyUp.Mechanics.Cleaning
{
    /// <summary>
    /// Something dusty the player cleans with a quick minigame (family K): dust bunnies to poof on a fixture, or a
    /// wipe timing bar for windows, counter tops and floor smudges.
    /// Required cleanables close their fixture's slots until clean ("clean before stocking"); optional ones are
    /// extra checklist goals. Dirt is shown without new materials: fixtures fade toward a soft dusty beige
    /// (MaterialPropertyBlock, cleared again once clean so batching comes back); smudges fade out.
    /// </summary>
    public sealed class Cleanable : MonoBehaviour, ISlotGate
    {
        public enum DirtLook
        {
            /// <summary>Fixture colours wash toward dust (shelves, counters, windows).</summary>
            Tint,
            /// <summary>The whole object is the dirt and fades away (smudges, cobwebs).</summary>
            Fade,
        }

        [SerializeField] private string _label = "Shelf";
        [Tooltip("Required: the fixture's slots stay closed until it's clean. Optional: an extra goal.")]
        [SerializeField] private bool _required = true;
        [SerializeField] private DirtLook _look = DirtLook.Tint;
        [Tooltip("Which minigame cleans this: dust bunnies (fixtures) or the wipe timing bar (windows, tops, smudges).")]
        [SerializeField] private CleanGame _game = CleanGame.DustBunnies;
        [Tooltip("Wipe game: hits needed.")]
        [SerializeField, Min(1)] private int _wipeHits = 3;
        [Tooltip("Dust bunny game: the bunny that pops out (DustBunny prefab).")]
        [SerializeField] private DustBunny _bunnyPrefab;
        [Tooltip("Slots that stay closed until this is clean (required cleanables).")]
        [SerializeField] private SlotGroup _gatedGroup;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly Color DustColor = new Color(0.78f, 0.73f, 0.64f);
        private const float TintStrength = 0.4f;
        /// <summary>Child renderers with this name are dust layers on top surfaces (built by the location kit).</summary>
        public const string DustFilmName = "Dust Film";

        private struct Part
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Color BaseColor;
            public Color Emission;
            public bool HasEmission;
            public float Smoothness;
            /// <summary>A dust layer (smudge, film on a shelf board): fades out instead of tinting.</summary>
            public bool IsDust;
        }

        private readonly List<Part> _parts = new List<Part>();
        private MaterialPropertyBlock _block;
        private float _dirt = 1f;
        private bool _initialised;

        public string Label => _label;
        public bool Required => _required;
        /// <summary>1 = fully dusty, 0 = clean.</summary>
        public float Dirt => _dirt;
        public bool IsClean => CleaningRules.IsClean(_dirt);
        public int PercentClean => Mathf.RoundToInt((1f - _dirt) * 100f);

        // ISlotGate: dusty required fixtures don't take items yet.
        public bool IsOpen => !_required || IsClean;
        public string ClosedMessage => $"The {_label.ToLowerInvariant()} is too dusty for anything to go on it. Clean it first [E].";

        public CleanGame Game => _game;
        public int WipeHitsNeeded => _wipeHits;
        /// <summary>Dust bunny game running: bunnies are out on the fixture.</summary>
        public bool BunniesOut => _bunnies.Count > 0;
        public int BunniesLeft => _bunnies.Count;

        /// <summary>Raised when dirt changes noticeably, and when the object becomes clean.</summary>
        public event Action<Cleanable> Changed;

        private void Awake() => Initialise();

        private void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            _block = new MaterialPropertyBlock();
            CollectParts();
            if (_required && _gatedGroup != null)
            {
                foreach (ItemSlot slot in _gatedGroup.Slots) if (slot != null) slot.SetGate(this);
            }
            ApplyLook();
        }

        /// <summary>Restores a saved state without feedback.</summary>
        public void SetCleanSilently(bool clean)
        {
            Initialise();
            _dirt = clean ? 0f : 1f;
            ApplyLook();
            RefreshGate();
        }

        // ───────────── Dust bunnies ─────────────

        private readonly List<DustBunny> _bunnies = new List<DustBunny>();
        private readonly List<Vector3> _bunnySpots = new List<Vector3>();
        private int _bunniesTotal;
        private int _popped;
        private readonly Dictionary<int, float> _frontmost = new Dictionary<int, float>();

        /// <summary>Facing direction rounded to 10° steps (slots on one shelf share it; a stand's ring doesn't).</summary>
        private static int FacingKey(Vector3 front) => Mathf.RoundToInt(Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg / 10f);

        /// <summary>
        /// Lets the dust bunnies out: a few fluffy bunnies on the side of the fixture facing the player. Each one
        /// poofed takes some dust with it; the last one leaves the fixture clean.
        /// </summary>
        public void ReleaseBunnies(Vector3 viewer)
        {
            Initialise();
            if (IsClean || BunniesOut || _bunnyPrefab == null) return;
            CollectBunnySpots(viewer);
            if (_bunnySpots.Count == 0) { FinishClean(transform.position); return; }

            int slotCount = _gatedGroup != null ? _gatedGroup.Slots.Length : 0;
            _bunniesTotal = Mathf.Min(CleaningRules.BunnyCount(slotCount), _bunnySpots.Count);
            _popped = 0;
            var used = new List<Vector3>(_bunniesTotal);
            for (int i = 0; i < _bunnySpots.Count && used.Count < _bunniesTotal; i++)
            {
                Vector3 spot = _bunnySpots[i];
                bool crowded = false;
                foreach (Vector3 u in used) if ((u - spot).sqrMagnitude < 0.3f * 0.3f) crowded = true;
                if (crowded && _bunnySpots.Count - i > _bunniesTotal - used.Count) continue;
                used.Add(spot);
                DustBunny bunny = Instantiate(_bunnyPrefab, spot, Quaternion.identity, transform);
                bunny.Init(this, used.Count - 1);
                _bunnies.Add(bunny);
            }
            _bunniesTotal = _bunnies.Count;
        }

        /// <summary>A free spot for a hopping bunny (not where another bunny sits).</summary>
        internal Vector3 NextSpot(Vector3 from)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                Vector3 spot = _bunnySpots[UnityEngine.Random.Range(0, _bunnySpots.Count)];
                if ((spot - from).sqrMagnitude < 0.25f * 0.25f) continue;
                bool taken = false;
                foreach (DustBunny b in _bunnies) if (b != null && (b.transform.position - spot).sqrMagnitude < 0.2f * 0.2f) taken = true;
                if (!taken) return spot;
            }
            return from;
        }

        internal void BunnyPopped(DustBunny bunny)
        {
            _bunnies.Remove(bunny);
            _popped++;
            SetDirt(_bunniesTotal == 0 ? 0f : 1f - (float)_popped / _bunniesTotal, bunny.transform.position);
        }

        /// <summary>Spots in front of the fixture's slots on the player's side, at a comfortable height, shuffled.</summary>
        private void CollectBunnySpots(Vector3 viewer)
        {
            _bunnySpots.Clear();
            if (_gatedGroup == null) return;
            Vector3 centre = _gatedGroup.transform.position;

            // Front row only: on deep shelves, a bunny in the back row hides behind the board above it.
            // "Front" is per facing direction, so every slot of a round stand still counts.
            _frontmost.Clear();
            foreach (ItemSlot slot in _gatedGroup.Slots)
            {
                if (slot == null) continue;
                Transform snap = slot.SnapPoint;
                Vector3 front = snap.rotation * Vector3.back;
                int facing = FacingKey(front);
                float depth = Vector3.Dot(snap.position - centre, front);
                if (!_frontmost.TryGetValue(facing, out float best) || depth > best) _frontmost[facing] = depth;
            }

            foreach (ItemSlot slot in _gatedGroup.Slots)
            {
                if (slot == null) continue;
                Transform snap = slot.SnapPoint;
                Vector3 front = snap.rotation * Vector3.back;
                if (Vector3.Dot(snap.position - centre, front) < _frontmost[FacingKey(front)] - 0.03f) continue;
                Vector3 spot = snap.position + front * 0.05f + Vector3.up * 0.005f;
                Vector3 toViewer = viewer - spot;
                toViewer.y = 0f;
                if (Vector3.Dot(front, toViewer.normalized) < 0.2f) continue;  // facing away from the player
                if (spot.y < 0.1f || spot.y > 2.0f) continue;                  // out of easy reach
                _bunnySpots.Add(spot);
            }
            for (int i = _bunnySpots.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_bunnySpots[i], _bunnySpots[j]) = (_bunnySpots[j], _bunnySpots[i]);
            }
        }

        // ───────────── Wipe ─────────────

        /// <summary>One good wipe (the Interactor runs the timing bar). The last one leaves it clean.</summary>
        public void WipeHit(int hits, Vector3 at)
        {
            Initialise();
            SetDirt(1f - Mathf.Clamp01((float)hits / _wipeHits), at);
        }

        // ───────────── Shared ─────────────

        private void SetDirt(float dirt, Vector3 at)
        {
            if (IsClean) return;
            _dirt = Mathf.Clamp01(dirt);
            ApplyLook();
            if (_dirt <= 0f) FinishClean(at);
            else Changed?.Invoke(this);
        }

        private void FinishClean(Vector3 at)
        {
            _dirt = 0f;
            foreach (DustBunny b in _bunnies) if (b != null) Destroy(b.gameObject);
            _bunnies.Clear();
            ApplyLook();
            RefreshGate();
            Changed?.Invoke(this);
            TidyUp.Core.GameServices.Feedback?.PlayCleaned(at);
            TidyUp.Core.GameServices.Announce(_required
                ? $"The {_label.ToLowerInvariant()} is clean! Things can go on it now."
                : $"The {_label.ToLowerInvariant()} is sparkling clean.");
        }

        private void RefreshGate()
        {
            if (_gatedGroup == null) return;
            foreach (ItemSlot slot in _gatedGroup.Slots) if (slot != null) slot.RefreshGate();
        }

        private void CollectParts()
        {
            _parts.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                // Items, slot markers and ghosts aren't part of the fixture.
                if (renderer.GetComponentInParent<ItemSlot>(true) != null || renderer.GetComponentInParent<TidyItem>(true) != null) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null || !material.HasProperty(BaseColorId)) continue;
                    bool emission = material.HasProperty(EmissionId) && material.IsKeywordEnabled("_EMISSION");
                    _parts.Add(new Part
                    {
                        Renderer = renderer,
                        MaterialIndex = m,
                        BaseColor = material.GetColor(BaseColorId),
                        Emission = emission ? material.GetColor(EmissionId) : Color.black,
                        HasEmission = emission,
                        Smoothness = material.HasProperty(SmoothnessId) ? material.GetFloat(SmoothnessId) : 0.3f,
                        IsDust = _look == DirtLook.Fade || renderer.name.StartsWith(DustFilmName),
                    });
                }
            }
        }

        private void ApplyLook()
        {
            for (int i = 0; i < _parts.Count; i++)
            {
                Part part = _parts[i];
                if (part.Renderer == null) continue;

                if (_dirt <= 0f)
                {
                    // Clean: no override at all, so the renderer batches with everything else again.
                    part.Renderer.SetPropertyBlock(null, part.MaterialIndex);
                    if (part.IsDust) part.Renderer.enabled = false;
                    continue;
                }

                _block.Clear();
                if (part.IsDust)
                {
                    Color c = part.BaseColor;
                    c.a *= _dirt;
                    _block.SetColor(BaseColorId, c);
                    part.Renderer.enabled = true;
                }
                else
                {
                    float t = _dirt * TintStrength;
                    Color c = Color.Lerp(part.BaseColor, DustColor, t);
                    c.a = part.BaseColor.a;
                    _block.SetColor(BaseColorId, c);
                    _block.SetFloat(SmoothnessId, Mathf.Lerp(part.Smoothness, 0.02f, _dirt));
                    if (part.HasEmission) _block.SetColor(EmissionId, part.Emission * Mathf.Lerp(1f, 0.35f, _dirt));
                }
                part.Renderer.SetPropertyBlock(_block, part.MaterialIndex);
            }
        }

        internal void EditorConfigure(string label, bool required, DirtLook look, CleanGame game, int wipeHits,
            SlotGroup gatedGroup, DustBunny bunnyPrefab)
        {
            _label = label;
            _required = required;
            _look = look;
            _game = game;
            _wipeHits = Mathf.Max(1, wipeHits);
            _gatedGroup = gatedGroup;
            _bunnyPrefab = bunnyPrefab;
        }
    }
}
