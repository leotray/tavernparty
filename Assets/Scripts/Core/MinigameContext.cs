using System.Collections.Generic;
using TavernParty.Players;

namespace TavernParty.Core
{
    /// <summary>
    /// Read-only snapshot of who is playing a round.
    /// Passed to every MinigameBase.OnBegin call.
    /// </summary>
    public class MinigameContext
    {
        public List<PlayerAvatar> ActivePlayers;
        public PlayerAvatar TrollHost;
        public float DurationSeconds;
        public int SurvivalPoints;
    }
}
