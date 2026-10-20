using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>Временная полоска выносливости для своего игрока.</summary>
    public class StaminaHud : MonoBehaviour
    {
        private PlayerController player;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner)
                return;

            const float width = 220f;
            const float height = 14f;
            var back = new Rect((Screen.width - width) / 2f, Screen.height - 40f, width, height);
            var fill = new Rect(back.x, back.y, width * player.Stamina.Normalized, height);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(back, Texture2D.whiteTexture);
            GUI.color = player.Stamina.IsExhausted ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.85f, 0.85f, 0.75f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
