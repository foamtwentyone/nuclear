using Nuclear.Items;
using Nuclear.Level;
using Nuclear.UI;
using Nuclear.Voice;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// HUD своего игрока в духе Peak, но как «тетрадь выжившего»: всё нарисовано маркером от руки (HudDraw).
    /// Слева внизу — одна полоса жизни (радиация съедает её справа штриховкой, удушье пульсирует у края),
    /// над ней штрих выносливости, значки фильтра и дозы, штамп «НЕТ ФИЛЬТРА»; по центру внизу — 4 бумажных
    /// кружка инвентаря, название выбранного предмета подписано от руки и гаснет; под прицелом — подсказки.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private static readonly Color HealthColor = new(0.88f, 0.27f, 0.22f);
        private static readonly Color StaminaColor = new(0.98f, 0.84f, 0.4f);
        private static readonly Color FilterColor = new(0.45f, 0.7f, 0.6f);
        private static readonly Color SafeAirColor = new(0.55f, 0.8f, 1f);
        private static readonly Color RadiationColor = new(0.85f, 0.95f, 0.4f);
        private static readonly Color Alarm = new(1f, 0.38f, 0.28f);
        private static readonly Color Amber = new(1f, 0.78f, 0.35f);
        private static readonly Color Good = new(0.6f, 0.95f, 0.5f);

        private const float NameShowTime = 2.5f;

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

        // Плавное появление: выносливость, доза, название выбранного предмета.
        private float staminaAlpha;
        private float doseAlpha;
        private float nameAlpha;
        private float nameShownAt = -10f;
        private int lastSelected = -1;
        private InventorySlot lastSelectedSlot;

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

            float fade = 4f * Time.deltaTime;
            bool staminaUsed = player.Stamina.Normalized < 0.995f || player.Stamina.IsExhausted;
            staminaAlpha = Mathf.MoveTowards(staminaAlpha, staminaUsed ? 1f : 0f, staminaUsed ? fade * 2f : fade * 0.5f);
            bool doseMatters = (dosimeter != null && dosimeter.Intensity > 0.01f) || survival.MaxHealth < 99.5f;
            doseAlpha = Mathf.MoveTowards(doseAlpha, doseMatters ? 1f : 0f, fade);

            if (gear != null)
            {
                var slot = gear[gear.Selected];
                // Заряд фонаря меняется постоянно — показываем название только при смене ячейки или предмета в ней.
                bool otherItem = slot.Filled != lastSelectedSlot.Filled || slot.Kind != lastSelectedSlot.Kind;
                if (gear.Selected != lastSelected || otherItem)
                    nameShownAt = Time.time;
                lastSelected = gear.Selected;
                lastSelectedSlot = slot;
                bool showName = slot.Filled && Time.time - nameShownAt < NameShowTime;
                nameAlpha = Mathf.MoveTowards(nameAlpha, showName ? 1f : 0f, showName ? fade * 2f : fade * 0.6f);
            }
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
            DrawInventory();
            DrawCenter();
        }

        // ---------- Итог, призрак, дверь ----------

        /// <summary>Итог уровня поверх всего — большой штамп. Возвращает true, если уровень закончен.</summary>
        private bool DrawOutcome()
        {
            var outcome = LevelOutcome.Current;
            if (outcome == null || outcome.Value.Value == LevelOutcome.Result.Playing)
                return false;

            bool won = outcome.Value.Value == LevelOutcome.Result.Won;
            float h = Screen.height, s = HudDraw.Scale;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.5f), 0f, 0f);
            var center = new Vector2(Screen.width / 2f, h * 0.45f);
            HudDraw.Stamp(center, won ? "ДОБРАЛИСЬ" : "ВСЕ ПОГИБЛИ", 54f, won ? new Color(0.35f, 0.75f, 0.3f, 0.95f) : HudDraw.StampRed, -5f, 900);
            HudDraw.Text(new Rect(0f, center.y + 70f * s, Screen.width, 36f * s),
                won ? "Бункер-12 открыт. Вы внутри." : "Забег окончен", 26f, HudDraw.Cream, TextAnchor.MiddleCenter);
            HudDraw.Text(new Rect(0f, center.y + 104f * s, Screen.width, 30f * s),
                "«Выйти» и «Создать игру» — начать заново", 19f, HudDraw.Dim, TextAnchor.MiddleCenter);
            return true;
        }

        private void DrawGhost()
        {
            float s = HudDraw.Scale;
            var ghostBlue = new Color(0.72f, 0.85f, 1f);
            HudDraw.Text(new Rect(0f, 26f * s, Screen.width, 40f * s), "Вы призрак", 32f, ghostBlue, TextAnchor.MiddleCenter);
            HudDraw.Text(new Rect(0f, 64f * s, Screen.width, 28f * s), "Слепыш вас не слышит. Подсказывайте живым, где он",
                19f, HudDraw.Dim, TextAnchor.MiddleCenter);
            DrawCrosshair();
            if (ghost == null)
                return;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f + 50f * s);
            float left = ghost.DistractCooldownLeft;
            if (Time.time - ghost.LastMissTime < 2f)
                HudDraw.KeyHints(center, 22f, Amber, ("", "Слишком далеко — подлетите ближе"));
            else if (left > 0f)
                HudDraw.KeyHints(center, 22f, HudDraw.Dim, ("", $"Шорох через {Mathf.CeilToInt(left)} с"));
            else
                HudDraw.KeyHints(center, 22f, HudDraw.Cream, ("ЛКМ", "шорох там, куда смотрите"));

            HudDraw.KeyHints(new Vector2(Screen.width / 2f, Screen.height - 46f * s), 19f, HudDraw.Dim,
                ("WASD", "лететь"), ("Пробел", "вверх"), ("Ctrl", "вниз"), ("Shift", "быстрее"));
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
                    text = "Дверь открывается — держитесь!";
                    color = Amber;
                    break;
                case BunkerDoor.DoorState.Open:
                    text = "Дверь открыта — все внутрь!";
                    color = Good;
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

            float s = HudDraw.Scale;
            HudDraw.Text(new Rect(0f, 22f * s, Screen.width, 40f * s), text, 28f, color, TextAnchor.MiddleCenter);
            if (door.State.Value == BunkerDoor.DoorState.Opening)
                HudDraw.Bar(new Rect((Screen.width - 280f * s) / 2f, 66f * s, 280f * s, 18f * s), door.Progress.Value, Amber, 300);
        }

        // ---------- Прицел ----------

        private void DrawCrosshair()
        {
            bool interactive = !survival.IsDead && ((carry != null && (carry.LookTarget != null || carry.HeldItem != null))
                || (pry != null && pry.LookHood != null) || (gear != null && gear.LookingAtTeammate));
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float s = HudDraw.Scale;
            if (interactive)
            {
                var ring = HudSketch.Circle(center, 11f * s, 7 + HudSketch.Boil / 31 % 3, 1f * s);
                HudSketch.Stroke(ring, 5f * s, HudDraw.WithAlpha(HudDraw.Ink, 0.7f), true, 7);
                HudSketch.Stroke(ring, 2.2f * s, HudDraw.Cream, true, 7);
            }
            HudSketch.Fill(HudSketch.Circle(center, 3.6f * s, 3, 0f), HudDraw.WithAlpha(HudDraw.Ink, 0.7f));
            HudSketch.Fill(HudSketch.Circle(center, 2.2f * s, 3, 0f), HudDraw.Cream);
        }

        // ---------- Здоровье, фильтр, выносливость, доза ----------

        private bool Choking => !survival.InSafeAir && (survival.IsSwappingFilter.Value || survival.FilterTime.Value <= 0f);

        private void DrawVitals()
        {
            float s = HudDraw.Scale;
            float x = 40f * s;
            float baseY = Screen.height - 54f * s;

            // Полоса жизни, как в Peak: радиация съедает её справа (штриховка), удушье пульсирует фиолетовым у края.
            float health = survival.Health.Value;
            bool critical = health < 30f;
            var healthColor = critical ? Color.Lerp(HealthColor, Alarm, HudDraw.Blink(9f)) : HealthColor;
            var bar = new Rect(x + 30f * s, baseY - 15f * s, 300f * s, 30f * s);
            HudDraw.Bar(bar, health / 100f, healthColor, 100, 1f - survival.MaxHealth / 100f, healthTrail / 100f,
                Choking ? 0.15f : 0f);
            float heart = 50f * s * (critical ? 1f + 0.12f * HudDraw.Blink(9f) : 1f);
            HudDraw.Icon(new Rect(x + 22f * s - heart / 2f, baseY - heart / 2f, heart, heart), HudIcon.Heart, healthColor);
            HudDraw.Text(new Rect(bar.xMax + 10f * s, baseY - 18f * s, 70f * s, 36f * s), Mathf.CeilToInt(health).ToString(), 30f, HudDraw.Cream);

            // Выносливость — штрих маркером над полосой, виден только пока тратится.
            if (staminaAlpha > 0.01f)
            {
                var stamina = player.Stamina;
                var color = stamina.IsExhausted ? Color.Lerp(StaminaColor, Alarm, HudDraw.Blink(8f)) : StaminaColor;
                HudDraw.InkLine(new Vector2(bar.x + 8f * s, bar.y - 12f * s), (bar.width - 16f * s) * stamina.Normalized, 5f * s, color, 120, staminaAlpha);
            }

            DrawFilter(new Vector2(x + 22f * s, baseY - 64f * s));

            // Доза — значок выше, только когда растёт или уже срезала здоровье.
            if (doseAlpha > 0.01f)
            {
                bool hot = dosimeter != null && dosimeter.Intensity > 0.01f;
                var center = new Vector2(x + 22f * s, baseY - 112f * s);
                HudDraw.Badge(center, 20f, HudIcon.Radiation, RadiationColor, hot ? 140 + HudSketch.Boil : 140, doseAlpha);
                string text = $"{survival.Dose.Value:0}";
                if (hot)
                    text += $"   +{dosimeter.Intensity:0.0} в сек.";
                HudDraw.Text(new Rect(center.x + 30f * s, center.y - 18f * s, 260f * s, 36f * s), text, 26f,
                    HudDraw.WithAlpha(hot ? RadiationColor : HudDraw.Cream, doseAlpha));
            }

            if (voice != null && voice.Current != Loudness.Quiet)
            {
                bool shout = voice.Current == Loudness.Shout;
                // В левом столбце над фильтром и дозой: справа от полосы на узких экранах начинается инвентарь.
                var center = new Vector2(x + 22f * s, baseY - (doseAlpha > 0.01f ? 160f : 112f) * s);
                HudDraw.Badge(center, 18f, HudIcon.Microphone, shout ? Alarm : Amber, shout ? 160 + HudSketch.Boil : 160);
                HudDraw.Text(new Rect(center.x + 26f * s, center.y - 17f * s, 80f * s, 34f * s), shout ? "20 м!" : "8 м", 24f, shout ? Alarm : Amber);
            }
        }

        private void DrawFilter(Vector2 center)
        {
            float s = HudDraw.Scale;
            bool swapping = survival.IsSwappingFilter.Value;
            float time = survival.FilterTime.Value;
            int seconds = Mathf.CeilToInt(time);
            bool alarm = !survival.InSafeAir && !swapping && (time <= 0f || seconds <= 30);

            Color tint = survival.InSafeAir ? SafeAirColor : swapping ? Amber : alarm ? Alarm : FilterColor;
            HudDraw.Badge(center, 20f, HudIcon.Filter, tint, alarm ? 130 + HudSketch.Boil : 130);
            var textRect = new Rect(center.x + 30f * s, center.y - 18f * s, 240f * s, 36f * s);

            if (swapping)
                HudDraw.Text(textRect, "меняю фильтр…", 24f, Amber);
            else if (survival.InSafeAir)
                HudDraw.Text(textRect, "чистый воздух", 24f, SafeAirColor);
            else if (time <= 0f)
            {
                bool spare = gear != null && gear.IndexOf(GearKind.Filter) >= 0;
                HudDraw.Stamp(new Vector2(center.x + 128f * s, center.y), "НЕТ ФИЛЬТРА", 18f, HudDraw.StampRed, -6f, 133 + HudSketch.Boil % 62);
                if (spare)
                    HudDraw.KeyHints(new Vector2(center.x + 286f * s, center.y), 21f, HudDraw.Cream, ("R", "сменить"));
            }
            else
            {
                var color = seconds <= 30 ? Color.Lerp(Alarm, HudDraw.Cream, HudDraw.Blink(6f)) : HudDraw.Cream;
                HudDraw.Text(textRect, $"{seconds / 60}:{seconds % 60:00}", 28f, color);
            }
        }

        // ---------- Инвентарь ----------

        private void DrawInventory()
        {
            if (gear == null)
                return;

            float s = HudDraw.Scale;
            const float radius = 30f, spacing = 80f;
            float centerY = Screen.height - 62f * s;
            float x0 = Screen.width / 2f - spacing * (PlayerGear.SlotCount - 1) / 2f * s;

            for (int i = 0; i < PlayerGear.SlotCount; i++)
            {
                var slot = gear[i];
                bool selected = gear.Selected == i;
                var center = new Vector2(x0 + i * spacing * s, centerY - (selected ? 6f * s : 0f));
                float r = (selected ? radius * 1.18f : radius) * s;
                bool lit = slot.Is(GearKind.Flashlight) && gear.LightSlot.Value == i;

                if (lit)
                    HudSketch.Fill(HudSketch.Circle(center, r + 9f * s, 200 + i, 2f * s), new Color(1f, 0.9f, 0.5f, 0.3f));
                var circle = HudSketch.Circle(center, r, 210 + i * 13, 1.5f * s);
                HudSketch.Fill(circle, HudDraw.WithAlpha(HudDraw.Paper, selected ? 1f : 0.72f));
                HudSketch.Stroke(circle, (selected ? 4.5f : 3f) * s, HudDraw.WithAlpha(HudDraw.Ink, selected ? 1f : 0.8f), true, 210 + i * 13);

                // Цифра снаружи кружка, по диагонали сверху слева — не лежит на обводке.
                HudDraw.Text(new Rect(center.x - r - 22f * s, center.y - r - 22f * s, 30f * s, 30f * s), (i + 1).ToString(),
                    selected ? 24f : 20f, HudDraw.WithAlpha(HudDraw.Cream, selected ? 1f : 0.75f), TextAnchor.MiddleCenter);

                if (!slot.Filled)
                    continue;
                float icon = r * 1.35f;
                var tint = lit ? new Color(1f, 0.9f, 0.55f) : IconTint(slot.Kind);
                HudDraw.Icon(new Rect(center.x - icon / 2f, center.y - icon / 2f, icon, icon), IconOf(slot.Kind), tint);

                if (slot.Is(GearKind.Flashlight))
                {
                    float charge = slot.Charge / gear.FlashlightDuration;
                    var color = charge < 0.2f ? Color.Lerp(Amber, Alarm, HudDraw.Blink(6f)) : new Color(1f, 0.88f, 0.45f);
                    HudDraw.InkLine(new Vector2(center.x - r * 0.7f, center.y + r + 9f * s), r * 1.4f * charge, 4f * s, color, 220 + i);
                }
            }

            // Название выбранного предмета и что с ним делать — подписано от руки, появляется при выборе и гаснет.
            var selectedSlot = gear[gear.Selected];
            if (nameAlpha <= 0.01f || !selectedSlot.Filled)
                return;
            float top = centerY - radius * 1.18f * s - 30f * s;
            HudDraw.Text(new Rect(0f, top - 82f * s, Screen.width, 38f * s), GearPickup.NameOf(selectedSlot.Kind), 30f,
                HudDraw.WithAlpha(HudDraw.Cream, nameAlpha), TextAnchor.MiddleCenter);
            string use = gear.UseHint();
            var dim = HudDraw.WithAlpha(HudDraw.Dim, nameAlpha);
            string drop = selectedSlot.Kind == GearKind.Can ? "бросить" : "выбросить";
            var hintCenter = new Vector2(Screen.width / 2f, top - 30f * s);
            if (use != null)
                HudDraw.KeyHints(hintCenter, 20f, dim, ("ПКМ", use), ("Q", drop));
            else
                HudDraw.KeyHints(hintCenter, 20f, dim, ("Q", drop));
        }

        /// <summary>Цвет «краски» иконки на бумажном кружке — как раскрашенный рисунок в тетради.</summary>
        private static Color IconTint(GearKind kind) => kind switch
        {
            GearKind.Bandage => new Color(0.97f, 0.94f, 0.88f),
            GearKind.Flashlight => new Color(0.55f, 0.62f, 0.4f),
            GearKind.Crowbar => new Color(0.82f, 0.3f, 0.24f),
            GearKind.FlashlightBattery => new Color(0.95f, 0.72f, 0.3f),
            GearKind.Filter => new Color(0.5f, 0.6f, 0.4f),
            _ => new Color(0.75f, 0.42f, 0.3f),
        };

        private static HudIcon IconOf(GearKind kind) => kind switch
        {
            GearKind.Bandage => HudIcon.Bandage,
            GearKind.Flashlight => HudIcon.Flashlight,
            GearKind.Crowbar => HudIcon.Crowbar,
            GearKind.FlashlightBattery => HudIcon.Cell,
            GearKind.Filter => HudIcon.Filter,
            _ => HudIcon.Can,
        };

        // ---------- Подсказки и прогресс под прицелом ----------

        private void DrawCenter()
        {
            float s = HudDraw.Scale;
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f + 50f * s);

            if (gear != null && gear.BandageProgress.Value > 0f)
            {
                Progress(center, "Перевязка… не двигайтесь", gear.BandageProgress.Value, HealthColor);
                return;
            }
            if (pry != null && pry.LookHood is { } prying && prying.Progress.Value > 0f)
            {
                Progress(center, prying.WithCrowbar.Value ? "Вскрываете ломом — это слышно!" : "Вскрываете вдвоём…", prying.Progress.Value, Amber);
                return;
            }
            if (carry == null)
                return;

            (string, string)[] parts = null;
            string detail = null;
            Color color = HudDraw.Cream;
            var held = carry.HeldItem;
            var target = carry.LookTarget;
            if (held != null)
            {
                parts = held.CanThrow ? new[] { ("E", "отпустить"), ("ЛКМ", "бросить") } : new[] { ("E", "отпустить") };
                detail = $"{held.DisplayName}{ChargeText(held)}";
                if (held.CarrierCount.Value > 1)
                    detail += " — несёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    detail += " — тяжело, позови помощь";
            }
            else if (target != null)
            {
                if (target.IsFull)
                    parts = new[] { ("", $"{target.DisplayName}: уже несут") };
                else if (target.TryGetComponent(out GearPickup _) && gear != null && gear.HasSpace)
                    parts = new[] { ("E", $"взять: {target.DisplayName}") };
                else
                    parts = new[] { ("E", $"нести: {target.DisplayName}{ChargeText(target)}") };
            }
            else if (pry != null && pry.LookHood is { } hood)
            {
                bool crowbar = gear != null && gear.HasCrowbar;
                if (hood.PryerCount.Value == 1 && !hood.WithCrowbar.Value)
                {
                    parts = new[] { ("", "Одному без лома не открыть — нужен второй игрок") };
                    color = Amber;
                }
                else
                {
                    parts = new[] { ("E", crowbar ? "держать — вскрыть капот" : "держать вдвоём — вскрыть капот") };
                    detail = crowbar ? "ломом быстро, но громко" : "без лома тихо, но медленно";
                }
            }

            if (parts == null)
                return;
            HudDraw.KeyHints(center, 24f, color, parts);
            if (detail != null)
                HudDraw.Text(new Rect(0f, center.y + 18f * s, Screen.width, 30f * s), detail, 19f, HudDraw.Dim, TextAnchor.MiddleCenter);
        }

        private static void Progress(Vector2 center, string title, float progress, Color color)
        {
            float s = HudDraw.Scale;
            HudDraw.Text(new Rect(0f, center.y - 18f * s, Screen.width, 34f * s), title, 24f, HudDraw.Cream, TextAnchor.MiddleCenter);
            HudDraw.Bar(new Rect(center.x - 120f * s, center.y + 18f * s, 240f * s, 18f * s), progress, color, 310);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }
}
