using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using Steamworks;
using TMPro;
using UnityEngine;

namespace TavernParty.Players
{
    public class PlayerAvatar : NetworkBehaviour
    {
        public static PlayerAvatar Local { get; private set; }
        public readonly SyncVar<int> Score = new SyncVar<int>(0);
        public readonly SyncVar<bool> IsAlive = new SyncVar<bool>(true);

        public static readonly List<PlayerAvatar> All = new List<PlayerAvatar>();

        public readonly SyncVar<string> PlayerName = new SyncVar<string>("");
        public readonly SyncVar<int> SkinIndex = new SyncVar<int>(0);
        public readonly SyncVar<bool> IsReady = new SyncVar<bool>(false);

        [Header("Name label")]
        [SerializeField] float labelHeight = 1.6f;
        [SerializeField] float labelFontSize = 28f;

        [Header("Skins")]
        [SerializeField] GameObject[] skinPrefabs;
        [SerializeField] float feetOffsetFromPivot = 1f;

        [Header("Elimination / Spectator")]
        [SerializeField] GameObject ghostVisuals;

        [Header("Seated pose")]
        [SerializeField] RuntimeAnimatorController seatedController;
        [SerializeField] Vector3 hipsOffsetFromSeat = new Vector3(0f, 0.1f, 0f);

        TextMeshProUGUI _label;
        string _currentName = "";
        GameObject _skinInstance;
        MeshRenderer _capsuleRenderer;
        CharacterController _characterController;

        public int SkinCount => skinPrefabs == null ? 0 : skinPrefabs.Length;

        public string GetSkinName(int index)
        {
            if (index < 0 || index >= SkinCount || skinPrefabs[index] == null) return "?";
            return skinPrefabs[index].name;
        }

        void Awake()
        {
            _capsuleRenderer = GetComponent<MeshRenderer>();
            _characterController = GetComponent<CharacterController>();
            PlayerName.OnChange += OnNameChanged;
            SkinIndex.OnChange += OnSkinChanged;
            IsAlive.OnChange += OnAliveChanged;
        }

        void OnDestroy()
        {
            PlayerName.OnChange -= OnNameChanged;
            SkinIndex.OnChange -= OnSkinChanged;
            IsAlive.OnChange -= OnAliveChanged;
            if (_label != null) Destroy(_label.gameObject);
        }

        public void RequestSetAlive(bool alive)
        {
            if (base.IsServerStarted)
                IsAlive.Value = alive;
        }

        void OnAliveChanged(bool prev, bool next, bool asServer)
        {
            ApplyAliveState(next);
        }

        void ApplyAliveState(bool alive)
        {
            if (_skinInstance != null) _skinInstance.SetActive(alive);
            if (_capsuleRenderer != null) _capsuleRenderer.enabled = alive && (_skinInstance == null);
            if (_characterController != null) _characterController.enabled = alive;

            if (ghostVisuals != null) ghostVisuals.SetActive(!alive);

            if (IsOwner && !alive)
            {
                // Directly call the component on this player instance!
                GetComponent<SpectatorCamController>()?.EnableSpectatorMode();
            }
        }

        // --- NETWORK TELEPORTATION ---

        [ObserversRpc]
        public void RpcTeleport(Vector3 position, Quaternion rotation)
        {
            if (_characterController != null) _characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (_characterController != null) _characterController.enabled = true;
        }

        public override void OnStartServer()
        {
            if (LobbySpawnPoints.Instance != null)
            {
                LobbySpawnPoints.Instance.Claim(ObjectId, out Vector3 pos, out Quaternion rot);
                transform.SetPositionAndRotation(pos, rot);
            }
            else
            {
                float x = (OwnerId % 8 - 3.5f) * 1.5f;
                transform.position = new Vector3(x, 1f, 0f);
            }

            if (SkinCount > 0)
                SkinIndex.Value = Mathf.Abs(OwnerId) % SkinCount;
        }

        public override void OnStopServer()
        {
            if (LobbySpawnPoints.Instance != null)
                LobbySpawnPoints.Instance.Release(ObjectId);
        }

        public override void OnStartClient()
        {
            ApplyName(PlayerName.Value);
            ApplySkin(SkinIndex.Value);
            ApplyAliveState(IsAlive.Value);

            if (!All.Contains(this)) All.Add(this);

            if (IsOwner)
            {
                Local = this;
                ServerSetName(GetLocalName());
            }
        }

        public override void OnStopClient()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        public void RequestReady(bool ready)
        {
            if (IsOwner) ServerSetReady(ready);
        }

        [ServerRpc]
        void ServerSetReady(bool ready)
        {
            LobbyController lobby = LobbyController.Instance;
            if (lobby != null && lobby.Phase.Value != GamePhase.Lobby) return;
            IsReady.Value = ready;
        }

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
            if (SkinCount == 0) return;

            index = Mathf.Clamp(index, 0, SkinCount - 1);
            GameObject prefab = skinPrefabs[index];
            if (prefab == null) return;

            if (_skinInstance != null) Destroy(_skinInstance);

            _skinInstance = Instantiate(prefab, transform);
            _skinInstance.transform.localPosition = Vector3.zero;

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
                    animator.Update(0f);
                }
            }

            PlaceSkin(_skinInstance, animator);

            if (_capsuleRenderer != null) _capsuleRenderer.enabled = false;
            ApplyAliveState(IsAlive.Value);
        }

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
            EnsureLabel();
            if (_label == null) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                _label.enabled = false;
                return;
            }

            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * labelHeight);
            bool visible = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;

            _label.enabled = visible;
            if (visible)
                _label.rectTransform.position = new Vector3(vp.x * Screen.width, vp.y * Screen.height, 0f);
        }
    }
}