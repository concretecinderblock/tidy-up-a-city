using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Mechanics.Cleaning
{
    /// <summary>
    /// A fluffy dust bunny on a dusty fixture (family K minigame). It bobs, watches the player, and now and then
    /// hops to another spot; look at it and press interact to poof it. Spawned by Cleanable, a handful at a time.
    /// </summary>
    public sealed class DustBunny : Interactable
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private Vector2 _hopInterval = new Vector2(1.6f, 2.6f);
        [SerializeField] private float _hopTime = 0.3f;
        [SerializeField] private float _hopHeight = 0.12f;

        private Cleanable _owner;
        private float _seed;
        private float _nextHop;
        private bool _hopping;
        private float _hopStart;
        private Vector3 _from;
        private Vector3 _to;
        private bool _popped;
        private float _spawnTime;

        public override string FocusLabel => _owner != null ? $"Dust bunny!  [E] poof  ·  {_owner.BunniesLeft} left" : "Dust bunny!  [E] poof";
        public override bool CanInteract => !_popped;

        internal void Init(Cleanable owner, int index)
        {
            _owner = owner;
            _seed = index * 1.37f + Random.value * 10f;
            _spawnTime = Time.time + index * 0.08f; // pop out one after another
            _nextHop = Time.time + Random.Range(_hopInterval.x, _hopInterval.y) + 0.8f;
            if (_visual != null) _visual.localScale = Vector3.zero;
        }

        public override void Interact(Interactor interactor) => Poof();

        public void Poof()
        {
            if (_popped) return;
            _popped = true;
            GameServices.Feedback?.PlayPoof(transform.position);
            if (_owner != null) _owner.BunnyPopped(this);
            Destroy(gameObject);
        }

        private void Update()
        {
            float t = Time.time;
            Transform viewer = GameServices.Player;

            // Pop in, then bob and squash a little.
            float grow = Mathf.Clamp01((t - _spawnTime) / 0.25f);
            float overshoot = grow < 1f ? Mathf.Sin(grow * Mathf.PI) * 0.25f + grow : 1f;
            float bob = Mathf.Sin((t + _seed) * 7f);
            if (_visual != null)
            {
                _visual.localScale = new Vector3(1f + bob * 0.06f, 1f - bob * 0.06f, 1f + bob * 0.06f) * overshoot;
            }

            // Hop to another spot every couple of seconds (keeps you on your toes, never for long).
            if (!_hopping && t >= _nextHop && _owner != null)
            {
                _from = transform.position;
                _to = _owner.NextSpot(_from);
                _hopping = _to != _from;
                _hopStart = t;
                _nextHop = t + Random.Range(_hopInterval.x, _hopInterval.y);
                if (_hopping) GameServices.Feedback?.PlayHop(_from);
            }
            if (_hopping)
            {
                float k = Mathf.Clamp01((t - _hopStart) / _hopTime);
                transform.position = Vector3.Lerp(_from, _to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * _hopHeight);
                if (k >= 1f) _hopping = false;
            }

            // Always look at the player (eyes on +Z).
            if (viewer != null)
            {
                Vector3 look = viewer.position - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            }
        }

        internal void EditorConfigure(Transform visual) => _visual = visual;
    }
}
