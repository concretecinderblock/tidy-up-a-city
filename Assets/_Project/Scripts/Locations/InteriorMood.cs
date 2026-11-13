using UnityEngine;
using UnityEngine.Rendering;

namespace TidyUp.Locations
{
    /// <summary>
    /// A location's interior atmosphere. Locations load additively under the City, so lighting settings
    /// (ambient, fog, reflections) belong to the City's scene; while a location is loaded this swaps in the
    /// interior's warm, dim ambient and haze, switches on its post-processing volume and renders its reflection
    /// probe once, and puts the street's settings back when the location unloads.
    /// </summary>
    public sealed class InteriorMood : MonoBehaviour
    {
        [Header("Ambient (trilight)")]
        // Colours are in display (sRGB) terms; the project renders in linear space, so they land darker and
        // more saturated than they look in the picker. Keep them light and only gently tinted.
        [SerializeField] private Color _ambientSky = new Color(0.32f, 0.26f, 0.22f);
        [SerializeField] private Color _ambientEquator = new Color(0.24f, 0.18f, 0.14f);
        [SerializeField] private Color _ambientGround = new Color(0.12f, 0.09f, 0.07f);

        [Header("Haze")]
        [SerializeField] private Color _fogColor = new Color(0.32f, 0.24f, 0.18f);
        [SerializeField, Range(0f, 0.1f)] private float _fogDensity = 0.022f;

        [Header("Parts")]
        [SerializeField] private Volume _volume;
        [SerializeField] private ReflectionProbe _probe;
        [SerializeField, Range(0f, 1f)] private float _reflectionIntensity = 0.6f;

        private struct Saved
        {
            public AmbientMode AmbientMode;
            public Color Sky, Equator, Ground, Ambient;
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity;
            public float ReflectionIntensity;
        }

        private Saved _saved;
        private bool _applied;

        private void OnEnable()
        {
            _saved = new Saved
            {
                AmbientMode = RenderSettings.ambientMode,
                Sky = RenderSettings.ambientSkyColor,
                Equator = RenderSettings.ambientEquatorColor,
                Ground = RenderSettings.ambientGroundColor,
                Ambient = RenderSettings.ambientLight,
                Fog = RenderSettings.fog,
                FogMode = RenderSettings.fogMode,
                FogColor = RenderSettings.fogColor,
                FogDensity = RenderSettings.fogDensity,
                ReflectionIntensity = RenderSettings.reflectionIntensity,
            };
            _applied = true;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
            RenderSettings.fog = _fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.reflectionIntensity = _reflectionIntensity;
            if (_volume != null) _volume.enabled = true;
        }

        private void Start()
        {
            // One render once the Muddle has laid things out; afterwards it's a static cubemap (no per-frame cost).
            if (_probe != null) _probe.RenderProbe();
        }

        private void OnDisable()
        {
            if (_volume != null) _volume.enabled = false;
            if (!_applied) return;
            _applied = false;
            RenderSettings.ambientMode = _saved.AmbientMode;
            RenderSettings.ambientSkyColor = _saved.Sky;
            RenderSettings.ambientEquatorColor = _saved.Equator;
            RenderSettings.ambientGroundColor = _saved.Ground;
            RenderSettings.ambientLight = _saved.Ambient;
            RenderSettings.fog = _saved.Fog;
            RenderSettings.fogMode = _saved.FogMode;
            RenderSettings.fogColor = _saved.FogColor;
            RenderSettings.fogDensity = _saved.FogDensity;
            RenderSettings.reflectionIntensity = _saved.ReflectionIntensity;
        }

        internal void EditorConfigure(Color sky, Color equator, Color ground, Color fogColor, float fogDensity,
            Volume volume, ReflectionProbe probe, float reflectionIntensity)
        {
            _ambientSky = sky;
            _ambientEquator = equator;
            _ambientGround = ground;
            _fogColor = fogColor;
            _fogDensity = fogDensity;
            _volume = volume;
            _probe = probe;
            _reflectionIntensity = reflectionIntensity;
        }
    }
}
