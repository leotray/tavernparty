using FishNet.Object;
using UnityEngine;
using TavernParty.Core;

namespace TavernParty.Players
{
    [RequireComponent(typeof(Rigidbody))]
    public class FirstPersonRigidbodyController : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float jumpForce = 5f;
        [SerializeField] LayerMask groundLayer;

        [Header("Camera Settings")]
        [SerializeField] Transform fpsCameraTransform;
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] float maxLookAngle = 80f;

        Rigidbody _rb;
        float _cameraPitch = 0f;
        bool _isGrounded;
        Vector3 _spawnPosition;
        Quaternion _spawnRotation;
        bool _spawnRecorded = false;

        public override void OnStartClient()
        {
            base.OnStartClient();
            _rb = GetComponent<Rigidbody>();

            if (!base.IsOwner)
            {
                enabled = false;
                if (fpsCameraTransform != null)
                    fpsCameraTransform.gameObject.SetActive(false);
                return;
            }

            // Disable camera until we confirm spawn position
            if (fpsCameraTransform != null)
                fpsCameraTransform.gameObject.SetActive(false);

            // Unlock cursor in lobby
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        bool IsPlaying()
        {
            return GameManager.Instance != null &&
                   (int)GameManager.Instance.Phase.Value == (int)GamePhase.Playing;
        }

        void Update()
        {
            if (!base.IsOwner) return;

            // Record spawn position one frame after spawn
            // so the server has had time to place us at the spawn point
            if (!_spawnRecorded)
            {
                _spawnPosition = transform.position;
                _spawnRotation = transform.rotation;
                _spawnRecorded = true;

                // Freeze rigidbody at spawn
                if (_rb != null)
                {
                    _rb.isKinematic = true;
                }

                // Activate camera now that we know where we are
                if (fpsCameraTransform != null)
                    fpsCameraTransform.gameObject.SetActive(true);

                return;
            }

            bool playing = IsPlaying();

            if (!playing)
            {
                // Locked at spawn in lobby
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (_rb != null && !_rb.isKinematic)
                    _rb.isKinematic = true;

                transform.position = _spawnPosition;
                transform.rotation = _spawnRotation;
                return;
            }

            // Playing phase — enable physics and lock cursor
            if (_rb != null && _rb.isKinematic)
            {
                _rb.isKinematic = false;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            HandleMouseLook();
        }

        void FixedUpdate()
        {
            if (!base.IsOwner) return;
            if (!_spawnRecorded) return;
            if (!IsPlaying()) return;
            HandleMovement();
        }

        void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            _cameraPitch -= mouseY;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -maxLookAngle, maxLookAngle);

            if (fpsCameraTransform != null)
                fpsCameraTransform.localEulerAngles = Vector3.right * _cameraPitch;
        }

        void HandleMovement()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");

            Vector3 moveDir = (transform.right * moveX + transform.forward * moveZ).normalized;
            Vector3 targetVelocity = moveDir * moveSpeed;

            Vector3 velocity = _rb.linearVelocity;
            velocity.x = targetVelocity.x;
            velocity.z = targetVelocity.z;

            _isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f, groundLayer);

            if (_isGrounded && Input.GetButton("Jump"))
                velocity.y = jumpForce;

            _rb.linearVelocity = velocity;
        }
    }
}