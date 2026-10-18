using System.Collections.Generic;
using FishNet.Object;

namespace TavernParty.Players
{
    /// <summary>Who is in the round, handed to the minigame when it begins.</summary>
    public class MinigameContext
    {
        public List<PlayerAvatar> ActivePlayers;
        public PlayerAvatar TrollHost;
        public float DurationSeconds;
    }

    /// <summary>
    /// Base class for every minigame. Each minigame scene has ONE object with a FishNet
    /// NetworkObject and a script that extends this class. The GameFlowController loads the
    /// scene, calls ServerBegin, waits for the time to run out (or Finish), calls ServerEnd,
    /// then reads Points.
    /// </summary>
    public abstract class MinigameBase : NetworkBehaviour
    {
        public static MinigameBase Instance { get; private set; }

        /// <summary>Server: set by Finish() when the minigame wants to end early.</summary>
        public bool IsFinished { get; private set; }

        /// <summary>Server: points earned this round, keyed by the player's OwnerId.</summary>
        public Dictionary<int, int> Points { get; } = new Dictionary<int, int>();

        public override void OnStartNetwork()
        {
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Server: the round starts now.</summary>
        public abstract void ServerBegin(MinigameContext context);

        /// <summary>Server: the round is over (time ran out, Finish was called, or it was aborted).</summary>
        public virtual void ServerEnd() { }

        protected void AddPoints(PlayerAvatar player, int amount)
        {
            if (player == null) return;
            Points.TryGetValue(player.OwnerId, out int current);
            Points[player.OwnerId] = current + amount;
        }

        /// <summary>Server: ends the round before the timer runs out.</summary>
        protected void Finish()
        {
            IsFinished = true;
        }
    }
}