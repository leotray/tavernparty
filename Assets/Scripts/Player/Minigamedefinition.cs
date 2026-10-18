using FishNet.Object;
using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Data for one minigame. Create one per minigame:
    /// right-click in the Project window -> Create -> TavernParty -> Minigame Definition.
    /// </summary>
    [CreateAssetMenu(menuName = "TavernParty/Minigame Definition", fileName = "NewMinigame")]
    public class MinigameDefinition : ScriptableObject
    {
        public string displayName = "New Minigame";

        [TextArea] public string description;

        [Tooltip("Name of the minigame scene. It must be added to Build Settings.")]
        public string sceneName;

        [Tooltip("Prefab with a NetworkObject and this minigame's script (a class extending MinigameBase). The game spawns it after the scene loads and despawns it when the round ends.")]
        public NetworkObject controllerPrefab;

        [Tooltip("Active players needed (this does NOT count the Troll Host). Use 0 for solo testing.")]
        public int minPlayers = 3;

        [Tooltip("Most active players this minigame supports (this does NOT count the Troll Host).")]
        public int maxPlayers = 7;

        [Tooltip("Round length in seconds. The minigame can also end early.")]
        public float durationSeconds = 60f;
    }
}