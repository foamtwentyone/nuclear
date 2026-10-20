using Nuclear.Items;
using Nuclear.Level;
using Nuclear.Voice;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Временный HUD своего игрока: прицел, здоровье, выносливость, противогаз, дозиметр
    /// подсказки по предметам, состояние двери бункера и итог уровня.
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
        private PlayerVoice voice;
        private PlayerGhost ghost;
        private PlayerGear gear;
        private GUIStyle hintStyle;
        private GUIStyle statusStyle;
        private GUIStyle deathStyle;
        private GUIStyle bannerStyle;
        private GUIStyle gearStyle;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            carry = GetComponent<PlayerCarry>();
            survival = GetComponent<PlayerSurvival>();
            dosimeter = GetComponent<Dosimeter>();
            voice = GetComponent<PlayerVoice>();
            ghost = GetComponent<PlayerGhost>();
            gear = GetComponent<PlayerGear>();
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner)
                return;

            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, richText = true };
            statusStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontSize = 15, richText = true };
            deathStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 32, richText = true };
            bannerStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 18, richText = true };

            if (DrawOutcome())
                return;

            if (survival != null && survival.IsDead)
            {
                DrawGhost();
                return;
            }

            DrawDoorBanner();
            DrawCrosshair();
            DrawBars();
            DrawStatus();
            DrawGear();
            DrawHint();
        }

        /// <summary>Итог уровня поверх всего. Возвращает true, если уровень закончен.</summary>
        private bool DrawOutcome()
        {
            var outcome = LevelOutcome.Current;
            if (outcome == null || outcome.Value.Value == LevelOutcome.Result.Playing)
                return false;

            string text = outcome.Value.Value == LevelOutcome.Result.Won
                ? "<color=#7fe07f>Вы добрались до Бункера-12</color>"
                : "<color=#e64d33>Все погибли. Забег окончен</color>";
            text += "\n<size=16>«Выйти» и «Создать игру» — начать заново</size>";
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), text, deathStyle);
            return true;
        }

        private void DrawGhost()
        {
            GUI.Label(new Rect(0f, 24f, Screen.width, 60f),
                "<color=#a8c8f0>Вы погибли и стали призраком</color>\n<size=16>Слепыш вас не слышит. Подсказывайте живым, где он</size>", bannerStyle);
            DrawCrosshair();
            if (ghost == null)
                return;

            float left = ghost.DistractCooldownLeft;
            string hint = left > 0f
                ? $"Шорох будет готов через {Mathf.CeilToInt(left)} с"
                : "ЛКМ — шорох там, куда смотрите (до 15 м): Слепыш пойдёт проверить";
            if (Time.time - ghost.LastMissTime < 2f)
                hint = "<color=#e6b84d>Слишком далеко — подлетите ближе</color>";
            hint += "\n<size=13>WASD — лететь, Пробел / Ctrl — вверх / вниз, Shift — быстрее. Не дальше 25 м от живых</size>";
            GUI.Label(new Rect(0f, Screen.height / 2f + 30f, Screen.width, 60f), hint, hintStyle);
        }

        private void DrawDoorBanner()
        {
            var door = BunkerDoor.Current;
            if (door == null)
                return;

            string text = null;
            switch (door.State.Value)
            {
                case BunkerDoor.DoorState.Opening:
                    text = $"Дверь Бункера-12 открывается: {Mathf.FloorToInt(door.Progress.Value * 100f)}%. Шумит — держитесь!";
                    break;
                case BunkerDoor.DoorState.Open:
                    text = "<color=#7fe07f>Дверь открыта — все внутрь!</color>";
                    break;
                default:
                    if (Time.time - door.LastRejectTime < 3f)
                        text = "<color=#e64d33>Аккумулятор разряжен — нужен другой</color>";
                    else if (Vector3.Distance(transform.position, door.SocketPosition) < 5f)
                        text = "Поднесите аккумулятор к щитку справа от двери";
                    break;
            }

            if (text != null)
                GUI.Label(new Rect(0f, 16f, Screen.width, 40f), text, bannerStyle);
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

            string voiceLine = voice == null ? "" : voice.Current switch
            {
                Loudness.Shout => "\n<color=#e64d33>Голос: КРИК — слышно за 20 м</color>",
                Loudness.Speech => "\n<color=#e6b84d>Голос: слышно за 8 м</color>",
                _ => "",
            };

            GUI.Label(new Rect(16f, Screen.height - 116f, 420f, 100f), $"{air}\n{spare}\n{dose}{voiceLine}", statusStyle);
        }

        /// <summary>Карман снаряжения справа внизу: выбранный предмет подсвечен, под ним — что делает ПКМ.</summary>
        private void DrawGear()
        {
            if (gear == null)
                return;

            string Slot(PlayerGear.Slot slot, string key, string text, bool has)
            {
                string color = !has ? "#6b6b60" : gear.Selected == slot ? "#ffe680" : "#d9d9bf";
                string mark = gear.Selected == slot ? "> " : "  ";
                return $"<color={color}>{mark}[{key}] {text}</color>";
            }

            int seconds = Mathf.CeilToInt(gear.FlashlightCharge.Value);
            string light = gear.HasFlashlight.Value
                ? $"Фонарь {seconds / 60}:{seconds % 60:00}{(gear.FlashlightOn.Value ? " ВКЛ" : "")}"
                : "Фонарь —";
            if (gear.SpareBatteries.Value > 0)
                light += $" +{gear.SpareBatteries.Value} бат.";

            string text = Slot(PlayerGear.Slot.Bandage, "1", gear.Bandages.Value > 0 ? "Бинт" : "Бинт —", gear.Bandages.Value > 0) + "\n"
                + Slot(PlayerGear.Slot.Flashlight, "2", light, gear.HasFlashlight.Value) + "\n"
                + Slot(PlayerGear.Slot.Crowbar, "3", gear.HasCrowbar.Value ? "Лом" : "Лом —", gear.HasCrowbar.Value);

            string action = gear.Selected switch
            {
                PlayerGear.Slot.Bandage when gear.Bandages.Value > 0 => gear.LookingAtTeammate ? "ПКМ — перевязать товарища" : "ПКМ — перевязать себя",
                PlayerGear.Slot.Flashlight when gear.HasFlashlight.Value => "ПКМ или F — фонарь",
                PlayerGear.Slot.Crowbar when gear.HasCrowbar.Value => "ПКМ — ударить Слепыша (оглушит, лом сломается)",
                _ => null,
            };
            if (action != null)
                text += $"\n<size=13>{action}</size>";

            gearStyle ??= new GUIStyle(statusStyle) { alignment = TextAnchor.LowerRight };
            GUI.Label(new Rect(Screen.width - 436f, Screen.height - 116f, 420f, 100f), text, gearStyle);

            if (gear.BandageProgress.Value > 0f)
            {
                GUI.Label(new Rect(0f, Screen.height / 2f + 60f, Screen.width, 30f), "Перевязка… не двигайтесь", hintStyle);
                DrawBar(Screen.height / 2f + 90f, gear.BandageProgress.Value, 1f, new Color(0.9f, 0.9f, 0.8f));
            }
        }

        private void DrawHint()
        {
            if (carry == null)
                return;

            string hint = null;
            var held = carry.HeldItem;
            var target = carry.LookTarget;
            if (held != null)
            {
                hint = $"{held.DisplayName}{ChargeText(held)}: E — отпустить";
                if (held.CanThrow)
                    hint += ", ЛКМ — бросить";
                if (held.CarrierCount.Value > 1)
                    hint += "\nНесёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    hint += "\nТяжело. Позови помощь";
            }
            else if (target != null)
            {
                if (target.IsFull)
                    hint = $"{target.DisplayName}{ChargeText(target)}: уже несут";
                else if (target.TryGetComponent(out FilterPickup _) && survival != null && survival.HasPocketSpace)
                    hint = $"E — в карман: {target.DisplayName}";
                else if (target.TryGetComponent(out GearPickup gearPickup) && gear != null && gear.HasSpaceFor(gearPickup.Kind))
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
