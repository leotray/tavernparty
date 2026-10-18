using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using Steamworks;
using TMPro;
using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Lobby avatar: a networked root object (capsule) with a floating name label and a
    /// swappable visual skin.
    /// - The owning client sends its Steam name and chosen skin to the server.
    /// - The server stores them in SyncVars, so every client (including late joiners) sees them.
    /// - The name label is screen-space UI (see PlayerLabelLayer), so it stays sharp.
    /// - Skins are visual-only prefabs placed as a child; the capsule mesh is hidden once a skin shows.
    /// Put this on the capsule prefab next to a NetworkObject component.
    /// </summary>
    public class PlayerAvatar : NetworkBehaviour
    {
        /// <summary>The avatar owned by this machine's player (client side).</summary>
        public static PlayerAvatar Local { get; private set; }

        /// <summary>Every player avatar this machine currently knows about.</summary>
        public static readonly List<PlayerAvatar> All = new List<PlayerAvatar>();

        public readonly SyncVar<string> PlayerName = new SyncVar<string>("");
        public readonly SyncVar<int> SkinIndex = new SyncVar<int>(0);
        public readonly SyncVar<bool> IsReady = new SyncVar<bool>(false);
        public readonly SyncVar<int> Score = new SyncVar<int>(0);

        [Header("Name label")]
        [Tooltip("How far above the avatar's pivot the name appears (world units).")]
        [SerializeField] float labelHeight = 1.6f;
        [SerializeField] float labelFontSize = 28f;

        [Header("Skins")]
        [Tooltip("Visual-only skin prefabs (no NetworkObject). Order = selection order.")]
        [SerializeField] GameObject[] skinPrefabs;
        [Tooltip("Height of this object's pivot above the spawn point (capsule = 1). Match LobbySpawnPoints' pivot height.")]
        [SerializeField] float feetOffsetFromPivot = 1f;

        [Header("Seated pose")]
        [Tooltip("Optional. Forces ONE controller on every skin. Leave empty to use the controller set on each skin prefab's own Animator.")]
        [SerializeField] RuntimeAnimatorController seatedController;
        [Tooltip("Where the hips end up relative to the spawn point. Place spawn points on the seat surface.")]
        [SerializeField] Vector3 hipsOffsetFromSeat = new Vector3(0f, 0.1f, 0f);

        TextMeshProUGUI _label;
        string _currentName = "";
        bool _presenceVisible = true;
        GameObject _skinInstance;
        MeshRenderer _capsuleRenderer;

        public int SkinCount => skinPrefabs == null ? 0 : skinPrefabs.Length;

        public string GetSkinName(int index)
        {
            if (index < 0 || index >= SkinCount || skinPrefabs[index] == null) return "?";
            return skinPrefabs[index].name;
        }

        void Awake()
        {
            _capsuleRenderer = GetComponent<MeshRenderer>();
            PlayerName.OnChange += OnNameChanged;
            SkinIndex.OnChange += OnSkinChanged;
        }

        void OnDestroy()
        {
            PlayerName.OnChange -= OnNameChanged;
            SkinIndex.OnChange -= OnSkinChanged;
            if (_label != null) Destroy(_label.gameObject);
        }

        public override void OnStartServer()
        {
            if (LobbySpawnPoints.Instance != null)
            {
                // Take the first free spawn point placed in the Lobby scene.
                LobbySpawnPoints.Instance.Claim(ObjectId, out Vector3 pos, out Quaternion rot);
                transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                // Fallback if no spawn points exist: spread players out along X.
                float x = (OwnerId % 8 - 3.5f) * 1.5f;
                transform.position = new Vector3(x, 1f, 0f);
            }

            // Give each new player a different starting skin.
            if (SkinCount > 0)
                SkinIndex.Value = Mathf.Abs(OwnerId) % SkinCount;
        }

        public override void OnStopServer()
        {
            // Free the spawn point so the next player who joins can use it.
            if (LobbySpawnPoints.Instance != null)
                LobbySpawnPoints.Instance.Release(ObjectId);
        }

        public override void OnStartClient()
        {
            // Late joiners get the current values from the SyncVars.
            ApplyName(PlayerName.Value);
            ApplySkin(SkinIndex.Value);

            if (!All.Contains(this)) All.Add(this);
            Debug.Log("[PlayerAvatar] Started on this machine. Owner " + OwnerId + ", is mine: " + IsOwner);

            if (IsOwner)
            {
                Local = this;
                ServerSetName(GetLocalName());
            }
        }

        public override void OnStopClient()
        {
            All.Remove(this);
            Debug.Log("[PlayerAvatar] Stopped on this machine. Owner " + OwnerId);
            if (Local == this) Local = null;
        }

        // ---------- Ready state ----------

        /// <summary>Called by this machine's player to toggle ready.</summary>
        public void RequestReady(bool ready)
        {
            if (IsOwner) ServerSetReady(ready);
        }

        [ServerRpc]
        void ServerSetReady(bool ready)
        {
            // Ready state can only change while we are still in the lobby.
            LobbyController lobby = LobbyController.Instance;
            if (lobby != null && lobby.Phase.Value != GamePhase.Lobby) return;
            IsReady.Value = ready;
        }

        // ---------- Name ----------

        [ServerRpc]
        void ServerSetName(string requestedName)
        {
            requestedName = (requestedName ?? "").Trim();
            if (requestedName.Length == 0) requestedName = "Player " + OwnerId;
            if (requestedName.Length > 20) requestedName = requestedName.Substring(0, 20);
            PlayerName.Value = requestedName;
        }

        string GetLocalName()
        {
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null && lobby.SteamReady)
                return SteamFriends.GetPersonaName();
            return "Player " + OwnerId;
        }

        void OnNameChanged(string previous, string next, bool asServer)
        {
            ApplyName(next);
        }

        void ApplyName(string value)
        {
            _currentName = value ?? "";
            if (_label != null) _label.text = _currentName;
        }

        // ---------- Skin ----------

        /// <summary>Called by this machine's player to pick a skin.</summary>
        public void RequestSkin(int index)
        {
            if (IsOwner) ServerSetSkin(index);
        }

        [ServerRpc]
        void ServerSetSkin(int index)
        {
            if (SkinCount == 0) return;
            SkinIndex.Value = Mathf.Clamp(index, 0, SkinCount - 1);
        }

        void OnSkinChanged(int previous, int next, bool asServer)
        {
            ApplySkin(next);
        }

        void ApplySkin(int index)
        {
            if (SkinCount == 0) return; // no skins assigned: keep showing the capsule

            index = Mathf.Clamp(index, 0, SkinCount - 1);
            GameObject prefab = skinPrefabs[index];
            if (prefab == null) return;

            if (_skinInstance != null) Destroy(_skinInstance);

            // Keep the prefab's own rotation and scale (imported models often carry an axis fix
            // like -90 on X). Only the position is reset. Resetting the rotation made skins lie face-down.
            _skinInstance = Instantiate(prefab, transform);
            _skinInstance.transform.localPosition = Vector3.zero;

            // Each skin can carry its own controller (set on the skin prefab's Animator component).
            // The optional "Seated Controller" slot on this script overrides it for every skin.
            Animator animator = _skinInstance.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (seatedController != null)
                    animator.runtimeAnimatorController = seatedController;

                if (animator.runtimeAnimatorController != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    animator.Update(0f); // evaluate the first frame now so we can measure the pose
                }
            }
            else if (seatedController != null)
            {
                Debug.LogWarning("Skin '" + prefab.name + "' has no Animator, so it cannot play the seated animation.");
            }

            PlaceSkin(_skinInstance, animator);

            if (_capsuleRenderer != null) _capsuleRenderer.enabled = false;
        }

        /// <summary>
        /// Moves the skin so its lowest point touches the floor, whatever its pivot
        /// or height is (the pivot can be in the middle of the body).
        /// </summary>
        void AlignFeet(GameObject skin)
        {
            Renderer[] renderers = skin.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            float desiredBottom = transform.position.y - feetOffsetFromPivot;
            skin.transform.position += Vector3.up * (desiredBottom - bounds.min.y);
        }

        /// <summary>
        /// Seated skins: puts the hips on the seat (the spawn point). Standing skins: feet on the floor.
        /// </summary>
        void PlaceSkin(GameObject skin, Animator animator)
        {
            if (animator != null && animator.runtimeAnimatorController != null && animator.isHuman)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    Vector3 seat = transform.position - Vector3.up * feetOffsetFromPivot;
                    skin.transform.position += (seat + hipsOffsetFromSeat) - hips.position;
                    return;
                }
            }

            AlignFeet(skin);
        }

        // ---------- Lobby presence ----------

        /// <summary>
        /// The lobby avatar only exists visually while the lobby is open. During minigames it
        /// hides itself (mesh, collider and name) but stays alive to keep the player's score and settings.
        /// </summary>
        void UpdatePresence()
        {
            GameFlowController flow = GameFlowController.Instance;
            bool shouldShow = flow == null || !flow.InMinigame.Value;
            if (shouldShow != _presenceVisible) SetPresence(shouldShow);
        }

        void SetPresence(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;

            // The plain capsule mesh stays hidden whenever a skin is in use.
            if (visible && SkinCount > 0 && _capsuleRenderer != null)
                _capsuleRenderer.enabled = false;

            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                c.enabled = visible;

            _presenceVisible = visible;
        }

        // ---------- Name label ----------

        void EnsureLabel()
        {
            if (_label != null) return;

            PlayerLabelLayer layer = PlayerLabelLayer.Instance;
            if (layer == null) return;

            var go = new GameObject("NameLabel_" + gameObject.name, typeof(RectTransform));
            go.transform.SetParent(layer.Container, false);

            _label = go.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = labelFontSize;
            _label.raycastTarget = false;
            _label.text = _currentName;
            _label.rectTransform.sizeDelta = new Vector2(400f, 50f);
        }

        void LateUpdate()
        {
            UpdatePresence();

            EnsureLabel();
            if (_label == null) return;

            if (!_presenceVisible)
            {
                _label.enabled = false;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                _label.enabled = false;
                return;
            }

            // Project the point above the head onto the screen. On a Screen Space - Overlay
            // canvas, a RectTransform's position is in screen pixels.
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * labelHeight);
            bool visible = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

            _label.enabled = visible;
            if (visible)
                _label.rectTransform.position = new Vector3(vp.x * Screen.width, vp.y * Screen.height, 0f);
        }
    }
}