using UnityEngine;
using System.Collections.Generic;
using TavernParty.Players;

namespace TavernParty.Core
{
    public class HotPotatoMinigame : MinigameBase
    {
        float passTimer;
        float passInterval = 2.5f; // Potato bounces every 2.5 seconds
        int currentHolderId = -1;

        public override void OnBegin(MinigameContext context)
        {
            base.OnBegin(context);
            PickNewHolder();
            Debug.Log("[HotPotato] Round started! Potato is live.");
        }

        public override void OnTick(float deltaTime)
        {
            base.OnTick(deltaTime);

            passTimer += deltaTime;
            if (passTimer >= passInterval)
            {
                passTimer = 0f;
                PickNewHolder();
            }
        }

        public override void OnEnd()
        {
            // Award survival points to everyone EXCEPT the player holding the potato at the end
            foreach (PlayerAvatar player in Context.ActivePlayers)
            {
                if (player.OwnerId != currentHolderId)
                {
                    AddPoints(player, Context.SurvivalPoints);
                }
            }

            Debug.Log("[HotPotato] Round ended! Holder ID " + currentHolderId + " got caught with the potato.");
        }

        void PickNewHolder()
        {
            if (Context.ActivePlayers == null || Context.ActivePlayers.Count == 0) return;

            int randomIndex = UnityEngine.Random.Range(0, Context.ActivePlayers.Count);
            PlayerAvatar newHolder = Context.ActivePlayers[randomIndex];

            if (newHolder != null)
            {
                currentHolderId = newHolder.OwnerId;
                Debug.Log("[HotPotato] Potato bounced to: " + newHolder.PlayerName.Value);
            }
        }
    }
}