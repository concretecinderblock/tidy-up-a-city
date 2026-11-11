using TidyUp.Items;
using UnityEngine;

namespace TidyUp.Mechanics.LivingItems
{
    /// <summary>
    /// Gives an item personality (mechanic family I). The behaviour comes from ItemDefinition.Living:
    /// Skittish hops away a few times then tires; Floating drifts through the air; Shy is a faint shimmer
    /// until the player is close; Disguised shows a stand-in look and name until looked at or grabbed.
    /// Cozy by design: nothing is ever lost or failed. Update only runs while the item is loose.
    /// </summary>
    [RequireComponent(typeof(TidyItem))]
    public sealed class LivingItem : MonoBehaviour, ILivingBehaviour
    {
        private const string ShimmerName = "A faint shimmer";

        [SerializeField] private LivingItemDirector _director;
        [Tooltip("The item's real look (body only; eyes live outside it so they stay visible).")]
        [SerializeField] private GameObject _realVisual;
        [Tooltip("Disguised items: the stand-in look shown until revealed.")]
        [SerializeField] private GameObject _disguiseVisual;
        [SerializeField] private Transform _eyes;
        [Tooltip("Shy items: material used while hidden.")]
        [SerializeField] private Material _shimmerMaterial;

        [Header("Skittish")]
        [SerializeField] private float _startleDistance = 1.4f;
        [SerializeField] private int _hopsBeforeTired = 3;
        [SerializeField] private float _hopDistance = 1.6f;
        [SerializeField] private float _hopDuration = 0.45f;
        [SerializeField] private float _hopHeight = 0.45f;
        [SerializeField] private float _hopCooldown = 0.6f;

        [Header("Floating")]
        [SerializeField] private float _floatHeight = 1.3f;
        [SerializeField] private float _floatRadius = 0.5f;
        [SerializeField] private float _floatBob = 0.08f;

        [Header("Shy")]
        [SerializeField] private float _revealDistance = 2.2f;
        [SerializeField] private float _revealFade = 0.6f;

        [Header("Disguised")]
        [SerializeField] private float _gazeToReveal = 0.8f;

        private TidyItem _item;
        private LivingBehaviour _behaviour;
        private ItemState _state = ItemState.Placed;
        private int _labelVersion;

        // Skittish
        private int _hopsLeft;
        private bool _tired;
        private bool _hopping;
        private float _hopTime;
        private float _cooldownUntil;
        private Vector3 _hopStart;
        private Vector3 _hopEnd;
        private Quaternion _hopRotation;
        private System.Random _random;

        // Floating
        private Vector3 _floatAnchor;
        private float _floatPhase;

        // Shy
        private Renderer[] _bodyRenderers;
        private Material[][] _bodyMaterials;
        private Material[][] _shimmerSets;
        private bool _shyShown = true;

        // Disguised
        private bool _revealed;
        private float _gaze;

        public LivingBehaviour Behaviour => _behaviour;
        public bool IsTired => _tired;
        public bool IsRevealed => _revealed;
        public bool IsShyHidden => _behaviour == LivingBehaviour.Shy && !_shyShown;
        public int LabelVersion => _labelVersion;

        public string DisplayNameOverride
        {
            get
            {
                if (_behaviour == LivingBehaviour.Disguised && !_revealed && _item.Definition != null) return _item.Definition.DisguiseName;
                if (IsShyHidden) return ShimmerName;
                return null;
            }
        }

        private void Awake()
        {
            _item = GetComponent<TidyItem>();
            _behaviour = _item.Definition != null ? _item.Definition.Living : LivingBehaviour.None;
            _hopsLeft = _hopsBeforeTired;
            string id = _item.Definition != null ? _item.Definition.Id : name;
            _random = new System.Random(unchecked(id.GetHashCode() * 31 + transform.position.GetHashCode()));
            _floatPhase = (float)_random.NextDouble() * 100f;

            if (_realVisual != null)
            {
                _bodyRenderers = _realVisual.GetComponentsInChildren<Renderer>(true);
                _bodyMaterials = new Material[_bodyRenderers.Length][];
                _shimmerSets = new Material[_bodyRenderers.Length][];
                for (int i = 0; i < _bodyRenderers.Length; i++)
                {
                    _bodyMaterials[i] = _bodyRenderers[i].sharedMaterials;
                    _shimmerSets[i] = new Material[_bodyMaterials[i].Length];
                    for (int m = 0; m < _shimmerSets[i].Length; m++) _shimmerSets[i][m] = _shimmerMaterial;
                }
            }
            enabled = false; // only ticks while loose
        }

        public void OnStateChanged(ItemState state)
        {
            _state = state;
            if (state == ItemState.Loose)
            {
                if (_behaviour == LivingBehaviour.Floating) BeginFloating();
                if (_behaviour == LivingBehaviour.Disguised && !_revealed) SetDisguised(true);
                enabled = _behaviour == LivingBehaviour.Skittish || _behaviour == LivingBehaviour.Floating || _behaviour == LivingBehaviour.Shy;
                return;
            }

            // Carried or placed: calm, visible, itself.
            _hopping = false;
            enabled = false;
            if (_behaviour == LivingBehaviour.Shy) SetShyShown(true, announce: false);
        }

        private void Update()
        {
            if (_state != ItemState.Loose) return;
            switch (_behaviour)
            {
                case LivingBehaviour.Skittish: UpdateSkittish(); break;
                case LivingBehaviour.Floating: UpdateFloating(); break;
                case LivingBehaviour.Shy: UpdateShy(); break;
            }
        }

        public bool TryCatch(out string message)
        {
            message = null;
            switch (_behaviour)
            {
                case LivingBehaviour.Skittish:
                    if (_tired) return true;
                    if (_hopping || _hopsLeft <= 0)
                    {
                        message = $"{_item.DisplayName} is too quick!";
                        return false;
                    }
                    Hop();
                    message = _hopsLeft > 0
                        ? $"{_item.DisplayName} hops out of reach!"
                        : $"{_item.DisplayName} hops away… and looks sleepy now.";
                    return false;

                case LivingBehaviour.Disguised:
                    if (_revealed) return true;
                    string disguise = _item.DisplayName;
                    Reveal();
                    message = $"That {disguise} was a {_item.DisplayName} in disguise!";
                    return false;

                default:
                    return true;
            }
        }

        public void OnGazed(float deltaTime)
        {
            if (_behaviour != LivingBehaviour.Disguised || _revealed || _state != ItemState.Loose) return;
            _gaze += deltaTime;
            if (_gaze >= _gazeToReveal) Reveal();
        }

        // ───────────── Skittish ─────────────

        private void UpdateSkittish()
        {
            if (_hopping)
            {
                _hopTime += Time.deltaTime;
                float t = _hopDuration <= 0f ? 1f : _hopTime / _hopDuration;
                _item.MoveTo(LivingRules.HopArc(_hopStart, _hopEnd, _hopHeight, t), _hopRotation);
                if (t >= 1f) Land();
                return;
            }

            if (_tired || _hopsLeft <= 0 || Time.time < _cooldownUntil || _director == null || _director.Player == null) return;
            Vector3 toPlayer = _director.Player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < _startleDistance * _startleDistance) Hop();
        }

        private void Hop()
        {
            if (_director == null || _director.Player == null) return;
            Vector3 start = transform.position;
            float angle = ((float)_random.NextDouble() * 2f - 1f) * 35f;
            Vector3 direction = LivingRules.FleeDirection(start, _director.Player.position, angle);
            Vector3 target = LivingRules.ClampToArea(start + direction * _hopDistance, _director.RoamBounds, 0.3f);

            Vector3 end = new Vector3(target.x, start.y, target.z);
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, _director.SurfaceMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point + Vector3.up * 0.01f;
            }

            _hopStart = start;
            _hopEnd = end;
            _hopRotation = Quaternion.LookRotation(direction, Vector3.up);
            _hopTime = 0f;
            _hopping = true;
            _hopsLeft--;
            _item.SetSimulated(false);
            _director.Feedback?.PlayHop(start);
        }

        private void Land()
        {
            _hopping = false;
            _cooldownUntil = Time.time + _hopCooldown;
            _item.MoveTo(_hopEnd, _hopRotation);
            _item.SetSimulated(true);
            if (_hopsLeft <= 0) BecomeTired();
        }

        private void BecomeTired()
        {
            _tired = true;
            if (_eyes != null)
            {
                Vector3 s = _eyes.localScale;
                _eyes.localScale = new Vector3(s.x, s.y * 0.25f, s.z); // sleepy eyes
            }
            _labelVersion++;
        }

        // ───────────── Floating ─────────────

        private void BeginFloating()
        {
            Vector3 p = LivingRules.ClampToArea(transform.position, _director != null ? _director.RoamBounds : new Bounds(transform.position, Vector3.one * 100f), _floatRadius + 0.3f);
            _floatAnchor = new Vector3(p.x, _floatHeight, p.z);
            _item.SetSimulated(false);
        }

        private void UpdateFloating()
        {
            Vector3 position = _floatAnchor + LivingRules.FloatOffset(Time.time, _floatRadius, _floatBob, _floatPhase);
            Quaternion rotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.9f + _floatPhase) * 8f, Time.time * 20f + _floatPhase * 10f, 0f);
            // Rise smoothly from wherever it was dropped instead of snapping up.
            _item.MoveTo(Vector3.Lerp(transform.position, position, 1f - Mathf.Exp(-3f * Time.deltaTime)), rotation);
        }

        // ───────────── Shy ─────────────

        private void UpdateShy()
        {
            if (_director == null || _director.Player == null) return;
            float distance = Vector3.Distance(_director.Player.position, transform.position);
            float visibility = LivingRules.ShyVisibility(distance, _revealDistance, _revealFade);
            bool shown = _shyShown ? visibility > 0.3f : visibility > 0.7f; // hysteresis: no flicker at the edge
            if (shown != _shyShown) SetShyShown(shown, announce: shown);
        }

        private void SetShyShown(bool shown, bool announce)
        {
            if (_shyShown == shown) return;
            _shyShown = shown;
            if (_bodyRenderers != null)
            {
                for (int i = 0; i < _bodyRenderers.Length; i++)
                {
                    _bodyRenderers[i].sharedMaterials = shown || _shimmerMaterial == null ? _bodyMaterials[i] : _shimmerSets[i];
                }
            }
            _labelVersion++;
            if (announce) _director?.Feedback?.PlayMagic(transform.position);
        }

        // ───────────── Disguised ─────────────

        private void SetDisguised(bool disguised)
        {
            if (_disguiseVisual != null) _disguiseVisual.SetActive(disguised);
            if (_realVisual != null) _realVisual.SetActive(!disguised);
            if (_eyes != null) _eyes.gameObject.SetActive(!disguised);
            _labelVersion++;
        }

        private void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            SetDisguised(false);
            _director?.Feedback?.PlayMagic(transform.position);
        }

        internal void EditorConfigure(GameObject realVisual, GameObject disguiseVisual, Transform eyes, Material shimmer)
        {
            _realVisual = realVisual;
            _disguiseVisual = disguiseVisual;
            _eyes = eyes;
            _shimmerMaterial = shimmer;
        }

        internal void EditorSetDirector(LivingItemDirector director) => _director = director;
    }
}
