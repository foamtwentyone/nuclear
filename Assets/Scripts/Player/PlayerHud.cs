using Nuclear.Items;
using Nuclear.Level;
using Nuclear.UI;
using Nuclear.Voice;
using UnityEngine;

namespace Nuclear.Player
{
    /// <summary>
    /// Минималистичный HUD своего игрока: без подложек, только иконки, тонкие полоски и текст с обводкой.
    /// Слева внизу — здоровье, фильтр, выносливость (видна, пока тратится) и доза (видна, когда важна);
    /// по центру внизу — 4 ячейки инвентаря, название предмета появляется при выборе и гаснет;
    /// под прицелом — подсказки «[клавиша] действие»; сверху — состояние двери; поверх всего — итог уровня.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private static readonly Color HealthColor = new(0.92f, 0.3f, 0.26f);
        private static readonly Color StaminaColor = new(0.98f, 0.84f, 0.4f);
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

        /// <summary>Итог уровня поверх всего. Возвращает true, если уровень закончен.</summary>
        private bool DrawOutcome()
        {
            var outcome = LevelOutcome.Current;
            if (outcome == null || outcome.Value.Value == LevelOutcome.Result.Playing)
                return false;

            bool won = outcome.Value.Value == LevelOutcome.Result.Won;
            float h = Screen.height;
            HudDraw.Fill(new Rect(0f, 0f, Screen.width, h), new Color(0f, 0f, 0f, 0.45f));
            HudDraw.Text(new Rect(0f, h * 0.42f, Screen.width, h * 0.08f),
                won ? "Вы добрались до Бункера-12" : "Все погибли", 44f, won ? Good : Alarm, TextAnchor.MiddleCenter);
            HudDraw.Text(new Rect(0f, h * 0.5f, Screen.width, h * 0.05f),
                "«Выйти» и «Создать игру» — начать заново", 17f, HudDraw.Dim, TextAnchor.MiddleCenter, HudDraw.Face.Body);
            return true;
        }

        private void DrawGhost()
        {
            float s = HudDraw.Scale;
            var ghostBlue = new Color(0.68f, 0.82f, 0.98f);
            HudDraw.Text(new Rect(0f, 30f * s, Screen.width, 34f * s), "Вы призрак", 24f, ghostBlue, TextAnchor.MiddleCenter);
            HudDraw.Text(new Rect(0f, 62f * s, Screen.width, 24f * s), "Слепыш вас не слышит. Подсказывайте живым, где он",
                15f, HudDraw.Dim, TextAnchor.MiddleCenter, HudDraw.Face.Body);
            DrawCrosshair();
            if (ghost == null)
                return;

            var center = new Vector2(Screen.width / 2f, Screen.height / 2f + 44f * s);
            float left = ghost.DistractCooldownLeft;
            if (Time.time - ghost.LastMissTime < 2f)
                HudDraw.KeyHints(center, 16f, Amber, ("", "Слишком далеко — подлетите ближе"));
            else if (left > 0f)
                HudDraw.KeyHints(center, 16f, HudDraw.Dim, ("", $"Шорох через {Mathf.CeilToInt(left)} с"));
            else
                HudDraw.KeyHints(center, 16f, HudDraw.Cream, ("ЛКМ", "шорох там, куда смотрите"));

            HudDraw.KeyHints(new Vector2(Screen.width / 2f, Screen.height - 40f * s), 14f, HudDraw.Dim,
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
                    text = "Дверь открывается — держитесь";
                    color = Amber;
                    break;
                case BunkerDoor.DoorState.Open:
                    text = "Дверь открыта — все внутрь";
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
            HudDraw.Text(new Rect(0f, 28f * s, Screen.width, 30f * s), text, 18f, color, TextAnchor.MiddleCenter);
            if (door.State.Value == BunkerDoor.DoorState.Opening)
                HudDraw.Bar(new Rect((Screen.width - 240f * s) / 2f, 62f * s, 240f * s, 5f * s), door.Progress.Value, Amber);
        }

        // ---------- Прицел ----------

        private void DrawCrosshair()
        {
            bool interactive = !survival.IsDead && ((carry != null && (carry.LookTarget != null || carry.HeldItem != null))
                || (pry != null && pry.LookHood != null) || (gear != null && gear.LookingAtTeammate));
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (interactive)
            {
                float ring = HudDraw.S(18f);
                var rect = new Rect(center.x - ring / 2f, center.y - ring / 2f, ring, ring);
                HudDraw.Frame(rect, new Color(0f, 0f, 0f, 0.5f), HudDraw.S(3.5f), ring / 2f);
                HudDraw.Frame(HudDraw.HudInset(rect, HudDraw.S(1f)), HudDraw.WithAlpha(HudDraw.Cream, 0.9f), HudDraw.S(1.5f), ring / 2f);
            }
            HudDraw.Circle(center, HudDraw.S(3f), new Color(0f, 0f, 0f, 0.5f));
            HudDraw.Circle(center, HudDraw.S(1.8f), HudDraw.WithAlpha(HudDraw.Cream, 0.9f));
        }

        // ---------- Здоровье, фильтр, выносливость, доза ----------

        private void DrawVitals()
        {
            float s = HudDraw.Scale;
            float x = 36f * s;
            float healthY = Screen.height - 58f * s;

            // Фильтр — над здоровьем: иконка и время (или состояние).
            DrawFilter(x, healthY - 34f * s);

            // Здоровье: светлый «хвост» недавнего урона, справа тусклым — съеденное радиацией.
            float health = survival.Health.Value;
            bool critical = health < 30f;
            var healthColor = critical ? Color.Lerp(HealthColor, Alarm, HudDraw.Blink(9f)) : HealthColor;
            float heart = 22f * s * (critical ? 1f + 0.15f * HudDraw.Blink(9f) : 1f);
            HudDraw.Icon(new Rect(x + 11f * s - heart / 2f, healthY + 5f * s - heart / 2f, heart, heart), HudIcon.Heart, healthColor);
            HudDraw.Bar(new Rect(x + 32f * s, healthY + 1f * s, 210f * s, 8f * s), health / 100f, healthColor,
                1f - survival.MaxHealth / 100f, healthTrail / 100f);
            HudDraw.Text(new Rect(x + 252f * s, healthY - 8f * s, 60f * s, 26f * s), Mathf.CeilToInt(health).ToString(), 16f, HudDraw.Cream);

            // Выносливость — тонкая линия под здоровьем, видна только пока тратится.
            if (staminaAlpha > 0.01f)
            {
                var stamina = player.Stamina;
                var color = stamina.IsExhausted ? Color.Lerp(StaminaColor, Alarm, HudDraw.Blink(8f)) : StaminaColor;
                HudDraw.Bar(new Rect(x + 32f * s, healthY + 17f * s, 210f * s, 4f * s), stamina.Normalized, color, alpha: staminaAlpha);
            }

            // Доза — над фильтром, только когда растёт или уже срезала здоровье.
            if (doseAlpha > 0.01f)
            {
                bool hot = dosimeter != null && dosimeter.Intensity > 0.01f;
                var color = HudDraw.WithAlpha(hot ? Color.Lerp(RadiationColor, Color.white, 0.4f * HudDraw.Blink(10f)) : RadiationColor, doseAlpha);
                float y = healthY - 66f * s;
                HudDraw.Icon(new Rect(x, y - 1f * s, 22f * s, 22f * s), HudIcon.Radiation, color);
                string text = $"{survival.Dose.Value:0}";
                if (hot)
                    text += $"  +{dosimeter.Intensity:0.0}/с";
                HudDraw.Text(new Rect(x + 32f * s, y - 4f * s, 200f * s, 28f * s), text, 16f, color);
            }

            if (voice != null && voice.Current != Loudness.Quiet)
            {
                bool shout = voice.Current == Loudness.Shout;
                var color = shout ? Color.Lerp(Alarm, Color.white, 0.3f * HudDraw.Blink(10f)) : Amber;
                HudDraw.Icon(new Rect(x + 252f * s, healthY - 33f * s, 20f * s, 20f * s), HudIcon.Microphone, color);
                HudDraw.Text(new Rect(x + 276f * s, healthY - 37f * s, 80f * s, 26f * s), shout ? "20 м" : "8 м", 14f, color);
            }
        }

        private void DrawFilter(float x, float y)
        {
            float s = HudDraw.Scale;
            bool swapping = survival.IsSwappingFilter.Value;
            float time = survival.FilterTime.Value;
            int seconds = Mathf.CeilToInt(time);

            string text;
            Color color = HudDraw.Cream;
            if (swapping)
            {
                text = "смена фильтра…";
                color = Amber;
            }
            else if (survival.InSafeAir)
            {
                text = "чистый воздух";
                color = SafeAirColor;
            }
            else if (time <= 0f)
            {
                text = gear != null && gear.IndexOf(GearKind.Filter) >= 0 ? "нет фильтра — R" : "нет фильтра";
                color = Color.Lerp(Alarm, Color.white, 0.35f * HudDraw.Blink(8f));
            }
            else
            {
                text = $"{seconds / 60}:{seconds % 60:00}";
                if (seconds <= 30)
                    color = Color.Lerp(Alarm, HudDraw.Cream, HudDraw.Blink(6f));
            }

            HudDraw.Icon(new Rect(x, y - 1f * s, 22f * s, 22f * s), HudIcon.Filter, color);
            HudDraw.Text(new Rect(x + 32f * s, y - 4f * s, 220f * s, 28f * s), text, 16f, color,
                face: swapping || survival.InSafeAir || time <= 0f ? HudDraw.Face.Body : HudDraw.Face.Heading);
        }

        // ---------- Инвентарь ----------

        private void DrawInventory()
        {
            if (gear == null)
                return;

            float s = HudDraw.Scale;
            const float size = 56f, gap = 8f;
            float side = size * s;
            float width = (size * PlayerGear.SlotCount + gap * (PlayerGear.SlotCount - 1)) * s;
            float x0 = (Screen.width - width) / 2f;
            float y = Screen.height - 36f * s - side;

            for (int i = 0; i < PlayerGear.SlotCount; i++)
            {
                var slot = gear[i];
                bool selected = gear.Selected == i;
                var rect = new Rect(x0 + i * (size + gap) * s, y, side, side);

                HudDraw.Fill(rect, new Color(0f, 0f, 0f, selected ? 0.42f : 0.26f), 8f * s);
                HudDraw.Frame(rect, HudDraw.WithAlpha(HudDraw.Cream, selected ? 0.9f : 0.22f), (selected ? 2f : 1.5f) * s, 8f * s);
                HudDraw.Text(new Rect(rect.x + 5f * s, rect.y + 2f * s, 16f * s, 16f * s), (i + 1).ToString(), 10f,
                    HudDraw.WithAlpha(HudDraw.Cream, selected ? 0.8f : 0.4f), TextAnchor.UpperLeft, outline: false);

                if (!slot.Filled)
                    continue;

                bool lit = slot.Is(GearKind.Flashlight) && gear.LightSlot.Value == i;
                float icon = side * 0.6f;
                var tint = lit ? new Color(1f, 0.92f, 0.6f) : HudDraw.WithAlpha(HudDraw.Cream, selected ? 1f : 0.75f);
                HudDraw.Icon(new Rect(rect.center.x - icon / 2f, rect.center.y - icon / 2f, icon, icon), IconOf(slot.Kind), tint);

                if (slot.Is(GearKind.Flashlight))
                {
                    float charge = slot.Charge / gear.FlashlightDuration;
                    var color = charge < 0.2f ? Color.Lerp(Amber, Alarm, HudDraw.Blink(6f)) : new Color(1f, 0.92f, 0.6f);
                    HudDraw.Bar(new Rect(rect.x + 8f * s, rect.yMax - 8f * s, side - 16f * s, 3f * s), charge, color);
                }
            }

            // Название выбранного предмета и что с ним можно сделать — появляются при выборе и гаснут.
            var selectedSlot = gear[gear.Selected];
            if (nameAlpha <= 0.01f || !selectedSlot.Filled)
                return;
            float centerX = Screen.width / 2f;
            HudDraw.Text(new Rect(0f, y - 50f * s, Screen.width, 24f * s), GearPickup.NameOf(selectedSlot.Kind), 16f,
                HudDraw.WithAlpha(HudDraw.Cream, nameAlpha), TextAnchor.MiddleCenter);
            string use = gear.UseHint();
            var dim = HudDraw.WithAlpha(HudDraw.Dim, nameAlpha);
            string drop = selectedSlot.Kind == GearKind.Can ? "бросить" : "выбросить";
            if (use != null)
                HudDraw.KeyHints(new Vector2(centerX, y - 16f * s), 13f, dim, ("ПКМ", use), ("Q", drop));
            else
                HudDraw.KeyHints(new Vector2(centerX, y - 16f * s), 13f, dim, ("Q", drop));
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

        // ---------- Подсказки и прогресс под прицелом ----------

        private void DrawCenter()
        {
            float s = HudDraw.Scale;
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f + 40f * s);

            if (gear != null && gear.BandageProgress.Value > 0f)
            {
                Progress(center, "Перевязка… не двигайтесь", gear.BandageProgress.Value, HudDraw.Cream);
                return;
            }
            if (pry != null && pry.LookHood is { } prying && prying.Progress.Value > 0f)
            {
                Progress(center, prying.WithCrowbar.Value ? "Вскрываете ломом — это слышно" : "Вскрываете вдвоём…", prying.Progress.Value, Amber);
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
                    detail += " · несёте вдвоём";
                else if (!held.IsFull && carry.IsHeavyLoad)
                    detail += " · тяжело, позови помощь";
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
            HudDraw.KeyHints(center, 16f, color, parts);
            if (detail != null)
                HudDraw.Text(new Rect(0f, center.y + 14f * s, Screen.width, 22f * s), detail, 13f, HudDraw.Dim,
                    TextAnchor.MiddleCenter, HudDraw.Face.Body);
        }

        private static void Progress(Vector2 center, string title, float progress, Color color)
        {
            float s = HudDraw.Scale;
            HudDraw.Text(new Rect(0f, center.y - 12f * s, Screen.width, 24f * s), title, 15f, HudDraw.Cream,
                TextAnchor.MiddleCenter, HudDraw.Face.Body);
            HudDraw.Bar(new Rect(center.x - 100f * s, center.y + 16f * s, 200f * s, 5f * s), progress, color);
        }

        private static string ChargeText(PhysicsItem item) =>
            item.TryGetComponent(out Battery battery) ? $" ({Mathf.RoundToInt(battery.Charge.Value * 100f)}%)" : "";
    }
}
