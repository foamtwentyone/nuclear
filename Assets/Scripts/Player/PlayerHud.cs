using Nuclear.Items;
using Nuclear.Level;
using Nuclear.UI;
using Nuclear.Voice;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// HUD своего игрока в стиле игры (HudDraw): слева внизу оливковый планшет — здоровье, выносливость,
    /// ЖК-экран дозиметра и манометр фильтра; по центру внизу стальные ящики снаряжения;
    /// подсказки — на малярном скотче под прицелом; сверху — табличка двери; поверх всего — итог уровня.
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
            float h = Screen.height, s = HudDraw.Scale;
            var band = new Rect(0f, h * 0.36f, Screen.width, h * 0.26f);
            HudDraw.Fill(band, new Color(0.06f, 0.06f, 0.05f, 0.8f));
            HudDraw.HazardStrip(new Rect(-10f, band.y - 7f * s, Screen.width + 20f, 14f * s));
            HudDraw.HazardStrip(new Rect(-10f, band.yMax - 7f * s, Screen.width + 20f, 14f * s));
            HudDraw.Text(new Rect(0f, band.y + band.height * 0.12f, Screen.width, band.height * 0.5f),
                won ? "ВЫ ДОБРАЛИСЬ ДО БУНКЕРА-12" : "ВСЕ ПОГИБЛИ. ЗАБЕГ ОКОНЧЕН",
                54f, won ? new Color(0.6f, 0.95f, 0.45f) : Alarm, TextAnchor.MiddleCenter);
            HudDraw.Tape(new Vector2(Screen.width / 2f, band.y + band.height * 0.76f), HudDraw.Ink, 18f,
                ("", "«Выйти» и «Создать игру» — начать заново"));
            return true;
        }

        private void DrawGhost()
        {
            float s = HudDraw.Scale;
            var ghostBlue = new Color(0.66f, 0.82f, 0.98f);
            HudDraw.Text(new Rect(0f, 26f * s, Screen.width, 44f * s), "ВЫ ПОГИБЛИ И СТАЛИ ПРИЗРАКОМ", 30f, ghostBlue, TextAnchor.MiddleCenter);
            HudDraw.Text(new Rect(0f, 70f * s, Screen.width, 28f * s), "Слепыш вас не слышит. Подсказывайте живым, где он",
                17f, HudDraw.Cream, TextAnchor.MiddleCenter, HudDraw.Face.Body);
            DrawCrosshair();
            if (ghost == null)
                return;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f + 56f * s);
            float left = ghost.DistractCooldownLeft;
            if (Time.time - ghost.LastMissTime < 2f)
                HudDraw.Tape(center, new Color(0.6f, 0.25f, 0.05f), 18f, ("", "Слишком далеко — подлетите ближе"));
            else if (left > 0f)
                HudDraw.Tape(center, HudDraw.Ink, 18f, ("", $"Шорох будет готов через {Mathf.CeilToInt(left)} с"));
            else
                HudDraw.Tape(center, HudDraw.Ink, 18f, ("ЛКМ", "шорох там, куда смотрите (до 15 м)"));

            HudDraw.Tape(new Vector2(Screen.width / 2f, Screen.height - 50f * s), HudDraw.Ink, 15f,
                ("WASD", "лететь"), ("Пробел", "вверх"), ("Ctrl", "вниз"), ("Shift", "быстрее"), ("", "не дальше 25 м от живых"));
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
                    text = $"ДВЕРЬ ОТКРЫВАЕТСЯ: {Mathf.FloorToInt(door.Progress.Value * 100f)}% — ШУМИТ, ДЕРЖИТЕСЬ!";
                    color = Amber;
                    break;
                case BunkerDoor.DoorState.Open:
                    text = "ДВЕРЬ ОТКРЫТА — ВСЕ ВНУТРЬ!";
                    color = new Color(0.6f, 0.95f, 0.45f);
                    break;
                default:
                    if (Time.time - door.LastRejectTime < 3f)
                    {
                        text = "АККУМУЛЯТОР РАЗРЯЖЕН — НУЖЕН ДРУГОЙ";
                        color = Alarm;
                    }
                    else if (Vector3.Distance(transform.position, door.SocketPosition) < 5f)
                        text = "ПОДНЕСИТЕ АККУМУЛЯТОР К ЩИТКУ СПРАВА ОТ ДВЕРИ";
                    break;
            }
            if (text == null)
                return;

            float s = HudDraw.Scale;
            float width = HudDraw.Measure(text, 20f, HudDraw.Face.Heading) + 110f * s;
            var plate = new Rect((Screen.width - width) / 2f, 18f * s, width, 50f * s);
            HudDraw.Plate(plate, 10f, HudDraw.Steel);
            HudDraw.HazardStrip(new Rect(plate.x + 10f * s, plate.y + 12f * s, 26f * s, plate.height - 24f * s));
            HudDraw.HazardStrip(new Rect(plate.xMax - 36f * s, plate.y + 12f * s, 26f * s, plate.height - 24f * s));
            HudDraw.Text(plate, text, 20f, color, TextAnchor.MiddleCenter);
            if (door.State.Value == BunkerDoor.DoorState.Opening)
                HudDraw.Bar(new Rect(plate.x + 46f * s, plate.yMax + 8f * s, plate.width - 92f * s, 14f * s), door.Progress.Value, Amber, 10);
        }

        // ---------- Прицел ----------

        private void DrawCrosshair()
        {
            bool interactive = !survival.IsDead && ((carry != null && (carry.LookTarget != null || carry.HeldItem != null))
                || (pry != null && pry.LookHood != null) || (gear != null && gear.LookingAtTeammate));
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (interactive)
            {
                float ring = HudDraw.S(24f);
                var rect = new Rect(center.x - ring / 2f, center.y - ring / 2f, ring, ring);
                HudDraw.Frame(rect, HudDraw.Ink, HudDraw.S(6f), ring / 2f);
                HudDraw.Frame(HudDraw.Inset(rect, HudDraw.S(1.5f)), HudDraw.Cream, HudDraw.S(3f), ring / 2f);
            }
            HudDraw.Circle(center, HudDraw.S(4f), HudDraw.Ink);
            HudDraw.Circle(center, HudDraw.S(2.2f), HudDraw.Cream);
        }

        // ---------- Планшет: здоровье, выносливость, доза, манометр фильтра ----------

        private void DrawVitals()
        {
            float s = HudDraw.Scale;
            var panel = new Rect(28f * s, Screen.height - (28f + 176f) * s, 456f * s, 176f * s);
            float x = panel.x, y = panel.y;

            // Бирки над планшетом: номер выжившего и микрофон (когда вас слышно).
            HudDraw.Tag(new Rect(x + 22f * s, y - 24f * s, 158f * s, 30f * s), $"ВЫЖИВШИЙ №{player.OwnerClientId + 1}", 14f);
            DrawVoiceTag(new Rect(x + 190f * s, y - 24f * s, 200f * s, 30f * s));

            HudDraw.Plate(panel, 16f, HudDraw.Olive);
            HudDraw.Rivets(panel);

            // Здоровье: светлый «хвост» недавнего урона, справа в полоску — съеденное радиацией.
            float health = survival.Health.Value;
            float lost = 1f - survival.MaxHealth / 100f;
            bool critical = health < 30f;
            float heart = 46f * s * (critical ? 1f + 0.12f * HudDraw.Blink(9f) : 1f);
            HudDraw.Icon(new Rect(x + 42f * s - heart / 2f, y + 44f * s - heart / 2f, heart, heart), HudIcon.Heart, HealthColor);
            var healthBar = new Rect(x + 72f * s, y + 28f * s, 252f * s, 32f * s);
            HudDraw.Bar(healthBar, health / 100f, critical ? Color.Lerp(HealthColor, Alarm, HudDraw.Blink(9f)) : HealthColor, 10, lost, healthTrail / 100f);
            HudDraw.Text(new Rect(healthBar.x, healthBar.y, healthBar.width - 10f * s, healthBar.height),
                Mathf.CeilToInt(health).ToString(), 20f, Color.white, TextAnchor.MiddleRight);

            // Выносливость.
            var stamina = player.Stamina;
            var staminaColor = stamina.IsExhausted ? Color.Lerp(StaminaColor, Alarm, HudDraw.Blink(8f)) : StaminaColor;
            HudDraw.Icon(new Rect(x + 28f * s, y + 72f * s, 30f * s, 30f * s), HudIcon.Stamina, staminaColor);
            HudDraw.Bar(new Rect(x + 72f * s, y + 78f * s, 252f * s, 18f * s), stamina.Normalized, staminaColor, 5);

            DrawDosimeter(new Rect(x + 28f * s, y + 110f * s, 296f * s, 52f * s), lost);
            DrawFilterGauge(new Vector2(x + 392f * s, y + 80f * s), 54f * s);
        }

        /// <summary>ЖК-экран дозиметра: накопленная доза и текущий фон.</summary>
        private void DrawDosimeter(Rect lcd, float lost)
        {
            float s = HudDraw.Scale;
            bool hot = dosimeter != null && dosimeter.Intensity > 0.01f;
            HudDraw.Fill(lcd, HudDraw.LcdBack, 6f * s);
            if (hot)
                HudDraw.Fill(HudDraw.Inset(lcd, 3f * s), new Color(0.4f, 1f, 0.3f, 0.12f * HudDraw.Blink(10f)), 4f * s);
            // Строки-развёртка, как у дешёвого экранчика.
            for (float line = lcd.y + 5f * s; line < lcd.yMax - 4f * s; line += 4f * s)
                HudDraw.Fill(new Rect(lcd.x + 4f * s, line, lcd.width - 8f * s, 1f * s), new Color(0f, 0f, 0f, 0.25f));
            HudDraw.Frame(lcd, HudDraw.Ink, 3f * s, 6f * s);

            var lit = hot ? Color.Lerp(HudDraw.LcdText, Color.white, 0.3f * HudDraw.Blink(10f)) : new Color(0.38f, 0.66f, 0.3f);
            HudDraw.Icon(new Rect(lcd.x + 9f * s, lcd.y + 9f * s, 34f * s, 34f * s), HudIcon.Radiation, lit);
            HudDraw.Text(new Rect(lcd.x + 52f * s, lcd.y + 3f * s, 230f * s, 26f * s), $"ДОЗА {survival.Dose.Value:0000}", 20f, lit,
                TextAnchor.MiddleLeft, HudDraw.Face.Heading, outline: false);
            string line2 = hot ? $"ФОН +{dosimeter.Intensity:0.0}/с" : lost > 0.001f ? $"МАКС. ЗДОРОВЬЕ {Mathf.RoundToInt(survival.MaxHealth)}" : "ФОН В НОРМЕ";
            HudDraw.Text(new Rect(lcd.x + 52f * s, lcd.y + 26f * s, 230f * s, 22f * s), line2, 13f, lit * new Color(1f, 1f, 1f, 0.85f),
                TextAnchor.MiddleLeft, HudDraw.Face.Heading, outline: false);
        }

        /// <summary>Манометр фильтра: стрелка показывает остаток, в окошке — время или состояние.</summary>
        private void DrawFilterGauge(Vector2 center, float radius)
        {
            float s = HudDraw.Scale;
            bool swapping = survival.IsSwappingFilter.Value;
            bool safe = survival.InSafeAir;
            float time = survival.FilterTime.Value;
            int seconds = Mathf.CeilToInt(time);

            HudDraw.Dial(center, radius, swapping ? 0f : time / survival.FilterDuration, new Color(0.82f, 0.16f, 0.12f));

            string text;
            Color color = new(0.14f, 0.12f, 0.1f);
            if (swapping)
            {
                text = "ЗАМЕНА";
                color = new Color(0.7f, 0.4f, 0.05f);
            }
            else if (safe)
            {
                text = "ЧИСТО";
                color = new Color(0.15f, 0.42f, 0.75f);
            }
            else if (time <= 0f)
            {
                text = "НЕТ!";
                color = Color.Lerp(new Color(0.8f, 0.12f, 0.08f), new Color(0.3f, 0.05f, 0.03f), HudDraw.Blink(8f));
            }
            else
            {
                text = $"{seconds / 60}:{seconds % 60:00}";
                if (seconds <= 30)
                    color = Color.Lerp(new Color(0.8f, 0.12f, 0.08f), new Color(0.14f, 0.12f, 0.1f), HudDraw.Blink(6f));
            }
            HudDraw.Text(new Rect(center.x - radius, center.y + radius * 0.22f, radius * 2f, radius * 0.42f), text, 15f, color,
                TextAnchor.MiddleCenter, HudDraw.Face.Heading, outline: false);
            HudDraw.Tag(new Rect(center.x - 44f * s, center.y + radius - 6f * s, 88f * s, 24f * s), "ФИЛЬТР", 12f);
        }

        private void DrawVoiceTag(Rect rect)
        {
            if (voice == null || voice.Current == Loudness.Quiet)
                return;
            float s = HudDraw.Scale;
            bool shout = voice.Current == Loudness.Shout;
            var color = shout ? Color.Lerp(Alarm, new Color(1f, 0.6f, 0.5f), HudDraw.Blink(10f)) : Amber;
            HudDraw.Tag(rect, "", 14f, color);
            HudDraw.Icon(new Rect(rect.x + 6f * s, rect.y + 3f * s, 24f * s, 24f * s), HudIcon.Microphone, HudDraw.Ink);
            HudDraw.Text(new Rect(rect.x + 30f * s, rect.y, rect.width - 34f * s, rect.height),
                shout ? "КРИК · 20 М" : "ГОЛОС · 8 М", 14f, HudDraw.Ink, TextAnchor.MiddleCenter, HudDraw.Face.Heading, outline: false);
        }

        // ---------- Инвентарь ----------

        private void DrawHotbar()
        {
            if (gear == null)
                return;

            float s = HudDraw.Scale;
            const float size = 86f, gap = 14f;
            float width = (size * PlayerGear.SlotCount + gap * (PlayerGear.SlotCount - 1)) * s;
            float x = (Screen.width - width) / 2f;
            float bottom = Screen.height - 34f * s;

            for (int i = 0; i < PlayerGear.SlotCount; i++)
            {
                var slot = gear[i];
                bool filled = slot.Filled;
                bool flashlight = slot.Is(GearKind.Flashlight);
                Slot(ref x, bottom, size, (i + 1).ToString(), filled ? IconOf(slot.Kind) : null, filled, gear.Selected == i, 0,
                    flashlight ? slot.Charge / gear.FlashlightDuration : -1f, flashlight && gear.LightSlot.Value == i);
            }

            var selected = gear[gear.Selected];
            if (!selected.Filled)
                return;
            string use = gear.UseHint();
            var parts = use != null
                ? new[] { ("", GearPickup.NameOf(selected.Kind) + ":"), ("ПКМ", use), ("Q", "выбросить") }
                : new[] { ("", GearPickup.NameOf(selected.Kind) + ":"), ("Q", selected.Kind == GearKind.Can ? "бросить (отвлечь Слепыша)" : "выбросить") };
            HudDraw.Tape(new Vector2(Screen.width / 2f, bottom - (size + 46f) * s), HudDraw.Ink, 16f, parts);
        }

        private static HudIcon IconOf(GearKind kind) => kind switch
        {
            GearKind.Bandage => HudIcon.Bandage,
            GearKind.Flashlight => HudIcon.Flashlight,
            GearKind.Crowbar => HudIcon.Crowbar,
            GearKind.FlashlightBattery => HudIcon.Cell,
            GearKind.Filter => HudIcon.Filter,
            _ => HudIcon.Can,
        };

        /// <summary>
        /// Ячейка инвентаря: стальной ящик с заклёпками, жёлтая бирка с клавишей, иконка предмета.
        /// Выбранный приподнят, обведён жёлтым и подчёркнут полосой «опасность».
        /// </summary>
        private static void Slot(ref float x, float bottom, float size, string key, HudIcon? icon, bool has, bool selected,
            int count, float charge, bool glowing)
        {
            float s = HudDraw.Scale;
            float side = size * s;
            float lift = selected ? 10f * s : 0f;
            var rect = new Rect(x, bottom - side - lift, side, side);
            x += side + 14f * s;

            HudDraw.Plate(rect, 12f, HudDraw.Steel, selected ? Selected : null, selected ? 4f : 3f);
            if (glowing)
                HudDraw.Fill(HudDraw.Inset(rect, 5f * s), new Color(1f, 0.88f, 0.45f, 0.22f), 9f * s);
            if (selected)
                HudDraw.HazardStrip(new Rect(rect.x + 4f * s, bottom + 2f * s, side - 8f * s, 8f * s));

            bool footer = charge >= 0f && has;
            float iconSize = side * (footer ? 0.56f : 0.64f);
            var iconRect = new Rect(rect.center.x - iconSize / 2f, rect.center.y - iconSize / 2f - (footer ? 7f * s : 0f), iconSize, iconSize);
            var tint = !has ? new Color(1f, 1f, 1f, 0.13f) : glowing ? new Color(1f, 0.94f, 0.62f) : HudDraw.Cream;
            if (icon.HasValue)
                HudDraw.Icon(iconRect, icon.Value, tint);

            if (key != null)
                HudDraw.Tag(new Rect(rect.x - 7f * s, rect.y - 9f * s, 28f * s, 26f * s), key, 15f, selected ? Selected : HudDraw.HazardYellow);

            if (footer)
            {
                var bar = new Rect(rect.x + 10f * s, rect.yMax - 18f * s, side - 20f * s, 10f * s);
                HudDraw.Bar(bar, charge, charge < 0.2f ? Color.Lerp(Amber, Alarm, HudDraw.Blink(6f)) : new Color(1f, 0.92f, 0.55f), 4);
            }

            if (count > 0)
            {
                var badge = new Vector2(rect.xMax - 4f * s, rect.yMax - 4f * s);
                HudDraw.Circle(badge, 14f * s, HudDraw.Ink);
                HudDraw.Circle(badge, 11f * s, new Color(0.28f, 0.28f, 0.26f));
                HudDraw.Text(new Rect(badge.x - 14f * s, badge.y - 14f * s, 28f * s, 28f * s), count.ToString(), 15f, HudDraw.Cream,
                    TextAnchor.MiddleCenter, HudDraw.Face.Heading, outline: false);
            }
        }

        // ---------- Прогресс и подсказки в центре ----------

        private void DrawProgress()
        {
            if (gear != null && gear.BandageProgress.Value > 0f)
                CenterProgress("Перевязка… не двигайтесь", gear.BandageProgress.Value, new Color(0.95f, 0.92f, 0.85f));
            else if (pry != null && pry.LookHood is { } hood && hood.Progress.Value > 0f)
                CenterProgress(hood.WithCrowbar.Value ? "Вскрываете ломом… Слепыш слышит за 15 м" : "Вскрываете капот вдвоём…", hood.Progress.Value, Amber);
        }

        private static void CenterProgress(string title, float progress, Color color)
        {
            float s = HudDraw.Scale;
            var tape = HudDraw.Tape(new Vector2(Screen.width / 2f, Screen.height / 2f + 56f * s), HudDraw.Ink, 17f, ("", title));
            float width = Mathf.Max(260f * s, tape.width - 30f * s);
            HudDraw.Bar(new Rect((Screen.width - width) / 2f, tape.yMax + 8f * s, width, 18f * s), progress, color, 8);
        }

        private void DrawHint()
        {
            if (carry == null || (gear != null && gear.BandageProgress.Value > 0f))
                return;

            (string, string)[] parts = null;
            string detail = null;
            Color color = HudDraw.Ink;
            var held = carry.HeldItem;
            var target = carry.LookTarget;
            if (held != null)
            {
                string name = $"{held.DisplayName}{ChargeText(held)}";
                parts = held.CanThrow
                    ? new[] { ("E", $"отпустить: {name}"), ("ЛКМ", "бросить") }
                    : new[] { ("E", $"отпустить: {name}") };
                if (held.CarrierCount.Value > 1)
                    detail = "Несёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    detail = "Тяжело. Позови помощь";
            }
            else if (target != null)
            {
                if (target.IsFull)
                    parts = new[] { ("", $"{target.DisplayName}{ChargeText(target)}: уже несут") };
                else if (target.TryGetComponent(out GearPickup _) && gear != null && gear.HasSpace)
                    parts = new[] { ("E", $"в инвентарь: {target.DisplayName}") };
                else
                    parts = new[] { ("E", $"взять: {target.DisplayName}{ChargeText(target)}") };
            }
            else if (pry != null && pry.LookHood is { } hood && hood.Progress.Value <= 0f)
            {
                bool crowbar = gear != null && gear.HasCrowbar;
                if (hood.PryerCount.Value == 1 && !hood.WithCrowbar.Value)
                {
                    parts = new[] { ("", "Одному без лома не открыть — нужен второй игрок") };
                    color = new Color(0.6f, 0.25f, 0.05f);
                }
                else
                {
                    parts = new[] { ("E", crowbar ? "держать — вскрыть капот ломом" : "держать вдвоём — вскрыть капот") };
                    detail = crowbar ? "Громко: Слепыш услышит за 15 м" : "Тихо, но медленно";
                }
            }

            if (parts == null)
                return;
            float s = HudDraw.Scale;
            var tape = HudDraw.Tape(new Vector2(Screen.width / 2f, Screen.height / 2f + 56f * s), color, 17f, parts);
            if (detail != null)
                HudDraw.Text(new Rect(0f, tape.yMax + 6f * s, Screen.width, 26f * s), detail.ToUpperInvariant(), 14f, HudDraw.Cream,
                    TextAnchor.MiddleCenter);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }

}
