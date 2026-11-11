using TidyUp.Core;
using UnityEngine;

namespace TidyUp.Mechanics.LivingItems
{
    /// <summary>
    /// Shared context for a location's living items: where the player is, where items may roam,
    /// and the feedback service for their sounds. Assigned to each LivingItem by the location builder.
    /// </summary>
    public sealed class LivingItemDirector : MonoBehaviour
    {
        [SerializeField] private Transform _player;
        [Tooltip("Living items stay inside this box (usually the Muddle scatter area).")]
        [SerializeField] private BoxCollider _roamArea;
        [SerializeField] private FeedbackService _feedback;
        [SerializeField] private LayerMask _surfaceMask = ~(1 << 2); // everything except Ignore Raycast

        // In the City setup the player lives in another scene: fall back to the registered services.
        public Transform Player => _player != null ? _player : GameServices.Player;
        public FeedbackService Feedback => _feedback != null ? _feedback : GameServices.Feedback;
        public LayerMask SurfaceMask => _surfaceMask;
        public Bounds RoamBounds => _roamArea != null ? _roamArea.bounds : new Bounds(Vector3.zero, Vector3.one * 100f);

        internal void EditorConfigure(Transform player, BoxCollider roamArea, FeedbackService feedback)
        {
            _player = player;
            _roamArea = roamArea;
            _feedback = feedback;
        }
    }
}
