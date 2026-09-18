using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>
    /// Welches der Modals gerade offen steht. Zwischen zwei Etagen laufen drei hintereinander -
    /// Verbesserung, Haendler, Route - und die automatische Vorfuehrung muss wissen, in welchem
    /// sie steht, statt Knopfnummern zu raten.
    /// </summary>
    public enum ModalKind { None, Perk, Shop, Routes, Ascension, RunEnd, Pause, Abandon }

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
        private PartyMember[] team = Array.Empty<PartyMember>();
        private Text[] teamStatus;
        private Image[] teamHealthFill;
        private bool skillWasReady = true;
        private bool heavyWasReady;
        private bool heavyEverReady;
        private Text knockoutText;
        private Image xpFill;
        private Text levelText;
        private Text goldText;
        private Text scoreText;
        private FloorModifierId floorAnomaly;

        /// <summary>Laufende Etage. Die Auswahl der Verbesserungen haengt daran.</summary>
        private int currentFloor = 1;
        private bool ultimateWasUnlocked = true;
        private Image vaultPanel;
        private Text vaultText;
        private Image anomalyPanel;
        private Text anomalyText;
        private Text streakText;
        private ScreenFade screenFade;
        private KillStreak killStreak;
        private RunDirector director;
        private int shownScore;
        private CanvasGroup announcementGroup;
        private Text announcementText;
        private float announcementUntil;
        private Sprite[] abilitySprites;
        private GameObject modal;
        private readonly List<Button> modalButtons = new();
        private ModalKind openModal;
        private Action<RoomKind, FloorModifierId> routeCallback;
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

        public void Configure(GameObject player, RunConfig config, BotInput[] companions = null)
        {
            runConfig = config ?? new RunConfig();
            // Die Leiste zeigt die Mitglieder, nicht ihre Koepfe: ein Mitglied hat einen Namen,
            // ein Leben und eine Farbe, egal ob ein Bot oder ein Mensch es fuehrt.
            team = CollectTeam(companions);
            playerTransform = player.transform;
            playerHealth = player.GetComponent<Health>();
            build = player.GetComponent<PlayerBuild>();
            controller = player.GetComponent<PlayerController>();
            weapon = player.GetComponent<WeaponSystem>();
            levelSystem = player.GetComponent<LevelSystem>();
            wallet = player.GetComponent<RunWallet>();
            if (wallet) wallet.Changed += RefreshGold;
            killStreak = player.GetComponent<KillStreak>();
            if (killStreak) killStreak.StepReached += OnStreakStep;
            BuildCanvas();
            Subscribe();
            RefreshHealth(playerHealth);
            RefreshHeavy(weapon.HeavyMeterNormalized, weapon.HeavyChargeNormalized, weapon.ChargingHeavy, weapon.HeavyPerfect);
            if (levelSystem) RefreshExperience(levelSystem.Level, levelSystem.CurrentXp, levelSystem.RequiredXp);
        }

        private void OnDestroy()
        {
            if (wallet) wallet.Changed -= RefreshGold;
            if (killStreak) killStreak.StepReached -= OnStreakStep;
            GameEvents.HealthChanged -= RefreshHealth;
            GameEvents.PerkSelected -= OnPerkSelected;
            GameEvents.RoomStarted -= OnRoomStarted;
            GameEvents.AnomalyChanged -= RefreshAnomaly;
            GameEvents.Notice -= ShowAnnouncement;
            GameEvents.VaultChanged -= RefreshVault;
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
            GameEvents.AnomalyChanged += RefreshAnomaly;
            GameEvents.Notice += ShowAnnouncement;
            GameEvents.VaultChanged += RefreshVault;
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
            hpText = CreateText(hpBack.transform, string.Empty, 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);

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

            roomText = CreateText(objectivePanel.transform, string.Empty, 24, TextAnchor.UpperCenter,
                new Vector2(0, -7), new Vector2(468, 28), new Vector2(0.5f, 1));
            roomText.fontStyle = FontStyle.Bold;
            objectiveText = CreateText(objectivePanel.transform, string.Empty, 16, TextAnchor.UpperCenter,
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
            // Das Anomalie-Schild sitzt direkt unter dem Etagenkopf und ist nur da, wenn die Etage
            // eine traegt. Ein Schild, das immer steht, wird nach der dritten Etage nicht mehr
            // gelesen - und genau diese Information muss im Kampf abrufbar bleiben.
            anomalyPanel = CreateImage(root.transform, "Floor Anomaly", new Color(0.09f, 0.06f, 0.05f, 0.92f),
                new Vector2(0, -126), new Vector2(330, 32), new Vector2(0.5f, 1));
            ApplyRounded(anomalyPanel);
            anomalyPanel.raycastTarget = false;
            anomalyText = CreateText(anomalyPanel.transform, string.Empty, 17, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            anomalyText.fontStyle = FontStyle.Bold;
            anomalyPanel.gameObject.SetActive(false);
            // Die Uhr der Schatzkammer sitzt unter der Ansage und ist nur in der Kammer da.
            vaultPanel = CreateImage(root.transform, "Vault Timer", new Color(0.1f, 0.07f, 0.02f, 0.92f),
                new Vector2(0, -272), new Vector2(390, 38), new Vector2(0.5f, 1));
            ApplyRounded(vaultPanel);
            vaultPanel.raycastTarget = false;
            vaultText = CreateText(vaultPanel.transform, string.Empty, 19, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            vaultText.fontStyle = FontStyle.Bold;
            vaultPanel.gameObject.SetActive(false);
            buildText = CreateText(root.transform, Loc.T("NO UPGRADES YET"), 12, TextAnchor.UpperLeft, new Vector2(112, -91), new Vector2(258, 22), new Vector2(0, 1));
            CreateExperienceBar(root.transform);
            CreateTeamFrames(root.transform);
            // Die Bedienflaechen zuerst: sie liegen damit unter den Aktionsknoepfen, und ein Tipp auf
            // einen Knopf geht an den Knopf, nicht an den Stick darunter.
            // Gefahr von aussen und kritisches Leben. Liegt unter den Bedienflaechen, damit nichts
            // davon einen Tipp abfaengt.
            var threats = new GameObject("Threat Layer", typeof(RectTransform), typeof(ThreatMarkers));
            threats.transform.SetParent(root.transform, false);
            var threatRect = (RectTransform)threats.transform;
            threatRect.anchorMin = Vector2.zero;
            threatRect.anchorMax = Vector2.one;
            threatRect.offsetMin = Vector2.zero;
            threatRect.offsetMax = Vector2.zero;
            threats.GetComponent<ThreatMarkers>().Configure(playerTransform, Camera.main, canvas);

            CreateTouchSticks(root.transform);
            CreateActionCluster(root.transform);
            CreateAnnouncement(root.transform);
            // Zuletzt, damit er oben liegt. Auf dem Telefon ist die ganze linke Haelfte die Flaeche des
            // Laufsticks; wurde der Knopf vor ihr gebaut, lag sie darueber und schluckte jede
            // Beruehrung - der Tipp auf die Pause startete den Stick. Am PC fiel das nie auf, weil es
            // dort keine Stickflaechen gibt.
            CreatePauseButton(root.transform);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
            if (!modal) HandlePauseTap();
            HandleModalInput();
            RefreshScore();
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
            levelText = CreateText(back.transform, string.Empty, 11, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            levelText.fontStyle = FontStyle.Bold;

            // Gold direkt unter dem Erfahrungsbalken: es entsteht im Kampf und wird gleich danach
            // beim Haendler ausgegeben, gehoert also in denselben Blick.
            goldText = CreateText(back.transform.parent, string.Empty, 15, TextAnchor.UpperLeft,
                new Vector2(96, -104), new Vector2(240, 22), new Vector2(0, 1));
            goldText.color = new Color(1f, 0.82f, 0.24f);
            goldText.fontStyle = FontStyle.Bold;
            RefreshGold();

            // Punkte oben in der Mitte unter dem Etagenkopf: dort schaut man beim Abschluss einer
            // Etage ohnehin hin. Die Serie sitzt gross darunter, weil sie im Kampf gelesen wird.
            scoreText = CreateText(back.transform.parent, "0", 20, TextAnchor.UpperRight,
                new Vector2(-20, -206), new Vector2(260, 26), new Vector2(1, 1));
            scoreText.color = new Color(0.72f, 0.86f, 1f);
            scoreText.fontStyle = FontStyle.Bold;
            streakText = CreateText(back.transform.parent, string.Empty, 30, TextAnchor.UpperRight,
                new Vector2(-20, -236), new Vector2(320, 40), new Vector2(1, 1));
            streakText.color = new Color(1f, 0.68f, 0.16f);
            streakText.fontStyle = FontStyle.Bold;
        }

        /// <summary>Punkte und Serie. Punkte zaehlen sichtbar hoch statt zu springen.</summary>
        private void RefreshScore()
        {
            if (!director) director = FindFirstObjectByType<RunDirector>();
            if (scoreText && director)
            {
                var live = ClimbScore.Raw(director.LiveResult);
                // Hochzaehlen statt setzen: eine Zahl, die laeuft, wird gelesen; eine, die springt, nicht.
                shownScore = live - shownScore > 400
                    ? Mathf.RoundToInt(Mathf.Lerp(shownScore, live, 8f * Time.unscaledDeltaTime))
                    : live;
                scoreText.text = shownScore.ToString("N0") + "  " + Loc.T("POINTS");
            }
            if (!streakText || !killStreak) return;
            if (killStreak.Count < 2)
            {
                streakText.text = string.Empty;
                return;
            }
            var multiplier = killStreak.Multiplier;
            streakText.text = killStreak.Count + "  " + Loc.T("STREAK") +
                              (multiplier > 1f ? "   x" + multiplier.ToString("0.0") : string.Empty);
            // Die Farbe waermt mit der Stufe, und die Anzeige verblasst, wenn die Serie auslaeuft.
            var heat = Mathf.InverseLerp(1f, 3f, multiplier);
            var color = Color.Lerp(new Color(1f, 0.78f, 0.3f), new Color(1f, 0.32f, 0.1f), heat);
            color.a = Mathf.Lerp(0.45f, 1f, killStreak.Remaining);
            streakText.color = color;
        }

        private void OnStreakStep(int count, float multiplier)
        {
            ShowAnnouncement(count + "  " + Loc.T("STREAK") + "   x" + multiplier.ToString("0.0"), 0.9f);
            Sfx.Play2D(Sound.StreakStep, 0.6f);
            CameraController.Impulse(0.05f);
        }

        private void CreateAnnouncement(Transform parent)
        {
            // Unter dem Anomalie-Schild bei -126 und drei Zeilen hoch: der Etagenkopf nennt
            // Thema, Etage, Raumart und Anomalie in einem Zug.
            var panel = CreateImage(parent, "Combat Announcement", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                new Vector2(0, -166), new Vector2(500, 96), new Vector2(0.5f, 1));
            ApplyRounded(panel);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.76f, 0.22f, 0.92f);
            outline.effectDistance = new Vector2(3f, -3f);
            announcementGroup = panel.gameObject.AddComponent<CanvasGroup>();
            announcementGroup.blocksRaycasts = false;
            announcementGroup.interactable = false;
            announcementText = CreateText(panel.transform, string.Empty, 20, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            announcementText.fontStyle = FontStyle.Bold;
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Die Mitglieder der Gruppe, ohne den Helden an diesem Geraet - der hat seine eigene Leiste
        /// unten links. Kommt aus den Bots, faellt aber auf die Anmeldung zurueck, damit auch ein
        /// Mitspieler aus dem Netz hier auftaucht, sobald es ihn gibt.
        /// </summary>
        private static PartyMember[] CollectTeam(BotInput[] companions)
        {
            var found = new List<PartyMember>();
            if (companions != null)
                foreach (var bot in companions)
                {
                    if (!bot) continue;
                    var member = bot.GetComponent<PartyMember>();
                    if (member) found.Add(member);
                }
            if (found.Count == 0)
                foreach (var member in PartyMember.Active)
                    if (member && !member.IsLocal) found.Add(member);
            return found.ToArray();
        }

        /// <summary>
        /// Team-Leiste oben rechts: je Mitglied Wappen, Name, was es gerade tut - und sein Leben.
        ///
        /// Der Lebensbalken stand hier frueher ausdruecklich nicht, mit der Begruendung, die Begleiter
        /// naehmen keinen Schaden und eine volle Leiste waere Deko. Genau das hat sich geaendert: sie
        /// sind Helden wie der Spieler, sie fallen, und dann steht er allein da. Das muss man sehen
        /// koennen, bevor es passiert.
        /// </summary>
        private void CreateTeamFrames(Transform parent)
        {
            teamStatus = new Text[team.Length];
            teamHealthFill = new Image[team.Length];
            for (var i = 0; i < team.Length; i++)
            {
                var member = team[i];
                if (!member) continue;
                var frame = CreateImage(parent, "Team Frame", new Color(0.09f, 0.06f, 0.05f, 0.9f),
                    new Vector2(-16, -16 - i * 74), new Vector2(280, 66), new Vector2(1, 1));
                ApplyRounded(frame);
                frame.raycastTarget = false;
                var outline = frame.gameObject.AddComponent<Outline>();
                outline.effectColor = member.Accent;
                outline.effectDistance = new Vector2(2f, -2f);
                var emblem = CreateImage(frame.transform, "Hero Emblem", Color.white, new Vector2(8, 0),
                    new Vector2(52, 52), new Vector2(0, 0.5f));
                emblem.sprite = UiIconFactory.Hero(member.HeroClass);
                emblem.preserveAspect = true;
                emblem.raycastTarget = false;
                CreateText(frame.transform,
                    member.DisplayName + "  <size=12>" + Loc.T(HeroCatalog.Role(member.HeroClass)) + "</size>", 18,
                    TextAnchor.UpperLeft, new Vector2(68, -7), new Vector2(200, 24), new Vector2(0, 1))
                    .fontStyle = FontStyle.Bold;
                teamStatus[i] = CreateText(frame.transform, Loc.T(member.Status), 13, TextAnchor.UpperLeft,
                    new Vector2(68, -30), new Vector2(200, 18), new Vector2(0, 1));
                teamStatus[i].color = member.Accent;

                var back = CreateImage(frame.transform, "Team Health", new Color(0.05f, 0.035f, 0.03f, 0.92f),
                    new Vector2(68, 8), new Vector2(196, 9), new Vector2(0, 0));
                ApplyRounded(back);
                var fill = CreateFill(back.transform, member.Accent);
                ((RectTransform)fill.transform).offsetMin = new Vector2(1, 1);
                ((RectTransform)fill.transform).offsetMax = new Vector2(-1, -1);
                teamHealthFill[i] = fill;
            }
            knockoutText = CreateText(parent, string.Empty, 16, TextAnchor.UpperRight,
                new Vector2(-20, -24 - team.Length * 74), new Vector2(300, 26), new Vector2(1, 1));
            knockoutText.color = new Color(1f, 0.78f, 0.2f);
        }

        private void UpdateTeamFrames()
        {
            if (teamStatus == null) return;
            for (var i = 0; i < team.Length; i++)
            {
                var member = team[i];
                if (!member) continue;
                if (teamStatus[i]) teamStatus[i].text = Loc.T(member.Status);
                if (!teamHealthFill[i]) continue;
                var down = member.GetComponent<FallenHero>();
                if (down && down.IsDown)
                {
                    // Am Boden zeigt derselbe Balken, wie weit das Aufheben ist. Ein leerer Balken
                    // saehe aus wie wenig Leben; ein steigender sagt, dass gerade jemand hilft.
                    teamHealthFill[i].fillAmount = Mathf.Max(0.04f, down.ReviveProgress);
                    teamHealthFill[i].color = down.BeingRevived
                        ? Color.Lerp(new Color(0.85f, 0.2f, 0.22f), member.Accent, down.ReviveProgress)
                        : new Color(0.5f, 0.18f, 0.2f);
                    continue;
                }
                teamHealthFill[i].fillAmount = member.Health ? member.Health.Normalized : 0f;
                teamHealthFill[i].color = member.Accent;
            }
        }


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
            if (ultimateButton != null && !weapon.UltimateUnlocked)
            {
                // Gesperrt: kein Fuellstand, der nie steigt, sondern die Etage, ab der es sie gibt.
                ultimateButton.Progress.fillAmount = 0f;
                ultimateButton.Cooldown.fillAmount = 1f;
                ultimateButton.Center.text = Loc.T("FLOOR") + " " + weapon.UltimateUnlockFloor;
                ultimateButton.Status.text = Loc.T("LOCKED");
                ultimateWasUnlocked = false;
            }
            else if (ultimateButton != null)
            {
                if (!ultimateWasUnlocked)
                {
                    ultimateWasUnlocked = true;
                    // Nur ansagen, wenn sie im Lauf freigeschaltet wurde - nicht beim Start eines
                    // Laufs, der auf einer spaeteren Etage beginnt.
                    if (currentFloor > 1)
                    {
                        ultimateButton.Punch();
                        ShowAnnouncement(Loc.T("ULTIMATE UNLOCKED") + "\n" + Loc.T(weapon.UltimateName) + " · "
                                         + Mathf.RoundToInt(weapon.UltimatePower * 100f) + "%", 1.6f);
                    }
                }
                var ready = weapon.UltimateReady;
                ultimateButton.Progress.fillAmount = weapon.UltimateNormalized;
                ultimateButton.Progress.color = ready
                    ? Color.Lerp(new Color(1f, 0.62f, 0.08f), new Color(1f, 0.96f, 0.72f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f))
                    : new Color(1f, 0.74f, 0.16f);
                ultimateButton.Cooldown.fillAmount = weapon.UltimateActive ? 1f : 1f - weapon.UltimateNormalized;
                ultimateButton.Center.text = ready || weapon.UltimateActive ? string.Empty : Mathf.FloorToInt(weapon.UltimateNormalized * 100f) + "%";
                // Unter voller Staerke steht der Anteil dabei: man soll sehen, dass sie noch waechst.
                var power = weapon.UltimatePower;
                ultimateButton.Status.text = ready
                    ? Loc.T("ULTIMATE READY")
                    : power < 0.999f ? Mathf.RoundToInt(power * 100f) + "% " + Loc.T("POWER") : string.Empty;
                if (ready && !ultimateWasReady)
                {
                    ultimateButton.Punch();
                    ShowAnnouncement(Loc.T(weapon.UltimateName) + " " + Loc.T("READY") + "\n" +
                        Loc.T(Application.isMobilePlatform ? "TAP ULTIMATE" : "PRESS R"), 1.2f);
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

            for (var i = 0; i < Mathf.Min(4, modalButtons.Count); i++)
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

        /// <summary>Welches Modal gerade offen steht.</summary>
        public ModalKind OpenModal => modal ? openModal : ModalKind.None;

        /// <summary>Wie viele Knoepfe es hat.</summary>
        public int OpenModalButtons => modal ? modalButtons.Count : 0;

        /// <summary>
        /// Drueckt einen Knopf des offenen Modals. Nur fuer die automatische Vorfuehrung: die
        /// Modals sind der einzige Teil der Oberflaeche, den keine Bildfolge ohne echte Eingabe
        /// erreicht - und genau dort steht die Wahl, die geprueft werden muss.
        /// </summary>
        public bool PressModalForCapture(int index)
        {
            if (!modal || index < 0 || index >= modalButtons.Count) return false;
            InvokeModalButton(index);
            return true;
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
                : $"{Loc.T("CORE DEFENDERS")}  ·  {remaining} {Loc.T("REMAINING")}";
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
            // Die Aufrufer setzen ihre Ansage schon uebersetzt zusammen; hier nur noch der
            // stille Durchlass fuer die, die einen reinen Schluessel uebergeben.
            announcementText.text = Loc.TQuiet(message);
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
                : charging ? Loc.T("RELEASE IN GOLD")
                : ready ? Loc.T(weapon.HeavyName) + " " + Loc.T("READY")
                : heavyEverReady ? string.Empty : Loc.T("LIGHT HITS CHARGE HEAVY");
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
            currentFloor = index;
            var counter = PathCatalog.FloorCounter(runConfig.Mode, index);
            roomText.text = $"{counter}  ·  {Loc.Of(kind)}";
            var anomaly = FloorModifierCatalog.For(floorAnomaly);
            // Eine Ansage fuer die ganze Etage. Zwei Banner hintereinander haben sich im ersten
            // Aufnahmelauf gegenseitig ueberdeckt.
            var line = anomaly.IsCalm
                ? string.Empty
                : $"\n{Loc.T("ANOMALY")}: {Loc.T(anomaly.Name)}";
            ShowAnnouncement($"{Loc.T(FloorCatalog.Name(FloorCatalog.ThemeFor(index)))}"
                + $"\n{Loc.T("FLOOR")} {index} · {Loc.Of(kind)}{line}", 2.2f);
        }
        /// <summary>
        /// Traegt die Anomalie der begonnenen Etage ein. Bei einer ruhigen Etage verschwindet das
        /// Schild wieder - sonst stuende dort dauerhaft "keine Anomalie", also Platz ohne Inhalt.
        /// </summary>
        private void RefreshAnomaly(FloorModifierId id)
        {
            floorAnomaly = id;
            var anomaly = FloorModifierCatalog.For(id);
            if (!anomalyPanel || !anomalyText) return;
            anomalyPanel.gameObject.SetActive(!anomaly.IsCalm);
            if (anomaly.IsCalm) return;
            // Nur der Name: die Zahlen standen auf dem Knopf, mit dem die Route gewaehlt wurde,
            // und ein Schild, das im Kampf vier Faktoren aufzaehlt, liest niemand.
            anomalyText.text = $"{Loc.T("ANOMALY")}:  {Loc.T(anomaly.Name)}";
            anomalyText.color = anomaly.Accent;
            var outline = anomalyPanel.GetComponent<Outline>() ?? anomalyPanel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(anomaly.Accent.r, anomaly.Accent.g, anomaly.Accent.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>
        /// Die Uhr der Schatzkammer. Vor dem ersten Hort steht nur, wie viele es sind - die Zeit
        /// laeuft erst, wenn man sie selbst gestartet hat, und das soll man am Bild sehen.
        /// </summary>
        private void RefreshVault(int opened, int total, float secondsLeft, bool active)
        {
            if (!vaultPanel || !vaultText) return;
            vaultPanel.gameObject.SetActive(active && total > 0);
            if (!active || total <= 0) return;
            var running = opened > 0;
            vaultText.text = running
                ? $"{Loc.T("VAULT")}  {opened}/{total}  ·  {Mathf.CeilToInt(secondsLeft)} s"
                : $"{Loc.T("VAULT")}  {opened}/{total}";
            // Die letzten fuenf Sekunden rot: bis dahin ist die Uhr Information, danach Druck.
            vaultText.color = running && secondsLeft <= 5f
                ? new Color(1f, 0.34f, 0.24f)
                : new Color(1f, 0.8f, 0.22f);
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
            var fusionName = build.IsInferno ? "INFERNO" : build.IsShatter ? "SHATTER"
                : build.IsChainStorm ? "CHAIN STORM" : null;
            var fusion = fusionName == null ? string.Empty
                : $"  ·  {Loc.T("FUSION")}: {Loc.T(fusionName)}";
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

        /// <summary>Blende auf einen Wert fahren. 1 ist schwarz. Gebraucht von der Aufzugsfahrt.</summary>
        public void Fade(float target, float seconds = 0.4f)
        {
            if (!screenFade) screenFade = ScreenFade.Attach(canvas.transform);
            screenFade.To(target, seconds);
        }

        /// <summary>Die Blende deckt vollstaendig ab - der Etagenwechsel ist jetzt unsichtbar.</summary>
        public bool FadeOpaque => screenFade && screenFade.Opaque;

        /// <summary>
        /// Zwischen zwei Etagen bleibt genau ein Fenster: die Verbesserung. Danach die Route.
        ///
        /// Vorher standen drei Vollbild-Fenster hintereinander - Verbesserung, Haendler, Route.
        /// Auf fuenfzehn Etagen sind das fuenfundvierzig, und der Haendler stand jedes Mal da,
        /// auch mit null Gold. Er ist jetzt ein Stand im Aufzugsraum, an dem man vorbeikommt:
        /// eine Entscheidung statt einer Unterbrechung.
        /// </summary>
        /// <param name="heading">Ueberschrift statt "Etage geschafft" - etwa fuer die Wahl zu Laufbeginn.</param>
        public void ShowFloorUpgrade(Action afterSelection, string heading = null)
        {
            postPerkCallback = afterSelection;
            selectingLevelPerk = false;
            perkHeading = heading;
            ShowPerkChoice();
        }

        private string perkHeading;

        /// <summary>
        /// Oeffnet den Haendler von aussen - vom Stand im Aufzugsraum. Gibt false zurueck, wenn
        /// gerade schon ein Fenster offen steht.
        /// </summary>
        public bool OpenTrader(Action afterShop, bool fromStall = false)
        {
            if (modal || !wallet) return false;
            shopAtStall = fromStall;
            ShowShop(afterShop);
            return true;
        }

        /// <summary>Am Stand geht man weiter, zwischen zwei Etagen steigt man auf.</summary>
        private bool shopAtStall;

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
            openModal = ModalKind.Shop;
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
            var leave = CreateButton(modal.transform, Loc.T(shopAtStall ? "LEAVE TRADER" : "CONTINUE CLIMB"),
                new Vector2(0f, -330f),
                new Vector2(420f, 90f), new Color(0.2f, 0.82f, 0.6f));
            leave.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                Destroy(modal);
                modal = null;
                openModal = ModalKind.None;
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
            perkHeading = null;
            ShowPerkChoice();
        }

        private void ShowPerkChoice()
        {
            if (modal) return;
            Time.timeScale = 0f;
            openModal = ModalKind.Perk;
            modal = CreateModal(
                $"{Loc.T(perkHeading ?? (selectingLevelPerk ? "LEVEL UP" : "FLOOR CLEARED"))} · {Loc.T("CHOOSE AN UPGRADE")}",
                Loc.T("EVERY UPGRADE CHANGES ONE OF YOUR ACTIONS"));
            var hero = runConfig.Hero;
            // Das Upgrade wirkt ab der naechsten Etage - dort muss die Ultimate schon da sein.
            // Wie viele Karten, sagt das Prestige: ein Meister waehlt aus vier.
            var choices = PerkCatalog.RollChoices(hero, new HashSet<PerkId>(build.Perks), perkRandom,
                currentFloor, build, weapon.UltimateUnlockFloor <= currentFloor + 1,
                HeroPrestige.UpgradeChoices(build.PrestigeStep));
            for (var i = 0; i < choices.Count; i++)
            {
                var perk = choices[i];
                var rarity = Loc.T(perk.Rarity.ToString().ToUpperInvariant()) +
                             (perk.Heroes.Length == 1
                                 ? $"  ·  {Loc.T("ONLY FOR")} {HeroCatalog.Name(hero)}"
                                 : string.Empty);
                // Was die Karte fuer den jetzigen Aufbau bedeutet: der naechste Rang einer Aktion,
                // die man schon zugespitzt hat, oder eine Fusion, die sie vollendet. Ohne das sind
                // drei Karten nur drei Namen nebeneinander.
                var meaning = PerkCatalog.MeaningFor(perk, build);
                var title = Loc.T(perk.Name)
                            + (string.IsNullOrEmpty(meaning) ? string.Empty : "  " + meaning);
                var button = CreateButton(modal.transform,
                    $"[{i + 1}]  {PerkCatalog.SlotLabel(perk.Slot, hero)}\n{title}\n\n{Loc.T(perk.Description)}\n\n{rarity}",
                    new Vector2((i - (choices.Count - 1) * 0.5f) * 390f, -20f), new Vector2(340f, 420f), perk.Color);
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
            openModal = ModalKind.None;
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
            perkHeading = null;
            var callback = postPerkCallback;
            postPerkCallback = null;
            callback?.Invoke();
        }

        /// <summary>
        /// Die Wahl am Aufzug. Jede Route traegt ihre eigene Anomalie, und die steht vor der Wahl
        /// offen auf dem Knopf: erst dadurch ist die Entscheidung eine Entscheidung und nicht das
        /// Antippen einer Beschriftung.
        /// </summary>
        public void ShowRoutes(int nextRoom, int runSeed, Action<RoomKind, FloorModifierId> callback)
        {
            if (modal) return;
            routeCallback = callback;
            Time.timeScale = 0f;
            openModal = ModalKind.Routes;
            modal = CreateModal($"{Loc.T("TEAM VOTE")} · {Loc.T("FLOOR")} {nextRoom}",
                Loc.T("YOUR PARTY FOLLOWS THE SELECTED ROUTE"));
            var options = PathCatalog.RoutesFor(runSeed, nextRoom);
            for (var i = 0; i < options.Length; i++)
            {
                var kind = options[i];
                var color = kind switch
                {
                    RoomKind.Elite => new Color(0.86f, 0.2f, 0.55f),
                    RoomKind.Boss => new Color(1f, 0.45f, 0.08f),
                    RoomKind.Treasure => new Color(1f, 0.78f, 0.16f),
                    _ => new Color(0.15f, 0.7f, 0.82f)
                };
                var heading = kind switch
                {
                    RoomKind.Elite => Loc.T("ELITE FIGHT"),
                    RoomKind.Boss => Loc.T("SPIRE WARDEN"),
                    _ => Loc.Of(kind)
                };
                var blurb = kind switch
                {
                    RoomKind.Elite => Loc.T("HIGH RISK, MORE SHARDS"),
                    RoomKind.Treasure => Loc.T("EXTRA HEALING"),
                    RoomKind.Mystery => Loc.T("UNKNOWN ENCOUNTER"),
                    RoomKind.Boss => Loc.T("ASCENSION TRIAL"),
                    _ => Loc.T("BALANCED RESISTANCE")
                };
                var anomalyId = FloorModifierCatalog.Offer(runSeed, nextRoom, kind);
                var anomaly = FloorModifierCatalog.For(anomalyId);
                // Der Name in der Farbe der Anomalie, darunter je Zeile eine Wirkung - rot, gruen oder gold.
                var anomalyName = anomaly.IsCalm
                    ? Loc.T(anomaly.Name)
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(anomaly.Accent)}>{Loc.T("ANOMALY")}: {Loc.T(anomaly.Name)}</color>";
                var button = CreateButton(modal.transform,
                    $"[{i + 1}]  {heading}\n{blurb}\n\n{anomalyName}\n{FloorModifierCatalog.Effects(anomaly)}",
                    new Vector2((i - (options.Length - 1) * 0.5f) * 390f, -20f), new Vector2(340f, 360f), color);
                button.onClick.AddListener(() =>
                {
                    Sfx.Play2D(Sound.UiConfirm);
                    SelectRoute(kind, anomalyId);
                });
                modalButtons.Add(button);
            }
        }

        private void SelectRoute(RoomKind kind, FloorModifierId anomaly)
        {
            Destroy(modal);
            modal = null;
            openModal = ModalKind.None;
            modalButtons.Clear();
            Time.timeScale = 1f;
            routeCallback?.Invoke(kind, anomaly);
        }

        public void ShowAscensionChoice(int floor, int carriedShards, bool canAscend, Action extract, Action ascend)
        {
            if (modal) Destroy(modal);
            extractCallback = extract;
            ascendCallback = ascend;
            Time.timeScale = 0f;
            openModal = ModalKind.Ascension;
            modal = CreateModal(Loc.T("THE ASCENSION GATE"),
                $"{Loc.T("FLOOR")} {floor} {Loc.T("CLEARED")}  ·  {carriedShards} {Loc.T("SHARDS AT STAKE")}");
            var extractButton = CreateButton(modal.transform,
                $"[1]  {Loc.T("EXTRACT")}\n\n{Loc.T("SECURE ALL SHARDS")}\n{Loc.T("RETURN TO SKYHOLD")}",
                new Vector2(-260, -30), new Vector2(420, 390), new Color(0.12f, 0.9f, 0.64f));
            extractButton.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                ChooseExtract();
            });
            modalButtons.Add(extractButton);
            if (canAscend)
            {
                var ascendButton = CreateButton(modal.transform,
                    $"[2]  {Loc.T("ASCEND")}\n\n{Loc.T("STRONGER ENEMIES")}\n{Loc.T("+35% TIER REWARDS")}",
                    new Vector2(260, -30), new Vector2(420, 390), new Color(0.72f, 0.25f, 1f));
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
            openModal = ModalKind.None;
            modalButtons.Clear();
            Time.timeScale = 1f;
        }

        public void ShowRunEnd(bool victory, int earned, MetaSaveData save, int roomsCleared,
            ClimbResult result, int score, int rankPoints, RunMode? unlockedPath = null, int badges = 0)
        {
            if (modal) Destroy(modal);
            if (Camera.main)
            {
                Camera.main.enabled = true;
                Camera.main.targetDisplay = 0;
            }
            Time.timeScale = 0f;
            openModal = ModalKind.RunEnd;
            // Ein neu geoeffneter Pfad ist die wichtigste Nachricht dieses Bildschirms - er steht oben.
            var subtitle = unlockedPath.HasValue
                ? PathCatalog.Name(unlockedPath.Value) + " · " + Loc.T("PATH UNLOCKED")
                : Loc.T(victory ? "THE TEAM RETURNS WITH SECURED SHARDS" : "A PORTION OF YOUR SHARDS SURVIVED");
            modal = CreateModal(Loc.T(victory ? "TOWER PATH CLEARED" : "CLIMB ENDED"), subtitle);

            // Links die Punkte dieses Aufstiegs, rechts was er fuer den Rang
            // bedeutet. Die Aufschluesselung steht bewusst da: ein Rang, dessen
            // Zustandekommen man nicht sieht, motiviert nicht.
            var scorePanel = CreateImage(modal.transform, "Climb Score", new Color(0.035f, 0.075f, 0.12f, 0.98f),
                new Vector2(-345, 25), new Vector2(660, 300), new Vector2(0.5f, 0.5f));
            ApplyRounded(scorePanel);
            CreateText(scorePanel.transform, Loc.T("CLIMB SCORE"), 22, TextAnchor.UpperCenter,
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
            labels.AppendLine(Loc.T(result.Extracted ? "EXTRACTED" : "FALLEN"));
            values.AppendLine(result.Extracted ? "x1.15" : "x0.70");
            CreateText(scorePanel.transform, labels.ToString(), 24, TextAnchor.UpperLeft,
                new Vector2(30, -122), new Vector2(320, 150), new Vector2(0f, 1f));
            CreateText(scorePanel.transform, values.ToString(), 24, TextAnchor.UpperRight,
                new Vector2(-30, -122), new Vector2(260, 150), new Vector2(1f, 1f));

            var rankPanel = CreateImage(modal.transform, "Rank", new Color(0.035f, 0.075f, 0.12f, 0.98f),
                new Vector2(345, 25), new Vector2(660, 300), new Vector2(0.5f, 0.5f));
            ApplyRounded(rankPanel);
            var tier = RankTable.TierFor(rankPoints);
            CreateText(rankPanel.transform, $"{Loc.T("RANK")} · {Loc.T("SHIFT")} {save.shiftIndex}", 22, TextAnchor.UpperCenter,
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
                ? Loc.T("HIGHEST RANK REACHED")
                : $"{toNext:N0} {Loc.T("POINTS TO")} {RankTable.Name((RankTier)((int)tier + 1))}";
            CreateText(rankPanel.transform,
                $"{Loc.T("RANK POINTS")}  {rankPoints:N0}\n{Loc.T("FROM YOUR")} {RankTable.ClimbCount} {Loc.T("BEST CLIMBS")}\n{next}\n\n{Loc.T("SHIFT ENDS IN")}  {ShiftCalendar.Countdown(ShiftCalendar.Remaining)}",
                24, TextAnchor.UpperCenter, new Vector2(0, -160), new Vector2(600, 130), new Vector2(0.5f, 1));

            var walletPanel = CreateImage(modal.transform, "Wallet", new Color(0.03f, 0.06f, 0.1f, 0.96f),
                new Vector2(0, -140), new Vector2(1010, 62), new Vector2(0.5f, 0.5f));
            ApplyRounded(walletPanel);
            CreateText(walletPanel.transform,
                $"{Loc.T("SHARDS")}  +{earned}  ·  {Loc.T("TOTAL")} {save.shards}      {Loc.T("TOKENS")}  {save.tokens}"
                + $"      {Loc.T("FLOOR")}  {Mathf.Max(1, roomsCleared)}  ·  {Loc.T("BEST")} {save.bestFloor}",
                26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            // Was der Lauf dem gespielten Helden gebracht hat. Steht neben der Kasse, weil es
            // dieselbe Frage beantwortet: was bleibt davon.
            var prestige = MetaSaveSystem.PrestigeStep(save, runConfig.Hero);
            var rank = Loc.T(HeroPrestige.RankName(prestige))
                       + (HeroPrestige.SubStep(prestige) > 0 ? " " + HeroPrestige.SubStep(prestige) : string.Empty);
            CreateText(walletPanel.transform,
                $"{HeroCatalog.Name(runConfig.Hero)}  ·  {rank}  ·  +{badges} {Loc.T("BADGES")}"
                + $"  ·  {Loc.T("YOU HAVE")} {MetaSaveSystem.Badges(save, runConfig.Hero)}",
                20, TextAnchor.MiddleCenter, new Vector2(0, -34), new Vector2(1010, 28),
                new Vector2(0.5f, 0.5f)).color = HeroPrestige.RankColor(prestige);
            var restart = CreateButton(modal.transform, Loc.T("CLIMB AGAIN"), new Vector2(225, -225), new Vector2(360, 96), new Color(0.1f, 0.86f, 0.72f));
            restart.onClick.AddListener(RestartRun);
            modalButtons.Add(restart);
            var home = CreateButton(modal.transform, Loc.T("MAIN MENU"), new Vector2(-225, -225), new Vector2(360, 96), new Color(0.46f, 0.38f, 0.7f));
            home.onClick.AddListener(RestartScene);
            modalButtons.Add(home);
        }

        // ── Pause ───────────────────────────────────────────────────────────

        /// <summary>
        /// Wird gerufen, wenn der Spieler den Aufstieg ueber die Pause verlaesst. Setzt der RunDirector:
        /// nur er kann den Lauf so abschliessen, dass Splitter und Erfahrung richtig verbucht werden.
        /// </summary>
        public Action AbandonRequested;

        /// <summary>Anteil der Splitter, der beim Verlassen bleibt - derselbe wie bei einer Niederlage.</summary>
        public float AbandonShareKept = 0.65f;

        private Button pauseButton;

        private void CreatePauseButton(Transform parent)
        {
            // Oben neben dem Heldenfeld, in Daumenreichweite und weit weg von den Aktionsknoepfen.
            var button = CreateButton(parent, "II", new Vector2(0, 0), new Vector2(64, 64), new Color(0.55f, 0.62f, 0.72f));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(412, -22);
            button.onClick.AddListener(ShowPause);
            button.transform.SetAsLastSibling();
            pauseButton = button;
        }

        /// <summary>
        /// Derselbe Weg wie bei den Fensterknoepfen: eine eigene Trefferpruefung, unabhaengig davon,
        /// welche Flaeche das Klick-System gerade fuer die oberste haelt. Loest der Knopf zusaetzlich
        /// selbst aus, passiert nichts Doppeltes - eine offene Pause oeffnet sich nicht ein zweites Mal.
        /// </summary>
        private void HandlePauseTap()
        {
            if (!pauseButton) return;
            var rect = (RectTransform)pauseButton.transform;
            // Auf dem Telefon meldet Unity jede Beruehrung zusaetzlich als Maus - dort zaehlen nur die Beruehrungen.
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0)
                && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null))
            {
                ShowPause();
                return;
            }
            for (var i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, touch.position, null)) continue;
                ShowPause();
                return;
            }
        }

        /// <summary>Esc schliesst, was offen ist, oder oeffnet die Pause - aber nie ueber einer Wahl.</summary>
        private void HandleEscape()
        {
            switch (openModal)
            {
                case ModalKind.None when !modal:
                    ShowPause();
                    break;
                case ModalKind.Pause:
                    ClosePause();
                    break;
                case ModalKind.Abandon:
                    ShowPause();
                    break;
            }
        }

        public void ShowPause()
        {
            // Ueber einer Upgrade-, Routen- oder Endwahl oeffnet die Pause nicht: diese Fenster halten
            // das Spiel selbst an, und zwei Fenster uebereinander liessen sich nicht sauber schliessen.
            if (modal && openModal != ModalKind.Abandon) return;
            if (modal) Destroy(modal);
            Time.timeScale = 0f;
            openModal = ModalKind.Pause;
            modal = CreateModal(Loc.T("PAUSED"), Loc.T("THE CLIMB WAITS FOR YOU"));
            var resume = CreateButton(modal.transform, "[1]  " + Loc.T("RESUME"), new Vector2(-210, -40),
                new Vector2(360, 110), new Color(0.1f, 0.86f, 0.72f));
            resume.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiConfirm);
                ClosePause();
            });
            modalButtons.Add(resume);
            var leave = CreateButton(modal.transform, "[2]  " + Loc.T("MAIN MENU"), new Vector2(210, -40),
                new Vector2(360, 110), new Color(0.46f, 0.38f, 0.7f));
            leave.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                ShowAbandonConfirm();
            });
            modalButtons.Add(leave);
        }

        /// <summary>
        /// Vor dem Verlassen steht die Folge im Bild. Ohne diese Rueckfrage waere "ins Menue und neu"
        /// ein Weg, einem schlechten Lauf auszuweichen - und ein versehentlicher Tipp kostete den Lauf.
        /// </summary>
        private void ShowAbandonConfirm()
        {
            if (modal) Destroy(modal);
            Time.timeScale = 0f;
            openModal = ModalKind.Abandon;
            var kept = Mathf.RoundToInt(AbandonShareKept * 100f);
            modal = CreateModal(Loc.T("LEAVE THE CLIMB?"),
                $"{Loc.T("THE CLIMB ENDS AS A DEFEAT")}  ·  {Loc.T("YOU KEEP")} {kept}% {Loc.T("OF YOUR SHARDS")}");
            var back = CreateButton(modal.transform, "[1]  " + Loc.T("BACK"), new Vector2(-210, -40),
                new Vector2(360, 110), new Color(0.1f, 0.86f, 0.72f));
            back.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                ShowPause();
            });
            modalButtons.Add(back);
            var confirm = CreateButton(modal.transform, "[2]  " + Loc.T("LEAVE"), new Vector2(210, -40),
                new Vector2(360, 110), new Color(0.9f, 0.3f, 0.3f));
            confirm.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiConfirm);
                if (AbandonRequested != null) AbandonRequested();
                else RestartScene();
            });
            modalButtons.Add(confirm);
        }

        private void ClosePause()
        {
            if (modal) Destroy(modal);
            modal = null;
            openModal = ModalKind.None;
            modalButtons.Clear();
            Time.timeScale = 1f;
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
            // Ein Durchlass fuer alle beim Aufbau gesetzten Texte. Ohne Meldung, weil hier auch
            // fertig zusammengesetzte Texte ankommen - siehe Loc.TQuiet.
            text.text = Loc.TQuiet(value);
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
