using System;
using TidyUp.Locations;
using TidyUp.Story;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Minimal service locator (CLAUDE.md section 8) joining the persistent player side (City scene: player,
    /// HUD, feedback, memories, loader) and the additively loaded location scenes, which can't hold
    /// cross-scene references. Location-side components resolve player services here at use time; the
    /// player side follows the active location through ActiveLocationChanged.
    /// Standalone test scenes register the same way, so both setups share one code path.
    /// </summary>
    public static class GameServices
    {
        public static FeedbackService Feedback { get; private set; }
        public static PlayerInputReader Input { get; private set; }
        public static PlayerCarry Carry { get; private set; }
        public static Interactor Interactor { get; private set; }
        public static FirstPersonController PlayerController { get; private set; }
        public static Transform Player => PlayerController != null ? PlayerController.transform : null;
        public static MemoryService Memories { get; private set; }

        /// <summary>The street's scene loader; null in standalone test scenes.</summary>
        public static LocationLoader Loader { get; private set; }

        /// <summary>The location the player is in, or null on the street.</summary>
        public static LocationRuntime ActiveLocation { get; private set; }

        public static event Action<LocationRuntime> ActiveLocationChanged;

        /// <summary>Short player-facing messages from systems that aren't the Interactor (doors, loader).</summary>
        public static event Action<string> Announcement;

        public static void RegisterPlayer(FeedbackService feedback, PlayerInputReader input, PlayerCarry carry,
            Interactor interactor, FirstPersonController controller, MemoryService memories)
        {
            Feedback = feedback;
            Input = input;
            Carry = carry;
            Interactor = interactor;
            PlayerController = controller;
            Memories = memories;
        }

        public static void RegisterLoader(LocationLoader loader) => Loader = loader;

        public static void SetActiveLocation(LocationRuntime location)
        {
            if (ActiveLocation == location) return;
            ActiveLocation = location;
            ActiveLocationChanged?.Invoke(location);
        }

        public static void ClearActiveLocation(LocationRuntime location)
        {
            if (ActiveLocation == location) SetActiveLocation(null);
        }

        public static void Announce(string message)
        {
            if (!string.IsNullOrEmpty(message)) Announcement?.Invoke(message);
        }

        // Statics survive between Play sessions when domain reload is off: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Feedback = null;
            Input = null;
            Carry = null;
            Interactor = null;
            PlayerController = null;
            Memories = null;
            Loader = null;
            ActiveLocation = null;
            ActiveLocationChanged = null;
            Announcement = null;
        }
    }
}
