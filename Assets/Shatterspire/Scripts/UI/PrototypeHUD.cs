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
        private RunWallet wallet;
        private Action shopContinue;
        private Image hpFill;
        private Image hpChip;
        private Text hpText;
        private Text roomText;
        private Text objectiveText;
        private Text navigationText;
        private Text encounterText;
        private Text buildText;
        private ActionButtonView lightButton;
        private ActionButtonView heavyButton;
        private ActionButtonView skillButton;
        private ActionButtonView dashButton;
        private ActionButtonView ultimateButton;
        private bool ultimateWasReady;
        private Text heavyStateText;
        private CompanionBot[] team = Array.Empty<CompanionBot>();
        private Text[] teamStatus;
        private Image[] teamSupportFill;
        private bool skillWasReady = true;
        private bool heavyWasReady;
        private bool heavyEverReady;
        private Text knockoutText;
        private Image xpFill;
        private Text levelText;
        private Text goldText;
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

        public void Configure(GameObject player, RunConfig config, CompanionBot[] companions = null)
        {
            runConfig = config ?? new RunConfig();
            team = companions ?? Array.Empty<CompanionBot>();
            playerTransform = player.transform;
            playerHealth = player.GetComponent<Health>();
            build = player.GetComponent<PlayerBuild>();
            controller = player.GetComponent<PlayerController>();
            weapon = player.GetComponent<WeaponSystem>();
            levelSystem = player.GetComponent<LevelSystem>();
            wallet = player.GetComponent<RunWallet>();
            if (wallet) wallet.Changed += RefreshGold;
            BuildCanvas();
            Subscribe();
            RefreshHealth(playerHealth);
            RefreshHeavy(weapon.HeavyMeterNormalized, weapon.HeavyChargeNormalized, weapon.ChargingHeavy, weapon.HeavyPerfect);
            if (levelSystem) RefreshExperience(levelSystem.Level, levelSystem.CurrentXp, levelSystem.RequiredXp);
        }

        private void OnDestroy()
        {
            if (wallet) wallet.Changed -= RefreshGold;
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
            buildText = CreateText(root.transform, "NO UPGRADES YET", 12, TextAnchor.UpperLeft, new Vector2(112, -91), new Vector2(258, 22), new Vector2(0, 1));
            CreateExperienceBar(root.transform);
            CreateTeamFrames(root.transform);
            // Die Bedienflaechen zuerst: sie liegen damit unter den Aktionsknoepfen, und ein Tipp auf
            // einen Knopf geht an den Knopf, nicht an den Stick darunter.
            CreateTouchSticks(root.transform);
            CreateActionCluster(root.transform);
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
            UpdateActionCluster();
            UpdateTeamFrames();
            UpdateObjectiveNavigation();
        }

        private void LoadArtAssets()
        {
            abilitySprites = new[]
            {
                UiIconFactory.Ability(runConfig.Hero, 0), UiIconFactory.Ability(runConfig.Hero, 1),
                UiIconFactory.Ability(runConfig.Hero, 2), UiIconFactory.Ability(runConfig.Hero, 3),
                UiIconFactory.Ability(runConfig.Hero, 4)
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

            // Gold direkt unter dem Erfahrungsbalken: es entsteht im Kampf und wird gleich danach
            // beim Haendler ausgegeben, gehoert also in denselben Blick.
            goldText = CreateText(back.transform.parent, "0 GOLD", 15, TextAnchor.UpperLeft,
                new Vector2(96, -104), new Vector2(240, 22), new Vector2(0, 1));
            goldText.color = new Color(1f, 0.82f, 0.24f);
            goldText.fontStyle = FontStyle.Bold;
            RefreshGold();
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

        /// <summary>
        /// Team-Leiste oben rechts: je Bot Wappen, Name, Rolle und was er gerade tut. Lebensbalken gibt es
        /// bewusst nicht - die Bots nehmen noch keinen Schaden, eine volle Leiste waere Deko ohne Aussage.
        /// </summary>
        private void CreateTeamFrames(Transform parent)
        {
            teamStatus = new Text[team.Length];
            teamSupportFill = new Image[team.Length];
            for (var i = 0; i < team.Length; i++)
            {
                var bot = team[i];
                if (!bot) continue;
                var frame = CreateImage(parent, "Team Frame", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                    new Vector2(-16, -16 - i * 74), new Vector2(280, 66), new Vector2(1, 1));
                ApplyRounded(frame);
                frame.raycastTarget = false;
                var outline = frame.gameObject.AddComponent<Outline>();
                outline.effectColor = bot.Accent;
                outline.effectDistance = new Vector2(2f, -2f);
                var emblem = CreateImage(frame.transform, "Role Emblem", Color.white, new Vector2(8, 0), new Vector2(52, 52), new Vector2(0, 0.5f));
                emblem.sprite = UiIconFactory.Hero(RoleHero(bot.Role));
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
                CreateText(frame.transform, $"{bot.DisplayName}  <size=12>{bot.Role.ToString().ToUpperInvariant()}</size>", 18,
                    TextAnchor.UpperLeft, new Vector2(68, -7), new Vector2(200, 24), new Vector2(0, 1)).fontStyle = FontStyle.Bold;
                teamStatus[i] = CreateText(frame.transform, bot.Status, 13, TextAnchor.UpperLeft, new Vector2(68, -32), new Vector2(200, 18), new Vector2(0, 1));
                teamStatus[i].color = bot.Accent;
                if (bot.Role != CompanionRole.Support) continue;
                var back = CreateImage(frame.transform, "Heal Charge", new Color(0.05f, 0.035f, 0.03f, 0.92f),
                    new Vector2(68, 8), new Vector2(196, 8), new Vector2(0, 0));
                ApplyRounded(back);
                var fill = CreateFill(back.transform, bot.Accent);
                ((RectTransform)fill.transform).offsetMin = new Vector2(1, 1);
                ((RectTransform)fill.transform).offsetMax = new Vector2(-1, -1);
                teamSupportFill[i] = fill;
            }
            knockoutText = CreateText(parent, "TEAM LIVES  ◆◆◆", 16, TextAnchor.UpperRight,
                new Vector2(-20, -24 - team.Length * 74), new Vector2(300, 26), new Vector2(1, 1));
            knockoutText.color = new Color(1f, 0.78f, 0.2f);
        }

        private void UpdateTeamFrames()
        {
            if (teamStatus == null) return;
            for (var i = 0; i < team.Length; i++)
            {
                if (!team[i]) continue;
                if (teamStatus[i]) teamStatus[i].text = Loc.T(team[i].Status);
                if (teamSupportFill[i]) teamSupportFill[i].fillAmount = team[i].SupportReadyNormalized;
            }
        }

        private static HeroClassId RoleHero(CompanionRole role) => role switch
        {
            CompanionRole.Guardian => HeroClassId.Guardian,
            CompanionRole.Support => HeroClassId.Arcanist,
            _ => HeroClassId.Ranger
        };

        /// <summary>
        /// Zwei schwebende Sticks: links laufen, rechts zielen und dabei schiessen. Jede Haelfte ist
        /// vollstaendig Bedienflaeche - der Stick entsteht unter dem Daumen, wo auch immer der aufsetzt.
        ///
        /// Vorher gab es nur einen Stick an einer festen Stelle und das Ziel suchte sich das Spiel
        /// selbst. Beides war im Handytest der Hauptgrund, warum sich die Steuerung schwerfaellig
        /// anfuehlte und Schuesse auf falsche Gegner gingen.
        /// </summary>
        private void CreateTouchSticks(Transform parent)
        {
            if (!Application.isMobilePlatform) return;
            CreateTouchStick(parent, StickRole.Move, "Lauf-Flaeche", new Vector2(0f, 0f), new Vector2(0.5f, 1f),
                new Color(0.2f, 0.8f, 1f, 0.55f));
            CreateTouchStick(parent, StickRole.Aim, "Ziel-Flaeche", new Vector2(0.5f, 0f), new Vector2(1f, 1f),
                new Color(1f, 0.66f, 0.18f, 0.55f));
        }

        private void CreateTouchStick(Transform parent, StickRole role, string name, Vector2 anchorMin,
            Vector2 anchorMax, Color accent)
        {
            var zoneObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            zoneObject.transform.SetParent(parent, false);
            var zone = (RectTransform)zoneObject.transform;
            zone.anchorMin = anchorMin;
            zone.anchorMax = anchorMax;
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            // Unsichtbar, aber anfassbar: eine voll durchsichtige Flaeche bekommt keine Beruehrung.
            var surface = zoneObject.GetComponent<Image>();
            surface.color = new Color(0f, 0f, 0f, 0.004f);

            var ring = CreateImage(zone, name + " Ring", new Color(accent.r, accent.g, accent.b, 0.2f),
                Vector2.zero, new Vector2(240, 240), new Vector2(0.5f, 0.5f));
            ring.sprite = UiIconFactory.Disc();
            ring.raycastTarget = false;
            var knob = CreateImage(zone, name + " Knopf", accent, Vector2.zero, new Vector2(96, 96),
                new Vector2(0.5f, 0.5f));
            knob.sprite = UiIconFactory.Disc();
            knob.raycastTarget = false;

            zoneObject.AddComponent<MobileJoystick>()
                .Configure((RectTransform)ring.transform, (RectTransform)knob.transform, role);
        }

        /// <summary>
        /// Aktionsknoepfe wie in Mobile-Actionspielen: grosser Angriff in der Ecke, Heavy, Skill und Dash im
        /// Bogen darum. Auf dem Telefon sind es die Touch-Knoepfe, am PC dieselben Anzeigen mit Tastenhinweis.
        /// Heavy zeigt seinen Balken als Ring, Skill die Abklingzeit als Fuellung, Dash die Ladungen als Punkte.
        /// Ersetzt die Kachelreihe und den Balken mit Dauertext unten in der Mitte.
        /// </summary>
        private void CreateActionCluster(Transform parent)
        {
            var mobile = Application.isMobilePlatform;
            lightButton = CreateActionButton(parent, 0, mobile ? weapon.LightName : "LMB", new Vector2(-150, 150), 170,
                new Color(0.1f, 0.82f, 0.95f), MobileAction.Attack);
            heavyButton = CreateActionButton(parent, 3, mobile ? weapon.HeavyName : "RMB", new Vector2(-345, 118), 118,
                new Color(1f, 0.68f, 0.12f), MobileAction.Heavy);
            skillButton = CreateActionButton(parent, 1, mobile ? weapon.SkillName : "Q", new Vector2(-300, 290), 112,
                new Color(0.62f, 0.32f, 1f), MobileAction.Skill);
            dashButton = CreateActionButton(parent, 2, mobile ? "DASH" : "SPACE", new Vector2(-140, 345), 100,
                new Color(0.18f, 0.74f, 1f), MobileAction.Dash);
            // Ultimate oben im Bogen: laedt sich im Kampf, der Ring zeigt die Ladung.
            ultimateButton = CreateActionButton(parent, 4, mobile ? weapon.UltimateName : "R", new Vector2(-300, 440), 110,
                new Color(1f, 0.74f, 0.16f), MobileAction.Ultimate);
            ultimateButton.Status = CreateText(ultimateButton.Root, string.Empty, 15, TextAnchor.MiddleCenter,
                new Vector2(0, ultimateButton.Size * 0.5f + 14f), new Vector2(200, 20), new Vector2(0.5f, 0.5f));
            ultimateButton.Status.color = new Color(1f, 0.82f, 0.2f);

            // Goldenes Fenster fuer den perfekten Heavy: 50 bis 76 % der Ladung.
            heavyButton.PerfectZone = CreateRing(heavyButton.Root, "Perfect Zone", heavyButton.Size + 28f, new Color(1f, 0.8f, 0.12f, 0.6f), 0.26f);
            heavyButton.PerfectZone.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -180f);
            heavyButton.PerfectZone.gameObject.SetActive(false);
            heavyStateText = CreateText(parent, string.Empty, 17, TextAnchor.LowerCenter, Vector2.zero, new Vector2(320, 24), new Vector2(1, 0));
            heavyStateText.rectTransform.pivot = new Vector2(0.5f, 0f);
            heavyStateText.rectTransform.anchoredPosition = new Vector2(-345f, 194f);

            dashButton.Pips = new Image[5];
            for (var k = 0; k < dashButton.Pips.Length; k++)
            {
                var pip = CreateImage(dashButton.Root, "Dash Charge", Color.white, Vector2.zero, new Vector2(13, 13), new Vector2(0.5f, 0.5f));
                pip.sprite = UiIconFactory.Disc();
                pip.raycastTarget = false;
                pip.gameObject.SetActive(false);
                dashButton.Pips[k] = pip;
            }
        }

        private ActionButtonView CreateActionButton(Transform parent, int spriteIndex, string label, Vector2 center, float size,
            Color accent, MobileAction action)
        {
            var socket = CreateImage(parent, "Action " + action, new Color(0.08f, 0.06f, 0.05f, 0.88f), center, new Vector2(size, size), new Vector2(1, 0));
            socket.sprite = UiIconFactory.Disc();
            var root = socket.rectTransform;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = center;
            var view = new ActionButtonView { Root = root, Size = size, Accent = accent };

            CreateRing(root, "Rim", size, accent, 1f, 0.09f);
            var icon = CreateImage(root, "Icon", Color.white, Vector2.zero, new Vector2(size * 0.74f, size * 0.74f), new Vector2(0.5f, 0.5f));
            if (abilitySprites != null && spriteIndex >= 0 && spriteIndex < abilitySprites.Length) icon.sprite = abilitySprites[spriteIndex];
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            view.Cooldown = CreateImage(root, "Cooldown", new Color(0f, 0.01f, 0.03f, 0.66f), Vector2.zero, new Vector2(size * 0.92f, size * 0.92f), new Vector2(0.5f, 0.5f));
            view.Cooldown.sprite = UiIconFactory.Disc();
            view.Cooldown.type = Image.Type.Filled;
            view.Cooldown.fillMethod = Image.FillMethod.Radial360;
            view.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            view.Cooldown.fillClockwise = true;
            view.Cooldown.fillAmount = 0f;
            view.Cooldown.raycastTarget = false;
            view.Progress = CreateRing(root, "Progress", size + 12f, accent, 0f, 0.16f);

            view.Center = CreateText(root, string.Empty, Mathf.RoundToInt(size * 0.3f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            view.Center.fontStyle = FontStyle.Bold;
            CreateText(root, label, 14, TextAnchor.MiddleCenter, new Vector2(0, -size * 0.5f - 13f), new Vector2(size + 70, 20), new Vector2(0.5f, 0.5f));
            view.Badge = CreateText(root, string.Empty, 17, TextAnchor.MiddleCenter, new Vector2(size * 0.38f, size * 0.38f), new Vector2(44, 24), new Vector2(0.5f, 0.5f));
            view.Badge.fontStyle = FontStyle.Bold;
            view.Badge.color = new Color(1f, 0.82f, 0.2f);

            socket.raycastTarget = Application.isMobilePlatform;
            if (Application.isMobilePlatform) socket.gameObject.AddComponent<MobileActionButton>().Configure(action);
            return view;
        }

        private Image CreateRing(RectTransform parent, string name, float diameter, Color color, float fill, float thickness = 0.14f)
        {
            var ring = CreateImage(parent, name, color, Vector2.zero, new Vector2(diameter, diameter), new Vector2(0.5f, 0.5f));
            ring.sprite = UiIconFactory.Ring(thickness);
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = fill;
            ring.raycastTarget = false;
            return ring;
        }

        private void UpdateActionCluster()
        {
            if (skillButton != null)
            {
                var remaining = weapon.SkillCooldownRemaining;
                var ready = remaining <= 0f;
                skillButton.Cooldown.fillAmount = ready ? 0f : 1f - weapon.SkillNormalized;
                skillButton.Center.text = ready ? string.Empty : remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                if (ready && !skillWasReady) skillButton.Punch();
                skillWasReady = ready;
            }
            if (dashButton != null)
            {
                var max = Mathf.Min(controller.MaxDashCharges, dashButton.Pips.Length);
                var charges = controller.DashCharges;
                for (var k = 0; k < dashButton.Pips.Length; k++)
                {
                    var pip = dashButton.Pips[k];
                    var visible = k < max;
                    if (pip.gameObject.activeSelf != visible) pip.gameObject.SetActive(visible);
                    if (!visible) continue;
                    var angle = (90f - (k - (max - 1) * 0.5f) * 22f) * Mathf.Deg2Rad;
                    pip.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (dashButton.Size * 0.5f + 15f);
                    pip.color = k < charges ? dashButton.Accent : new Color(0.1f, 0.12f, 0.16f, 0.92f);
                }
                dashButton.Progress.fillAmount = charges >= controller.MaxDashCharges ? 0f : controller.DashRechargeNormalized;
                dashButton.Cooldown.fillAmount = charges <= 0 ? 1f - controller.DashRechargeNormalized : 0f;
            }
            if (heavyButton != null && weapon.HeavyReady && !weapon.ChargingHeavy)
                heavyButton.Progress.color = Color.Lerp(new Color(1f, 0.66f, 0.1f), new Color(1f, 0.95f, 0.7f),
                    0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f));
            if (ultimateButton != null)
            {
                var ready = weapon.UltimateReady;
                ultimateButton.Progress.fillAmount = weapon.UltimateNormalized;
                ultimateButton.Progress.color = ready
                    ? Color.Lerp(new Color(1f, 0.62f, 0.08f), new Color(1f, 0.96f, 0.72f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f))
                    : new Color(1f, 0.74f, 0.16f);
                ultimateButton.Cooldown.fillAmount = weapon.UltimateActive ? 1f : 1f - weapon.UltimateNormalized;
                ultimateButton.Center.text = ready || weapon.UltimateActive ? string.Empty : Mathf.FloorToInt(weapon.UltimateNormalized * 100f) + "%";
                ultimateButton.Status.text = ready ? Loc.T("ULTIMATE READY") : string.Empty;
                if (ready && !ultimateWasReady)
                {
                    ultimateButton.Punch();
                    ShowAnnouncement($"{weapon.UltimateName} READY\n{(Application.isMobilePlatform ? "TAP ULTIMATE" : "PRESS R")}", 1.2f);
                }
                ultimateWasReady = ready;
            }
            lightButton?.Tick();
            ultimateButton?.Tick();
            heavyButton?.Tick();
            skillButton?.Tick();
            dashButton?.Tick();
        }

        private sealed class ActionButtonView
        {
            public RectTransform Root;
            public Image Cooldown;
            public Image Progress;
            public Image PerfectZone;
            public Image[] Pips = Array.Empty<Image>();
            public Text Center;
            public Text Badge;
            public Text Status;
            public float Size;
            public Color Accent;
            public int BadgeCount;
            private float punchStarted = -10f;

            public void Punch() => punchStarted = Time.unscaledTime;

            /// <summary>Kurzes Aufploppen, wenn eine Aktion bereit wird oder ein Upgrade bekommt.</summary>
            public void Tick()
            {
                if (!Root) return;
                var t = (Time.unscaledTime - punchStarted) / 0.28f;
                var scale = t >= 0f && t < 1f ? 1f + Mathf.Sin(t * Mathf.PI) * 0.14f : 1f;
                Root.localScale = new Vector3(scale, scale, 1f);
            }
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
            var localized = Loc.T(instruction);
            objectiveText.text = required > 1 ? $"{localized}  ·  {current}/{required}" : localized;
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
                ? Loc.T("IRON WARDEN  ·  BOSS ENGAGED")
                : Loc.Language == Language.German
                    ? $"KERN-VERTEIDIGER  ·  {remaining} ÜBRIG"
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
            if (levelText) levelText.text = $"{Loc.T("LV")} {level}  ·  {current} / {required}";
        }

        private void RefreshWave(int current, int total)
        {
            if (current <= 0 || total <= 0) return;
            ShowAnnouncement($"RIFT WAVE {current} / {total}\nINCOMING", current == 1 ? 1.15f : 1.4f);
        }

        private void ShowAnnouncement(string message, float seconds)
        {
            if (!announcementGroup || !announcementText) return;
            announcementText.text = Loc.T(message);
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
            if (heavyButton == null) return;
            var ready = meter >= 0.999f;
            heavyButton.Progress.fillAmount = charging ? charge : meter;
            heavyButton.Progress.color = perfect ? new Color(1f, 0.82f, 0.12f)
                : charging ? Color.white
                : ready ? new Color(1f, 0.72f, 0.12f) : new Color(0.15f, 0.72f, 1f);
            if (heavyButton.PerfectZone) heavyButton.PerfectZone.gameObject.SetActive(charging);
            if (ready && !heavyWasReady)
            {
                heavyButton.Punch();
                heavyEverReady = true;
            }
            heavyWasReady = ready;
            if (!heavyStateText) return;
            // Der Erklaertext steht nur, bis der Heavy zum ersten Mal voll war. Danach reicht der Ring.
            heavyStateText.text = perfect ? Loc.T(Application.isMobilePlatform ? "PERFECT!  RELEASE" : "PERFECT!  RELEASE RMB")
                : charging ? "RELEASE IN GOLD"
                : ready ? weapon.HeavyName + " READY"
                : heavyEverReady ? string.Empty : "LIGHT HITS CHARGE HEAVY";
            heavyStateText.color = perfect ? new Color(1f, 0.82f, 0.14f) : new Color(1f, 0.95f, 0.86f);
        }

        private void RefreshKnockout(int skulls, int maximum, float reviveProgress, bool downed)
        {
            if (!knockoutText) return;
            if (downed && skulls < maximum)
            {
                knockoutText.text = $"{Loc.T("ALLY REVIVING")} {HeroCatalog.Name(runConfig.Hero)}  {reviveProgress:P0}";
                knockoutText.color = new Color(0.3f, 1f, 0.58f);
                return;
            }
            var lives = Mathf.Max(0, maximum - skulls);
            knockoutText.text = Loc.T("TEAM LIVES") + "  " + new string('◆', lives) + new string('◇', maximum - lives);
            knockoutText.color = lives <= 1 ? new Color(1f, 0.32f, 0.22f) : new Color(1f, 0.78f, 0.2f);
        }

        private void OnRoomStarted(int index, RoomKind kind)
        {
            var counter = PathCatalog.FloorCounter(runConfig.Mode, index);
            roomText.text = $"{counter}  ·  {Loc.Of(kind)}";
            ShowAnnouncement($"{FloorCatalog.Name(FloorCatalog.ThemeFor(index))}\n{Loc.T("FLOOR")} {index} · {Loc.Of(kind)}", 1.8f);
        }
        private void OnPerkSelected(PerkDefinition _) => RefreshBuild();

        /// <summary>Upgrades zeigen sich an der Aktion, die sie veraendern: "+2" am Knopf. Passive stehen unter dem Namen.</summary>
        private void RefreshBuild()
        {
            var counts = new int[Enum.GetValues(typeof(ActionSlot)).Length];
            var passives = new List<string>();
            foreach (var id in build.Perks)
            {
                var perk = PerkCatalog.Find(id);
                if (perk == null) continue;
                counts[(int)perk.Slot]++;
                if (perk.Slot == ActionSlot.Passive) passives.Add(Loc.T(perk.Name));
            }
            SetBadge(lightButton, counts[(int)ActionSlot.Light]);
            SetBadge(heavyButton, counts[(int)ActionSlot.Heavy]);
            SetBadge(skillButton, counts[(int)ActionSlot.Skill]);
            SetBadge(dashButton, counts[(int)ActionSlot.Dash]);
            SetBadge(ultimateButton, counts[(int)ActionSlot.Ultimate]);
            var fusion = build.IsInferno ? "  ·  FUSION: INFERNO" : build.IsShatter ? "  ·  FUSION: SHATTER" : build.IsChainStorm ? "  ·  FUSION: CHAIN STORM" : string.Empty;
            buildText.text = passives.Count > 0 ? string.Join(" · ", passives) + fusion
                : Loc.T(build.Perks.Count > 0 ? "UPGRADES SHOWN ON YOUR ACTIONS" : "NO UPGRADES YET");
        }

        private static void SetBadge(ActionButtonView view, int count)
        {
            if (view?.Badge == null) return;
            view.Badge.text = count > 0 ? "+" + count : string.Empty;
            if (count > view.BadgeCount) view.Punch();
            view.BadgeCount = count;
        }

        public void ShowFloorUpgrade(Action afterSelection)
        {
            // Reihenfolge zwischen zwei Etagen: erst eine Verbesserung waehlen, dann der Haendler,
            // danach geht es weiter. Wer beim Haendler nichts kauft, verliert nichts.
            postPerkCallback = () => ShowShop(afterSelection);
            selectingLevelPerk = false;
            ShowPerkChoice();
        }

        /// <summary>
        /// Haendler zwischen den Etagen. Gold kommt aus erledigten Gegnern, die Preise steigen mit
        /// jedem Kauf derselben Ware, und die Waffe gibt es genau einmal je Aufstieg.
        /// </summary>
        private void ShowShop(Action afterShop)
        {
            if (modal || !wallet)
            {
                afterShop?.Invoke();
                return;
            }
            Time.timeScale = 0f;
            shopContinue = afterShop;
            BuildShopModal();
        }

        private void BuildShopModal()
        {
            if (modal) Destroy(modal);
            modalButtons.Clear();
            modal = CreateModal($"{Loc.T("TRADER")}  ·  {wallet.Gold} {Loc.T("GOLD")}",
                Loc.T("SPEND GOLD OR KEEP IT FOR LATER"));
            var offers = ShopCatalog.All;
            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                var owned = wallet.TimesBought(offer.Id);
                var soldOut = offer.Unique && owned > 0;
                var price = wallet.PriceOf(offer);
                var affordable = wallet.CanAfford(offer);
                var label = soldOut ? Loc.T("SOLD OUT") : $"{price} {Loc.T("GOLD")}";
                var stack = owned > 0 && !offer.Unique ? "\n" + Loc.T("OWNED") + "  " + owned : string.Empty;
                var column = i % 3;
                var row = i / 3;
                var button = CreateButton(modal.transform,
                    Loc.T(offer.Name) + "\n\n" + Loc.T(offer.Description) + "\n\n" + label + stack,
                    new Vector2(-390f + column * 390f, 130f - row * 250f), new Vector2(340f, 220f),
                    affordable ? offer.Color : new Color(0.34f, 0.36f, 0.4f));
                if (affordable)
                {
                    var chosen = offer;
                    button.onClick.AddListener(() =>
                    {
                        if (!wallet.Buy(chosen, build)) return;
                        Sfx.Play2D(Sound.UiConfirm);
                        RefreshGold();
                        // Preise und Kassenstand haben sich geaendert - der Laden wird neu gezeichnet.
                        BuildShopModal();
                    });
                }
                modalButtons.Add(button);
            }
            var leave = CreateButton(modal.transform, Loc.T("CONTINUE CLIMB"), new Vector2(0f, -330f),
                new Vector2(420f, 90f), new Color(0.2f, 0.82f, 0.6f));
            leave.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                Destroy(modal);
                modal = null;
                modalButtons.Clear();
                Time.timeScale = 1f;
                var next = shopContinue;
                shopContinue = null;
                next?.Invoke();
            });
            modalButtons.Add(leave);
        }

        private void RefreshGold()
        {
            if (goldText && wallet) goldText.text = $"{wallet.Gold} {Loc.T("GOLD")}";
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
                    $"[{i + 1}]  {Loc.T(PerkCatalog.SlotLabel(perk.Slot, hero))}\n{Loc.T(perk.Name)}\n\n{Loc.T(perk.Description)}\n\n{rarity}",
                    new Vector2(-390f + i * 390f, -20f), new Vector2(340f, 420f), perk.Color);
                button.onClick.AddListener(() =>
                {
                    Sfx.Play2D(Sound.UiConfirm);
                    SelectPerk(perk);
                });
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
                button.onClick.AddListener(() =>
                {
                    Sfx.Play2D(Sound.UiConfirm);
                    SelectRoute(kind);
                });
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
            extractButton.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                ChooseExtract();
            });
            modalButtons.Add(extractButton);
            if (canAscend)
            {
                var ascendButton = CreateButton(modal.transform,
                    "[2]  ASCEND\n\nSTRONGER ENEMIES\n+35% TIER REWARDS", new Vector2(260, -30), new Vector2(420, 390), new Color(0.72f, 0.25f, 1f));
                ascendButton.onClick.AddListener(() =>
                {
                    Sfx.Play2D(Sound.UiConfirm);
                    ChooseAscend();
                });
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
            CreateText(scorePanel.transform, "CLIMB SCORE", 22, TextAnchor.UpperCenter,
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
            labels.AppendLine(result.Extracted ? "EXTRACTED" : "FALLEN");
            values.AppendLine(result.Extracted ? "x1.15" : "x0.70");
            CreateText(scorePanel.transform, labels.ToString(), 24, TextAnchor.UpperLeft,
                new Vector2(30, -122), new Vector2(320, 150), new Vector2(0f, 1f));
            CreateText(scorePanel.transform, values.ToString(), 24, TextAnchor.UpperRight,
                new Vector2(-30, -122), new Vector2(260, 150), new Vector2(1f, 1f));

            var rankPanel = CreateImage(modal.transform, "Rank", new Color(0.035f, 0.075f, 0.12f, 0.98f),
                new Vector2(345, 25), new Vector2(660, 300), new Vector2(0.5f, 0.5f));
            ApplyRounded(rankPanel);
            var tier = RankTable.TierFor(rankPoints);
            CreateText(rankPanel.transform, $"RANK · SHIFT {save.shiftIndex}", 22, TextAnchor.UpperCenter,
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
                ? "HIGHEST RANK REACHED"
                : $"{toNext:N0} POINTS TO {RankTable.Name((RankTier)((int)tier + 1))}";
            CreateText(rankPanel.transform,
                $"RANK POINTS  {rankPoints:N0}\nFROM YOUR {RankTable.ClimbCount} BEST CLIMBS\n{next}\n\nSHIFT ENDS IN  {ShiftCalendar.Countdown(ShiftCalendar.Remaining)}",
                24, TextAnchor.UpperCenter, new Vector2(0, -160), new Vector2(600, 130), new Vector2(0.5f, 1));

            var walletPanel = CreateImage(modal.transform, "Wallet", new Color(0.03f, 0.06f, 0.1f, 0.96f),
                new Vector2(0, -140), new Vector2(1010, 62), new Vector2(0.5f, 0.5f));
            ApplyRounded(walletPanel);
            CreateText(walletPanel.transform,
                $"SHARDS  +{earned}  ·  TOTAL {save.shards}      TOKENS  {save.tokens}      FLOOR  {Mathf.Max(1, roomsCleared)}  ·  BEST {save.bestFloor}",
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

        private static void RestartSceneInternal() => PrototypeBootstrap.Reload();

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
            // Ein Durchlass fuer alle beim Aufbau gesetzten Texte.
            text.text = Loc.T(value);
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
