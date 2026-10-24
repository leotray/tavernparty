using System.Collections.Generic;
using TavernParty.Players;

namespace TavernParty.Core
{
    /// <summary>
    /// Plain C# base class for all minigame logic.
    /// No MonoBehaviour, no NetworkObject — runs entirely inside GameManager's coroutine.
    /// Extend this for each minigame.
    /// </summary>
    public class MinigameBase
    {
        /// <summary>Server: set to true to end the round early.</summary>
        public bool IsFinished { get; protected set; }

        /// <summary>Server: points earned this round, keyed by OwnerId.</summary>
        public Dictionary<int, int> Points { get; } = new Dictionary<int, int>();

        protected MinigameContext Context { get; private set; }

        /// <summary>Server: called once when the round begins.</summary>
        public virtual void OnBegin(MinigameContext context)
        {
            Context = context;
        }

        /// <summary>Server: called every server tick while the round runs.</summary>
        public virtual void OnTick(float deltaTime) { }

        /// <summary>Server: called once when the round ends (timer or early finish).</summary>
        public virtual void OnEnd() { }

        protected void AddPoints(PlayerAvatar player, int amount)
        {
            if (player == null) return;
            Points.TryGetValue(player.OwnerId, out int current);
            Points[player.OwnerId] = current + amount;
        }

        protected void Finish() => IsFinished = true;
    }
}
