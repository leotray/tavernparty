using System.Collections.Generic;
using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Lives in the Lobby scene. Holds the spawn points and hands them out to players
    /// (server side). If no points are assigned, it uses its own child objects in order.
    /// Place each point on the FLOOR; the script lifts the player by pivotHeightOffset.
    /// </summary>
    public class LobbySpawnPoints : MonoBehaviour
    {
        public static LobbySpawnPoints Instance { get; private set; }

        [Tooltip("Leave empty to use this object's children, in hierarchy order.")]
        [SerializeField] Transform[] points;

        [Tooltip("Lifts the player so a capsule's center sits above the floor (capsule = 1).")]
        [SerializeField] float pivotHeightOffset = 1f;

        // claimId (the player's ObjectId) -> index of the point it holds
        readonly Dictionary<int, int> _claims = new Dictionary<int, int>();

        void Awake()
        {
            if (points == null || points.Length == 0)
            {
                points = new Transform[transform.childCount];
                for (int i = 0; i < points.Length; i++)
                    points[i] = transform.GetChild(i);
            }
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
            _claims.Clear();
        }

        /// <summary>Gives the first free point to this claim id. Server only.</summary>
        public void Claim(int claimId, out Vector3 position, out Quaternion rotation)
        {
            Release(claimId);

            if (points == null || points.Length == 0)
            {
                position = Vector3.up * pivotHeightOffset;
                rotation = Quaternion.identity;
                return;
            }

            for (int i = 0; i < points.Length; i++)
            {
                if (_claims.ContainsValue(i)) continue;
                if (points[i] == null) continue;

                _claims[claimId] = i;
                position = points[i].position + Vector3.up * pivotHeightOffset;
                rotation = points[i].rotation;
                return;
            }

            // More players than points: stack them beside the first point.
            Transform first = points[0];
            int overflow = _claims.Count - points.Length + 1;
            position = first.position + Vector3.up * pivotHeightOffset + first.right * (1.5f * overflow);
            rotation = first.rotation;
        }

        public void Release(int claimId)
        {
            _claims.Remove(claimId);
        }

        void OnDrawGizmos()
        {
            Transform[] list = points;
            if (list == null || list.Length == 0)
            {
                list = new Transform[transform.childCount];
                for (int i = 0; i < list.Length; i++) list[i] = transform.GetChild(i);
            }

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(list[i].position + Vector3.up * pivotHeightOffset, 0.4f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(list[i].position + Vector3.up * pivotHeightOffset, list[i].forward * 0.8f);
            }
        }
    }
}