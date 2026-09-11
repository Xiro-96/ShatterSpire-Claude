using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shatterspire
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        private Font font;
        private Canvas canvas;
        private Health playerHealth;
        private Transform playerTransform;
        private PlayerBuild build;
        private PlayerController controller;
        private WeaponSystem weapon;
        private LevelSystem levelSystem;
        private Image hpFill;
        private Image hpChip;
        private Text hpText;
        private Text roomText;
        private Text objectiveText;
        private Text navigationText;
        private Text encounterText;
        private Text buildText;
        private Image heavyFill;
        private Image heavyPerfectZone;
        private Text heavyStateText;
        private Text skillStateText;
        private Text dashStateText;
        private Text knockoutText;
        private Image xpFill;
        private Text levelText;
        private CanvasGroup announcementGroup;
        private Text announcementText;
        private float announcementUntil;
        private Sprite[] abilitySprites;
        private GameObject modal;
        private readonly List<Button> modalButtons = new();
        private Action<RoomKind> routeCallback;
        private Action postPerkCallback;
        private Vector3 objectiveTarget;
        private string objectiveTargetLabel;
        private bool hasObjectiveTarget;
        private float hpTargetValue = 1f;
        private float hpChipValue = 1f;
        private RunConfig runConfig;
        private Action ascendCallback;
        private Action extractCallback;
        private bool selectingLevelPerk;
        private readonly System.Random perkRandom = new();

        public void Configure(GameObject player, RunConfig config)
        {
            runConfig = config ?? new RunConfig();
            playerTransform = player.transform;
            playerHealth = player.GetComponent<Health>();
            build = player.GetComponent<PlayerBuild>();
            controller = player.GetComponent<PlayerController>();
            weapon = player.GetComponent<WeaponSystem>();
            levelSystem = player.GetComponent<LevelSystem>();
            BuildCanvas();
            Subscribe();
            RefreshHealth(playerHealth);
            RefreshHeavy(weapon.HeavyMeterNormalized, weapon.HeavyChargeNormalized, weapon.ChargingHeavy, weapon.HeavyPerfect);
            if (levelSystem) RefreshExperience(levelSystem.Level, levelSystem.CurrentXp, levelSystem.RequiredXp);
        }

        private void OnDestroy()
        {
            GameEvents.HealthChanged -= RefreshHealth;
            GameEvents.PerkSelected -= OnPerkSelected;
            GameEvents.RoomStarted -= OnRoomStarted;
            GameEvents.ObjectiveChanged -= RefreshObjective;
            GameEvents.ObjectiveTargetChanged -= RefreshObjectiveTarget;
            GameEvents.EncounterChanged -= RefreshEncounter;
            GameEvents.HeavyAttackChanged -= RefreshHeavy;
            GameEvents.KnockoutChanged -= RefreshKnockout;
            GameEvents.ExperienceChanged -= RefreshExperience;
            GameEvents.WaveChanged -= RefreshWave;
            GameEvents.LevelUp -= OnLevelUp;
            MobileInput.Reset();
        }

        private void Subscribe()
        {
            GameEvents.HealthChanged += RefreshHealth;
            GameEvents.PerkSelected += OnPerkSelected;
            GameEvents.RoomStarted += OnRoomStarted;
            GameEvents.ObjectiveChanged += RefreshObjective;
            GameEvents.ObjectiveTargetChanged += RefreshObjectiveTarget;
            GameEvents.EncounterChanged += RefreshEncounter;
            GameEvents.HeavyAttackChanged += RefreshHeavy;
            GameEvents.KnockoutChanged += RefreshKnockout;
            GameEvents.ExperienceChanged += RefreshExperience;
            GameEvents.WaveChanged += RefreshWave;
            GameEvents.LevelUp += OnLevelUp;
        }

        private void BuildCanvas()
        {
            // Roboto Black statt Arial: kraeftige Buchstaben wie in Mobile-Actionspielen.
            // Faellt auf die eingebaute Schrift zurueck, falls das Asset fehlt.
            font = Resources.Load<Font>("Fonts/Roboto-Black") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.62f;
            LoadArtAssets();

            if (!FindAnyObjectByType<EventSystem>())
                new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));

            var championBack = CreateImage(root.transform, "Champion Status Panel", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                new Vector2(16, -16), new Vector2(382, 122), new Vector2(0, 1));
            ApplyRounded(championBack);
            var championOutline = championBack.gameObject.AddComponent<Outline>();
            championOutline.effectColor = Color.Lerp(HeroCatalog.Accent(runConfig.Hero), Color.black, 0.18f);
            championOutline.effectDistance = new Vector2(2f, -2f);
            championBack.raycastTarget = false;
            var championAccent = CreateImage(championBack.transform, "Champion Cyan Accent",
                HeroCatalog.Accent(runConfig.Hero), new Vector2(0, -1), new Vector2(382, 5), new Vector2(0, 1));
            championAccent.raycastTarget = false;

            CreateChampionPanel(root.transform);
            var hpBack = CreateImage(root.transform, "HP", new Color(0.05f, 0.035f, 0.03f, 0.92f),
                new Vector2(112, -62), new Vector2(268, 23), new Vector2(0, 1));
            ApplyRounded(hpBack);
            hpChip = CreateFill(hpBack.transform, new Color(1f, 0.72f, 0.24f));
            ApplyRoundedFill(hpChip);
            hpFill = CreateFill(hpBack.transform, new Color(0.95f, 0.16f, 0.2f));
            ApplyRoundedFill(hpFill);
            hpText = CreateText(hpBack.transform, "100 / 100", 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);

            var objectivePanel = CreateImage(root.transform, "Floor Objective Panel", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                new Vector2(0, -16), new Vector2(500, 106), new Vector2(0.5f, 1));
            ApplyRounded(objectivePanel);
            var objectiveOutline = objectivePanel.gameObject.AddComponent<Outline>();
            objectiveOutline.effectColor = new Color(1f, 0.76f, 0.22f, 0.92f);
            objectiveOutline.effectDistance = new Vector2(2f, -2f);
            objectivePanel.raycastTarget = false;
            var objectiveAccent = CreateImage(objectivePanel.transform, "Objective Accent",
                new Color(1f, 0.76f, 0.22f, 0.92f), new Vector2(0, -1), new Vector2(500, 5), new Vector2(0.5f, 1));
            objectiveAccent.raycastTarget = false;

            roomText = CreateText(objectivePanel.transform, "FLOOR 1 / 15", 24, TextAnchor.UpperCenter,
                new Vector2(0, -7), new Vector2(468, 28), new Vector2(0.5f, 1));
            roomText.fontStyle = FontStyle.Bold;
            objectiveText = CreateText(objectivePanel.transform, "FIND THE RIFT CELLS", 16, TextAnchor.UpperCenter,
                new Vector2(0, -34), new Vector2(468, 25), new Vector2(0.5f, 1));
            objectiveText.color = new Color(1f, 0.95f, 0.86f);
            navigationText = CreateText(objectivePanel.transform, string.Empty, 17, TextAnchor.UpperCenter,
                new Vector2(0, -59), new Vector2(468, 23), new Vector2(0.5f, 1));
            navigationText.color = new Color(1f, 0.78f, 0.15f);
            navigationText.gameObject.SetActive(false);
            encounterText = CreateText(objectivePanel.transform, string.Empty, 15, TextAnchor.UpperCenter,
                new Vector2(0, -82), new Vector2(468, 21), new Vector2(0.5f, 1));
            encounterText.color = new Color(1f, 0.35f, 0.28f);
            encounterText.gameObject.SetActive(false);
            buildText = CreateText(root.transform, "NO PERKS", 12, TextAnchor.UpperLeft, new Vector2(112, -91), new Vector2(258, 22), new Vector2(0, 1));
            CreateExperienceBar(root.transform);
            CreateTeamPanel(root.transform);
            knockoutText = CreateText(root.transform, "TEAM LIVES  ◆ ◆ ◆", 15, TextAnchor.UpperRight,
                new Vector2(-24, -102), new Vector2(300, 28), new Vector2(1, 1));
            knockoutText.color = new Color(1f, 0.78f, 0.2f);
            CreateHeavyBar(root.transform);
            CreateDesktopAbilityBar(root.transform);
            CreateMobileControls(root.transform);
            CreateAnnouncement(root.transform);
        }

        private void Update()
        {
            HandleModalInput();
            if (hpChip)
            {
                hpChipValue = hpTargetValue >= hpChipValue
                    ? hpTargetValue
                    : Mathf.MoveTowards(hpChipValue, hpTargetValue, Time.unscaledDeltaTime * 0.38f);
                hpChip.fillAmount = hpChipValue;
            }
            if (announcementGroup)
            {
                var remaining = announcementUntil - Time.unscaledTime;
                announcementGroup.alpha = remaining > 0.35f ? 1f : Mathf.Clamp01(remaining / 0.35f);
                if (remaining <= 0f && announcementGroup.gameObject.activeSelf)
                    announcementGroup.gameObject.SetActive(false);
            }
            if (!controller || !weapon) return;
            if (skillStateText) skillStateText.text = weapon.SkillNormalized >= 1f ? "Q  READY" : $"Q  {weapon.SkillNormalized:P0}";
            if (dashStateText) dashStateText.text = $"SPACE {controller.DashCharges}/{controller.MaxDashCharges}";
            UpdateObjectiveNavigation();
        }

        private void LoadArtAssets()
        {
            abilitySprites = new[]
            {
                UiIconFactory.Ability(runConfig.Hero, 0), UiIconFactory.Ability(runConfig.Hero, 1),
                UiIconFactory.Ability(runConfig.Hero, 2), UiIconFactory.Ability(runConfig.Hero, 3)
            };
        }

        private void CreateChampionPanel(Transform parent)
        {
            var frame = CreateImage(parent, "Hero Emblem Frame", new Color(0.13f, 0.09f, 0.07f, 0.96f),
                new Vector2(22, -22), new Vector2(78, 78), new Vector2(0, 1));
            ApplyRounded(frame);
            var outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = HeroCatalog.Accent(runConfig.Hero);
            outline.effectDistance = new Vector2(3, -3);

            var portrait = CreateImage(frame.transform, "Hero Class Emblem", Color.white, Vector2.zero,
                new Vector2(72, 72), new Vector2(0.5f, 0.5f));
            portrait.sprite = UiIconFactory.Hero(runConfig.Hero);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var name = CreateText(parent, HeroCatalog.Name(runConfig.Hero), 24, TextAnchor.UpperLeft, new Vector2(112, -19), new Vector2(210, 27), new Vector2(0, 1));
            name.fontStyle = FontStyle.Bold;
            var role = CreateText(parent, HeroCatalog.Role(runConfig.Hero), 11, TextAnchor.UpperLeft, new Vector2(112, -43), new Vector2(245, 18), new Vector2(0, 1));
            role.color = HeroCatalog.Accent(runConfig.Hero);
        }

        private void CreateExperienceBar(Transform parent)
        {
            var back = CreateImage(parent, "Rift Experience", new Color(0.05f, 0.035f, 0.03f, 0.92f),
                new Vector2(16, -146), new Vector2(382, 16), new Vector2(0, 1));
            ApplyRounded(back);
            xpFill = CreateFill(back.transform, new Color(0.62f, 0.28f, 1f));
            ApplyRoundedFill(xpFill);
            levelText = CreateText(back.transform, "LV 1  ·  0 / 30", 11, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            levelText.fontStyle = FontStyle.Bold;
        }

        private void CreateAnnouncement(Transform parent)
        {
            var panel = CreateImage(parent, "Combat Announcement", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                new Vector2(0, -148), new Vector2(460, 74), new Vector2(0.5f, 1));
            ApplyRounded(panel);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.76f, 0.22f, 0.92f);
            outline.effectDistance = new Vector2(3f, -3f);
            announcementGroup = panel.gameObject.AddComponent<CanvasGroup>();
            announcementGroup.blocksRaycasts = false;
            announcementGroup.interactable = false;
            announcementText = CreateText(panel.transform, string.Empty, 22, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            announcementText.fontStyle = FontStyle.Bold;
            panel.gameObject.SetActive(false);
        }

        private void CreateDesktopAbilityBar(Transform parent)
        {
            if (Application.isMobilePlatform) return;
            // Drei Aktionen und Dash wie in R.I.S.E. Abstand 106 fuer die Sockel (92 breit plus Rand).
            CreateAbilityTile(parent, 0, "LMB", new Vector2(-40, 40), new Color(0.1f, 0.82f, 0.95f));
            CreateAbilityTile(parent, 3, "RMB", new Vector2(-146, 40), new Color(1f, 0.68f, 0.12f));
            skillStateText = CreateAbilityTile(parent, 1, "Q", new Vector2(-252, 40), new Color(0.55f, 0.3f, 1f));
            dashStateText = CreateAbilityTile(parent, 2, "SPACE", new Vector2(-358, 40), new Color(0.18f, 0.74f, 1f));
        }

        private void CreateTeamPanel(Transform parent)
        {
            var panel = CreateImage(parent, "Offline Team", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                new Vector2(-22, -22), new Vector2(218, 52), new Vector2(1, 1));
            ApplyRounded(panel);
            var team = runConfig.Hero == HeroClassId.Arcanist ? "BRAX  ·  ORION  ·  REX"
                : runConfig.Hero == HeroClassId.Guardian ? "REX  ·  BRAX  ·  MIRA" : "BRAX  ·  REX  ·  MIRA";
            var text = CreateText(panel.transform, "RIFT TEAM  3/3\n" + team, 13, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            text.color = new Color(1f, 0.86f, 0.5f);
        }

        private void CreateHeavyBar(Transform parent)
        {
            var back = CreateImage(parent, "Heavy Attack Meter", new Color(0.05f, 0.035f, 0.03f, 0.92f),
                new Vector2(0, 34), new Vector2(340, 15), new Vector2(0.5f, 0));
            ApplyRounded(back);
            heavyFill = CreateFill(back.transform, new Color(0.15f, 0.78f, 1f));
            ApplyRoundedFill(heavyFill);
            heavyPerfectZone = CreateImage(back.transform, "Perfect Release Zone", new Color(1f, 0.78f, 0.12f, 0.42f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var zoneRect = (RectTransform)heavyPerfectZone.transform;
            zoneRect.anchorMin = new Vector2(0.5f, 0f);
            zoneRect.anchorMax = new Vector2(0.74f, 1f);
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.offsetMin = Vector2.zero;
            zoneRect.offsetMax = Vector2.zero;
            heavyPerfectZone.gameObject.SetActive(false);
            // Der Text steht ohne Hintergrund direkt ueber dem Boden. Auf dem alten
            // dunklen Terrakotta war hellblau lesbar, auf dem hellen Boden nach dem
            // Grafik-Paket verschwand er. Die Kontur macht ihn unabhaengig vom
            // Untergrund lesbar, die groessere Schrift zusaetzlich auf dem Telefon.
            heavyStateText = CreateText(parent, weapon.LightName + " CHARGES " + weapon.HeavyName, 16, TextAnchor.LowerCenter,
                new Vector2(0, 55), new Vector2(520, 26), new Vector2(0.5f, 0));
            // Kontur und Schatten setzt jetzt CreateText fuer jeden Text.
            heavyStateText.color = new Color(1f, 0.95f, 0.86f);
        }

        private Text CreateAbilityTile(Transform parent, int spriteIndex, string keyLabel, Vector2 position, Color accent)
        {
            // Eigener Sockel hinter dem Icon. Das Icon-Sprite ueberdeckt die Grundfarbe des Tiles
            // (tile.color wird auf Weiss gesetzt), deshalb hatte Einfaerben bisher keine Wirkung.
            // Groesser und mit dickem Rand in der Faehigkeitsfarbe, wie Aktionsknoepfe in
            // Mobile-Actionspielen.
            var socket = CreateImage(parent, "Ability Socket", new Color(0.1f, 0.07f, 0.06f, 0.94f), position,
                new Vector2(92, 92), new Vector2(1, 0));
            ApplyRounded(socket);
            socket.raycastTarget = false;
            var rim = socket.gameObject.AddComponent<Outline>();
            rim.effectColor = accent;
            rim.effectDistance = new Vector2(4, -4);

            var tile = CreateImage(socket.transform, "Ability", new Color(0.025f, 0.045f, 0.09f, 0.95f),
                new Vector2(0, 7), new Vector2(70, 70), new Vector2(0.5f, 0.5f));
            if (abilitySprites != null && spriteIndex >= 0 && spriteIndex < abilitySprites.Length)
            {
                tile.sprite = abilitySprites[spriteIndex];
                tile.color = Color.white;
                tile.preserveAspect = true;
            }
            tile.raycastTarget = false;
            var label = CreateText(socket.transform, keyLabel, 15, TextAnchor.LowerCenter, new Vector2(0, 3),
                new Vector2(92, 20), new Vector2(0.5f, 0));
            label.color = Color.white;
            return label;
        }

        private void HandleModalInput()
        {
            if (!modal || modalButtons.Count == 0) return;

            for (var i = 0; i < Mathf.Min(3, modalButtons.Count); i++)
            {
                var alphaKey = (KeyCode)((int)KeyCode.Alpha1 + i);
                var keypadKey = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
                {
                    InvokeModalButton(i);
                    return;
                }
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && modalButtons.Count == 1)
            {
                InvokeModalButton(0);
                return;
            }

            if (Input.GetMouseButtonDown(0))
                TryInvokeModalAt(Input.mousePosition);

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) TryInvokeModalAt(touch.position);
            }
        }

        private void TryInvokeModalAt(Vector2 screenPosition)
        {
            for (var i = modalButtons.Count - 1; i >= 0; i--)
            {
                var button = modalButtons[i];
                if (!button)
                {
                    modalButtons.RemoveAt(i);
                    continue;
                }
                if (!button.interactable || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, screenPosition, null)) continue;
                button.onClick.Invoke();
                return;
            }
        }

        private void InvokeModalButton(int index)
        {
            if (index < 0 || index >= modalButtons.Count) return;
            var button = modalButtons[index];
            if (button && button.interactable) button.onClick.Invoke();
        }

        private void RefreshHealth(Health value)
        {
            if (value != playerHealth || !hpFill) return;
            hpTargetValue = value.Normalized;
            if (hpTargetValue > hpChipValue) hpChipValue = hpTargetValue;
            hpFill.fillAmount = hpTargetValue;
            hpFill.color = HealthColor(hpTargetValue);
            hpText.text = $"{Mathf.CeilToInt(value.Current)} / {Mathf.CeilToInt(value.Maximum)}";
        }

        /// <summary>
        /// Der Balken war vorher konstant rot, auch bei 87 % Leben — damit sah der
        /// Spieler dauernd aus wie kurz vor dem Tod und echte Gefahr fiel nicht auf.
        /// Rot ist jetzt dem unteren Drittel vorbehalten.
        /// </summary>
        private static Color HealthColor(float normalized)
        {
            var low = new Color(0.95f, 0.16f, 0.2f);
            var mid = new Color(1f, 0.72f, 0.16f);
            var high = new Color(0.2f, 0.86f, 0.52f);
            if (normalized <= 0.3f) return low;
            if (normalized >= 0.6f) return high;
            return normalized < 0.45f
                ? Color.Lerp(low, mid, (normalized - 0.3f) / 0.15f)
                : Color.Lerp(mid, high, (normalized - 0.45f) / 0.15f);
        }

        private void RefreshObjective(int current, int required, string instruction)
        {
            if (!objectiveText) return;
            objectiveText.text = required > 1 ? $"{instruction}  ·  {current}/{required}" : instruction;
            objectiveText.color = current >= required
                ? new Color(0.2f, 1f, 0.55f)
                : new Color(0.82f, 0.96f, 1f);
        }

        private void RefreshObjectiveTarget(Vector3 position, string label, bool visible)
        {
            objectiveTarget = position;
            objectiveTargetLabel = label;
            hasObjectiveTarget = visible;
            if (navigationText) navigationText.gameObject.SetActive(visible);
        }

        private void RefreshEncounter(int remaining, int total, bool boss)
        {
            if (!encounterText) return;
            var visible = total > 0 && remaining > 0;
            encounterText.gameObject.SetActive(visible);
            if (!visible) return;
            encounterText.text = boss
                ? "IRON WARDEN  ·  BOSS ENGAGED"
                : $"CORE DEFENDERS  ·  {remaining} REMAINING";
            encounterText.color = boss
                ? new Color(1f, 0.58f, 0.12f)
                : remaining <= Mathf.Max(2, total / 3)
                    ? new Color(0.3f, 1f, 0.58f)
                    : new Color(1f, 0.35f, 0.28f);
        }

        private void RefreshExperience(int level, int current, int required)
        {
            if (xpFill) xpFill.fillAmount = required <= 0 ? 0f : Mathf.Clamp01(current / (float)required);
            if (levelText) levelText.text = $"LV {level}  ·  {current} / {required}";
        }

        private void RefreshWave(int current, int total)
        {
            if (current <= 0 || total <= 0) return;
            ShowAnnouncement($"RIFT WAVE {current} / {total}\nINCOMING", current == 1 ? 1.15f : 1.4f);
        }

        private void ShowAnnouncement(string message, float seconds)
        {
            if (!announcementGroup || !announcementText) return;
            announcementText.text = message;
            announcementGroup.alpha = 1f;
            announcementGroup.gameObject.SetActive(true);
            announcementUntil = Time.unscaledTime + seconds;
        }

        private void UpdateObjectiveNavigation()
        {
            if (!navigationText || !hasObjectiveTarget || !playerTransform || !Camera.main) return;
            var horizontal = objectiveTarget - playerTransform.position;
            horizontal.y = 0f;
            var distance = horizontal.magnitude;
            var screen = Camera.main.WorldToScreenPoint(objectiveTarget + Vector3.up * 1.5f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var offset = new Vector2(screen.x, screen.y) - center;
            if (screen.z < 0f) offset *= -1f;
            var arrow = distance <= 3.8f ? "◆"
                : Mathf.Abs(offset.x) > Mathf.Abs(offset.y) ? offset.x < 0f ? "◀" : "▶"
                : offset.y < 0f ? "▼" : "▲";
            navigationText.text = $"{arrow}  {objectiveTargetLabel}  ·  {Mathf.CeilToInt(distance)} m";
        }

        private void RefreshHeavy(float meter, float charge, bool charging, bool perfect)
        {
            if (!heavyFill) return;
            heavyFill.fillAmount = charging ? charge : meter;
            heavyFill.color = perfect
                ? new Color(1f, 0.78f, 0.08f)
                : charging ? new Color(0.15f, 0.9f, 1f) : new Color(0.15f, 0.68f, 1f);
            if (heavyPerfectZone) heavyPerfectZone.gameObject.SetActive(charging);
            if (!heavyStateText) return;
            var release = Application.isMobilePlatform ? "RELEASE HEAVY" : "RELEASE RMB";
            var hold = Application.isMobilePlatform ? "HOLD HEAVY" : "HOLD RMB";
            heavyStateText.text = perfect ? "PERFECT!  " + release : charging ? weapon.HeavyName + " · RELEASE IN GOLD ZONE"
                : meter >= 0.999f ? weapon.HeavyName + " READY · " + hold : weapon.LightName + " COMBO CHARGES HEAVY";
            heavyStateText.color = perfect ? new Color(1f, 0.82f, 0.14f) : new Color(1f, 0.95f, 0.86f);
        }

        private void RefreshKnockout(int skulls, int maximum, float reviveProgress, bool downed)
        {
            if (!knockoutText) return;
            if (downed && skulls < maximum)
            {
                knockoutText.text = $"ALLY REVIVING {HeroCatalog.Name(runConfig.Hero)}  {reviveProgress:P0}";
                knockoutText.color = new Color(0.3f, 1f, 0.58f);
                return;
            }
            var lives = Mathf.Max(0, maximum - skulls);
            knockoutText.text = "TEAM LIVES  " + new string('◆', lives) + new string('◇', maximum - lives);
            knockoutText.color = lives <= 1 ? new Color(1f, 0.32f, 0.22f) : new Color(1f, 0.78f, 0.2f);
        }

        private void OnRoomStarted(int index, RoomKind kind)
        {
            var counter = PathCatalog.FloorCounter(runConfig.Mode, index);
            roomText.text = $"{counter}  ·  {kind.ToString().ToUpperInvariant()}";
            ShowAnnouncement($"{FloorCatalog.Name(FloorCatalog.ThemeFor(index))}\nFLOOR {index} · {kind.ToString().ToUpperInvariant()}", 1.8f);
        }
        private void OnPerkSelected(PerkDefinition _) => RefreshBuild();

        private void RefreshBuild()
        {
            var names = new List<string>();
            foreach (var id in build.Perks)
            {
                var perk = PerkCatalog.Find(id);
                names.Add(perk == null ? id.ToString() : $"{PerkCatalog.SlotLabel(perk.Slot)} {perk.Name}");
            }
            var fusion = build.IsInferno ? "\nFUSION: INFERNO" : build.IsShatter ? "\nFUSION: SHATTER" : build.IsChainStorm ? "\nFUSION: CHAIN STORM" : string.Empty;
            buildText.text = names.Count == 0 ? "NO PERKS" : string.Join(" · ", names) + fusion;
        }

        public void ShowFloorUpgrade(Action afterSelection)
        {
            postPerkCallback = afterSelection;
            selectingLevelPerk = false;
            ShowPerkChoice();
        }

        private void OnLevelUp(int _)
        {
            if (modal) return;
            selectingLevelPerk = true;
            ShowPerkChoice();
        }

        private void ShowPerkChoice()
        {
            if (modal) return;
            Time.timeScale = 0f;
            modal = CreateModal(selectingLevelPerk ? "LEVEL UP · CHOOSE AN UPGRADE" : "FLOOR CLEARED · CHOOSE AN UPGRADE",
                "EVERY UPGRADE CHANGES ONE OF YOUR ACTIONS");
            var hero = runConfig.Hero;
            var choices = PerkCatalog.RollThree(hero, new HashSet<PerkId>(build.Perks), perkRandom);
            for (var i = 0; i < choices.Count; i++)
            {
                var perk = choices[i];
                var rarity = perk.Rarity.ToString().ToUpperInvariant() +
                             (perk.Heroes.Length == 1 ? "  ·  " + HeroCatalog.Name(hero) + " ONLY" : string.Empty);
                var button = CreateButton(modal.transform,
                    $"[{i + 1}]  {PerkCatalog.SlotLabel(perk.Slot, hero)}\n{perk.Name}\n\n{perk.Description}\n\n{rarity}",
                    new Vector2(-390f + i * 390f, -20f), new Vector2(340f, 420f), perk.Color);
                button.onClick.AddListener(() => SelectPerk(perk));
                modalButtons.Add(button);
            }
        }

        private void SelectPerk(PerkDefinition perk)
        {
            build.Apply(perk);
            Destroy(modal);
            modal = null;
            modalButtons.Clear();
            Time.timeScale = 1f;
            if (selectingLevelPerk) levelSystem?.ConsumeChoice();
            if (levelSystem && levelSystem.HasPendingChoice)
            {
                selectingLevelPerk = true;
                ShowPerkChoice();
                return;
            }
            selectingLevelPerk = false;
            var callback = postPerkCallback;
            postPerkCallback = null;
            callback?.Invoke();
        }

        public void ShowRoutes(int nextRoom, Action<RoomKind> callback)
        {
            if (modal) return;
            routeCallback = callback;
            Time.timeScale = 0f;
            modal = CreateModal($"TEAM VOTE · FLOOR {nextRoom}", "YOUR OFFLINE PARTY FOLLOWS THE SELECTED ROUTE");
            var options = nextRoom % 5 == 0 ? new[] { RoomKind.Boss } : new[] { RoomKind.Combat, RoomKind.Elite, UnityEngine.Random.value < 0.5f ? RoomKind.Treasure : RoomKind.Mystery };
            for (var i = 0; i < options.Length; i++)
            {
                var kind = options[i];
                var color = kind switch { RoomKind.Elite => new Color(0.86f, 0.2f, 0.55f), RoomKind.Boss => new Color(1f, 0.45f, 0.08f), RoomKind.Treasure => new Color(1f, 0.78f, 0.16f), _ => new Color(0.15f, 0.7f, 0.82f) };
                var text = kind switch { RoomKind.Elite => "ELITE FIGHT\nHigh risk · bonus shards", RoomKind.Treasure => "TREASURE\nExtra healing · bonus shards", RoomKind.Mystery => "MYSTERY\nUnknown encounter", RoomKind.Boss => "SPIRE WARDEN\nAscension trial", _ => "COMBAT\nBalanced resistance" };
                var button = CreateButton(modal.transform, $"[{i + 1}]  {text}", new Vector2((i - (options.Length - 1) * 0.5f) * 390f, -20f), new Vector2(340f, 360f), color);
                button.onClick.AddListener(() => SelectRoute(kind));
                modalButtons.Add(button);
            }
        }

        private void SelectRoute(RoomKind kind)
        {
            Destroy(modal);
            modal = null;
            modalButtons.Clear();
            Time.timeScale = 1f;
            routeCallback?.Invoke(kind);
        }

        public void ShowAscensionChoice(int floor, int carriedShards, bool canAscend, Action extract, Action ascend)
        {
            if (modal) Destroy(modal);
            extractCallback = extract;
            ascendCallback = ascend;
            Time.timeScale = 0f;
            modal = CreateModal("THE ASCENSION GATE", $"FLOOR {floor} CLEARED  ·  {carriedShards} SHARDS AT STAKE");
            var extractButton = CreateButton(modal.transform,
                "[1]  EXTRACT\n\nSECURE ALL SHARDS\nRETURN TO SKYHOLD", new Vector2(-260, -30), new Vector2(420, 390), new Color(0.12f, 0.9f, 0.64f));
            extractButton.onClick.AddListener(ChooseExtract);
            modalButtons.Add(extractButton);
            if (canAscend)
            {
                var ascendButton = CreateButton(modal.transform,
                    "[2]  ASCEND\n\nSTRONGER ENEMIES\n+35% TIER REWARDS", new Vector2(260, -30), new Vector2(420, 390), new Color(0.72f, 0.25f, 1f));
                ascendButton.onClick.AddListener(ChooseAscend);
                modalButtons.Add(ascendButton);
            }
        }

        private void ChooseExtract()
        {
            CloseModal();
            extractCallback?.Invoke();
        }

        private void ChooseAscend()
        {
            CloseModal();
            ascendCallback?.Invoke();
        }

        private void CloseModal()
        {
            if (modal) Destroy(modal);
            modal = null;
            modalButtons.Clear();
            Time.timeScale = 1f;
        }

        public void ShowRunEnd(bool victory, int earned, MetaSaveData save, int roomsCleared,
            ClimbResult result, int score, int rankPoints)
        {
            if (modal) Destroy(modal);
            if (Camera.main)
            {
                Camera.main.enabled = true;
                Camera.main.targetDisplay = 0;
            }
            Time.timeScale = 0f;
            modal = CreateModal(victory ? "TOWER PATH CLEARED" : "CLIMB ENDED",
                victory ? "THE TEAM RETURNS WITH SECURED SHARDS" : "A PORTION OF YOUR SHARDS SURVIVED");

            // Links die Punkte dieses Aufstiegs, rechts was er fuer den Rang
            // bedeutet. Die Aufschluesselung steht bewusst da: ein Rang, dessen
            // Zustandekommen man nicht sieht, motiviert nicht.
            var scorePanel = CreateImage(modal.transform, "Climb Score", new Color(0.035f, 0.075f, 0.12f, 0.98f),
                new Vector2(-345, 25), new Vector2(660, 300), new Vector2(0.5f, 0.5f));
            ApplyRounded(scorePanel);
            CreateText(scorePanel.transform, "AUFSTIEGSWERTUNG", 22, TextAnchor.UpperCenter,
                new Vector2(0, -16), new Vector2(600, 30), new Vector2(0.5f, 1));
            CreateText(scorePanel.transform, $"{score:N0}", 58, TextAnchor.UpperCenter,
                new Vector2(0, -46), new Vector2(600, 70), new Vector2(0.5f, 1));

            // Zwei Textspalten statt Leerzeichen-Auffuellung: die HUD-Schrift ist
            // proportional, ausgerichtet wird deshalb ueber die Rechtecke.
            var labels = new System.Text.StringBuilder();
            var values = new System.Text.StringBuilder();
            foreach (var entry in ClimbScore.Breakdown(result))
            {
                labels.AppendLine(entry.Label);
                values.AppendLine($"{entry.Points:N0}");
            }
            labels.AppendLine(result.Extracted ? "EXTRAHIERT" : "GEFALLEN");
            values.AppendLine(result.Extracted ? "x1,15" : "x0,70");
            CreateText(scorePanel.transform, labels.ToString(), 24, TextAnchor.UpperLeft,
                new Vector2(30, -122), new Vector2(320, 150), new Vector2(0f, 1f));
            CreateText(scorePanel.transform, values.ToString(), 24, TextAnchor.UpperRight,
                new Vector2(-30, -122), new Vector2(260, 150), new Vector2(1f, 1f));

            var rankPanel = CreateImage(modal.transform, "Rank", new Color(0.035f, 0.075f, 0.12f, 0.98f),
                new Vector2(345, 25), new Vector2(660, 300), new Vector2(0.5f, 0.5f));
            ApplyRounded(rankPanel);
            var tier = RankTable.TierFor(rankPoints);
            CreateText(rankPanel.transform, $"RANG · SHIFT {save.shiftIndex}", 22, TextAnchor.UpperCenter,
                new Vector2(0, -16), new Vector2(600, 30), new Vector2(0.5f, 1));
            var rankLabel = CreateText(rankPanel.transform, RankTable.Name(tier), 52, TextAnchor.UpperCenter,
                new Vector2(0, -46), new Vector2(600, 66), new Vector2(0.5f, 1));
            rankLabel.color = RankTable.Accent(tier);

            var barBack = CreateImage(rankPanel.transform, "Rank Bar", new Color(0.05f, 0.09f, 0.14f, 1f),
                new Vector2(0, -126), new Vector2(560, 22), new Vector2(0.5f, 1));
            ApplyRounded(barBack);
            var barFill = CreateFill(barBack.transform, RankTable.Accent(tier));
            ApplyRoundedFill(barFill);
            barFill.fillAmount = RankTable.ProgressInTier(rankPoints);

            var toNext = RankTable.PointsToNext(rankPoints);
            var next = RankTable.IsHighest(tier)
                ? "HOECHSTER RANG ERREICHT"
                : $"{toNext:N0} PUNKTE BIS {RankTable.Name((RankTier)((int)tier + 1))}";
            CreateText(rankPanel.transform,
                $"RANGPUNKTE  {rankPoints:N0}\nAUS DEN {RankTable.ClimbCount} BESTEN AUFSTIEGEN\n{next}\n\nSHIFT ENDET IN  {ShiftCalendar.Countdown(ShiftCalendar.Remaining)}",
                24, TextAnchor.UpperCenter, new Vector2(0, -160), new Vector2(600, 130), new Vector2(0.5f, 1));

            var wallet = CreateImage(modal.transform, "Wallet", new Color(0.03f, 0.06f, 0.1f, 0.96f),
                new Vector2(0, -140), new Vector2(1010, 62), new Vector2(0.5f, 0.5f));
            ApplyRounded(wallet);
            CreateText(wallet.transform,
                $"SHARDS  +{earned}  ·  GESAMT {save.shards}      TOKENS  {save.tokens}      ETAGE  {Mathf.Max(1, roomsCleared)}  ·  BEST {save.bestFloor}",
                26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            var restart = CreateButton(modal.transform, "CLIMB AGAIN", new Vector2(225, -225), new Vector2(360, 96), new Color(0.1f, 0.86f, 0.72f));
            restart.onClick.AddListener(RestartRun);
            modalButtons.Add(restart);
            var home = CreateButton(modal.transform, "MAIN MENU", new Vector2(-225, -225), new Vector2(360, 96), new Color(0.46f, 0.38f, 0.7f));
            home.onClick.AddListener(RestartScene);
            modalButtons.Add(home);
        }

        private void RestartRun()
        {
            RunLaunchSettings.Prepare(runConfig);
            RestartSceneInternal();
        }

        private void RestartScene()
        {
            RunLaunchSettings.Clear();
            RestartSceneInternal();
        }

        private static void RestartSceneInternal()
        {
            Time.timeScale = 1f;
            GameEvents.Reset();
            var active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.name)) SceneManager.LoadScene(active.name);
        }

        private GameObject CreateModal(string title, string subtitle)
        {
            modalButtons.Clear();
            var blocker = CreateImage(canvas.transform, "Modal Blocker", new Color(0.005f, 0.012f, 0.027f, 0.9f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            var panelImage = CreateImage(blocker.transform, "Modal", new Color(0.018f, 0.035f, 0.075f, 0.99f),
                Vector2.zero, new Vector2(1420, 790), new Vector2(0.5f, 0.5f));
            ApplyRounded(panelImage);
            var panelOutline = panelImage.gameObject.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.12f, 0.72f, 0.86f, 0.72f);
            panelOutline.effectDistance = new Vector2(3f, -3f);
            var panel = panelImage.gameObject;
            var headerAccent = CreateImage(panel.transform, "Modal Energy Header", new Color(0.12f, 0.84f, 0.9f, 0.92f),
                new Vector2(0, -2), new Vector2(1420, 6), new Vector2(0.5f, 1));
            headerAccent.raycastTarget = false;
            var titleText = CreateText(panel.transform, title, 43, TextAnchor.UpperCenter, new Vector2(0, -34), new Vector2(1100, 64), new Vector2(0.5f, 1));
            titleText.fontStyle = FontStyle.Bold;
            var sub = CreateText(panel.transform, subtitle, 21, TextAnchor.UpperCenter, new Vector2(0, -98), new Vector2(1100, 42), new Vector2(0.5f, 1));
            sub.color = new Color(0.55f, 0.86f, 1f);
            return blocker.gameObject;
        }

        private void CreateMobileControls(Transform parent)
        {
            if (!Application.isMobilePlatform) return;
            var stick = CreateImage(parent, "Move Stick", new Color(0.2f, 0.8f, 1f, 0.22f), new Vector2(190, 190), new Vector2(270, 270), Vector2.zero);
            var knob = CreateImage(stick.transform, "Knob", new Color(0.25f, 0.9f, 1f, 0.7f), Vector2.zero, new Vector2(105, 105), new Vector2(0.5f, 0.5f));
            stick.gameObject.AddComponent<MobileJoystick>().Configure((RectTransform)knob.transform);
            CreateAction(parent, weapon.LightName, new Vector2(-160, 175), 158, MobileAction.Attack, new Color(0.1f, 0.82f, 1f, 0.82f));
            CreateAction(parent, weapon.HeavyName, new Vector2(-345, 120), 132, MobileAction.Heavy, new Color(1f, 0.66f, 0.1f, 0.82f));
            CreateAction(parent, weapon.SkillName, new Vector2(-300, 305), 124, MobileAction.Skill, new Color(0.62f, 0.28f, 1f, 0.8f));
            CreateAction(parent, "DASH", new Vector2(-455, 250), 112, MobileAction.Dash, new Color(0.18f, 0.74f, 1f, 0.8f));
        }

        private void CreateAction(Transform parent, string label, Vector2 pos, float size, MobileAction action, Color color)
        {
            var image = CreateImage(parent, label, color, pos, new Vector2(size, size), new Vector2(1, 0));
            var spriteIndex = action switch { MobileAction.Attack => 0, MobileAction.Heavy => 3, MobileAction.Skill => 1, MobileAction.Dash => 2, _ => 0 };
            if (abilitySprites != null && spriteIndex < abilitySprites.Length)
            {
                image.sprite = abilitySprites[spriteIndex];
                image.color = Color.white;
                image.preserveAspect = true;
            }
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(4, -4);
            image.gameObject.AddComponent<MobileActionButton>().Configure(action);
            CreateText(image.transform, label, 18, TextAnchor.LowerCenter, new Vector2(0, 7), new Vector2(size, 28), new Vector2(0.5f, 0));
        }

        private Image CreateImage(Transform parent, string name, Color color, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void ApplyRounded(Image image)
        {
            if (!image) return;
            image.sprite = WorldHealthBar.RoundedUiSprite;
            image.type = Image.Type.Sliced;
        }

        private static void ApplyRoundedFill(Image image)
        {
            if (!image) return;
            image.sprite = WorldHealthBar.RoundedUiSprite;
        }

        private Image CreateFill(Transform parent)
            => CreateFill(parent, Color.white);

        private Image CreateFill(Transform parent, Color color)
        {
            var fill = CreateImage(parent, "Fill", color, Vector2.zero, Vector2.zero, new Vector2(0, 0.5f));
            var rect = (RectTransform)fill.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(4, 4);
            rect.offsetMax = new Vector2(-4, -4);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return fill;
        }

        private Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor)
            => CreateText(parent, value, size, alignment, position, dimensions, anchor, anchor, anchor);

        private Text CreateText(Transform parent, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions, Vector2 anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            if (anchorMin != anchorMax) { rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            // Roboto hat eine hoehere Zeilenhoehe als Arial. Legacy-Text blendet Zeilen aus, die
            // nicht ins Rechteck passen - so verschwanden Heldenname, Etagentitel, Punktzahl und
            // Rangname. Ueberlauf statt Ausblenden.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Helle Schrift mit dunkler Kontur und Schlagschatten: liest sich auf jedem Untergrund
            // und gibt der UI das Gewicht, das ihr als Platzhalter gefehlt hat.
            var stroke = go.AddComponent<Outline>();
            stroke.effectColor = new Color(0.04f, 0.025f, 0.02f, 0.9f);
            stroke.effectDistance = new Vector2(1.6f, -1.6f);
            var drop = go.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.5f);
            drop.effectDistance = new Vector2(0f, -2.6f);
            return text;
        }

        private Button CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateImage(parent, label, new Color(color.r * 0.28f, color.g * 0.28f, color.b * 0.28f, 1f), position, size, new Vector2(0.5f, 0.5f));
            ApplyRounded(image);
            var shadow = image.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(9f, -11f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(4, -4);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(Color.white, color, 0.45f);
            colors.pressedColor = color;
            button.colors = colors;
            var accent = CreateImage(image.transform, "Card Rarity Accent", color,
                new Vector2(0, -2), new Vector2(size.x, 7), new Vector2(0.5f, 1));
            accent.raycastTarget = false;
            var buttonLabel = CreateText(image.transform, label, 25, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            buttonLabel.fontStyle = FontStyle.Bold;
            return button;
        }
    }
}
