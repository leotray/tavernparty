using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using Steamworks;
using TMPro;
using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Lobby avatar: a capsule with a floating name label.
    /// The owning client sends its Steam name to the server, the server stores it
    /// in a SyncVar, and every client shows it above the capsule.
    /// Put this on the capsule prefab next to a NetworkObject component.
    /// </summary>
    public class PlayerAvatar : NetworkBehaviour
    {
        public readonly SyncVar<string> PlayerName = new SyncVar<string>("");

        TextMeshPro _label;
        Transform _labelTransform;

        void Awake()
        {
            CreateLabel();
            PlayerName.OnChange += OnNameChanged;
        }

        void OnDestroy()
        {
            PlayerName.OnChange -= OnNameChanged;
        }

        public override void OnStartServer()
        {
            // Spread players out along X so they don't overlap.
            float x = (OwnerId % 8 - 3.5f) * 1.5f;
            transform.position = new Vector3(x, 1f, 0f);
        }

        public override void OnStartClient()
        {
            // Late joiners get the current value from the SyncVar.
            ApplyName(PlayerName.Value);

            if (IsOwner)
                ServerSetName(GetLocalName());
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
            if (_label != null) _label.text = value;
        }

        void CreateLabel()
        {
            var go = new GameObject("NameLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            _label = go.AddComponent<TextMeshPro>();
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 4f;
            _label.text = "";
            _labelTransform = go.transform;
        }

        void LateUpdate()
        {
            // Billboard: label always faces the camera.
            Camera cam = Camera.main;
            if (cam != null && _labelTransform != null)
                _labelTransform.rotation = cam.transform.rotation;
        }
    }
}