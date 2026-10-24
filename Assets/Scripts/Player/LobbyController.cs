using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using UnityEngine;

namespace TavernParty.Players
{
    public enum GamePhase
    {
        Lobby,
        Starting,
        Playing
    }

    /// <summary>
    /// Server-driven lobby state: ready-up, the host's Start button, a short countdown,
    /// and the phase everyone can read. Put this on a GameObject in the Lobby scene
    /// together with a FishNet NetworkObject component (a scene object, not a prefab).
    /// The GameFlowController will take over once the phase becomes Playing.
    /// </summary>
    public class LobbyController : NetworkBehaviour
    {
        public static LobbyController Instance { get; private set; }

        public readonly SyncVar<GamePhase> Phase = new SyncVar<GamePhase>(GamePhase.Lobby);
        public readonly SyncVar<int> Countdown = new SyncVar<int>(0);

        [Tooltip("Players needed to start. Use 1 or 2 while testing alone, 4 for the real game.")]
        [SerializeField] int minPlayers = 4;

        [SerializeField] int countdownSeconds = 3;

        [SerializeField] bool showDebugPanel = true;

        public override void OnStartNetwork()
        {
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Rules ----------

        public bool CanStart(out string reason)
        {
            if (Phase.Value != GamePhase.Lobby)
            {
                reason = "The game has already started.";
                return false;
            }

            int count = PlayerAvatar.All.Count;
            if (count < minPlayers)
            {
                reason = "Need at least " + minPlayers + " players (" + count + " connected).";
                return false;
            }

            foreach (PlayerAvatar player in PlayerAvatar.All)
            {
                if (!player.IsReady.Value)
                {
                    reason = "Waiting for everyone to be ready.";
                    return false;
                }
            }

            reason = "";
            return true;
        }

        // ---------- Requests (anyone can ask, the server decides) ----------

        [ServerRpc(RequireOwnership = false)]
        public void RequestStart(NetworkConnection sender = null)
        {
            // Only the player running the server (the lobby host) may start the game.
            if (sender == null || !sender.IsHost) return;
            if (!CanStart(out _)) return;

            StartCoroutine(StartSequence());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReturnToLobby(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost) return;
            if (Phase.Value == GamePhase.Lobby) return;

            StopAllCoroutines();
            Countdown.Value = 0;
            Phase.Value = GamePhase.Lobby;

            foreach (PlayerAvatar player in PlayerAvatar.All)
                player.IsReady.Value = false;

            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.SetJoinable(true);
        }

        // ---------- Server logic ----------

        IEnumerator StartSequence()
        {
            Phase.Value = GamePhase.Starting;

            // Nobody new can join once the game is starting.
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.SetJoinable(false);

            for (int seconds = countdownSeconds; seconds > 0; seconds--)
            {
                Countdown.Value = seconds;
                yield return new WaitForSeconds(1f);
            }

            Countdown.Value = 0;
            Phase.Value = GamePhase.Playing;
            Debug.Log("[LobbyController] Game started. The game flow takes over from here.");
        }

        // ---------- Temporary debug UI (replace with real UI later) ----------

        void OnGUI()
        {
            if (!showDebugPanel) return;

            const float width = 300f;
            GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, 380f), GUI.skin.box);

            GUILayout.Label("Players (" + PlayerAvatar.All.Count + ")");
            foreach (PlayerAvatar player in PlayerAvatar.All)
            {
                string mark = player.IsReady.Value ? "[READY] " : "[  ...  ] ";
                GUILayout.Label(mark + player.PlayerName.Value);
            }

            GUILayout.Space(8f);

            switch (Phase.Value)
            {
                case GamePhase.Lobby:
                    PlayerAvatar me = PlayerAvatar.Local;
                    if (me != null && GUILayout.Button(me.IsReady.Value ? "Not Ready" : "Ready"))
                        me.RequestReady(!me.IsReady.Value);

                    if (IsHostStarted)
                    {
                        bool can = CanStart(out string reason);
                        GUI.enabled = can;
                        if (GUILayout.Button("Start Game")) RequestStart();
                        GUI.enabled = true;
                        if (!can) GUILayout.Label(reason);
                    }
                    else
                    {
                        GUILayout.Label("Waiting for the host to start...");
                    }
                    break;

                case GamePhase.Starting:
                    GUILayout.Label("Starting in " + Countdown.Value + "...");
                    break;

                case GamePhase.Playing:
                    GUILayout.Label("Game in progress (placeholder).");
                    if (IsHostStarted && GUILayout.Button("Back To Lobby"))
                        RequestReturnToLobby();
                    break;
            }

            GUILayout.EndArea();
        }
    }
}