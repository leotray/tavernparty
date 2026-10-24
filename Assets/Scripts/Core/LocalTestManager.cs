using FishNet;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace TavernParty.Networking
{
    /// <summary>
    /// Local testing replacement for SteamLobbyManager.
    /// Disable SteamLobbyManager and enable this when testing on localhost.
    /// Shows a simple OnGUI panel to host or join.
    /// </summary>
    public class LocalTestManager : MonoBehaviour
    {
        public static LocalTestManager Instance { get; private set; }
        public bool InLobby { get; private set; }

        [SerializeField] Tugboat tugboat;

        string _status = "Not connected";
        string _ipInput = "127.0.0.1";

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState += OnClientState;
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState += OnServerState;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientState;
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState -= OnServerState;
        }

        void OnServerState(FishNet.Transporting.ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
            {
                _status = "Hosting on localhost";
                InLobby = true;
            }
            else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
            {
                _status = "Not connected";
                InLobby = false;
            }
        }

        void OnClientState(FishNet.Transporting.ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Started)
            {
                _status = "Connected to " + _ipInput;
                InLobby = true;
            }
            else if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped)
            {
                _status = "Disconnected";
                InLobby = false;
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 220), GUI.skin.box);
            GUILayout.Label("=== LOCAL TEST MODE ===");
            GUILayout.Label(_status);
            GUILayout.Space(4);

            if (!InLobby)
            {
                if (GUILayout.Button("Host"))
                {
                    InstanceFinder.ServerManager.StartConnection();
                    InstanceFinder.ClientManager.StartConnection();
                }

                GUILayout.Label("Join IP:");
                _ipInput = GUILayout.TextField(_ipInput);

                if (GUILayout.Button("Join"))
                    InstanceFinder.ClientManager.StartConnection(_ipInput);
            }
            else
            {
                if (GUILayout.Button("Disconnect"))
                {
                    InstanceFinder.ClientManager.StopConnection();
                    InstanceFinder.ServerManager.StopConnection(true);
                }
            }

            GUILayout.EndArea();
        }
    }
}