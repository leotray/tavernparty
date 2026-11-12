using UnityEngine;
using TavernParty.Players;
using System.Collections.Generic;

namespace TavernParty.Core
{
    public class HotPotatoMinigame : MinigameBase
    {
        int currentHolderId = -1;
        float roundTimer = 10f;
        List<PlayerAvatar> survivingPlayers = new List<PlayerAvatar>();

        public int CurrentHolderId => currentHolderId;

        public override void OnBegin(MinigameContext context)
        {
            base.OnBegin(context);
            survivingPlayers = new List<PlayerAvatar>(Context.ActivePlayers);

            // Ensure all participating players start the round alive
            foreach (var p in survivingPlayers)
            {
                if (p != null) p.RequestSetAlive(true);
            }

            StartNewEliminationRound();
        }

        void StartNewEliminationRound()
        {
            survivingPlayers.RemoveAll(p => p == null || !p.IsAlive.Value);

            if (survivingPlayers.Count <= 1)
            {
                IsFinished = true;
                return;
            }

            int randomIndex = Random.Range(0, survivingPlayers.Count);
            PlayerAvatar startHolder = survivingPlayers[randomIndex];

            currentHolderId = startHolder.OwnerId;
            if (GameManager.Instance != null)
                GameManager.Instance.PotatoHolderId.Value = currentHolderId;

            roundTimer = 10f;
            Debug.Log("[HotPotato] Elimination round started! Potato held by: " + startHolder.PlayerName.Value);
        }

        public override void OnTick(float deltaTime)
        {
            if (IsFinished) return;

            roundTimer -= deltaTime;
            if (GameManager.Instance != null)
                GameManager.Instance.SecondsLeft.Value = Mathf.CeilToInt(roundTimer);

            if (roundTimer <= 0f)
            {
                ExplodeHolder();
            }
        }

        void ExplodeHolder()
        {
            PlayerAvatar victim = FindPlayer(currentHolderId);
            if (victim != null)
            {
                Debug.Log("[HotPotato] BOOM! " + victim.PlayerName.Value + " exploded and was eliminated.");

                // Set player to dead / spectator mode
                victim.RequestSetAlive(false);
                survivingPlayers.Remove(victim);

                // Award survival points to surviving players
                foreach (PlayerAvatar p in survivingPlayers)
                {
                    if (p != null && p.IsAlive.Value)
                    {
                        AddPoints(p, Context.SurvivalPoints);
                    }
                }

                if (survivingPlayers.Count <= 1)
                {
                    IsFinished = true;
                }
                else
                {
                    StartNewEliminationRound();
                }
            }
            else
            {
                StartNewEliminationRound();
            }
        }

        public override void OnEnd()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PotatoHolderId.Value = -1;

            // Reset all players back to alive state when minigame closes
            foreach (var p in Context.ActivePlayers)
            {
                if (p != null) p.RequestSetAlive(true);
            }

            Debug.Log("[HotPotato] Minigame session ended.");
        }

        public void SetHolder(int ownerId)
        {
            currentHolderId = ownerId;
            if (GameManager.Instance != null)
                GameManager.Instance.PotatoHolderId.Value = currentHolderId;
        }

        PlayerAvatar FindPlayer(int ownerId)
        {
            foreach (PlayerAvatar p in Context.ActivePlayers)
            {
                if (p != null && p.OwnerId == ownerId) return p;
            }
            return null;
        }
    }
}