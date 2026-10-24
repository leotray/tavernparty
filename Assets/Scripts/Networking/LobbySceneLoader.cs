using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using UnityEngine;

namespace TavernParty.Networking
{
    /// <summary>
    /// When the FishNet server starts, loads the Lobby scene as a global scene
    /// (additively, so Bootstrap stays loaded). Clients that connect afterwards
    /// load it automatically. Put this on the NetworkManager object.
    /// </summary>
    public class LobbySceneLoader : MonoBehaviour
    {
        [SerializeField] string lobbySceneName = "Lobby";

        void Awake()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        void OnDestroy()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            Debug.Log("[LobbySceneLoader] Server state: " + args.ConnectionState);
            if (args.ConnectionState != LocalConnectionState.Started) return;

            Debug.Log("[LobbySceneLoader] Loading scene: " + lobbySceneName);
            var sceneData = new SceneLoadData(lobbySceneName);
            InstanceFinder.SceneManager.LoadGlobalScenes(sceneData);
        }
    }
}