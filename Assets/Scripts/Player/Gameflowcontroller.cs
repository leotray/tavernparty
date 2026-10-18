using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TavernParty.Networking;
using UnityEngine;

namespace TavernParty.Players
{
    public enum FlowState
    {
        Idle,
        Intro,
        Playing,
        Results
    }

    /// <summary>
    /// Server-driven round loop. Put this on a GameObject in the BOOTSTRAP scene together with a
    /// FishNet NetworkObject (a scene object). It must live in Bootstrap because the Lobby and
    /// minigame scenes are loaded and unloaded around it.
    ///
    /// A session lasts one round per player. Each round a different player is the non-playing
    /// Troll Host, a minigame is picked and loaded, played, scored, and unloaded.
    /// </summary>
    public class GameFlowController : NetworkBehaviour
    {
        public static GameFlowController Instance { get; private set; }

        public readonly SyncVar<FlowState> State = new SyncVar<FlowState>(FlowState.Idle);
        public readonly SyncVar<bool> InMinigame = new SyncVar<bool>(false);
        public readonly SyncVar<int> Round = new SyncVar<int>(0);
        public readonly SyncVar<int> TotalRounds = new SyncVar<int>(0);
        public readonly SyncVar<int> TrollHostOwnerId = new SyncVar<int>(-1);
        public readonly SyncVar<string> MinigameName = new SyncVar<string>("");
        public readonly SyncVar<int> SecondsLeft = new SyncVar<int>(0);

        [SerializeField] string lobbySceneName = "Lobby";

        [Tooltip("Minigames the game can pick from. A minigame is only picked if the number of active players fits its min/max.")]
        [SerializeField] MinigameDefinition[] minigames;

        [SerializeField] float introSeconds = 4f;
        [SerializeField] float resultsSeconds = 5f;
        [SerializeField] float sceneLoadTimeout = 15f;
        [SerializeField] bool showDebugPanel = true;

        Coroutine _session;
        string _loadedMinigameScene = "";
        NetworkObject _activeController;

        public override void OnStartNetwork()
        {
            Instance = this;
        }

        public override void OnStopNetwork()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Entry points ----------

        /// <summary>Server: called by the LobbyController when the countdown ends.</summary>
        [Server]
        public void ServerBeginSession()
        {
            if (_session != null) return;
            _session = StartCoroutine(RunSession());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestAbort(NetworkConnection sender = null)
        {
            if (sender == null || !sender.IsHost) return;
            if (State.Value == FlowState.Idle) return;

            if (_session != null) StopCoroutine(_session);

            MinigameBase current = MinigameBase.Instance;
            if (current != null) current.ServerEnd();

            _session = StartCoroutine(ReturnToLobby());
        }

        // ---------- The round loop (server) ----------

        IEnumerator RunSession()
        {
            List<PlayerAvatar> players = new List<PlayerAvatar>(PlayerAvatar.All);
            foreach (PlayerAvatar player in players)
                player.Score.Value = 0;

            Debug.Log("[GameFlow] Session started with " + players.Count + " players.");

            // Everyone is the Troll Host exactly once, in random order.
            List<PlayerAvatar> hostOrder = new List<PlayerAvatar>(players);
            Shuffle(hostOrder);
            TotalRounds.Value = hostOrder.Count;

            // Leave the lobby tavern. The avatars stay alive but hide themselves.
            UnloadScene(lobbySceneName);
            InMinigame.Value = true;
            yield return new WaitForSeconds(0.5f);

            for (int i = 0; i < hostOrder.Count; i++)
            {
                PlayerAvatar host = hostOrder[i];
                if (host == null) continue; // this player left

                List<PlayerAvatar> active = new List<PlayerAvatar>();
                foreach (PlayerAvatar player in players)
                    if (player != null && player != host) active.Add(player);

                MinigameDefinition definition = PickMinigame(active.Count);
                if (definition == null)
                {
                    Debug.LogError("[GameFlow] No minigame fits " + active.Count + " active players. " +
                                   "Check the Minigames list and each definition's min/max players.");
                    break;
                }

                Round.Value = i + 1;
                TrollHostOwnerId.Value = host.OwnerId;
                MinigameName.Value = definition.displayName;

                Debug.Log("[GameFlow] Round " + (i + 1) + ": Troll Host = " + host.PlayerName.Value +
                          ", minigame = " + definition.displayName + ", loading scene '" + definition.sceneName + "'.");

                // 1. Load the minigame scene and wait until it is loaded on this machine.
                LoadScene(definition.sceneName);
                _loadedMinigameScene = definition.sceneName;

                float waited = 0f;
                while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(definition.sceneName).isLoaded &&
                       waited < sceneLoadTimeout)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                // 2. Spawn the minigame's controller prefab (no networked scene objects needed).
                MinigameBase minigame = SpawnMinigameController(definition);
                if (minigame == null)
                {
                    UnloadScene(_loadedMinigameScene);
                    _loadedMinigameScene = "";
                    continue;
                }

                // 2. Intro
                Debug.Log("[GameFlow] Minigame scene loaded. Intro starts.");
                State.Value = FlowState.Intro;
                yield return new WaitForSeconds(introSeconds);

                // 3. Play
                Debug.Log("[GameFlow] Playing.");
                State.Value = FlowState.Playing;
                minigame.ServerBegin(new MinigameContext
                {
                    ActivePlayers = active,
                    TrollHost = host,
                    DurationSeconds = definition.durationSeconds
                });

                float remaining = definition.durationSeconds;
                while (remaining > 0f && !minigame.IsFinished)
                {
                    SecondsLeft.Value = Mathf.CeilToInt(remaining);
                    remaining -= Time.deltaTime;
                    yield return null;
                }
                SecondsLeft.Value = 0;
                minigame.ServerEnd();

                // 4. Score
                foreach (KeyValuePair<int, int> entry in minigame.Points)
                {
                    PlayerAvatar scorer = players.Find(p => p != null && p.OwnerId == entry.Key);
                    if (scorer != null) scorer.Score.Value += entry.Value;
                }

                State.Value = FlowState.Results;
                yield return new WaitForSeconds(resultsSeconds);

                // 5. Remove the controller, unload the scene and wait until it is really gone
                DespawnController();
                UnloadScene(_loadedMinigameScene);
                _loadedMinigameScene = "";

                waited = 0f;
                while (MinigameBase.Instance != null && waited < 5f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            _session = StartCoroutine(ReturnToLobby());
        }

        IEnumerator ReturnToLobby()
        {
            Debug.Log("[GameFlow] Returning to the lobby.");
            DespawnController();
            if (!string.IsNullOrEmpty(_loadedMinigameScene))
            {
                UnloadScene(_loadedMinigameScene);
                _loadedMinigameScene = "";
                yield return new WaitForSeconds(0.5f);
            }

            foreach (PlayerAvatar player in PlayerAvatar.All)
                player.IsReady.Value = false;

            if (LobbyController.Instance != null)
                LobbyController.Instance.ServerResetToLobby();

            LoadScene(lobbySceneName);

            InMinigame.Value = false;
            Round.Value = 0;
            TotalRounds.Value = 0;
            TrollHostOwnerId.Value = -1;
            MinigameName.Value = "";
            SecondsLeft.Value = 0;
            State.Value = FlowState.Idle;

            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.SetJoinable(true);

            _session = null;
        }

        // ---------- Minigame controller (spawned from a prefab) ----------

        MinigameBase SpawnMinigameController(MinigameDefinition definition)
        {
            if (definition.controllerPrefab == null)
            {
                Debug.LogError("[GameFlow] '" + definition.displayName + "' has no Controller Prefab assigned.");
                return null;
            }

            NetworkObject controller = Instantiate(definition.controllerPrefab);
            InstanceFinder.ServerManager.Spawn(controller);
            _activeController = controller;

            MinigameBase minigame = controller.GetComponent<MinigameBase>();
            if (minigame == null)
                Debug.LogError("[GameFlow] The controller prefab of '" + definition.displayName + "' has no MinigameBase script.");

            return minigame;
        }

        void DespawnController()
        {
            if (_activeController == null) return;

            if (_activeController.IsSpawned)
                InstanceFinder.ServerManager.Despawn(_activeController);

            _activeController = null;
        }

        // ---------- Helpers ----------

        MinigameDefinition PickMinigame(int activePlayerCount)
        {
            List<MinigameDefinition> valid = new List<MinigameDefinition>();
            if (minigames != null)
            {
                foreach (MinigameDefinition definition in minigames)
                {
                    if (definition == null || string.IsNullOrEmpty(definition.sceneName)) continue;
                    if (activePlayerCount < definition.minPlayers || activePlayerCount > definition.maxPlayers) continue;
                    valid.Add(definition);
                }
            }

            if (valid.Count == 0) return null;
            return valid[Random.Range(0, valid.Count)];
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        void LoadScene(string sceneName)
        {
            InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData(sceneName));
        }

        void UnloadScene(string sceneName)
        {
            InstanceFinder.SceneManager.UnloadGlobalScenes(new SceneUnloadData(sceneName));
        }

        PlayerAvatar FindPlayer(int ownerId)
        {
            foreach (PlayerAvatar player in PlayerAvatar.All)
                if (player != null && player.OwnerId == ownerId) return player;
            return null;
        }

        // ---------- Temporary debug UI (replace with real UI later) ----------

        void OnGUI()
        {
            if (!showDebugPanel || State.Value == FlowState.Idle) return;

            const float width = 420f;
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2f, 10f, width, 300f), GUI.skin.box);

            GUILayout.Label("Round " + Round.Value + " / " + TotalRounds.Value + "  -  " + MinigameName.Value);

            PlayerAvatar host = FindPlayer(TrollHostOwnerId.Value);
            GUILayout.Label("Troll Host: " + (host != null ? host.PlayerName.Value : "?"));

            switch (State.Value)
            {
                case FlowState.Intro: GUILayout.Label("Get ready..."); break;
                case FlowState.Playing: GUILayout.Label("Time left: " + SecondsLeft.Value); break;
                case FlowState.Results: GUILayout.Label("Round over!"); break;
            }

            GUILayout.Space(6f);
            GUILayout.Label("Scores");
            List<PlayerAvatar> ranked = new List<PlayerAvatar>(PlayerAvatar.All);
            ranked.Sort((a, b) => b.Score.Value.CompareTo(a.Score.Value));
            foreach (PlayerAvatar player in ranked)
                GUILayout.Label(player.Score.Value + "  -  " + player.PlayerName.Value);

            if (IsHostStarted && GUILayout.Button("Abort Session"))
                RequestAbort();

            GUILayout.EndArea();
        }
    }
}