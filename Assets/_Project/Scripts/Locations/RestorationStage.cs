using System;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// Something that comes back when a location reaches a restoration threshold: lamps switching on,
    /// props and NPCs popping in, signs lighting up. Driven by the RestorationController.
    /// Only runs Update while animating.
    /// </summary>
    public sealed class RestorationStage : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _threshold = 0.5f;
        [Tooltip("Objects that pop in (scale) when the stage is reached.")]
        [SerializeField] private GameObject[] _reveal = Array.Empty<GameObject>();
        [Tooltip("Lights that fade up to their authored intensity.")]
        [SerializeField] private Light[] _lights = Array.Empty<Light>();
        [Tooltip("Renderers that swap to the 'on' material (lamp bulbs, neon).")]
        [SerializeField] private Renderer[] _glowRenderers = Array.Empty<Renderer>();
        [SerializeField] private Material _glowOff;
        [SerializeField] private Material _glowOn;
        [SerializeField] private float _revealDuration = 0.6f;
        [SerializeField] private float _lightFadeDuration = 1.2f;

        private Vector3[] _revealScales = Array.Empty<Vector3>();
        private float[] _lightIntensities = Array.Empty<float>();
        private bool _cached;
        private bool _isOn;
        private float _animationTime = -1f;

        public float Threshold => _threshold;
        public bool IsOn => _isOn;

        private void Awake()
        {
            CacheTargets();
            enabled = false;
        }

        private void CacheTargets()
        {
            if (_cached) return;
            _cached = true;
            _revealScales = new Vector3[_reveal.Length];
            for (int i = 0; i < _reveal.Length; i++) _revealScales[i] = _reveal[i] != null ? _reveal[i].transform.localScale : Vector3.one;
            _lightIntensities = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }

        /// <summary>Applies the restoration value. Returns true if the stage just switched on.</summary>
        public bool Apply(float restoration, bool instant)
        {
            CacheTargets();
            bool shouldBeOn = RestorationMath.IsReached(restoration, _threshold);
            if (shouldBeOn == _isOn && !instant) return false;

            bool switchedOn = shouldBeOn && !_isOn;
            _isOn = shouldBeOn;

            SetGlow(shouldBeOn);
            if (!shouldBeOn || instant)
            {
                SetVisuals(shouldBeOn ? 1f : 0f, shouldBeOn ? 1f : 0f, shouldBeOn);
                _animationTime = -1f;
                enabled = false;
            }
            else
            {
                SetVisuals(0f, 0f, true);
                _animationTime = 0f;
                enabled = true;
            }
            return switchedOn && !instant;
        }

        private void Update()
        {
            if (_animationTime < 0f)
            {
                enabled = false;
                return;
            }

            _animationTime += Time.deltaTime;
            float reveal = _revealDuration <= 0f ? 1f : Mathf.Clamp01(_animationTime / _revealDuration);
            float light = _lightFadeDuration <= 0f ? 1f : Mathf.Clamp01(_animationTime / _lightFadeDuration);
            SetVisuals(RestorationMath.PopIn(reveal), RestorationMath.Ease(light), true);

            if (reveal >= 1f && light >= 1f)
            {
                _animationTime = -1f;
                enabled = false;
            }
        }

        private void SetVisuals(float revealT, float lightT, bool visible)
        {
            for (int i = 0; i < _reveal.Length; i++)
            {
                GameObject go = _reveal[i];
                if (go == null) continue;
                if (go.activeSelf != visible) go.SetActive(visible);
                go.transform.localScale = _revealScales[i] * revealT;
            }
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null) continue;
                _lights[i].intensity = _lightIntensities[i] * lightT;
                _lights[i].enabled = lightT > 0f;
            }
        }

        private void SetGlow(bool on)
        {
            Material material = on ? _glowOn : _glowOff;
            if (material == null) return;
            for (int i = 0; i < _glowRenderers.Length; i++)
            {
                if (_glowRenderers[i] != null) _glowRenderers[i].sharedMaterial = material;
            }
        }

        internal void EditorConfigure(float threshold, GameObject[] reveal, Light[] lights, Renderer[] glowRenderers,
            Material glowOff, Material glowOn)
        {
            _threshold = threshold;
            _reveal = reveal ?? Array.Empty<GameObject>();
            _lights = lights ?? Array.Empty<Light>();
            _glowRenderers = glowRenderers ?? Array.Empty<Renderer>();
            _glowOff = glowOff;
            _glowOn = glowOn;
        }
    }
}
