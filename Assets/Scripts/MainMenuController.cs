using TavernParty.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FishNet;

namespace TavernParty.UI
{
    /// <summary>
    /// Put this on the menu Canvas in the Bootstrap scene.
    /// Handles Host, Join, Code Input, and Close/Quit, and hides when in a lobby.
    /// Works with both SteamLobbyManager (Steam mode) and LocalTestManager (local mode).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Elements")]
        [SerializeField] GameObject menuRoot;

        [Header("Main Menu Layout")]
        [SerializeField] Button hostButton;
        [SerializeField] TMP_InputField codeInput;
        [SerializeField] Button joinButton;
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text statusText;

        void Start()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (codeInput != null)
            {
                codeInput.characterLimit = 4;
                codeInput.onValueChanged.AddListener(value =>
                    codeInput.SetTextWithoutNotify(value.ToUpperInvariant()));
            }
        }

        void Update()
        {
            SteamLobbyManager lobby = SteamLobbyManager.Instance;
            LocalTestManager localTest = LocalTestManager.Instance;

            // In lobby if either Steam or local test says so
            bool inLobby = (lobby != null && lobby.InLobby) ||
                           (localTest != null && localTest.InLobby);

            if (menuRoot != null)
                menuRoot.SetActive(!inLobby);

            // Buttons interactable when Steam is ready OR in local test mode
            bool canConnect = (lobby != null && lobby.SteamReady) || localTest != null;
            if (hostButton != null) hostButton.interactable = canConnect;
            if (joinButton != null) joinButton.interactable = canConnect;

            // Status text
            if (statusText != null)
            {
                if (localTest != null)
                    statusText.text = "LOCAL TEST MODE";
                else if (lobby != null)
                    statusText.text = lobby.Status;
            }
        }

        void OnHostClicked()
        {
            if (LocalTestManager.Instance != null)
            {
                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();
                return;
            }
            if (SteamLobbyManager.Instance != null)
                SteamLobbyManager.Instance.HostGame();
        }

        void OnJoinClicked()
        {
            if (LocalTestManager.Instance != null)
            {
                if (codeInput != null)
                    InstanceFinder.ClientManager.StartConnection(codeInput.text);
                return;
            }
            if (SteamLobbyManager.Instance == null || codeInput == null) return;
            SteamLobbyManager.Instance.JoinByCode(codeInput.text);
        }

        void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}