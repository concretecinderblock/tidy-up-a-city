using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>
    /// Reads TidyInput once per frame and exposes frame state to gameplay components.
    /// Also owns cursor lock: Esc releases the cursor, the next click recaptures it (and is swallowed).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private bool _lockCursorOnStart = true;

        private TidyInput _input;

        public Vector2 Move { get; private set; }
        /// <summary>Mouse delta in pixels this frame (no deltaTime scaling needed).</summary>
        public Vector2 LookDelta { get; private set; }
        /// <summary>Gamepad stick, -1..1 (scale by deltaTime).</summary>
        public Vector2 LookStick { get; private set; }
        public bool Sprint { get; private set; }
        public bool InteractPressed { get; private set; }
        /// <summary>Interact is held down.</summary>
        public bool InteractHeld { get; private set; }
        public bool DropPressed { get; private set; }
        public bool TidySensePressed { get; private set; }
        /// <summary>-1, 0 or +1 this frame.</summary>
        public int CycleDirection { get; private set; }
        public bool HasFocus => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>
        /// While true (e.g. a memory is playing) movement, look and gameplay buttons read as idle;
        /// only ConfirmPressed / CancelPressed still report, for advancing or skipping UI.
        /// </summary>
        public bool GameplayBlocked { get; set; }
        /// <summary>True while a menu (pause menu) is open: no gameplay input, no cursor capture.</summary>
        public bool MenuOpen { get; set; }

        /// <summary>Esc / Start pressed during gameplay. The pause menu listens.</summary>
        public event System.Action PauseRequested;

        public void SetCursorLocked(bool locked) => LockCursor(locked);

        /// <summary>Interact button, reported even while gameplay is blocked.</summary>
        public bool ConfirmPressed { get; private set; }
        /// <summary>Drop button, reported even while gameplay is blocked (used as Skip).</summary>
        public bool CancelPressed { get; private set; }

        private void Awake()
        {
            _input = new TidyInput();
        }

        private void OnEnable()
        {
            _input.Enable();
            if (_lockCursorOnStart) LockCursor(true);
        }

        private void OnDisable()
        {
            _input.Disable();
            LockCursor(false);
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void Update()
        {
            ClearFrameState();

            // A menu owns input and the cursor while it's open (it handles Esc/Start itself).
            if (MenuOpen) return;

            if (_input.Pause.WasPressedThisFrame())
            {
                if (PauseRequested != null) PauseRequested.Invoke();
                else LockCursor(false); // no pause menu in this scene: just free the mouse
                return;
            }

            if (!HasFocus)
            {
                if (_input.Interact.WasPressedThisFrame()) LockCursor(true);
                return;
            }

            ConfirmPressed = _input.Interact.WasPressedThisFrame();
            CancelPressed = _input.Drop.WasPressedThisFrame();
            if (GameplayBlocked) return;

            Move = Vector2.ClampMagnitude(_input.Move.ReadValue<Vector2>(), 1f);
            LookDelta = _input.LookDelta.ReadValue<Vector2>();
            LookStick = _input.LookStick.ReadValue<Vector2>();
            InteractHeld = _input.Interact.IsPressed();
            Sprint = _input.Sprint.IsPressed();
            InteractPressed = _input.Interact.WasPressedThisFrame();
            DropPressed = _input.Drop.WasPressedThisFrame();
            TidySensePressed = _input.TidySense.WasPressedThisFrame();

            int cycle = 0;
            if (_input.CycleNext.WasPressedThisFrame()) cycle++;
            if (_input.CyclePrevious.WasPressedThisFrame()) cycle--;
            float scroll = _input.Scroll.ReadValue<float>();
            if (scroll > 0.01f) cycle--;
            else if (scroll < -0.01f) cycle++;
            CycleDirection = Mathf.Clamp(cycle, -1, 1);
        }

        private void ClearFrameState()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            LookStick = Vector2.zero;
            Sprint = false;
            InteractPressed = false;
            InteractHeld = false;
            DropPressed = false;
            TidySensePressed = false;
            CycleDirection = 0;
            ConfirmPressed = false;
            CancelPressed = false;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
