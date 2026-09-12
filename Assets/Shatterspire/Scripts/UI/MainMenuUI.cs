using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>
    /// Lobby statt Textliste: der gewaehlte Held steht gross in der Mitte, daneben die zwei Bots der
    /// Party. Links Held und Relics, rechts Pfad und CLIMB, oben Waehrung und Rang. Vorher lag eine
    /// zu 76 % deckende Flaeche ueber der Szene und die Helden waren kaum zu sehen.
    /// </summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        private static readonly HeroClassId[] Heroes = { HeroClassId.Ranger, HeroClassId.Guardian, HeroClassId.Arcanist, HeroClassId.Bomber };
        private static readonly RunMode[] Paths = { RunMode.Brave, RunMode.Heroic, RunMode.Legendary };

        private readonly List<Button> buttons = new();
        private readonly Dictionary<KeyCode, Action> shortcuts = new();
        private readonly RunConfig config = new();
        private Font font;
        private Canvas canvas;
        private GameObject screen;
        private LobbyStage stage;
        private Action backAction;
        private RectTransform[] plates;

        public void Configure(LobbyStage lobbyStage)
        {
            stage = lobbyStage;
            // Roboto Black statt Arial: kraeftige Buchstaben wie in Mobile-Actionspielen.
            font = Resources.Load<Font>("Fonts/Roboto-Black") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var save = MetaSaveSystem.Load();
            config.Relics.Clear();
            foreach (var value in save.equippedRelics)
                if (Enum.IsDefined(typeof(RelicId), value) && config.Relics.Count < 3)
                    config.Relics.Add((RelicId)value);
            if (Enum.IsDefined(typeof(HeroClassId), save.lastHero)) config.Hero = (HeroClassId)save.lastHero;
            if (Enum.IsDefined(typeof(RunMode), save.lastPath)) config.Mode = (RunMode)save.lastPath;
            BuildCanvas();
            ShowLobby();
        }

        private void BuildCanvas()
        {
            // Eingaben laufen direkt ueber Update, damit Maus und Touch unabhaengig vom EventSystem gehen.
            var root = new GameObject("SHATTERSPIRE Front End", typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.6f;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && backAction != null) { backAction.Invoke(); return; }
            foreach (var shortcut in shortcuts)
            {
                if (!Input.GetKeyDown(shortcut.Key)) continue;
                shortcut.Value.Invoke();
                return;
            }
            if (Input.GetMouseButtonDown(0)) TryClick(Input.mousePosition);
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) TryClick(Input.GetTouch(0).position);
            UpdatePlates();
        }

        // ── Lobby ───────────────────────────────────────────────────────────

        private void ShowLobby()
        {
            BeginScreen(false);
            backAction = null;
            var save = MetaSaveSystem.Load();
            var hero = config.Hero;
            var accent = HeroCatalog.Accent(hero);
            stage.Show(hero);

            Fade(true, 260f, 0.85f);
            Fade(false, 360f, 0.9f);

            var title = Text(screen.transform, "SHATTERSPIRE", 46, TextAnchor.UpperLeft, new Vector2(56, -28), new Vector2(620, 60), new Vector2(0, 1));
            title.fontStyle = FontStyle.Bold;
            Text(screen.transform, "CLIMB  ·  ADAPT  ·  RISK IT ALL", 18, TextAnchor.UpperLeft, new Vector2(60, -86),
                new Vector2(620, 26), new Vector2(0, 1)).color = new Color(0.16f, 0.92f, 0.86f);
            BuildTopRight(save);
            BuildHeroColumn(hero, accent);
            BuildRelicSlots(accent);
            BuildPathColumn();
            BuildPartyPlates(hero, accent);
            AnnounceShiftRewardOnce();

            if (!Application.isMobilePlatform)
                Text(screen.transform, Loc.T("ARROW KEYS HINT"), 15,
                    TextAnchor.LowerCenter, new Vector2(0, 14), new Vector2(900, 24), new Vector2(0.5f, 0)).color = new Color(0.62f, 0.7f, 0.8f);

            shortcuts[KeyCode.LeftArrow] = () => CycleHero(-1);
            shortcuts[KeyCode.A] = () => CycleHero(-1);
            shortcuts[KeyCode.RightArrow] = () => CycleHero(1);
            shortcuts[KeyCode.D] = () => CycleHero(1);
            shortcuts[KeyCode.UpArrow] = () => CyclePath(-1);
            shortcuts[KeyCode.W] = () => CyclePath(-1);
            shortcuts[KeyCode.DownArrow] = () => CyclePath(1);
            shortcuts[KeyCode.S] = () => CyclePath(1);
            for (var i = 0; i < Heroes.Length; i++)
            {
                var choice = Heroes[i];
                shortcuts[KeyCode.Alpha1 + i] = () => SelectHero(choice);
            }
            shortcuts[KeyCode.R] = ShowRelics;
            shortcuts[KeyCode.F] = ShowForge;
            shortcuts[KeyCode.Return] = BeginRun;
            shortcuts[KeyCode.KeypadEnter] = BeginRun;
            shortcuts[KeyCode.Space] = BeginRun;
        }

        private void BuildTopRight(MetaSaveData save)
        {
            var rankPoints = MetaSaveSystem.RankPoints(save);
            var tier = RankTable.TierFor(rankPoints);
            var rank = Panel(screen.transform, new Vector2(-48, -26), new Vector2(380, 116), new Vector2(1, 1),
                new Color(0.018f, 0.035f, 0.07f, 0.92f), RankTable.Accent(tier));
            Text(rank.transform, Loc.T("SHIFT") + " " + save.shiftIndex + "  ·  " + Loc.T("ENDS IN") + " " +
                ShiftCalendar.Countdown(ShiftCalendar.Remaining), 15,
                TextAnchor.UpperLeft, new Vector2(24, -12), new Vector2(340, 22), new Vector2(0, 1)).color = new Color(0.62f, 0.72f, 0.84f);
            var rankName = Text(rank.transform, RankTable.Name(tier), 38, TextAnchor.UpperLeft, new Vector2(22, -34), new Vector2(340, 46), new Vector2(0, 1));
            rankName.fontStyle = FontStyle.Bold;
            rankName.color = RankTable.Accent(tier);
            var next = RankTable.IsHighest(tier)
                ? "HIGHEST RANK"
                : RankTable.PointsToNext(rankPoints).ToString("N0") + " " + Loc.T("TO") + " " +
                  RankTable.Name((RankTier)((int)tier + 1));
            Text(rank.transform, rankPoints.ToString("N0") + " " + Loc.T("RANK POINTS") + "  ·  " + next, 15, TextAnchor.UpperLeft,
                new Vector2(24, -84), new Vector2(340, 22), new Vector2(0, 1));

            Chip(new Vector2(-448, -26), "SHARDS", save.shards.ToString("N0"), new Color(0.3f, 0.78f, 1f));
            Chip(new Vector2(-448, -86), "TOKENS", save.tokens.ToString("N0"), new Color(1f, 0.74f, 0.2f));
        }

        private void Chip(Vector2 position, string label, string value, Color color)
        {
            var chip = Panel(screen.transform, position, new Vector2(220, 52), new Vector2(1, 1), new Color(0.018f, 0.035f, 0.07f, 0.9f), color);
            Text(chip.transform, label, 14, TextAnchor.MiddleLeft, new Vector2(18, 0), new Vector2(90, 40), new Vector2(0, 0.5f)).color = color;
            var amount = Text(chip.transform, value, 24, TextAnchor.MiddleRight, new Vector2(-18, 0), new Vector2(120, 40), new Vector2(1, 0.5f));
            amount.fontStyle = FontStyle.Bold;
        }

        private void BuildHeroColumn(HeroClassId hero, Color accent)
        {
            for (var i = 0; i < Heroes.Length; i++)
            {
                var candidate = Heroes[i];
                var selected = candidate == hero;
                var tab = Button(screen.transform, string.Empty, new Vector2(56 + i * 108, -150), new Vector2(92, 92), new Vector2(0, 1),
                    selected ? Color.white : new Color(0.36f, 0.44f, 0.54f), () => SelectHero(candidate), selected);
                var icon = CreateImage(tab.transform, "Hero Emblem", Color.white, Vector2.zero, new Vector2(78, 78), new Vector2(0.5f, 0.5f));
                icon.sprite = UiIconFactory.Hero(candidate);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            var name = Text(screen.transform, HeroCatalog.Name(hero), 78, TextAnchor.UpperLeft, new Vector2(52, -262), new Vector2(560, 92), new Vector2(0, 1));
            name.fontStyle = FontStyle.Bold;
            Text(screen.transform, HeroCatalog.Role(hero), 22, TextAnchor.UpperLeft, new Vector2(58, -350), new Vector2(560, 30), new Vector2(0, 1)).color = accent;
            Text(screen.transform,
                Loc.T("LIGHT") + "   " + Loc.T(HeroCatalog.LightAttackName(hero)) + "\n" +
                Loc.T("HEAVY") + "   " + Loc.T(HeroCatalog.HeavyAttackName(hero)) + "\n" +
                Loc.T("SKILL") + "   " + Loc.T(HeroCatalog.SkillName(hero)) + "\n" +
                Loc.T("ULTIMATE") + "   " + Loc.T(HeroCatalog.UltimateName(hero)) + "\n\n" +
                Loc.T("HP") + "   " + HeroCatalog.BaseHealth(hero).ToString("0"),
                19, TextAnchor.UpperLeft, new Vector2(58, -396), new Vector2(520, 150), new Vector2(0, 1)).color = new Color(0.86f, 0.92f, 0.98f);
        }

        private void BuildRelicSlots(Color accent)
        {
            Text(screen.transform, Loc.T("RELICS") + "  " + config.Relics.Count + "/3", 18, TextAnchor.LowerLeft, new Vector2(58, 214), new Vector2(400, 26), new Vector2(0, 0))
                .color = new Color(0.62f, 0.72f, 0.84f);
            for (var i = 0; i < 3; i++)
            {
                var filled = i < config.Relics.Count;
                var label = filled ? Loc.T(RelicCatalog.Name(config.Relics[i])) : Loc.T("+ EMPTY");
                Button(screen.transform, label, new Vector2(56 + i * 166, 108), new Vector2(154, 96), new Vector2(0, 0),
                    filled ? accent : new Color(0.32f, 0.4f, 0.5f), ShowRelics, labelSize: 16);
            }
            Button(screen.transform, "META FORGE", new Vector2(56, 28), new Vector2(486, 62), new Vector2(0, 0),
                new Color(1f, 0.55f, 0.08f), ShowForge, labelSize: 20);
            // Sprachschalter: der Text steht bewusst in der jeweils anderen Sprache, damit man
            // sieht, worauf man umschaltet. Nach dem Wechsel baut die Lobby sich neu auf.
            Button(screen.transform, Loc.LanguageName, new Vector2(56, 100), new Vector2(238, 52), new Vector2(0, 0),
                new Color(0.35f, 0.55f, 0.72f), () =>
                {
                    Loc.Toggle();
                    ShowLobby();
                }, labelSize: 16);
        }

        private void BuildPathColumn()
        {
            Text(screen.transform, "CHOOSE YOUR PATH", 18, TextAnchor.LowerRight, new Vector2(-56, 420), new Vector2(460, 26), new Vector2(1, 0))
                .color = new Color(0.62f, 0.72f, 0.84f);
            for (var i = 0; i < Paths.Length; i++)
            {
                var path = Paths[i];
                var selected = path == config.Mode;
                Button(screen.transform, PathCatalog.Name(path) + "\n<size=16>" + Loc.T(PathCatalog.Summary(path)) + "</size>",
                    new Vector2(-56, 326 - i * 94), new Vector2(460, 84), new Vector2(1, 0),
                    selected ? PathCatalog.Accent(path) : new Color(0.32f, 0.4f, 0.5f), () => SelectPath(path), selected);
            }
            var climb = Button(screen.transform, Loc.T("CLIMB") + "\n<size=18>" + PathCatalog.Name(config.Mode)
                + "  ·  " + Loc.T(PathCatalog.Summary(config.Mode)) + "</size>",
                new Vector2(-56, 28), new Vector2(460, 112), new Vector2(1, 0), PathCatalog.Accent(config.Mode), BeginRun, true, 42);
            climb.GetComponentInChildren<Text>().color = Color.white;
        }

        private void BuildPartyPlates(HeroClassId hero, Color accent)
        {
            var team = PrototypeBootstrap.OfflineTeamFor(hero);
            plates = new RectTransform[1 + team.Length];
            plates[0] = Plate("YOU", HeroCatalog.Name(hero), accent);
            for (var i = 0; i < team.Length; i++)
                plates[i + 1] = Plate("BOT", team[i].Name + "  " + Loc.Of(team[i].Role), team[i].Accent);
            UpdatePlates();
        }

        private RectTransform Plate(string tag, string name, Color color)
        {
            var plate = Panel(screen.transform, Vector2.zero, new Vector2(230, 58), new Vector2(0.5f, 0.5f),
                new Color(0.018f, 0.035f, 0.07f, 0.88f), color);
            var rect = (RectTransform)plate.transform;
            rect.pivot = new Vector2(0.5f, 1f);
            Text(plate.transform, tag, 13, TextAnchor.UpperCenter, new Vector2(0, -6), new Vector2(210, 18), new Vector2(0.5f, 1)).color = color;
            Text(plate.transform, name, 19, TextAnchor.UpperCenter, new Vector2(0, -24), new Vector2(220, 26), new Vector2(0.5f, 1)).fontStyle = FontStyle.Bold;
            return rect;
        }

        private void UpdatePlates()
        {
            if (plates == null || !stage || !stage.View || !screen) return;
            var area = (RectTransform)screen.transform;
            for (var i = 0; i < plates.Length; i++)
            {
                if (!plates[i]) continue;
                var point = stage.View.WorldToScreenPoint(stage.FootOf(i));
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, point, null, out var local)) continue;
                plates[i].anchoredPosition = local + new Vector2(0f, -18f);
            }
        }

        private void CycleHero(int step)
        {
            var index = Array.IndexOf(Heroes, config.Hero);
            SelectHero(Heroes[(index + step + Heroes.Length) % Heroes.Length]);
        }

        private void CyclePath(int step)
        {
            var index = Array.IndexOf(Paths, config.Mode);
            SelectPath(Paths[Mathf.Clamp(index + step, 0, Paths.Length - 1)]);
        }

        private void SelectHero(HeroClassId hero)
        {
            config.Hero = hero;
            MetaSaveSystem.SaveLobbySelection(config.Hero, config.Mode);
            ShowLobby();
        }

        private void SelectPath(RunMode path)
        {
            config.Mode = path;
            MetaSaveSystem.SaveLobbySelection(config.Hero, config.Mode);
            ShowLobby();
        }

        /// <summary>Ist zwischen zwei Sitzungen ein Shift abgelaufen, gibt es die Tokens dafuer genau einmal zu sehen.</summary>
        private void AnnounceShiftRewardOnce()
        {
            if (!MetaSaveSystem.ConsumeShiftReward(out var tier, out var tokens)) return;
            var banner = Panel(screen.transform, new Vector2(0, -150), new Vector2(760, 96), new Vector2(0.5f, 1),
                new Color(0.03f, 0.06f, 0.05f, 0.96f), RankTable.Accent(tier));
            Text(banner.transform, "SHIFT COMPLETE", 22, TextAnchor.UpperCenter, new Vector2(0, -12), new Vector2(700, 28), new Vector2(0.5f, 1))
                .color = RankTable.Accent(tier);
            Text(banner.transform, Loc.T("RANK") + " " + RankTable.Name(tier) + "  ·  +" + tokens + " " + Loc.T("TOKENS"), 26, TextAnchor.UpperCenter,
                new Vector2(0, -46), new Vector2(700, 34), new Vector2(0.5f, 1));
        }

        // ── Relics ──────────────────────────────────────────────────────────

        private void ShowRelics()
        {
            BeginScreen(true);
            backAction = CloseRelics;
            var accent = HeroCatalog.Accent(config.Hero);
            Header(Loc.T("RELIC LOADOUT"),
                config.Relics.Count + "/3 " + Loc.T("EQUIPPED  ·  CARRIED INTO EVERY CLIMB"), accent);
            var relics = (RelicId[])Enum.GetValues(typeof(RelicId));
            // Vier Spalten: mit fuenfzehn Relikten liefe ein Raster aus drei Spalten unten aus dem Bild.
            const int columns = 4;
            for (var i = 0; i < relics.Length; i++)
            {
                var relic = relics[i];
                var selected = config.Relics.Contains(relic);
                var label = (selected ? "◆  " : "◇  ") + Loc.T(RelicCatalog.Name(relic))
                            + "\n<size=16>" + Loc.T(RelicCatalog.Description(relic)) + "</size>";
                Button(screen.transform, label,
                    new Vector2(-585 + (i % columns) * 390, 170 - (i / columns) * 132),
                    new Vector2(364, 112), new Vector2(0.5f, 0.5f),
                    selected ? accent : new Color(0.32f, 0.42f, 0.54f), () => ToggleRelic(relic), selected);
                // Zifferntasten nur fuer die ersten neun - mehr Ziffern gibt es nicht.
                if (i < 9) shortcuts[KeyCode.Alpha1 + i] = () => ToggleRelic(relic);
            }
            Button(screen.transform, "DONE", new Vector2(0, -380), new Vector2(360, 88), new Vector2(0.5f, 0.5f), accent, CloseRelics, true);
            shortcuts[KeyCode.R] = CloseRelics;
            shortcuts[KeyCode.Return] = CloseRelics;
        }

        private void ToggleRelic(RelicId relic)
        {
            if (config.Relics.Contains(relic)) config.Relics.Remove(relic);
            else if (config.Relics.Count < 3) config.Relics.Add(relic);
            ShowRelics();
        }

        private void CloseRelics()
        {
            MetaSaveSystem.SaveRelics(config.Relics);
            ShowLobby();
        }

        // ── Forge ───────────────────────────────────────────────────────────

        private void ShowForge()
        {
            BeginScreen(true);
            backAction = ShowLobby;
            var save = MetaSaveSystem.Load();
            Header(Loc.T("META FORGE"), Loc.T("SHARDS") + "  " + save.shards.ToString("N0") + "  ·  " + Loc.T("PERMANENT, CAPPED BONUSES"), new Color(1f, 0.55f, 0.08f));
            var upgrades = new[] { MetaUpgradeId.Vitality, MetaUpgradeId.Might, MetaUpgradeId.Agility };
            var names = new[] { "VITAL CORE", "TEMPERED EDGE", "WIND GLYPH" };
            var effects = new[] { "+5 MAX HP / LEVEL", "+4% DAMAGE / LEVEL", "+2% MOVE SPEED / LEVEL" };
            var colors = new[] { new Color(0.1f, 0.88f, 0.58f), new Color(1f, 0.42f, 0.08f), new Color(0.16f, 0.72f, 1f) };
            for (var i = 0; i < upgrades.Length; i++)
            {
                var id = upgrades[i];
                var level = MetaSaveSystem.UpgradeLevel(save, id);
                var cost = MetaSaveSystem.UpgradeCost(save, id);
                Action buy = () => { MetaSaveSystem.Purchase(id); ShowForge(); };
                Button(screen.transform, Loc.T(names[i]) + "\n\n<size=19>" + Loc.T(effects[i]) + "\n\n"
                    + Loc.T("LEVEL") + " " + level + " / 10\n"
                    + (level >= 10 ? Loc.T("MAXIMUM") : Loc.T("UPGRADE") + "  " + cost + " " + Loc.T("SHARDS")) + "</size>",
                    new Vector2(-510 + i * 510, 0), new Vector2(430, 400), new Vector2(0.5f, 0.5f), colors[i], buy);
                shortcuts[KeyCode.Alpha1 + i] = buy;
            }
            Button(screen.transform, "BACK TO LOBBY", new Vector2(0, -330), new Vector2(360, 88), new Vector2(0.5f, 0.5f),
                new Color(0.34f, 0.42f, 0.52f), ShowLobby);
            shortcuts[KeyCode.F] = ShowLobby;
        }

        private void BeginRun()
        {
            MetaSaveSystem.SaveRelics(config.Relics);
            MetaSaveSystem.SaveLobbySelection(config.Hero, config.Mode);
            RunLaunchSettings.Prepare(config);
            PrototypeBootstrap.Reload();
        }

        // ── Bausteine ───────────────────────────────────────────────────────

        private void BeginScreen(bool shaded)
        {
            if (screen) Destroy(screen);
            buttons.Clear();
            shortcuts.Clear();
            plates = null;
            var image = CreateImage(canvas.transform, "Front End Screen",
                shaded ? new Color(0.006f, 0.014f, 0.035f, 0.9f) : Color.clear, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            image.raycastTarget = shaded;
            screen = image.gameObject;
            var rect = (RectTransform)screen.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Dunkler Verlauf am oberen oder unteren Rand: Text bleibt lesbar, die Szene bleibt hell.</summary>
        private void Fade(bool top, float height, float alpha)
        {
            var image = CreateImage(screen.transform, top ? "Top Fade" : "Bottom Fade", new Color(0.01f, 0.02f, 0.05f, alpha),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, top ? 1f : 0f));
            image.sprite = UiIconFactory.VerticalFade();
            image.raycastTarget = false;
            var rect = (RectTransform)image.transform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, top ? -height * 0.5f : height * 0.5f);
            if (top) rect.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }

        private void Header(string title, string subtitle, Color accent)
        {
            var header = Panel(screen.transform, new Vector2(0, -40), new Vector2(1530, 132), new Vector2(0.5f, 1),
                new Color(0.018f, 0.035f, 0.07f, 0.97f), accent);
            Text(header.transform, title, 46, TextAnchor.UpperCenter, new Vector2(0, -18), new Vector2(1200, 56), new Vector2(0.5f, 1)).fontStyle = FontStyle.Bold;
            Text(header.transform, subtitle, 20, TextAnchor.UpperCenter, new Vector2(0, -78), new Vector2(1200, 34), new Vector2(0.5f, 1)).color = accent;
        }

        private Image Panel(Transform parent, Vector2 pos, Vector2 size, Vector2 anchor, Color fill, Color outlineColor)
        {
            var panel = CreateImage(parent, "Panel", fill, pos, size, anchor);
            panel.sprite = WorldHealthBar.RoundedUiSprite;
            panel.type = Image.Type.Sliced;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            return panel;
        }

        private Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Vector2 anchor, Color color, Action action,
            bool filled = false, int labelSize = 22)
        {
            var strength = filled ? 0.5f : 0.14f;
            var panel = Panel(parent, pos, size, anchor, new Color(color.r * strength, color.g * strength, color.b * strength, 0.96f), color);
            var button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                Sfx.Play2D(Sound.UiClick);
                action?.Invoke();
            });
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(Color.white, color, 0.45f);
            colors.pressedColor = color;
            button.colors = colors;
            if (!string.IsNullOrEmpty(label))
                Text(panel.transform, label, labelSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one).fontStyle = FontStyle.Bold;
            buttons.Add(button);
            return button;
        }

        private Image CreateImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text Text(Transform parent, string value, int size, TextAnchor align, Vector2 pos, Vector2 dimensions, Vector2 anchor)
            => Text(parent, value, size, align, pos, dimensions, anchor, anchor, anchor);

        private Text Text(Transform parent, string value, int size, TextAnchor align, Vector2 pos, Vector2 dimensions,
            Vector2 anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = dimensions;
            if (anchorMin != anchorMax) { rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
            var text = go.GetComponent<Text>();
            text.font = font;
            // Einziger Durchlass fuer Menuetexte - hier wird uebersetzt.
            text.text = Loc.TQuiet(value);
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            // Roboto hat eine hoehere Zeilenhoehe als Arial; Ueberlauf statt Ausblenden.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var stroke = go.AddComponent<Outline>();
            stroke.effectColor = new Color(0.04f, 0.025f, 0.02f, 0.9f);
            stroke.effectDistance = new Vector2(1.6f, -1.6f);
            var drop = go.AddComponent<Shadow>();
            drop.effectColor = new Color(0f, 0f, 0f, 0.5f);
            drop.effectDistance = new Vector2(0f, -2.6f);
            return text;
        }

        private void TryClick(Vector2 point)
        {
            for (var i = buttons.Count - 1; i >= 0; i--)
            {
                var button = buttons[i];
                if (!button || !button.interactable) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)button.transform, point, null)) continue;
                button.onClick.Invoke();
                return;
            }
        }
    }

    /// <summary>
    /// 3D-Buehne der Lobby: eigene Kamera, der gewaehlte Held in der Mitte mit Blick zur Kamera,
    /// die zwei Bots leicht versetzt dahinter, jeweils mit Lichtring am Boden.
    /// </summary>
    public sealed class LobbyStage : MonoBehaviour
    {
        private static readonly Vector3 HeroSpot = Vector3.zero;
        private static readonly Vector3[] BotSpots = { new(-1.8f, 0f, 1.1f), new(1.8f, 0f, 1.1f) };
        private readonly List<GameObject> actors = new();
        private Camera view;
        private bool built;
        private HeroClassId hero;

        public Camera View => view;

        private void Awake()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(transform, false);
            view = go.AddComponent<Camera>();
            StylizedArt.ConfigureCamera(view);
            // Naeher und flacher als im Kampf: hier sind die Figuren das Thema, nicht die Arena.
            view.orthographicSize = 2.9f;
            go.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            go.transform.position = new Vector3(0f, 0.95f, 0.35f) - go.transform.forward * 16f;
            go.AddComponent<AudioListener>();
        }

        /// <summary>Fusspunkt fuer das Namensschild: 0 ist der Held, danach die Bots.</summary>
        public Vector3 FootOf(int index)
            => transform.TransformPoint(index <= 0 ? HeroSpot : BotSpots[Mathf.Min(index - 1, BotSpots.Length - 1)]);

        public void Show(HeroClassId selected)
        {
            if (built && selected == hero) return;
            var changed = built;
            built = true;
            hero = selected;
            foreach (var actor in actors)
                if (actor) Destroy(actor);
            actors.Clear();

            actors.Add(Actor(HeroCatalog.Name(selected) + " · Lobby", HeroSpot, 180f, HeroCatalog.Accent(selected), 2.3f, 0.85f,
                root => AuthoredArt.TryBuildHero(root, selected, out _)));
            var team = PrototypeBootstrap.OfflineTeamFor(selected);
            for (var i = 0; i < team.Length; i++)
            {
                var member = team[i];
                actors.Add(Actor(member.Name + " · Lobby Bot", BotSpots[i], i == 0 ? 160f : 200f, member.Accent, 1.6f, 0.45f,
                    root => AuthoredArt.TryBuildCompanion(root, member.Role, member.Accent, out _)));
            }
            if (changed) PrototypeVfx.SpawnExplosion(transform.TransformPoint(HeroSpot) + Vector3.up * 0.6f, 1.5f, HeroCatalog.Accent(selected));
        }

        private GameObject Actor(string name, Vector3 spot, float yaw, Color accent, float ringSize, float ringAlpha,
            Func<Transform, bool> build)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = spot;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (!build(root.transform)) StylizedArt.BuildRex(root.transform);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Lobby Pedestal";
            PrototypeFactory.RemoveCollider(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * 0.04f;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(ringSize, ringSize, 1f);
            var renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(new Color(accent.r, accent.g, accent.b, ringAlpha), 0.68f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return root;
        }
    }
}
