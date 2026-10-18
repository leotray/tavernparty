using UnityEngine;

namespace TavernParty.Players
{
    /// <summary>
    /// Temporary debug UI for picking a skin in the lobby (replace with real UI later).
    /// Put this on any GameObject in the Lobby scene.
    /// </summary>
    public class LobbySkinPicker : MonoBehaviour
    {
        void OnGUI()
        {
            PlayerAvatar me = PlayerAvatar.Local;
            if (me == null || me.SkinCount == 0) return;

            int current = me.SkinIndex.Value;

            GUILayout.BeginArea(new Rect(10, Screen.height - 80, 360, 70), GUI.skin.box);
            GUILayout.Label("Skin: " + me.GetSkinName(current));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Previous"))
                me.RequestSkin((current - 1 + me.SkinCount) % me.SkinCount);
            if (GUILayout.Button("Next >"))
                me.RequestSkin((current + 1) % me.SkinCount);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}