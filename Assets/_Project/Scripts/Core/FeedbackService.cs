using UnityEngine;
using UnityEngine.InputSystem;

namespace TidyUp.Core
{
    /// <summary>
    /// Placement feedback: tone (pitch rises as a shelf fills), haptic tick, particle sparkle.
    /// Everything fires synchronously in the same frame as the placement. Audio voices and particle
    /// systems are pooled. Placeholder until FMOD; the public API stays the same.
    /// </summary>
    public sealed class FeedbackService : MonoBehaviour
    {
        // Major pentatonic steps in semitones: rising fill never sounds dissonant.
        private static readonly int[] PentatonicSteps = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

        [Header("Audio")]
        [SerializeField] private int _voiceCount = 8;
        [SerializeField, Range(0f, 1f)] private float _placeVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _rejectVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _pickupVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _hintVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _hopVolume = 0.3f;
        [SerializeField] private float _baseFrequency = 523.25f; // C5

        [Header("Particles")]
        [SerializeField] private ParticleSystem _sparklePrefab;
        [SerializeField] private int _sparklePoolSize = 8;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float _hapticLow = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _hapticHigh = 0.35f;
        [SerializeField] private float _hapticDuration = 0.05f;

        private AudioSource[] _voices;
        private int _nextVoice;
        private ParticleSystem[] _sparkles;
        private int _nextSparkle;
        private AudioClip _placeClip;
        private AudioClip _perfectClip;
        private AudioClip _rejectClip;
        private AudioClip _pickupClip;
        private AudioClip _hintClip;
        private AudioClip _hopClip;
        private float _hapticStopTime = -1f;
        private Gamepad _hapticPad;

        private void Awake()
        {
            _placeClip = ToneSynth.Bell("Place", _baseFrequency, 0.45f, 7f);
            _perfectClip = ToneSynth.Bell("Perfect", _baseFrequency * 2f, 0.6f, 5f);
            _rejectClip = ToneSynth.SoftReject("Reject", 220f, 165f, 0.16f);
            _pickupClip = ToneSynth.Bell("Pickup", _baseFrequency * 1.5f, 0.08f, 40f);
            _hintClip = ToneSynth.Bell("Hint", _baseFrequency * 0.75f, 0.9f, 3.5f);
            _hopClip = ToneSynth.SoftReject("Hop", 300f, 720f, 0.16f); // the same soft glide, upward = "boing"
            _scrubClip = ToneSynth.Brush("Scrub", 0.22f, 0.55f);

            _voices = new AudioSource[Mathf.Max(1, _voiceCount)];
            for (int i = 0; i < _voices.Length; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                _voices[i] = source;
            }

            if (_sparklePrefab != null)
            {
                _sparkles = new ParticleSystem[Mathf.Max(1, _sparklePoolSize)];
                for (int i = 0; i < _sparkles.Length; i++)
                {
                    _sparkles[i] = Instantiate(_sparklePrefab, transform);
                    _sparkles[i].gameObject.name = "Sparkle " + i;
                }

                // Dust puffs for scrubbing: the sparkle, recoloured soft beige, slower and drifting down.
                _dust = new ParticleSystem[4];
                for (int i = 0; i < _dust.Length; i++)
                {
                    ParticleSystem dust = Instantiate(_sparklePrefab, transform);
                    dust.gameObject.name = "Dust " + i;
                    ParticleSystem.MainModule main = dust.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.8f, 0.7f, 0.75f), new Color(0.95f, 0.9f, 0.8f, 0.5f));
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                    main.gravityModifier = 0.08f;
                    _dust[i] = dust;
                }
            }
        }

        private ParticleSystem[] _dust;
        private int _nextDust;
        private AudioClip _scrubClip;

        private int _poofStreak;
        private float _lastPoofTime = -10f;

        /// <summary>
        /// A dust bunny poofed or a good wipe: a puff of dust, a soft swish and a bell that climbs a step with
        /// each poof in a quick row (the run resets after a short pause).
        /// </summary>
        public void PlayPoof(Vector3 position)
        {
            _poofStreak = Time.unscaledTime - _lastPoofTime < 2.5f ? _poofStreak + 1 : 0;
            _lastPoofTime = Time.unscaledTime;
            int step = PentatonicSteps[Mathf.Min(_poofStreak, PentatonicSteps.Length - 1)];
            PlayClip(_placeClip, Mathf.Pow(2f, step / 12f) * 1.5f, _placeVolume * 0.7f);
            PlayClip(_scrubClip, 0.85f + Random.value * 0.3f, _pickupVolume * 0.8f);
            if (_dust != null)
            {
                ParticleSystem dust = _dust[_nextDust];
                _nextDust = (_nextDust + 1) % _dust.Length;
                dust.transform.position = position;
                dust.Play(true);
            }
            Haptic(_hapticLow * 0.5f, _hapticLow);
        }

        /// <summary>Something is clean: a sparkle and a bright, happy two-note chime.</summary>
        public void PlayCleaned(Vector3 position)
        {
            PlayClip(_placeClip, 1.5f, _placeVolume * 0.8f);
            PlayClip(_perfectClip, 1f, _placeVolume * 0.5f);
            PlaySparkle(position);
            Haptic(_hapticLow, _hapticHigh);
        }

        private void Update()
        {
            if (_hapticStopTime > 0f && Time.unscaledTime >= _hapticStopTime) StopHaptics();
        }

        private void OnDisable() => StopHaptics();

        /// <param name="fillRatio">0..1 fill of the shelf after placing; drives pitch.</param>
        public void PlayPlaced(Vector3 position, float fillRatio, bool perfectAlignment)
        {
            int step = PentatonicSteps[Mathf.Clamp(Mathf.RoundToInt(fillRatio * (PentatonicSteps.Length - 1)), 0, PentatonicSteps.Length - 1)];
            PlayClip(_placeClip, Mathf.Pow(2f, step / 12f), _placeVolume);
            if (perfectAlignment) PlayClip(_perfectClip, Mathf.Pow(2f, step / 12f), _placeVolume * 0.5f);
            PlaySparkle(position);
            Haptic(_hapticLow, _hapticHigh);
        }

        public void PlayRejected(Vector3 position)
        {
            PlayClip(_rejectClip, 1f, _rejectVolume);
        }

        public void PlayPickup(Vector3 position)
        {
            PlayClip(_pickupClip, 1f, _pickupVolume);
        }

        /// <summary>Living item hop: a quick upward "boing".</summary>
        public void PlayHop(Vector3 position)
        {
            PlayClip(_hopClip, 0.9f + Random.value * 0.2f, _hopVolume);
        }

        /// <summary>Something magical revealed itself: sparkle and a bright two-note shimmer.</summary>
        public void PlayMagic(Vector3 position)
        {
            PlayClip(_perfectClip, 1.5f, _placeVolume * 0.5f);
            PlayClip(_hintClip, 2f, _hintVolume * 0.6f);
            PlaySparkle(position);
        }

        /// <summary>Restoration stage reached: a soft major chord. The final "location opens" chord is fuller.</summary>
        public void PlayRestore(bool final)
        {
            float volume = final ? _placeVolume : _placeVolume * 0.6f;
            PlayClip(_hintClip, 1f, volume);
            PlayClip(_hintClip, 1.26f, volume * 0.8f);  // major third
            PlayClip(_hintClip, 1.5f, volume * 0.7f);   // fifth
            if (final) PlayClip(_perfectClip, 1f, volume * 0.6f);
        }

        /// <summary>Tidy Sense chime: two soft notes a fifth apart.</summary>
        public void PlayHint()
        {
            PlayClip(_hintClip, 1f, _hintVolume);
            PlayClip(_hintClip, 1.5f, _hintVolume * 0.6f);
        }

        private void PlayClip(AudioClip clip, float pitch, float volume)
        {
            AudioSource voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = volume;
            voice.Play();
        }

        private void PlaySparkle(Vector3 position)
        {
            if (_sparkles == null) return;
            ParticleSystem sparkle = _sparkles[_nextSparkle];
            _nextSparkle = (_nextSparkle + 1) % _sparkles.Length;
            sparkle.transform.position = position;
            sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparkle.Play(true);
        }

        private void Haptic(float low, float high)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return;
            if (_hapticPad != null && _hapticPad != pad) _hapticPad.SetMotorSpeeds(0f, 0f);
            _hapticPad = pad;
            pad.SetMotorSpeeds(low, high);
            _hapticStopTime = Time.unscaledTime + _hapticDuration;
        }

        private void StopHaptics()
        {
            _hapticStopTime = -1f;
            if (_hapticPad == null) return;
            _hapticPad.SetMotorSpeeds(0f, 0f);
            _hapticPad = null;
        }

        internal void EditorConfigure(ParticleSystem sparklePrefab) => _sparklePrefab = sparklePrefab;
    }
}
