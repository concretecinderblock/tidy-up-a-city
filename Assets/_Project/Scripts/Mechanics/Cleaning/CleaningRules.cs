using UnityEngine;

namespace TidyUp.Mechanics.Cleaning
{
    /// <summary>The two cleaning minigames (family K). Each takes a few seconds, never long.</summary>
    public enum CleanGame
    {
        /// <summary>Fluffy dust bunnies pop out over a fixture; look at each and poof it.</summary>
        DustBunnies,
        /// <summary>A timing bar: tap when the marker is in the sparkly zone.</summary>
        Wipe,
    }

    /// <summary>Pure cleaning rules, EditMode-testable.</summary>
    public static class CleaningRules
    {
        public static bool IsClean(float dirt) => dirt <= 0f;

        /// <summary>Dust bunnies for a fixture: a few more on big fixtures, always quick (4–6).</summary>
        public static int BunnyCount(int slotCount) => Mathf.Clamp(4 + slotCount / 150, 4, 6);

        /// <summary>How many flags are set (cleaned count for the checklist).</summary>
        public static int CountTrue(bool[] flags)
        {
            if (flags == null) return 0;
            int n = 0;
            for (int i = 0; i < flags.Length; i++) if (flags[i]) n++;
            return n;
        }
    }

    /// <summary>
    /// The wipe minigame: a marker slides back and forth along a bar; pressing while it's inside the zone is a
    /// hit. Each hit moves the zone and speeds the marker up a little. Misses cost nothing. Plain C#, testable.
    /// </summary>
    public sealed class TimingBar
    {
        public const float ZoneWidth = 0.22f;
        public const float StartPeriod = 1.2f;   // seconds for one sweep end to end
        public const float SpeedUpPerHit = 0.85f;

        private System.Random _random;
        private float _position;   // 0..1
        private int _direction = 1;
        private float _period = StartPeriod;

        public float Marker => _position;
        public float ZoneStart { get; private set; }
        public float ZoneEnd => ZoneStart + ZoneWidth;
        public int Hits { get; private set; }
        public int Needed { get; private set; }
        public bool Done => Hits >= Needed;

        public void Start(int needed, System.Random random)
        {
            _random = random ?? new System.Random();
            Needed = Mathf.Max(1, needed);
            Hits = 0;
            _position = 0f;
            _direction = 1;
            _period = StartPeriod;
            PlaceZone();
        }

        /// <summary>Slides the marker, bouncing at both ends.</summary>
        public void Advance(float deltaTime)
        {
            if (Done) return;
            _position += _direction * deltaTime / _period;
            while (_position > 1f || _position < 0f)
            {
                if (_position > 1f) { _position = 2f - _position; _direction = -1; }
                if (_position < 0f) { _position = -_position; _direction = 1; }
            }
        }

        public bool InZone => _position >= ZoneStart && _position <= ZoneEnd;

        /// <summary>The player tapped. Returns true on a hit.</summary>
        public bool Press()
        {
            if (Done || !InZone) return false;
            Hits++;
            _period *= SpeedUpPerHit;
            if (!Done) PlaceZone();
            return true;
        }

        /// <summary>New zone, well away from where the marker is now (no free double hits).</summary>
        private void PlaceZone()
        {
            for (int i = 0; i < 8; i++)
            {
                float start = (float)_random.NextDouble() * (1f - ZoneWidth);
                float centre = start + ZoneWidth * 0.5f;
                if (Mathf.Abs(centre - _position) > 0.3f || i == 7)
                {
                    ZoneStart = start;
                    return;
                }
            }
        }
    }
}
