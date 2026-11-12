using FishNet.Object;
using UnityEngine;
using TavernParty.Core;

namespace TavernParty.Players
{
    public class TrollHostController : NetworkBehaviour
    {
        [Header("Top-down Camera")]
        [SerializeField] Camera trollCamera;
        [SerializeField] float camHeight = 15f;
        [SerializeField] float camMoveSpeed = 8f;

        [Header("Troll Assign")]
        [SerializeField] float trollCooldown = 8f;
        [SerializeField] LayerMask playerLayer;

        float _cooldownTimer = 0f;
        bool _isTrollHost = false;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (trollCamera != null) trollCamera.gameObject.SetActive(false);
            if (!IsOwner) { enabled = false; return; }
        }

        void Update()
        {
            if (!IsOwner) return;
            if (GameManager.Instance == null) return;

            bool shouldBeTroll = (int)GameManager.Instance.Phase.Value == (int)GamePhase.Playing
                                 && GameManager.Instance.TrollHostId.Value == OwnerId;

            if (shouldBeTroll != _isTrollHost)
            {
                _isTrollHost = shouldBeTroll;
                OnTrollStateChanged(_isTrollHost);
            }

            if (!_isTrollHost) return;

            HandleTopDownMovement();

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (Input.GetMouseButtonDown(0) && _cooldownTimer <= 0f)
                TryTrollAssign();
        }

        void OnTrollStateChanged(bool isTroll)
        {
            if (trollCamera != null) trollCamera.gameObject.SetActive(isTroll);

            FirstPersonRigidbodyController fps = GetComponent<FirstPersonRigidbodyController>();
            if (fps != null) fps.enabled = !isTroll;

            if (isTroll)
            {
                if (trollCamera != null)
                {
                    trollCamera.transform.position = new Vector3(0f, camHeight, 0f);
                    trollCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _cooldownTimer = 0f;
            }
        }

        void HandleTopDownMovement()
        {
            if (trollCamera == null) return;
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            trollCamera.transform.position += new Vector3(h, 0f, v) * camMoveSpeed * Time.deltaTime;
        }

        void TryTrollAssign()
        {
            if (trollCamera == null) return;
            Ray ray = trollCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, playerLayer))
            {
                PlayerAvatar target = hit.collider.GetComponentInParent<PlayerAvatar>();
                if (target != null && target.OwnerId != OwnerId)
                {
                    GameManager.Instance.RequestTrollAssignPotato(target.OwnerId);
                    _cooldownTimer = trollCooldown;
                    Debug.Log("[TrollHost] Force-assigned potato to: " + target.PlayerName.Value);
                }
            }
        }

        void OnGUI()
        {
            if (!IsOwner || !_isTrollHost) return;
            float cd = Mathf.Max(0f, _cooldownTimer);
            string status = cd > 0f ? "Cooldown: " + cd.ToString("F1") + "s" : "Click a player to assign potato";
            GUI.Box(new Rect(Screen.width / 2f - 160f, 10f, 320f, 36f), "");
            GUI.Label(new Rect(Screen.width / 2f - 150f, 18f, 300f, 24f), "YOU ARE THE TROLL HOST  |  " + status);
        }
    }
}