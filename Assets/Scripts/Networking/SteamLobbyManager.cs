using System;
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace TavernParty.Networking
{
    /// <summary>
    /// Steam lobby + FishNet connection glue.
    /// Host: creates a Steam lobby, starts FishNet server + client (host mode).
    /// Join: via Steam friend invite / "Join Game", launch arg, or a short join code.
    /// Initializes Steamworks itself, so do NOT also add Steamworks.NET's SteamManager.
    /// </summary>
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        public const int MaxPlayers = 8;

        const string KeyGame = "game";
        const string KeyCode = "code";
        const string GameId = "TavernParty";
        const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I, O, 0, 1
        const int CodeLength = 4;

        [Tooltip("Public lets join codes work for anyone. FriendsOnly hides the lobby from code search for non-friends.")]
        [SerializeField] ELobbyType lobbyType = ELobbyType.k_ELobbyTypePublic;

        [SerializeField] bool showDebugPanel = true;

        public bool SteamReady { get; private set; }
        public bool InLobby { get; private set; }
        public bool IsHost { get; private set; }
        public string JoinCode { get; private set; } = "";
        public string Status { get; private set; } = "Idle";
        public CSteamID LobbyId => _lobbyId;

        public event Action<string> StatusChanged;

        CSteamID _lobbyId;
        string _codeInput = "";

        CallResult<LobbyCreated_t> _createResult;
        CallResult<LobbyMatchList_t> _listResult;
        Callback<GameLobbyJoinRequested_t> _joinRequested;
        Callback<LobbyEnter_t> _lobbyEntered;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                SteamReady = SteamAPI.Init();
            }
            catch (Exception e)
            {
                Debug.LogError("Steamworks failed to initialize: " + e);
            }

            SetStatus(SteamReady
                ? "Steam ready as " + SteamFriends.GetPersonaName()
                : "Steam NOT ready. Is the Steam client running and steam_appid.txt present?");
        }

        void OnEnable()
        {
            if (!SteamReady) return;
            _createResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _listResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        void Start()
        {
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;

            if (!SteamReady) return;

            // Game launched through Steam's "Join Game" on a friend: +connect_lobby <lobbyId>
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong id) && id != 0)
                {
                    JoinLobby(new CSteamID(id));
                    break;
                }
            }
        }

        void Update()
        {
            if (SteamReady) SteamAPI.RunCallbacks();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            LeaveLobby();
            if (SteamReady) SteamAPI.Shutdown();
        }

        // ---------- Public API ----------

        public void HostGame()
        {
            if (!SteamReady || InLobby) return;
            SetStatus("Creating lobby...");
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(lobbyType, MaxPlayers);
            _createResult.Set(call);
        }

        public void JoinByCode(string code)
        {
            if (!SteamReady || InLobby) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != CodeLength)
            {
                SetStatus("Join code must be " + CodeLength + " characters.");
                return;
            }

            SetStatus("Searching for lobby " + code + "...");
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyGame, GameId, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyCode, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
            _listResult.Set(call);
        }

        /// <summary>Opens the Steam overlay friend list to invite people to this lobby.</summary>
        public void OpenInviteDialog()
        {
            if (!InLobby) return;
            SteamFriends.ActivateGameOverlayInviteDialog(_lobbyId);
        }

        public void LeaveLobby()
        {
            _leaving = true;
            if (InstanceFinder.NetworkManager != null)
            {
                if (InstanceFinder.IsClientStarted) InstanceFinder.ClientManager.StopConnection();
                if (InstanceFinder.IsServerStarted) InstanceFinder.ServerManager.StopConnection(true);
            }

            if (SteamReady && InLobby) SteamMatchmaking.LeaveLobby(_lobbyId);

            InLobby = false;
            IsHost = false;
            JoinCode = "";
            _lobbyId = CSteamID.Nil;
            _leaving = false;
            SetStatus("Left lobby.");
        }

        // ---------- FishNet connection state ----------

        bool _leaving;

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    SetStatus(IsHost
                        ? "Hosting. Join code: " + JoinCode
                        : "Connected to lobby " + JoinCode);
                    break;

                case LocalConnectionState.Stopped:
                    if (InLobby && !IsHost && !_leaving)
                    {
                        LeaveLobby();
                        SetStatus("Disconnected from host.");
                    }
                    break;
            }
        }

        // ---------- Steam callbacks ----------

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                SetStatus("Lobby creation failed: " + result.m_eResult);
                return;
            }

            _lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            IsHost = true;
            JoinCode = GenerateCode();

            SteamMatchmaking.SetLobbyData(_lobbyId, KeyGame, GameId);
            SteamMatchmaking.SetLobbyData(_lobbyId, KeyCode, JoinCode);
            SteamMatchmaking.SetLobbyJoinable(_lobbyId, true);

            // Host mode: server and client in the same instance.
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            InLobby = true;
            SetStatus("Hosting. Join code: " + JoinCode);
        }

        void OnLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            if (ioFailure || result.m_nLobbiesMatching == 0)
            {
                SetStatus("No lobby found for that code.");
                return;
            }

            JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
        }

        void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            // Friend invite accepted in the Steam overlay / friends list.
            if (InLobby) LeaveLobby();
            JoinLobby(request.m_steamIDLobby);
        }

        void OnLobbyEntered(LobbyEnter_t entered)
        {
            if (IsHost) return; // we created this lobby; server/client already started

            uint response = entered.m_EChatRoomEnterResponse;
            if (response != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                SetStatus("Could not join lobby (response " + response + ").");
                return;
            }

            _lobbyId = new CSteamID(entered.m_ulSteamIDLobby);
            JoinCode = SteamMatchmaking.GetLobbyData(_lobbyId, KeyCode);

            // The lobby owner is the FishNet host. Point the transport at their Steam ID.
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(_lobbyId);
            InstanceFinder.NetworkManager.TransportManager.Transport.SetClientAddress(owner.ToString());
            InstanceFinder.ClientManager.StartConnection();

            InLobby = true;
            SetStatus("Joined lobby " + JoinCode + ", connecting...");
        }

        // ---------- Helpers ----------

        void JoinLobby(CSteamID lobby)
        {
            SetStatus("Joining lobby...");
            SteamMatchmaking.JoinLobby(lobby); // result arrives in OnLobbyEntered
        }

        static string GenerateCode()
        {
            char[] chars = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                chars[i] = CodeAlphabet[UnityEngine.Random.Range(0, CodeAlphabet.Length)];
            return new string(chars);
        }

        void SetStatus(string message)
        {
            Status = message;
            Debug.Log("[SteamLobby] " + message);
            StatusChanged?.Invoke(message);
        }

        // ---------- Temporary debug UI (replace with real UI later) ----------

        void OnGUI()
        {
            if (!showDebugPanel) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 300), GUI.skin.box);
            GUILayout.Label("Tavern Party - Lobby (debug)");
            GUILayout.Label(Status);

            if (!InLobby)
            {
                if (GUILayout.Button("Host Game")) HostGame();
                GUILayout.BeginHorizontal();
                _codeInput = GUILayout.TextField(_codeInput, CodeLength, GUILayout.Width(100));
                if (GUILayout.Button("Join By Code")) JoinByCode(_codeInput);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Code: " + JoinCode + (IsHost ? " (you are host)" : ""));
                if (GUILayout.Button("Invite Friends (Steam overlay)")) OpenInviteDialog();
                if (GUILayout.Button("Leave")) LeaveLobby();
            }
            GUILayout.EndArea();
        }
    }
}