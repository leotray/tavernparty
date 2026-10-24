using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using TavernParty.Players;
using UnityEngine;

namespace TavernParty.Core
{
    public enum GamePhase { Lobby, Starting, Playing, Results, EndOfSession }

    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        public readonly SyncVar<GamePhase> Phase = new SyncVar<GamePhase>(GamePhase.Lobby);
        public readonly SyncVar<int> Countdown = new SyncVar<int>(0);
        public readonly SyncVar<int> Round = new SyncVar<int>(0);
        public readonly SyncVar<int> TotalRounds = new SyncVar<int>(0);
        public readonly SyncVar<int> SecondsLeft = new SyncVar<int>(0);
        public readonly SyncVar<int> TrollHostId = new SyncVar<int>(-1);
        public readonly SyncVar<string> MinigameName = new SyncVar<string>("");

        [Header("Lobby")]
        [SerializeField] int minPlayersToStart = 2;
        [SerializeField] int countdownSeconds = 3;
        [SerializeField] int resultSeconds = 5;

        [Header("Minigames")]
        [SerializeField] MinigameDefinition[] minigames;

        [Header("Scenes")]
        [SerializeField] string lobbySceneName = "Lobby";
        [SerializeField] bool showDebugPanel = true;
        MonoBehaviour gamemanager;
        Coroutine _session;

        public override void OnStartNetwork()
        {
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanStart(out string reason)
        {
            if (Phase.Value != GamePhase.Lobby)
            { reason = "Already started."; return false; }

            if (PlayerAvatar.All.Count < minPlayersToStart)
            { reason = "Need " + minPlayersToStart + " players (" + PlayerAvatar.All.Count + " here)."; return false; }

            foreach (PlayerAvatar p in PlayerAvatar.All)
                if (!p.IsReady.Value)
                { reason = "Not everyone is ready."; return false; }

            reason = ""; return true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestStart(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost) return;
            if (!CanStart(out _)) return;
            if (_session != null) return;
            _session = StartCoroutine(RunSession());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestAbort(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost) return;
            if (_session == null) return;
            StopCoroutine(_session);
            _session = null;
            StartCoroutine(ReturnToLobby("Session aborted."));
        }

        IEnumerator RunSession()
        {
            Phase.Value = GamePhase.Starting;
            SteamLobbyManager.Instance?.SetJoinable(false);

            for (int s = countdownSeconds; s > 0; s--)
            {
                Countdown.Value = s;
                yield return new WaitForSeconds(1f);
            }
            Countdown.Value = 0;

            // SAFETY CHECK: Ensure all connected clients are fully authenticated before shifting scenes
            float authWait = 0f;
            bool allAuthenticated = false;
            while (!allAuthenticated && authWait < 5f)
            {
                allAuthenticated = true;
                foreach (NetworkConnection conn in InstanceFinder.ServerManager.Clients.Values)
                {
                    if (!conn.IsAuthenticated)
                    {
                        allAuthenticated = false;
                        break;
                    }
                }
                if (!allAuthenticated)
                {
                    authWait += Time.deltaTime;
                    yield return null;
                }
            }

            List<PlayerAvatar> players = new List<PlayerAvatar>(PlayerAvatar.All);
            foreach (PlayerAvatar p in players) p.Score.Value = 0;

            List<PlayerAvatar> hostOrder = new List<PlayerAvatar>(players);
            Shuffle(hostOrder);
            TotalRounds.Value = hostOrder.Count;

            UnloadScene(lobbySceneName);
            yield return new WaitForSeconds(0.5f); // Slightly padded buffer for scene tear-down

            for (int i = 0; i < hostOrder.Count; i++)
            {
                PlayerAvatar host = hostOrder[i];
                if (host == null) continue;

                List<PlayerAvatar> active = new List<PlayerAvatar>();
                foreach (PlayerAvatar p in players)
                    if (p != null && p != host) active.Add(p);

                MinigameDefinition def = PickMinigame();
                if (def == null)
                {
                    Debug.LogError("[GameManager] No minigame definitions assigned.");
                    break;
                }

                Round.Value = i + 1;
                TrollHostId.Value = host.OwnerId;
                MinigameName.Value = def.displayName;
                Phase.Value = GamePhase.Playing;

                LoadScene(def.sceneName);

                float waited = 0f;
                while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(def.sceneName).isLoaded && waited < 15f)
                { waited += Time.deltaTime; yield return null; }

                yield return new WaitForSeconds(2f);

                MinigameBase game = CreateMinigame(def);
                MinigameContext ctx = new MinigameContext
                {
                    ActivePlayers = active,
                    TrollHost = host,
                    DurationSeconds = def.durationSeconds,
                    SurvivalPoints = def.survivalPoints
                };
                game.OnBegin(ctx);

                float remaining = def.durationSeconds;
                while (remaining > 0f && !game.IsFinished)
                {
                    SecondsLeft.Value = Mathf.CeilToInt(remaining);
                    game.OnTick(Time.deltaTime); 
                    remaining -= Time.deltaTime;
                    yield return null;
                }
                SecondsLeft.Value = 0;
                game.OnEnd();

                foreach (KeyValuePair<int, int> entry in game.Points)
                {
                    PlayerAvatar scorer = players.Find(p => p != null && p.OwnerId == entry.Key);
                    if (scorer != null) scorer.Score.Value += entry.Value;
                }

                Phase.Value = GamePhase.Results;
                yield return new WaitForSeconds(resultSeconds);

                UnloadScene(def.sceneName);
                yield return new WaitForSeconds(0.3f);
            }

            Phase.Value = GamePhase.EndOfSession;
            yield return new WaitForSeconds(resultSeconds);

            _session = null;
            StartCoroutine(ReturnToLobby("Session finished."));
        }

        IEnumerator ReturnToLobby(string reason)
        {
            Debug.Log("[GameManager] Returning to lobby: " + reason);

            foreach (PlayerAvatar p in PlayerAvatar.All) p.IsReady.Value = false;

            Round.Value = 0; TotalRounds.Value = 0;
            TrollHostId.Value = -1; MinigameName.Value = "";
            SecondsLeft.Value = 0; Countdown.Value = 0;
            Phase.Value = GamePhase.Lobby;

            LoadScene(lobbySceneName);
            SteamLobbyManager.Instance?.SetJoinable(true);
            _session = null;
            yield break;
        }

        MinigameDefinition PickMinigame()
        {
            if (minigames == null || minigames.Length == 0) return null;
            return minigames[Random.Range(0, minigames.Length)];
        }

        MinigameBase CreateMinigame(MinigameDefinition def)
        {
            if (def == null) return new PlaceholderMinigame();

            if (def.sceneName.Contains("HotPotato") || def.displayName.Contains("Hot Potato"))
                return new HotPotatoMinigame();

            return new PlaceholderMinigame();
        }
        void LoadScene(string sceneName)
        {
            SceneLoadData sld = new SceneLoadData(sceneName);

            // Carry all active player avatars into the new scene so they don't get destroyed
            List<NetworkObject> playerObjects = new List<NetworkObject>();
            foreach (PlayerAvatar p in PlayerAvatar.All)
            {
                if (p != null && p.NetworkObject != null)
                    playerObjects.Add(p.NetworkObject);
            }

            if (playerObjects.Count > 0)
            {
                sld.MovedNetworkObjects = playerObjects.ToArray();
            }

            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }

        void UnloadScene(string sceneName)
        {
            UnityEngine.SceneManagement.Scene s = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (s.IsValid() && s.isLoaded)
                InstanceFinder.SceneManager.UnloadGlobalScenes(new SceneUnloadData(sceneName));
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        PlayerAvatar FindPlayer(int ownerId)
        {
            foreach (PlayerAvatar p in PlayerAvatar.All)
                if (p != null && p.OwnerId == ownerId) return p;
            return null;
        }

        void OnGUI()
        {
            if (!showDebugPanel) return;

            const float w = 320f;
            float y = 10f;

            GUILayout.BeginArea(new Rect(Screen.width - w - 10f, y, w, Screen.height - 20f), GUI.skin.box);

            GUILayout.Label("=== GameManager Debug ===");
            GUILayout.Label("Phase: " + Phase.Value);

            switch (Phase.Value)
            {
                case GamePhase.Lobby:
                    GUILayout.Space(4);
                    GUILayout.Label("Players (" + PlayerAvatar.All.Count + "):");
                    foreach (PlayerAvatar p in PlayerAvatar.All)
                        GUILayout.Label((p.IsReady.Value ? "[RDY] " : "[ .. ] ") + p.PlayerName.Value + "  (" + p.Score.Value + " pts)");

                    GUILayout.Space(4);
                    PlayerAvatar me = PlayerAvatar.Local;
                    if (me != null)
                    {
                        if (GUILayout.Button(me.IsReady.Value ? "Not Ready" : "Ready"))
                            me.RequestReady(!me.IsReady.Value);
                    }

                    if (base.IsServerStarted)
                    {
                        bool can = CanStart(out string reason);
                        GUI.enabled = can;
                        if (GUILayout.Button("Start Game")) RequestStart();
                        GUI.enabled = true;
                        if (!can) GUILayout.Label(reason);
                    }
                    else
                    {
                        GUILayout.Label("Waiting for host to start...");
                    }
                    break;

                case GamePhase.Starting:
                    GUILayout.Label("Starting in " + Countdown.Value + "...");
                    break;

                case GamePhase.Playing:
                case GamePhase.Results:
                    GUILayout.Label("Round " + Round.Value + " / " + TotalRounds.Value);
                    GUILayout.Label("Minigame: " + MinigameName.Value);
                    PlayerAvatar host = FindPlayer(TrollHostId.Value);
                    GUILayout.Label("Troll Host: " + (host != null ? host.PlayerName.Value : "?"));
                    GUILayout.Label(Phase.Value == GamePhase.Playing
                        ? "Time left: " + SecondsLeft.Value + "s"
                        : "Round over!");

                    GUILayout.Space(4);
                    GUILayout.Label("Scores:");
                    List<PlayerAvatar> ranked = new List<PlayerAvatar>(PlayerAvatar.All);
                    ranked.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
                    foreach (PlayerAvatar p in ranked)
                        GUILayout.Label(p.Score.Value + "  " + p.PlayerName.Value);

                    if (base.IsServerStarted && GUILayout.Button("Abort")) RequestAbort();
                    break;

                case GamePhase.EndOfSession:
                    GUILayout.Label("Session over! Final scores:");
                    List<PlayerAvatar> final = new List<PlayerAvatar>(PlayerAvatar.All);
                    final.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
                    foreach (PlayerAvatar p in final)
                        GUILayout.Label(p.Score.Value + "  " + p.PlayerName.Value);
                    break;
            }

            GUILayout.EndArea();
        }
    }
}