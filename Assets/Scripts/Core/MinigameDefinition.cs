using UnityEngine;

namespace TavernParty.Core
{
    /// <summary>
    /// One asset per minigame. Drag it into GameManager's minigame list.
    /// </summary>
    [CreateAssetMenu(menuName = "TavernParty/Minigame Definition", fileName = "NewMinigame")]
    public class MinigameDefinition : ScriptableObject
    {
        public string displayName = "New Minigame";
        [TextArea] public string description;

        [Tooltip("Scene name (must be in Build Settings).")]
        public string sceneName;

        [Tooltip("Seconds the round lasts.")]
        public float durationSeconds = 30f;

        [Tooltip("Points active players receive for surviving to the end.")]
        public int survivalPoints = 100;
    }
}
