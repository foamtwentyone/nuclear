using Nuclear.Items;
using Nuclear.Level;
using Nuclear.UI;
using Nuclear.Voice;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// HUD своего игрока в мультяшном стиле (HudDraw): слева внизу здоровье, выносливость, фильтр и доза,
    /// по центру внизу слоты снаряжения, в центре экрана прицел и подсказки, сверху — дверь бункера,
    /// поверх всего — итог уровня. У погибшего — подсказки призрака.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private static readonly Color HealthColor = new(0.9f, 0.24f, 0.22f);
        private static readonly Color StaminaColor = new(0.98f, 0.8f, 0.26f);
        private static readonly Color FilterColor = new(0.42f, 0.8f, 0.68f);
        private static readonly Color SafeAirColor = new(0.5f, 0.78f, 1f);
        private static readonly Color Alarm = new(1f, 0.35f, 0.25f);
        private static readonly Color Amber = new(1f, 0.75f, 0.3f);
        private static readonly Color Selected = new(1f, 0.84f, 0.3f);

        private PlayerController player;
        private PlayerCarry carry;
        private PlayerSurvival survival;
        private Dosimeter dosimeter;
        private PlayerVoice voice;
        private PlayerGhost ghost;
        private PlayerGear gear;
        private PlayerPry pry;

        // «Хвост» урона: светлая часть полоски здоровья, которая догоняет текущее значение.
        private float healthTrail = -1f;
        private float trailHoldUntil;
        private float lastHealth;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            carry = GetComponent<PlayerCarry>();
            survival = GetComponent<PlayerSurvival>();
            dosimeter = GetComponent<Dosimeter>();
            voice = GetComponent<PlayerVoice>();
            ghost = GetComponent<PlayerGhost>();
            gear = GetComponent<PlayerGear>();
            pry = GetComponent<PlayerPry>();
        }

        private void Update()
        {
            if (survival == null || !survival.IsSpawned)
                return;
            float health = survival.Health.Value;
            // После удара «хвост» полсекунды стоит на месте, потом догоняет.
            if (health < lastHealth - 0.5f)
                trailHoldUntil = Time.time + 0.5f;
            lastHealth = health;
            if (healthTrail < 0f || health > healthTrail)
                healthTrail = health;
            else if (health < healthTrail && Time.time >= trailHoldUntil)
                healthTrail = Mathf.MoveTowards(healthTrail, health, 40f * Time.deltaTime);
        }

        private void OnGUI()
        {
            if (player == null || !player.IsSpawned || !player.IsOwner)
                return;

            HudDraw.Begin();
            if (DrawOutcome())
                return;

            if (survival != null && survival.IsDead)
            {
                DrawGhost();
                return;
            }

            DrawDoorBanner();
            DrawCrosshair();
            DrawVitals();
            DrawHotbar();
            DrawProgress();
            DrawHint();
        }

        // ---------- Итог, призрак, дверь ----------

        /// <summary>Итог уровня поверх всего. Возвращает true, если уровень закончен.</summary>
        private bool DrawOutcome()
        {
            var outcome = LevelOutcome.Current;
            if (outcome == null || outcome.Value.Value == LevelOutcome.Result.Playing)
                return false;

            bool won = outcome.Value.Value == LevelOutcome.Result.Won;
            float h = Screen.height;
            HudDraw.Fill(new Rect(0f, h * 0.38f, Screen.width, h * 0.24f), new Color(0f, 0f, 0f, 0.45f));
            HudDraw.Label(new Rect(0f, h * 0.4f, Screen.width, h * 0.12f),
                won ? "Вы добрались до Бункера-12" : "Все погибли. Забег окончен",
                48f, won ? new Color(0.55f, 0.95f, 0.5f) : Alarm, TextAnchor.MiddleCenter);
            HudDraw.Label(new Rect(0f, h * 0.51f, Screen.width, h * 0.06f),
                "«Выйти» и «Создать игру» — начать заново", 20f, HudDraw.Cream, TextAnchor.MiddleCenter, bold: false);
            return true;
        }

        private void DrawGhost()
        {
            HudDraw.Label(new Rect(0f, HudDraw.S(24f), Screen.width, HudDraw.S(40f)),
                "Вы погибли и стали призраком", 26f, new Color(0.66f, 0.8f, 0.96f), TextAnchor.MiddleCenter);
            HudDraw.Label(new Rect(0f, HudDraw.S(62f), Screen.width, HudDraw.S(28f)),
                "Слепыш вас не слышит. Подсказывайте живым, где он", 17f, HudDraw.Cream, TextAnchor.MiddleCenter, bold: false);
            DrawCrosshair();
            if (ghost == null)
                return;

            float left = ghost.DistractCooldownLeft;
            string hint = left > 0f
                ? $"Шорох будет готов через {Mathf.CeilToInt(left)} с"
                : "ЛКМ — шорох там, куда смотрите (до 15 м): Слепыш пойдёт проверить";
            var color = HudDraw.Cream;
            if (Time.time - ghost.LastMissTime < 2f)
            {
                hint = "Слишком далеко — подлетите ближе";
                color = Amber;
            }
            CenterHint(hint, color, 0);
            HudDraw.Label(new Rect(0f, Screen.height - HudDraw.S(60f), Screen.width, HudDraw.S(30f)),
                "WASD — лететь   Пробел / Ctrl — вверх / вниз   Shift — быстрее   Не дальше 25 м от живых",
                15f, new Color(0.85f, 0.85f, 0.8f, 0.85f), TextAnchor.MiddleCenter, bold: false);
        }

        private void DrawDoorBanner()
        {
            var door = BunkerDoor.Current;
            if (door == null)
                return;

            string text = null;
            Color color = HudDraw.Cream;
            switch (door.State.Value)
            {
                case BunkerDoor.DoorState.Opening:
                    text = $"Дверь Бункера-12 открывается: {Mathf.FloorToInt(door.Progress.Value * 100f)}%. Шумит — держитесь!";
                    color = Amber;
                    break;
                case BunkerDoor.DoorState.Open:
                    text = "Дверь открыта — все внутрь!";
                    color = new Color(0.55f, 0.95f, 0.5f);
                    break;
                default:
                    if (Time.time - door.LastRejectTime < 3f)
                    {
                        text = "Аккумулятор разряжен — нужен другой";
                        color = Alarm;
                    }
                    else if (Vector3.Distance(transform.position, door.SocketPosition) < 5f)
                        text = "Поднесите аккумулятор к щитку справа от двери";
                    break;
            }

            if (text == null)
                return;
            HudDraw.Label(new Rect(0f, HudDraw.S(20f), Screen.width, HudDraw.S(40f)), text, 22f, color, TextAnchor.MiddleCenter);
            if (door.State.Value == BunkerDoor.DoorState.Opening)
            {
                float width = HudDraw.S(360f);
                HudDraw.Bar(new Rect((Screen.width - width) / 2f, HudDraw.S(64f), width, HudDraw.S(14f)), door.Progress.Value, Amber);
            }
        }

        // ---------- Прицел ----------

        private void DrawCrosshair()
        {
            bool interactive = !survival.IsDead && ((carry != null && (carry.LookTarget != null || carry.HeldItem != null))
                || (pry != null && pry.LookHood != null) || (gear != null && gear.LookingAtTeammate));
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (interactive)
            {
                float ring = HudDraw.S(22f);
                var rect = new Rect(center.x - ring / 2f, center.y - ring / 2f, ring, ring);
                HudDraw.Frame(rect, HudDraw.Ink, HudDraw.S(5f), ring / 2f);
                HudDraw.Frame(rect.Expand(-HudDraw.S(1f)), HudDraw.Cream, HudDraw.S(2.5f), ring / 2f);
            }
            float dot = HudDraw.S(7f);
            HudDraw.Fill(new Rect(center.x - dot / 2f, center.y - dot / 2f, dot, dot), HudDraw.Ink, dot / 2f);
            float inner = HudDraw.S(4f);
            HudDraw.Fill(new Rect(center.x - inner / 2f, center.y - inner / 2f, inner, inner), HudDraw.Cream, inner / 2f);
        }

        // ---------- Здоровье, выносливость, фильтр, доза ----------

        private void DrawVitals()
        {
            float s = HudDraw.Scale;
            var panel = new Rect(HudDraw.S(28f), Screen.height - HudDraw.S(28f + 146f), HudDraw.S(410f), HudDraw.S(146f));
            HudDraw.Panel(panel, 18f);
            float x = panel.x, y = panel.y;

            // Здоровье: светлый «хвост» недавнего урона, тёмно-оливковая часть — съеденная радиацией.
            float health = survival.Health.Value;
            float lost = 1f - survival.MaxHealth / 100f;
            bool critical = health < 30f;
            float pulse = critical ? 1f + 0.12f * HudDraw.Blink(9f) : 1f;
            float heart = 40f * s * pulse;
            HudDraw.Icon(new Rect(x + 34f * s - heart / 2f, y + 32f * s - heart / 2f, heart, heart), HudIcon.Heart, HealthColor);
            var healthBar = new Rect(x + 62f * s, y + 18f * s, 330f * s, 28f * s);
            HudDraw.Bar(healthBar, health / 100f, critical ? Color.Lerp(HealthColor, Alarm, HudDraw.Blink(9f)) : HealthColor, lost, healthTrail / 100f);
            HudDraw.Label(new Rect(healthBar.x, healthBar.y, healthBar.width - 12f * s, healthBar.height),
                Mathf.CeilToInt(health).ToString(), 19f, Color.white, TextAnchor.MiddleRight);

            // Выносливость.
            var stamina = player.Stamina;
            var staminaColor = stamina.IsExhausted ? Color.Lerp(StaminaColor, Alarm, HudDraw.Blink(8f)) : StaminaColor;
            HudDraw.Icon(new Rect(x + 20f * s, y + 54f * s, 28f * s, 28f * s), HudIcon.Stamina, staminaColor);
            HudDraw.Bar(new Rect(x + 62f * s, y + 60f * s, 330f * s, 16f * s), stamina.Normalized, staminaColor);

            // Фильтр противогаза.
            DrawFilterRow(x, y + 86f * s, s);

            // Доза и фон дозиметра.
            bool hot = dosimeter != null && dosimeter.Intensity > 0.01f;
            var radiation = hot ? Color.Lerp(new Color(0.85f, 0.95f, 0.3f), Color.white, HudDraw.Blink(10f)) : new Color(0.85f, 0.85f, 0.7f, 0.75f);
            HudDraw.Icon(new Rect(x + 22f * s, y + 114f * s, 24f * s, 24f * s), HudIcon.Radiation, radiation);
            string dose = $"Доза {survival.Dose.Value:0}";
            if (lost > 0.001f)
                dose += $"   макс. здоровье {Mathf.RoundToInt(survival.MaxHealth)}";
            if (hot)
                dose += $"   <color=#d8f060>+{dosimeter.Intensity:0.0}/с</color>";
            HudDraw.Label(new Rect(x + 62f * s, y + 112f * s, 340f * s, 28f * s), dose, 15f, HudDraw.Cream, bold: false);

            DrawVoice(new Vector2(panel.x, panel.y - 8f * s));
        }

        private void DrawFilterRow(float x, float y, float s)
        {
            bool swapping = survival.IsSwappingFilter.Value;
            bool empty = survival.FilterTime.Value <= 0f;
            bool safe = survival.InSafeAir;
            int seconds = Mathf.CeilToInt(survival.FilterTime.Value);

            Color color = safe ? SafeAirColor : swapping ? Amber : seconds <= 30 ? Color.Lerp(FilterColor, Alarm, HudDraw.Blink(6f)) : FilterColor;
            HudDraw.Icon(new Rect(x + 20f * s, y, 28f * s, 28f * s), HudIcon.Filter, empty && !safe && !swapping ? Alarm : color);

            var bar = new Rect(x + 62f * s, y + 6f * s, 236f * s, 16f * s);
            HudDraw.Bar(bar, survival.FilterTime.Value / survival.FilterDuration, color);

            string text;
            Color textColor = HudDraw.Cream;
            if (swapping)
            {
                text = "замена…";
                textColor = Amber;
            }
            else if (safe)
            {
                text = "чисто";
                textColor = SafeAirColor;
            }
            else if (empty)
            {
                text = "НЕТ!";
                textColor = Color.Lerp(Alarm, Color.white, HudDraw.Blink(8f));
            }
            else
                text = $"{seconds / 60}:{seconds % 60:00}";
            HudDraw.Label(new Rect(bar.xMax + 10f * s, y - 2f * s, 90f * s, 32f * s), text, 18f, textColor);
        }

        /// <summary>Микрофон над панелью, пока Слепыш может вас слышать.</summary>
        private void DrawVoice(Vector2 bottomLeft)
        {
            if (voice == null || voice.Current == Loudness.Quiet)
                return;
            float s = HudDraw.Scale;
            bool shout = voice.Current == Loudness.Shout;
            var color = shout ? Alarm : Amber;
            var rect = new Rect(bottomLeft.x, bottomLeft.y - 40f * s, 290f * s, 40f * s);
            HudDraw.Panel(rect, 14f, color);
            HudDraw.Icon(new Rect(rect.x + 8f * s, rect.y + 6f * s, 28f * s, 28f * s), HudIcon.Microphone, color);
            HudDraw.Label(new Rect(rect.x + 42f * s, rect.y, rect.width - 46f * s, rect.height),
                shout ? "КРИК — слышно за 20 м" : "Голос — слышно за 8 м", 16f, color);
        }

        // ---------- Слоты снаряжения ----------

        private void DrawHotbar()
        {
            if (gear == null)
                return;

            float s = HudDraw.Scale;
            const float big = 78f, small = 60f, gap = 10f, separator = 24f;
            float width = (big * 3f + gap * 2f + separator + small * 2f + gap) * s;
            float x = (Screen.width - width) / 2f;
            float bottom = Screen.height - 28f * s;

            int seconds = Mathf.CeilToInt(gear.FlashlightCharge.Value);
            bool lightOn = gear.FlashlightOn.Value;

            Slot(ref x, bottom, big, "1", HudIcon.Bandage, gear.Bandages.Value > 0, gear.Selected == PlayerGear.Slot.Bandage, null, 0f, false);
            Slot(ref x, bottom, big, "2", HudIcon.Flashlight, gear.HasFlashlight.Value, gear.Selected == PlayerGear.Slot.Flashlight,
                gear.HasFlashlight.Value ? $"{seconds / 60}:{seconds % 60:00}" : null,
                gear.HasFlashlight.Value ? gear.FlashlightCharge.Value / gear.FlashlightDuration : -1f, lightOn);
            Slot(ref x, bottom, big, "3", HudIcon.Crowbar, gear.HasCrowbar.Value, gear.Selected == PlayerGear.Slot.Crowbar, null, 0f, false);

            x += (separator - gap) * s;
            int filters = survival.SpareFilters.Value;
            Slot(ref x, bottom, small, "R", HudIcon.Filter, filters > 0, false, filters > 0 ? $"×{filters}" : null, 0f, false);
            int cells = gear.SpareBatteries.Value;
            Slot(ref x, bottom, small, null, HudIcon.Cell, cells > 0, false, cells > 0 ? $"×{cells}" : null, 0f, false);

            string action = gear.Selected switch
            {
                PlayerGear.Slot.Bandage when gear.Bandages.Value > 0 => gear.LookingAtTeammate ? "ПКМ — перевязать товарища" : "ПКМ — перевязать себя",
                PlayerGear.Slot.Flashlight when gear.HasFlashlight.Value => lightOn ? "ПКМ или F — выключить фонарь" : "ПКМ или F — включить фонарь",
                PlayerGear.Slot.Crowbar when gear.HasCrowbar.Value => "ПКМ — ударить Слепыша (оглушит, лом сломается)",
                _ => null,
            };
            if (action != null)
                HudDraw.Label(new Rect(0f, bottom - (big + 44f) * s, Screen.width, 30f * s), action, 16f, HudDraw.Cream, TextAnchor.MiddleCenter);
        }

        /// <summary>Один слот: подложка, клавиша, иконка; у выбранного — жёлтая рамка, и он приподнят.</summary>
        private static void Slot(ref float x, float bottom, float size, string key, HudIcon icon, bool has, bool selected,
            string caption, float charge, bool glowing)
        {
            float s = HudDraw.Scale;
            float side = size * s;
            float lift = selected ? 8f * s : 0f;
            var rect = new Rect(x, bottom - side - lift, side, side);
            x += side + 10f * s;

            HudDraw.Panel(rect, 14f, selected ? Selected : HudDraw.Ink, selected ? 3.5f : 2f);
            if (glowing)
                HudDraw.Fill(rect.Expand(-4f * s), new Color(1f, 0.9f, 0.5f, 0.18f), 12f * s);

            // Если внизу есть полоска заряда или счётчик, иконка чуть меньше и выше.
            bool footer = caption != null || (charge >= 0f && has);
            float iconSize = side * (footer ? 0.58f : 0.66f);
            var iconRect = new Rect(rect.center.x - iconSize / 2f, rect.center.y - iconSize / 2f - (footer ? 6f * s : 0f), iconSize, iconSize);
            var tint = !has ? new Color(1f, 1f, 1f, 0.16f) : glowing ? new Color(1f, 0.93f, 0.6f) : HudDraw.Cream;
            HudDraw.Icon(iconRect, icon, tint);

            if (key != null)
                HudDraw.Label(new Rect(rect.x + 7f * s, rect.y + 3f * s, 24f * s, 20f * s), key, 14f,
                    selected ? Selected : new Color(0.9f, 0.88f, 0.8f, 0.8f), TextAnchor.UpperLeft);

            if (charge >= 0f && has)
            {
                var bar = new Rect(rect.x + 8f * s, rect.yMax - 13f * s, side - 16f * s, 7f * s);
                HudDraw.Bar(bar, charge, charge < 0.2f ? Color.Lerp(Amber, Alarm, HudDraw.Blink(6f)) : new Color(1f, 0.92f, 0.6f));
            }
            else if (caption != null)
            {
                HudDraw.Label(new Rect(rect.x, rect.yMax - 22f * s, side, 20f * s), caption, 14f, HudDraw.Cream, TextAnchor.MiddleCenter);
            }
        }

        // ---------- Прогресс и подсказки в центре ----------

        private void DrawProgress()
        {
            if (gear != null && gear.BandageProgress.Value > 0f)
                CenterProgress("Перевязка… не двигайтесь", gear.BandageProgress.Value, new Color(0.95f, 0.92f, 0.85f));
            else if (pry != null && pry.LookHood is { } hood && hood.Progress.Value > 0f)
                CenterProgress(hood.WithCrowbar.Value ? "Вскрываете ломом… Слепыш слышит за 15 м" : "Вскрываете капот…", hood.Progress.Value, Amber);
        }

        private static void CenterProgress(string title, float progress, Color color)
        {
            float s = HudDraw.Scale;
            float width = 280f * s;
            float y = Screen.height / 2f + 70f * s;
            HudDraw.Label(new Rect(0f, y - 30f * s, Screen.width, 28f * s), title, 17f, HudDraw.Cream, TextAnchor.MiddleCenter);
            HudDraw.Bar(new Rect((Screen.width - width) / 2f, y, width, 16f * s), progress, color);
        }

        private void DrawHint()
        {
            if (carry == null)
                return;

            string hint = null, detail = null;
            Color color = HudDraw.Cream;
            var held = carry.HeldItem;
            var target = carry.LookTarget;
            if (held != null)
            {
                hint = $"{held.DisplayName}{ChargeText(held)}: E — отпустить";
                if (held.CanThrow)
                    hint += ", ЛКМ — бросить";
                if (held.CarrierCount.Value > 1)
                    detail = "Несёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    detail = "Тяжело. Позови помощь";
            }
            else if (target != null)
            {
                if (target.IsFull)
                    hint = $"{target.DisplayName}{ChargeText(target)}: уже несут";
                else if (target.TryGetComponent(out FilterPickup _) && survival.HasPocketSpace)
                    hint = $"E — в карман: {target.DisplayName}";
                else if (target.TryGetComponent(out GearPickup gearPickup) && gear != null && gear.HasSpaceFor(gearPickup.Kind))
                    hint = $"E — в карман: {target.DisplayName}";
                else
                    hint = $"E — взять: {target.DisplayName}{ChargeText(target)}";
            }
            else if (pry != null && pry.LookHood is { } hood && hood.Progress.Value <= 0f)
            {
                bool crowbar = gear != null && gear.HasCrowbar.Value;
                if (hood.PryerCount.Value == 1 && !hood.WithCrowbar.Value)
                {
                    hint = "Одному без лома не открыть — нужен второй игрок";
                    color = Amber;
                }
                else
                {
                    hint = crowbar ? "Держите E — вскрыть капот ломом" : "Держите E вдвоём — вскрыть капот";
                    detail = crowbar ? "Громко: Слепыш услышит за 15 м" : "Тихо, но медленно";
                }
            }

            if (hint == null)
                return;
            CenterHint(hint, color, 0);
            if (detail != null)
                CenterHint(detail, new Color(0.85f, 0.85f, 0.8f, 0.85f), 1);
        }

        /// <summary>Строка подсказки под прицелом; line — номер строки.</summary>
        private static void CenterHint(string text, Color color, int line)
        {
            float s = HudDraw.Scale;
            float y = Screen.height / 2f + (28f + line * 26f) * s;
            HudDraw.Label(new Rect(0f, y, Screen.width, 28f * s), text, line == 0 ? 19f : 15f, color, TextAnchor.MiddleCenter, bold: line == 0);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }

    internal static class RectExtensions
    {
        public static Rect Expand(this Rect rect, float amount) =>
            new(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }
}
