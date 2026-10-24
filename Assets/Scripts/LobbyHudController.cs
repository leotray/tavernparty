using TavernParty.Networking;
using TavernParty.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TavernParty.UI
{
    public class LobbyHudController : MonoBehaviour
    {
        [Header("Lobby Info & Actions")]
        [SerializeField] TMP_Text codeLabel;
        [SerializeField] TMP_Text statusText;
        [SerializeField] Button inviteButton;
        [SerializeField] Button leaveButton;

        [Header("Player List & Game Start")]
        [SerializeField] TMP_Text playerListText;
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonText;
        [SerializeField] Button startGameButton;
        [SerializeField] TMP_Text startReasonText;

        [Header("Skin Picker")]
        [SerializeField] TMP_Text skinNameText;
        [SerializeField] Button prevSkinButton;
        [SerializeField] Button nextSkinButton;

        void Start()
        {
            if (inviteButton != null) inviteButton.onClick.AddListener(OnInviteClicked);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);

            if (prevSkinButton != null) prevSkinButton.onClick.AddListener(OnPrevSkinClicked);
            if (nextSkinButton != null) nextSkinButton.onClick.AddListener(OnNextSkinClicked);
        }

        void Update()
        {
            SteamLobbyManager lobby = SteamLobbyManager.Instance;
            LobbyController lobbyController = LobbyController.Instance;
            PlayerAvatar localPlayer = PlayerAvatar.Local;

            // 1. Update Join Code
            if (codeLabel != null && lobby != null)
            {
                codeLabel.text = "CODE: " + lobby.JoinCode;
            }

            // 2. Update Status Text
            if (statusText != null)
            {
                if (lobbyController != null && lobbyController.Phase.Value == GamePhase.Starting)
                {
                    statusText.text = "Starting in " + lobbyController.Countdown.Value + "...";
                }
                else if (lobby != null)
                {
                    statusText.text = lobby.Status;
                }
            }

            // 3. Update Player List and Ready Statuses
            if (playerListText != null)
            {
                string playerListString = "Players (" + PlayerAvatar.All.Count + ")\n";
                foreach (PlayerAvatar player in PlayerAvatar.All)
                {
                    string mark = player.IsReady.Value ? "[READY] " : "[  ...  ] ";
                    playerListString += mark + player.PlayerName.Value + "\n";
                }
                playerListText.text = playerListString;
            }

            // 4. Update Ready Button Text
            if (readyButtonText != null && localPlayer != null)
            {
                readyButtonText.text = localPlayer.IsReady.Value ? "NOT READY" : "READY UP";
            }

            // 5. Handle Host Start Button Logic
            if (startGameButton != null && lobbyController != null)
            {
                bool isHost = SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsHost;
                startGameButton.gameObject.SetActive(isHost);

                if (isHost)
                {
                    bool canStart = lobbyController.CanStart(out string reason);
                    startGameButton.interactable = canStart;

                    if (startReasonText != null)
                    {
                        startReasonText.text = canStart ? "" : reason;
                    }
                }
            }

            // 6. Update Skin Picker Name Display
            if (skinNameText != null)
            {
                if (localPlayer != null && localPlayer.SkinCount > 0)
                {
                    int currentSkin = localPlayer.SkinIndex.Value;
                    skinNameText.text = localPlayer.GetSkinName(currentSkin);
                }
                else
                {
                    skinNameText.text = "No Skins";
                }
            }
        }

        void OnInviteClicked()
        {
            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.OpenInviteDialog();
        }

        void OnLeaveClicked()
        {
            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.LeaveLobby();
        }

        void OnReadyClicked()
        {
            PlayerAvatar localPlayer = PlayerAvatar.Local;
            if (localPlayer != null)
            {
                localPlayer.RequestReady(!localPlayer.IsReady.Value);
            }
        }

        void OnStartGameClicked()
        {
            if (LobbyController.Instance != null)
            {
                LobbyController.Instance.RequestStart();
            }
        }

        void OnPrevSkinClicked()
        {
            PlayerAvatar localPlayer = PlayerAvatar.Local;
            if (localPlayer != null && localPlayer.SkinCount > 0)
            {
                int newIndex = (localPlayer.SkinIndex.Value - 1 + localPlayer.SkinCount) % localPlayer.SkinCount;
                localPlayer.RequestSkin(newIndex);
            }
        }

        void OnNextSkinClicked()
        {
            PlayerAvatar localPlayer = PlayerAvatar.Local;
            if (localPlayer != null && localPlayer.SkinCount > 0)
            {
                int newIndex = (localPlayer.SkinIndex.Value + 1) % localPlayer.SkinCount;
                localPlayer.RequestSkin(newIndex);
            }
        }
    }
}