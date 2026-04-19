using UnityEngine;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// FPS-style character controller for the walker demo: WASD relative to
    /// camera yaw, mouse-look for yaw (on the character) and pitch (on a
    /// child CameraTarget transform), gravity + jump via CharacterController.
    ///
    /// Input keys are authored relative to the camera's facing, not the
    /// world — standard FPS convention. Escape releases the cursor lock so
    /// the editor stays usable; click back into the game window to re-lock.
    [RequireComponent(typeof(CharacterController))]
    public sealed class WalkerCharacter : MonoBehaviour
    {
        public float MoveSpeed = 4f;
        public float MouseSensitivity = 0.12f;
        public float Gravity = 20f;
        public float JumpSpeed = 5f;

        /// Child transform that holds camera pitch. Yaw is applied to the
        /// character root; pitch to this pivot so body roll stays level.
        public Transform CameraTarget;

        private CharacterController _cc;
        private float _pitch;
        private float _yaw;
        private float _vy;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _yaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw   += delta.x * MouseSensitivity;
                _pitch -= delta.y * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (CameraTarget != null)
                    CameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += transform.forward;
            if (kb.sKey.isPressed) move -= transform.forward;
            if (kb.dKey.isPressed) move += transform.right;
            if (kb.aKey.isPressed) move -= transform.right;
            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (_cc.isGrounded)
            {
                if (_vy < 0f) _vy = -1f;
                if (kb.spaceKey.wasPressedThisFrame) _vy = JumpSpeed;
            }
            _vy -= Gravity * Time.deltaTime;

            Vector3 velocity = move * MoveSpeed + Vector3.up * _vy;
            _cc.Move(velocity * Time.deltaTime);
        }
    }
}
