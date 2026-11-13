using UnityEngine;

namespace TidyUp.Ambience
{
    /// <summary>
    /// A townsperson strolling a pavement: walks from A to B with a gentle bob, pauses to look about, and turns
    /// back. Decoration only (no collider in the way of the player's walk, no pathfinding).
    /// </summary>
    public sealed class Stroller : MonoBehaviour
    {
        [SerializeField] private Vector3 _a;
        [SerializeField] private Vector3 _b;
        [SerializeField] private float _speed = 1.1f;
        [SerializeField] private Transform _body;

        private bool _toB = true;
        private float _pauseUntil;
        private float _seed;

        private void Start()
        {
            _seed = Random.value * 10f;
            transform.position = Vector3.Lerp(_a, _b, Random.value);
        }

        private void Update()
        {
            float t = Time.time;
            Vector3 target = _toB ? _b : _a;
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (t < _pauseUntil)
            {
                // Pause: look around a little.
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.Euler(0f, transform.eulerAngles.y + Mathf.Sin(t * 0.8f + _seed) * 40f * Time.deltaTime, 0f), 1f);
                if (_body != null) _body.localPosition = Vector3.zero;
                return;
            }
            if (to.sqrMagnitude < 0.04f)
            {
                _toB = !_toB;
                _pauseUntil = t + Random.Range(1.5f, 4f);
                return;
            }
            transform.position += to.normalized * _speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 4f);
            if (_body != null) _body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(t * 7f + _seed)) * 0.04f, 0f);
        }

        internal void EditorConfigure(Vector3 a, Vector3 b, Transform body, float speed)
        {
            _a = a;
            _b = b;
            _body = body;
            _speed = speed;
        }
    }
}
