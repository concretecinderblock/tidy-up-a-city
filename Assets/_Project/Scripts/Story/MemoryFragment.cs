using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Story
{
    /// <summary>
    /// The object that holds a location's memory (a glowing photo on the Bakery counter). Revealed by a
    /// RestorationStage at 100%; announces itself once, bobs gently, and plays its memory on interact.
    /// Replayable after the first viewing.
    /// </summary>
    public sealed class MemoryFragment : Interactable
    {
        [SerializeField] private MemoryDefinition _memory;
        [SerializeField] private MemoryService _service;
        [Tooltip("Shown on the HUD the first time the fragment appears.")]
        [SerializeField] private string _announcement = "Something glimmers nearby…";
        [SerializeField] private Transform _visual;
        [SerializeField] private float _bobHeight = 0.025f;
        [SerializeField] private float _bobSpeed = 1.6f;

        private bool _announced;
        private Vector3 _visualBase;

        public MemoryDefinition Memory => _memory;

        // The memory service lives on the player side (City scene), so resolve it at use time.
        private MemoryService Service => _service != null ? _service : GameServices.Memories;

        public override string FocusLabel
        {
            get
            {
                if (_memory == null) return "Memory";
                bool seen = Service != null && Service.Journal.Has(_memory.Id);
                return seen ? $"Memory: {_memory.Title}  (relive)" : $"Memory: {_memory.Title}";
            }
        }

        public override bool CanInteract => _memory != null && Service != null && !Service.IsPlaying;

        private void Awake()
        {
            if (_visual != null) _visualBase = _visual.localPosition;
        }

        private void OnEnable()
        {
            // Restoration stages enable this at 100%. Scale-from-zero means it was just revealed, not loaded in.
            if (_announced || Service == null) return;
            _announced = true;
            if (_memory != null && Service.Journal.Has(_memory.Id)) return; // already seen: no need to point it out again
            Service.Announce(_announcement);
        }

        private void Update()
        {
            if (_visual == null) return;
            _visual.localPosition = _visualBase + Vector3.up * (Mathf.Sin(Time.time * _bobSpeed) * _bobHeight);
        }

        public override void Interact(Interactor interactor)
        {
            if (CanInteract) Service.Play(_memory);
        }

        internal void EditorConfigure(MemoryDefinition memory, MemoryService service, string announcement, Transform visual)
        {
            _memory = memory;
            _service = service;
            _announcement = announcement;
            _visual = visual;
        }
    }
}
