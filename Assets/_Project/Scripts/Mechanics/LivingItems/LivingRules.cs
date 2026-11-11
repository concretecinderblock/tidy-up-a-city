using UnityEngine;

namespace TidyUp.Mechanics.LivingItems
{
    /// <summary>Pure maths for living items (EditMode-testable).</summary>
    public static class LivingRules
    {
        /// <summary>
        /// Horizontal direction away from the player, turned by angleOffset degrees (for a bit of
        /// unpredictability). Falls back to +Z when the player stands exactly on the item.
        /// </summary>
        public static Vector3 FleeDirection(Vector3 item, Vector3 player, float angleOffset)
        {
            Vector3 away = item - player;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.forward;
            return Quaternion.Euler(0f, angleOffset, 0f) * away.normalized;
        }

        /// <summary>Clamps a point's X/Z into an area (shrunk by margin), keeping Y.</summary>
        public static Vector3 ClampToArea(Vector3 point, Bounds area, float margin)
        {
            Vector3 min = area.min;
            Vector3 max = area.max;
            point.x = Mathf.Clamp(point.x, Mathf.Min(min.x + margin, area.center.x), Mathf.Max(max.x - margin, area.center.x));
            point.z = Mathf.Clamp(point.z, Mathf.Min(min.z + margin, area.center.z), Mathf.Max(max.z - margin, area.center.z));
            return point;
        }

        /// <summary>Point on a hop arc: linear from start to end plus a parabola of the given height.</summary>
        public static Vector3 HopArc(Vector3 start, Vector3 end, float height, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(start, end, t);
            p.y += 4f * height * t * (1f - t);
            return p;
        }

        /// <summary>Slow looping drift offset for floating items (a lazy figure-eight with a gentle bob).</summary>
        public static Vector3 FloatOffset(float time, float radius, float bob, float phase)
        {
            float t = time + phase;
            return new Vector3(Mathf.Sin(t * 0.35f) * radius, Mathf.Sin(t * 1.3f) * bob, Mathf.Sin(t * 0.7f) * radius * 0.6f);
        }

        /// <summary>Shy items: 0 = hidden (far), 1 = fully shown (close), smooth in between.</summary>
        public static float ShyVisibility(float distance, float revealDistance, float fadeWidth)
        {
            if (fadeWidth <= 0f) return distance <= revealDistance ? 1f : 0f;
            return Mathf.Clamp01((revealDistance + fadeWidth - distance) / fadeWidth);
        }
    }
}
