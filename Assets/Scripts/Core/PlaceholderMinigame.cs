using UnityEngine;
using TavernParty.Players;

namespace TavernParty.Core
{
    /// <summary>
    /// Placeholder minigame. Gives every active player survival points at the end.
    /// No gameplay, just proves the round loop, scene loading and scoring work.
    /// Replace this with real minigames later.
    /// </summary>
    public class PlaceholderMinigame : MinigameBase
    {
        public override void OnBegin(MinigameContext context)
        {
            base.OnBegin(context);
            Debug.Log("[PlaceholderMinigame] Round started. Active players: " +
                      context.ActivePlayers.Count + ", Troll Host: " +
                      (context.TrollHost != null ? context.TrollHost.PlayerName.Value : "none"));
        }

        public override void OnEnd()
        {
            // Give every active player their survival points.
            foreach (PlayerAvatar player in Context.ActivePlayers)
                AddPoints(player, Context.SurvivalPoints);

            Debug.Log("[PlaceholderMinigame] Round ended. Points awarded.");
        }
    }
}
