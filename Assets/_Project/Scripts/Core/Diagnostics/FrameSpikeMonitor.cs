using UnityEngine;

namespace TidyUp.Core.Diagnostics
{
    /// <summary>
    /// Development aid: logs any frame slower than the threshold (with what was going on), so lag spikes can be
    /// traced to a cause. Editor and development builds only; it removes itself from release builds.
    /// Also keeps the worst frame of the last few seconds for quick checks.
    /// </summary>
    public sealed class FrameSpikeMonitor : MonoBehaviour
    {
        [SerializeField] private float _thresholdMs = 80f;

        public static float WorstRecentMs { get; private set; }
        public static int SpikeCount { get; private set; }
        private float _windowEnd;
        private float _windowWorst;

        private void Awake()
        {
            if (!Debug.isDebugBuild) Destroy(this);
        }

        private void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (Time.frameCount > 10 && ms > _thresholdMs)
            {
                SpikeCount++;
                Debug.Log($"[Tidy Up][Spike] {ms:0} ms frame (scenes loaded: {UnityEngine.SceneManagement.SceneManager.sceneCount})", this);
            }
            _windowWorst = Mathf.Max(_windowWorst, ms);
            if (Time.unscaledTime >= _windowEnd)
            {
                WorstRecentMs = _windowWorst;
                _windowWorst = 0f;
                _windowEnd = Time.unscaledTime + 3f;
            }
        }

        public static void ResetCounters()
        {
            SpikeCount = 0;
            WorstRecentMs = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetCounters();
    }
}
