using UnityEngine;

namespace TidyUp.Hints
{
    /// <summary>
    /// The Tidy Sense marker: a glowing orb with a light column so it reads from across the room.
    /// One reusable instance; Show() moves it and pulses it for a few seconds.
    /// </summary>
    public sealed class HintBeacon : MonoBehaviour
    {
        [SerializeField] private Transform _orb;
        [SerializeField] private Transform _column;
        [SerializeField] private float _pulseSpeed = 6f;
        [SerializeField] private float _pulseAmount = 0.25f;

        private float _hideTime;
        private Vector3 _orbScale;
        private Vector3 _columnScale;

        public bool IsShowing => gameObject.activeSelf;

        private void Awake()
        {
            if (_orb != null) _orbScale = _orb.localScale;
            if (_column != null) _columnScale = _column.localScale;
            // Awake also runs on the first Show() when the beacon starts inactive; only hide if nothing is showing.
            if (_hideTime <= Time.time) gameObject.SetActive(false);
        }

        public void Show(Vector3 position, float duration)
        {
            transform.position = position;
            _hideTime = Time.time + duration;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            float remaining = _hideTime - Time.time;
            if (remaining <= 0f)
            {
                Hide();
                return;
            }

            float fade = Mathf.Clamp01(remaining / 0.4f); // shrink away over the last 0.4 s
            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount;
            if (_orb != null) _orb.localScale = _orbScale * (pulse * fade);
            if (_column != null) _column.localScale = new Vector3(_columnScale.x * fade, _columnScale.y, _columnScale.z * fade);
        }

        internal void EditorConfigure(Transform orb, Transform column)
        {
            _orb = orb;
            _column = column;
        }
    }
}
