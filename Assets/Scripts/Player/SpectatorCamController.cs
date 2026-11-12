using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace TavernParty.Players
{
    public class SpectatorCamController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera spectatorCamera;
        [SerializeField] private MonoBehaviour firstPersonCameraScript;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 3, -4);
        [SerializeField] private float smoothSpeed = 10f;

        private List<Transform> aliveTargets = new List<Transform>();
        private int currentTargetIndex = 0;
        private bool isSpectating = false;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            if (spectatorCamera == null)
                spectatorCamera = GetComponentInChildren<Camera>();
        }

        public void EnableSpectatorMode()
        {
            if (!IsOwner) return;

            isSpectating = true;

            if (firstPersonCameraScript != null)
                firstPersonCameraScript.enabled = false;

            FindAliveTargets();
            CycleTarget(0);
        }

        public void DisableSpectatorMode()
        {
            if (!IsOwner) return;

            isSpectating = false;

            if (firstPersonCameraScript != null)
                firstPersonCameraScript.enabled = true;
        }

        private void Update()
        {
            if (!IsOwner || !isSpectating) return;

            if (Input.GetMouseButtonDown(0)) CycleTarget(1);
            else if (Input.GetMouseButtonDown(1)) CycleTarget(-1);
        }

        private void LateUpdate()
        {
            if (!IsOwner || !isSpectating || spectatorCamera == null) return;
            FindAliveTargets();
            if (aliveTargets.Count == 0) return;

            if (currentTargetIndex >= aliveTargets.Count) currentTargetIndex = 0;
            Transform target = aliveTargets[currentTargetIndex];

            if (target != null)
            {
                Vector3 desiredPosition = target.position + cameraOffset;
                spectatorCamera.transform.position = Vector3.Lerp(spectatorCamera.transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
                spectatorCamera.transform.LookAt(target.position + Vector3.up * 1.5f);
            }
        }

        private void FindAliveTargets()
        {
            aliveTargets.Clear();
            foreach (PlayerAvatar p in PlayerAvatar.All)
            {
                if (p == null) continue;

                // STRICT FILTER:
                // 1. Must NOT be your own player instance (OwnerId != OwnerId check)
                // 2. Must be marked alive on the network (p.IsAlive.Value)
                // 3. The actual game object / character controller must be active in the scene
                if (p.OwnerId != OwnerId && p.IsAlive.Value && p.gameObject.activeInHierarchy)
                {
                    aliveTargets.Add(p.transform);
                }
            }

            // Ensure our target index stays within valid bounds after filtering
            if (currentTargetIndex >= aliveTargets.Count)
            {
                currentTargetIndex = 0;
            }
        }

        private void CycleTarget(int direction)
        {
            FindAliveTargets();
            if (aliveTargets.Count == 0) return;

            currentTargetIndex += direction;
            if (currentTargetIndex >= aliveTargets.Count) currentTargetIndex = 0;
            if (currentTargetIndex < 0) currentTargetIndex = aliveTargets.Count - 1;
        }
    }
}