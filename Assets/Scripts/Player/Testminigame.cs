using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Placeholder minigame used to test the round loop. It does nothing except give
    /// every active player a few random points. Replace it with real minigames later.
    /// </summary>
    public class TestMinigame : MinigameBase
    {
        public override void ServerBegin(MinigameContext context)
        {
            Debug.Log("[TestMinigame] Begin. Troll Host: " +
                      (context.TrollHost != null ? context.TrollHost.PlayerName.Value : "none") +
                      ", active players: " + context.ActivePlayers.Count);

            foreach (PlayerAvatar player in context.ActivePlayers)
                AddPoints(player, Random.Range(0, 4));
        }

        public override void ServerEnd()
        {
            Debug.Log("[TestMinigame] End.");
        }
    }
}