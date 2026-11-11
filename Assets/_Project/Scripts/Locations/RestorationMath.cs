using UnityEngine;

namespace TidyUp.Locations
{
    /// <summary>Pure helpers for restoration curves and thresholds (EditMode-testable).</summary>
    public static class RestorationMath
    {
        /// <summary>Smoothstep: slow at the ends, so the first and last placements still feel like change.</summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>True when a stage at threshold should be on for this restoration value.</summary>
        public static bool IsReached(float restoration, float threshold)
        {
            // Small epsilon so a 1.0 threshold triggers at exactly 100% despite float maths.
            return restoration >= threshold - 0.0001f;
        }

        /// <summary>Overshooting ease for "pop in" reveals (easeOutBack).</summary>
        public static float PopIn(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
