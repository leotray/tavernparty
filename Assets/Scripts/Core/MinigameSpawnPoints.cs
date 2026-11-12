using UnityEngine;

namespace TavernParty.Core
{
    public class MinigameSpawnPoints : MonoBehaviour
    {
        public static MinigameSpawnPoints Instance { get; private set; }

        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform trollSpawnPoint; // Dedicated spot for the Troll Host

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Vector3 GetSpawnPosition(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                float angle = index * 360f / Mathf.Max(1, 8);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * 3f;
                return transform.position + offset;
            }

            Transform pt = spawnPoints[index % spawnPoints.Length];
            return pt != null ? pt.position : transform.position;
        }

        public Vector3 GetTrollSpawnPosition()
        {
            if (trollSpawnPoint != null) return trollSpawnPoint.position;
            return transform.position + new Vector3(0f, 0f, -5f); // Fallback position
        }
    }
}