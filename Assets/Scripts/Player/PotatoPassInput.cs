using System;
using FishNet.Object;
using UnityEngine;
using TavernParty.Core;

namespace TavernParty.Players
{
    public class PotatoPassInput : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Drag your Player's First-Person Camera transform here in the Inspector!")]
        [SerializeField] Transform cameraTransform;

        [Header("Settings")]
        [SerializeField] float passRange = 10f;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            // Fallback if not assigned in Inspector
            if (cameraTransform == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) cameraTransform = cam.transform;
            }

            if (cameraTransform == null)
            {
                Debug.LogError($"[PotatoPassInput] No camera transform assigned on {gameObject.name}!");
            }
        }

        void Update()
        {
            if (!IsOwner) return;
            if (GameManager.Instance == null) return;
            if ((int)GameManager.Instance.Phase.Value != (int)GamePhase.Playing) return;
            if (GameManager.Instance.PotatoHolderId.Value != OwnerId) return;
            if (GameManager.Instance.TrollHostId.Value == OwnerId) return;

            if (Input.GetMouseButtonDown(0))
            {
                TryPass();
            }
        }

        void TryPass()
        {
            if (cameraTransform == null)
            {
                Debug.LogError("[PotatoPassInput] Cannot pass: cameraTransform is null!");
                return;
            }

            // Fire straight out from where the camera is looking
            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

            // Visible red line in Scene view for debugging
            Debug.DrawRay(ray.origin, ray.direction * passRange, Color.red, 2f);

            RaycastHit[] hits = Physics.RaycastAll(ray, passRange);

            if (hits.Length == 0)
            {
                Debug.Log("[PotatoPassInput] Ray hit nothing.");
                return;
            }

            // Sort hits by distance from closest to furthest
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                PlayerAvatar target = hit.collider.GetComponentInParent<PlayerAvatar>();
                if (target != null)
                {
                    // If we hit our own body's collider, ignore it and check what's behind us
                    if (target.OwnerId == OwnerId) continue;

                    Debug.Log($"[PotatoPassInput] SUCCESS! Passing potato to: {target.PlayerName.Value} (ID: {target.OwnerId})");
                    GameManager.Instance.RequestPassPotato(OwnerId, target.OwnerId);
                    return;
                }
            }

            Debug.Log("[PotatoPassInput] Ray hit environment objects (walls/floors), but no other player was in the line of sight.");
        }
    }
}