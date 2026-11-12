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

            if (fpsCameraTransform != null)
                fpsCameraTransform.gameObject.SetActive(false);

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

            if (!_spawnRecorded)
            {
                _spawnPosition = transform.position;
                _spawnRotation = transform.rotation;
                _spawnRecorded = true;

                if (_rb != null)
                    _rb.isKinematic = true;

                if (fpsCameraTransform != null)
                    fpsCameraTransform.gameObject.SetActive(true);

                return;
            }

            bool playing = IsPlaying();

            if (!playing)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (_rb != null && !_rb.isKinematic)
                    _rb.isKinematic = true;

                transform.position = _spawnPosition;
                transform.rotation = _spawnRotation;
                return;
            }

            if (_rb != null && _rb.isKinematic)
                _rb.isKinematic = false;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Application.isFocused)
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
            if (!Application.isFocused)
            {
                Vector3 v = _rb.linearVelocity;
                v.x = 0f;
                v.z = 0f;
                _rb.linearVelocity = v;
                return;
            }

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
        void OnDisable()
        {
            // Stop any residual movement when script/component is disabled
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }
        void OnApplicationFocus(bool hasFocus)
        {
            if (!base.IsOwner) return;

            if (!hasFocus)
            {
                // Instantly kill horizontal momentum when tabbing out
                if (_rb != null)
                {
                    Vector3 v = _rb.linearVelocity;
                    v.x = 0f;
                    v.z = 0f;
                    _rb.linearVelocity = v;
                }
            }
        }
    }

}