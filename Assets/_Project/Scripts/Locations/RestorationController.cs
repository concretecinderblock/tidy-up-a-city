using System;
using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>
    /// Turns location completion into things that come back at thresholds (RestorationStage), e.g. the
    /// baker returning when the Bakery is fully tidy. Locations are warm, lit and colourful from the start:
    /// restoration adds life, it never starts from a grey or dark room (cozy tone, Jesse 2026-11-09).
    /// Music layers will listen to RestorationChanged once FMOD is in.
    /// </summary>
    public sealed class RestorationController : MonoBehaviour
    {
        [SerializeField] private LocationProgress _progress;
        [Tooltip("For a mega location: follow the grand total over all its rooms instead.")]
        [SerializeField] private MegaLocation _mega;
        [SerializeField] private FeedbackService _feedback;
        [SerializeField] private RestorationStage[] _stages = Array.Empty<RestorationStage>();

        private float _applied = -1f;

        /// <summary>Current restoration 0..1.</summary>
        public float Restoration => Mathf.Max(0f, _applied);

        /// <summary>Raised whenever restoration changes. Hook for adaptive music layers.</summary>
        public event Action<float> RestorationChanged;

        private void Update()
        {
            if (_progress == null && _mega == null) return;
            float target = _mega != null ? _mega.CompletionRatio : _progress.CompletionRatio;
            if (target == _applied) return;

            // First frame (after the Muddle scattered in Start): show the current state without fanfare.
            bool instant = _applied < 0f;
            _applied = target;
            Apply(instant);
        }

        private void Apply(bool instant)
        {
            bool anyOn = false;
            bool finalOn = false;
            for (int i = 0; i < _stages.Length; i++)
            {
                if (_stages[i] == null) continue;
                if (_stages[i].Apply(_applied, instant))
                {
                    anyOn = true;
                    if (_stages[i].Threshold >= 0.999f) finalOn = true;
                }
            }
            FeedbackService feedback = _feedback != null ? _feedback : GameServices.Feedback; // player side may live in the City scene
            if (anyOn && feedback != null) feedback.PlayRestore(finalOn);

            RestorationChanged?.Invoke(_applied);
        }

        internal void EditorConfigure(LocationProgress progress, FeedbackService feedback, RestorationStage[] stages, MegaLocation mega = null)
        {
            _progress = progress;
            _mega = mega;
            _feedback = feedback;
            _stages = stages ?? Array.Empty<RestorationStage>();
        }
    }
}
