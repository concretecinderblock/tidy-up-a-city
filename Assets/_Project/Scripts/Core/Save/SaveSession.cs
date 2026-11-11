using UnityEngine;

namespace TidyUp.Core.Save
{
    /// <summary>
    /// What the next City load should do, and which slot the current game belongs to. Survives scene loads
    /// (static); reset when Play mode or the game starts.
    /// </summary>
    public static class SaveSession
    {
        /// <summary>Slot the current game saves into (1..3), or 0 when there is none (e.g. City played directly).</summary>
        public static int CurrentSlot { get; set; }

        /// <summary>Save to apply when the City scene starts; null = keep whatever is there.</summary>
        public static SaveData PendingLoad { get; private set; }

        /// <summary>True when the City should start a brand-new game.</summary>
        public static bool PendingNewGame { get; private set; }

        public static float PlayTimeSeconds { get; set; }

        public static void RequestNewGame(int slot)
        {
            CurrentSlot = slot;
            PendingLoad = null;
            PendingNewGame = true;
            PlayTimeSeconds = 0f;
        }

        public static void RequestLoad(int slot, SaveData data)
        {
            CurrentSlot = slot;
            PendingLoad = data;
            PendingNewGame = false;
            PlayTimeSeconds = data != null ? data.playTimeSeconds : 0f;
        }

        public static void ClearPending()
        {
            PendingLoad = null;
            PendingNewGame = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentSlot = 0;
            PendingLoad = null;
            PendingNewGame = false;
            PlayTimeSeconds = 0f;
        }
    }
}
