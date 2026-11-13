using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Ambience
{
    /// <summary>
    /// A little flock of pigeons on the street: they hop about and peck, and when the player comes close they
    /// burst up and away, circle, and land again a few seconds later. Pure decoration; one Update for the flock.
    /// Children are the birds (built by the street builder).
    /// </summary>
    public sealed class PigeonFlock : MonoBehaviour
    {
        [SerializeField] private float _radius = 2.5f;
        [SerializeField] private float _scareDistance = 3f;

        private enum Mode { Ground, Flying }

        private struct Bird
        {
            public Transform T;
            public Vector3 Target;
            public float NextMove;
            public float Peck;
            public float Seed;
        }

        private Bird[] _birds;
        private Mode _mode = Mode.Ground;
        private float _modeTime;
        private Vector3 _home;

        private void Start()
        {
            _home = transform.position;
            _birds = new Bird[transform.childCount];
            for (int i = 0; i < _birds.Length; i++)
            {
                Transform t = transform.GetChild(i);
                _birds[i] = new Bird { T = t, Target = t.position, NextMove = Random.Range(0f, 2f), Seed = Random.value * 10f };
            }
        }

        private void Update()
        {
            if (_birds == null) return;
            float t = Time.time;
            Transform player = GameServices.Player;
            bool near = player != null && (player.position - _home).sqrMagnitude < _scareDistance * _scareDistance;

            if (_mode == Mode.Ground && near)
            {
                _mode = Mode.Flying;
                _modeTime = t;
            }
            else if (_mode == Mode.Flying && t - _modeTime > 5f && !near)
            {
                _mode = Mode.Ground;
                _modeTime = t;
            }

            for (int i = 0; i < _birds.Length; i++)
            {
                ref Bird b = ref _birds[i];
                if (b.T == null) continue;
                if (_mode == Mode.Flying)
                {
                    // Up, out and round in a lazy loop above the rooftops.
                    float a = (t - _modeTime) * 1.2f + b.Seed;
                    Vector3 sky = _home + new Vector3(Mathf.Cos(a) * (4f + b.Seed * 0.2f), 6f + Mathf.Sin(a * 1.7f) * 0.6f, Mathf.Sin(a) * 4f);
                    Vector3 to = sky - b.T.position;
                    b.T.position += to * Mathf.Min(1f, Time.deltaTime * 1.8f);
                    if (to.sqrMagnitude > 0.01f) b.T.rotation = Quaternion.Slerp(b.T.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 6f);
                    b.T.localScale = Vector3.one * (1f + Mathf.Sin(t * 30f + b.Seed) * 0.08f); // flap
                    b.Target = _home + Random.insideUnitSphere.WithY(0f) * _radius;
                    continue;
                }

                // Ground: glide back if landing, otherwise little hops between pecks.
                Vector3 pos = b.T.position;
                Vector3 ground = new Vector3(pos.x, _home.y, pos.z);
                if (pos.y > _home.y + 0.01f) b.T.position = Vector3.MoveTowards(pos, b.Target, Time.deltaTime * 5f);
                else if (t >= b.NextMove)
                {
                    b.Target = _home + Random.insideUnitSphere.WithY(0f) * _radius;
                    b.NextMove = t + Random.Range(1.2f, 3.5f);
                }
                Vector3 step = b.Target - ground;
                if (pos.y <= _home.y + 0.01f && step.sqrMagnitude > 0.0025f)
                {
                    Vector3 move = Vector3.ClampMagnitude(step, Time.deltaTime * 0.6f);
                    float hop = Mathf.Abs(Mathf.Sin(t * 14f + b.Seed)) * 0.03f;
                    b.T.position = new Vector3(ground.x + move.x, _home.y + hop, ground.z + move.z);
                    b.T.rotation = Quaternion.Slerp(b.T.rotation, Quaternion.LookRotation(step.normalized), Time.deltaTime * 8f);
                }
                // Peck: dip forward now and then.
                float peck = Mathf.Max(0f, Mathf.Sin(t * 3f + b.Seed * 3f)) > 0.95f ? 25f : 0f;
                b.Peck = Mathf.Lerp(b.Peck, peck, Time.deltaTime * 12f);
                Vector3 e = b.T.eulerAngles;
                b.T.rotation = Quaternion.Euler(b.Peck, e.y, 0f);
                b.T.localScale = Vector3.one;
            }
        }
    }

    internal static class VectorExtensions
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
