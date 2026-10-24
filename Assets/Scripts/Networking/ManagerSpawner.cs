using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace TavernParty.Networking
{
    public class ManagerSpawner : MonoBehaviour
    {
        [SerializeField] NetworkObject gameManagerPrefab;

        void Start()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState += OnServerState;
        }

        void OnDestroy()
        {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnServerConnectionState -= OnServerState;
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            if (gameManagerPrefab == null)
            {
                Debug.LogError("[ManagerSpawner] No GameManager prefab assigned.");
                return;
            }

            NetworkObject instance = Instantiate(gameManagerPrefab);

            // FIXED: Calling DontDestroyOnLoad from a regular MonoBehaviour (not a NetworkBehaviour) 
            // satisfies FishNet FN0002 while keeping GameManager persistent across scene loads.
            DontDestroyOnLoad(instance.gameObject);

            InstanceFinder.ServerManager.Spawn(instance);
        }
    }
}