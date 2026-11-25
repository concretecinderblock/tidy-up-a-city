using UnityEngine;

namespace TidyUp.Core
{
    /// <summary>Simple grey-box first-person movement and look. Yaw on the body, pitch on the camera.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _cameraPivot;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 3f;
        [SerializeField] private float _sprintSpeed = 5f;
        [SerializeField] private float _acceleration = 20f;
        [SerializeField] private float _gravity = -20f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] private float _mouseSensitivity = 0.08f;
        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] private float _stickSensitivity = 160f;
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            // Walk through Muddle piles rather than bumping into them (thousands of items on the floor).
            if (TidyLayers.Items >= 0) Physics.IgnoreLayerCollision(gameObject.layer, TidyLayers.Items, true);
            if (_cameraPivot != null) _pitch = NormalizePitch(_cameraPivot.localEulerAngles.x);
        }

        private void Update()
        {
            if (_input == null) return;
            float dt = Time.deltaTime;
            UpdateLook(dt);
            UpdateMove(dt);
        }

        private void UpdateLook(float dt)
        {
            Vector2 look = _input.LookDelta * _mouseSensitivity + _input.LookStick * (_stickSensitivity * dt);
            transform.Rotate(0f, look.x, 0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void UpdateMove(float dt)
        {
            if (_riding) return; // a lift moves us
            Vector2 move = _input.Move;
            float speed = _input.Sprint ? _sprintSpeed : _walkSpeed;
            Vector3 target = (transform.right * move.x + transform.forward * move.y) * speed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, _acceleration * dt);

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += _gravity * dt;

            Vector3 velocity = _horizontalVelocity;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * dt);
            KeepInTheWorld();
        }


        private Vector3 _lastSafePosition;
        private bool _hasSafePosition;
        private float _nextSafeSample;
        private const float FallLimit = -25f;

        /// <summary>
        /// A safety net: remembers where the player last stood on solid ground, and if they ever drop out of the
        /// world (a gap in a floor), puts them back there instead of falling forever.
        /// </summary>
        private void KeepInTheWorld()
        {
            if (_controller.isGrounded && Time.time >= _nextSafeSample)
            {
                _lastSafePosition = transform.position;
                _hasSafePosition = true;
                _nextSafeSample = Time.time + 0.5f;
            }
            if (transform.position.y > FallLimit) return;
            Debug.LogWarning($"[Tidy Up] The player fell out of the world near {transform.position}; putting them back.", this);
            float yaw = transform.eulerAngles.y;
            Teleport(_hasSafePosition ? _lastSafePosition + Vector3.up * 0.2f : Vector3.up, yaw);
        }

        private bool _riding;

        /// <summary>Something (a lift) carries the player: walking and gravity stop, looking around still works.</summary>
        public void BeginRide()
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            _riding = true;
            _controller.enabled = false;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        public void EndRide()
        {
            _riding = false;
            _controller.enabled = true;
            _lastSafePosition = transform.position; // the new floor is solid ground
            _hasSafePosition = true;
        }
        /// <summary>Moves the player instantly (doors, scene changes), facing yaw degrees, looking level.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            if (_controller == null) _controller = GetComponent<CharacterController>();
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _pitch = 0f;
            _lastSafePosition = position; // a door or load never counts as falling
            _hasSafePosition = true;
            if (_cameraPivot != null) _cameraPivot.localRotation = Quaternion.identity;
        }

        private static float NormalizePitch(float angle) => angle > 180f ? angle - 360f : angle;

        internal void EditorConfigure(PlayerInputReader input, Transform cameraPivot)
        {
            _input = input;
            _cameraPivot = cameraPivot;
        }
    }
}
