using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Put this on a Screen Space - Overlay Canvas in the Bootstrap scene.
    /// Player name labels are created under it, so they render at full screen
    /// resolution on top of the low-res retro image and stay sharp.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class PlayerLabelLayer : MonoBehaviour
    {
        public static PlayerLabelLayer Instance { get; private set; }
        public RectTransform Container { get; private set; }

        void Awake()
        {
            Instance = this;
            Container = (RectTransform)transform;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}