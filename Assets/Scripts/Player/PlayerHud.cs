using Nuclear.Items;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Временный HUD своего игрока: прицел, здоровье, выносливость, противогаз, дозиметр
    /// и подсказки по предметам.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private static readonly Color BarBack = new(0f, 0f, 0f, 0.5f);
        private static readonly Color Warning = new(0.9f, 0.3f, 0.2f);
        private static readonly Color Normal = new(0.85f, 0.85f, 0.75f);

        private PlayerController player;
        private PlayerCarry carry;
        private PlayerSurvival survival;
        private Dosimeter dosimeter;
        private GUIStyle hintStyle;
        private GUIStyle statusStyle;
        private GUIStyle deathStyle;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            carry = GetComponent<PlayerCarry>();
            survival = GetComponent<PlayerSurvival>();
            dosimeter = GetComponent<Dosimeter>();
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner)
                return;

            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            statusStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontSize = 15, richText = true };
            deathStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 32 };

            if (survival != null && survival.IsDead)
            {
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "Вы погибли", deathStyle);
                return;
            }

            DrawCrosshair();
            DrawBars();
            DrawStatus();
            DrawHint();
        }

        private static void DrawCrosshair()
        {
            const float size = 4f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f, size, size), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawBars()
        {
            DrawBar(Screen.height - 40f, player.Stamina.Normalized, 1f, player.Stamina.IsExhausted ? Warning : Normal);

            if (survival == null)
                return;

            // Тёмная часть справа — здоровье, отнятое радиацией.
            float maxShare = survival.MaxHealth / 100f;
            DrawBar(Screen.height - 60f, survival.Health.Value / 100f, maxShare, new Color(0.75f, 0.2f, 0.2f));
        }

        private static void DrawBar(float y, float fillShare, float backShare, Color color)
        {
            const float width = 220f;
            const float height = 14f;
            float x = (Screen.width - width) / 2f;

            GUI.color = BarBack;
            GUI.DrawTexture(new Rect(x, y, width * backShare, height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(fillShare), height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawStatus()
        {
            if (survival == null)
                return;

            string air;
            if (survival.IsSwappingFilter.Value)
                air = "<color=#e6b84d>Меняю фильтр…</color>";
            else if (survival.FilterTime.Value <= 0f)
                air = "<color=#e64d33>НЕТ ФИЛЬТРА</color>";
            else
            {
                int seconds = Mathf.CeilToInt(survival.FilterTime.Value);
                string time = $"{seconds / 60}:{seconds % 60:00}";
                air = seconds <= 30 ? $"Фильтр: <color=#e64d33>{time}</color>" : $"Фильтр: {time}";
            }

            if (survival.InSafeAir)
                air += "  <color=#7fd4ff>(чистый воздух)</color>";

            string spare = $"Запасных фильтров: {survival.SpareFilters.Value}";
            if (survival.SpareFilters.Value > 0 && !survival.IsSwappingFilter.Value)
                spare += "  (R — сменить)";

            string dose = $"Доза: {survival.Dose.Value:0}";
            if (dosimeter != null && dosimeter.Intensity > 0.01f)
                dose += $"  <color=#d4e64d>фон {dosimeter.Intensity:0.0}/с</color>";

            GUI.Label(new Rect(16f, Screen.height - 96f, 420f, 80f), $"{air}\n{spare}\n{dose}", statusStyle);
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
                if (target.IsFull)
                    hint = $"{target.DisplayName}{ChargeText(target)}: уже несут";
                else if (target.TryGetComponent(out FilterPickup _) && survival != null && survival.HasPocketSpace)
                    hint = $"E — в карман: {target.DisplayName}";
                else
                    hint = $"E — взять: {target.DisplayName}{ChargeText(target)}";
            }

            if (hint != null)
                GUI.Label(new Rect(0f, Screen.height / 2f + 30f, Screen.width, 50f), hint, hintStyle);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }
}
