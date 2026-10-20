using Nuclear.Items;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>Временный HUD своего игрока: прицел, выносливость, подсказки по предметам.</summary>
    public class PlayerHud : MonoBehaviour
    {
        private PlayerController player;
        private PlayerCarry carry;
        private GUIStyle hintStyle;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            carry = GetComponent<PlayerCarry>();
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner)
                return;

            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };

            DrawCrosshair();
            DrawStamina();
            DrawHint();
        }

        private static void DrawCrosshair()
        {
            const float size = 4f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawStamina()
        {
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

        private void DrawHint()
        {
            if (carry == null)
                return;

            string hint = null;
            if (carry.HeldItem is { } held)
            {
                hint = $"{held.DisplayName}{ChargeText(held)}: E — отпустить";
                if (held.CanThrow)
                    hint += ", ЛКМ — бросить";
                if (held.CarrierCount.Value > 1)
                    hint += "\nНесёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    hint += "\nТяжело. Позови помощь";
            }
            else if (carry.LookTarget is { } target)
            {
                hint = target.IsFull
                    ? $"{target.DisplayName}{ChargeText(target)}: уже несут"
                    : $"E — взять: {target.DisplayName}{ChargeText(target)}";
            }

            if (hint != null)
                GUI.Label(new Rect(0f, Screen.height / 2f + 30f, Screen.width, 50f), hint, hintStyle);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }
}
