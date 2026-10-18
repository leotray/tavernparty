using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace TavernParty.Networking
{
    /// <summary>
    /// Spawns the always-present networked managers (LobbyController, GameFlowController) from
    /// prefabs when the server starts. Put this on the NetworkManager object.
    /// Prefab-spawned objects are sent to every client that joins, with their current state,
    /// so they never depend on scene-object IDs matching between machines.
    /// FishNet despawns them again when the server stops, so every new host starts clean.
    /// </summary>
    public class ManagerSpawner : MonoBehaviour
    {
        [Tooltip("Prefabs (with a NetworkObject) to spawn when the server starts.")]
        [SerializeField] NetworkObject[] managerPrefabs;

        void Start()
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
            if (args.ConnectionState != LocalConnectionState.Started) return;
            if (managerPrefabs == null) return;

            foreach (NetworkObject prefab in managerPrefabs)
            {
                if (prefab == null) continue;

                NetworkObject instance = Instantiate(prefab);
                InstanceFinder.ServerManager.Spawn(instance);
            }
        }
    }
}