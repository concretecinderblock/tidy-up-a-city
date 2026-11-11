using TidyUp.Story;
using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>Registers the player-side services with GameServices before anything else wakes up.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerServices : MonoBehaviour
    {
        [SerializeField] private FeedbackService _feedback;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerCarry _carry;
        [SerializeField] private Interactor _interactor;
        [SerializeField] private FirstPersonController _controller;
        [SerializeField] private MemoryService _memories;

        private void Awake()
        {
            GameServices.RegisterPlayer(_feedback, _input, _carry, _interactor, _controller, _memories);
        }

        internal void EditorConfigure(FeedbackService feedback, PlayerInputReader input, PlayerCarry carry,
            Interactor interactor, FirstPersonController controller, MemoryService memories)
        {
            _feedback = feedback;
            _input = input;
            _carry = carry;
            _interactor = interactor;
            _controller = controller;
            _memories = memories;
        }
    }
}
