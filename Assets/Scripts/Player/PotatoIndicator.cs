using FishNet.Object;
using UnityEngine;
using TavernParty.Core;

namespace TavernParty.Players
{
    public class PotatoIndicator : NetworkBehaviour
    {
        [SerializeField] GameObject potatoVisual;

        void Awake()
        {
            if (potatoVisual != null)
                potatoVisual.SetActive(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (potatoVisual != null)
                potatoVisual.SetActive(false);
        }

        void Update()
        {
            if (potatoVisual == null) return;
            if (GameManager.Instance == null)
            {
                potatoVisual.SetActive(false);
                return;
            }

            bool isPlaying = (int)GameManager.Instance.Phase.Value == (int)GamePhase.Playing;
            int holderId = GameManager.Instance.PotatoHolderId.Value;
            bool iAmHolder = isPlaying && holderId != -1 && holderId == OwnerId;

            potatoVisual.SetActive(iAmHolder);
        }
    }
}