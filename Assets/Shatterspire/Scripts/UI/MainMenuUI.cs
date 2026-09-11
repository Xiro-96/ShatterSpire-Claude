using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>Complete runtime front end: mode, hero, relic loadout and permanent forge.</summary>
    public sealed class MainMenuUI : MonoBehaviour
    {
        private Font font;
        private Canvas canvas;
        private GameObject screen;
        private readonly List<Button> buttons = new();
        private RunConfig config = new();
        private Action backAction;

        public void Configure()
        {
            // Roboto Black statt Arial: kraeftige Buchstaben wie in Mobile-Actionspielen.
            // Faellt auf die eingebaute Schrift zurueck, falls das Asset fehlt.
            font = Resources.Load<Font>("Fonts/Roboto-Black") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var save = MetaSaveSystem.Load();
            config.Relics.Clear();
            foreach (var value in save.equippedRelics)
                if (Enum.IsDefined(typeof(RelicId), value) && config.Relics.Count < 3)
                    config.Relics.Add((RelicId)value);
            BuildCanvas();
            ShowHome();
        }

        private void BuildCanvas()
        {
            // Input is handled directly below so mouse and touch remain reliable even
            // when a project uses a different EventSystem/input package.
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
            for (var i = 0; i < Mathf.Min(9, buttons.Count); i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) { Invoke(i); return; }
            if (Input.GetMouseButtonDown(0)) TryClick(Input.mousePosition);
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) TryClick(Input.GetTouch(0).position);
        }

        private void ShowHome()
        {
            BeginScreen(new Color(0.006f, 0.014f, 0.035f, 0.76f));
            backAction = null;
            var save = MetaSaveSystem.Load();
            var title = Text(screen.transform, "SHATTERSPIRE", 82, TextAnchor.MiddleLeft,
                new Vector2(120, -118), new Vector2(900, 100), new Vector2(0, 1));
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.92f, 0.97f, 1f);
            var subtitle = Text(screen.transform, "CLIMB  ·  ADAPT  ·  RISK IT ALL", 25, TextAnchor.MiddleLeft,
                new Vector2(126, -208), new Vector2(800, 42), new Vector2(0, 1));
            subtitle.color = new Color(0.16f, 0.92f, 0.86f);

            // Drei Pfade wie in R.I.S.E.: vor dem Start gewaehlt, mit fester Laenge.
            Panel(screen.transform, new Vector2(120, -284), new Vector2(580, 474), new Vector2(0, 1),
                new Color(0.02f, 0.045f, 0.08f, 0.96f), new Color(0.12f, 0.72f, 0.72f));
            var paths = new[] { RunMode.Brave, RunMode.Heroic, RunMode.Legendary };
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                Button(screen.transform, $"[{i + 1}]  {PathCatalog.Name(path)}\n{PathCatalog.Summary(path)}",
                    new Vector2(150, -316 - i * 104), new Vector2(520, 92), new Vector2(0, 1), PathCatalog.Accent(path),
                    () => ShowPreparation(path));
            }
            Button(screen.transform, "[4]  META FORGE\nPERMANENT POWER",
                new Vector2(150, -628), new Vector2(520, 80), new Vector2(0, 1), new Color(1f, 0.55f, 0.08f), ShowForge);

            // Rang und Shift-Restlaufzeit gehoeren nach oben rechts, weil sie der
            // Grund sind, ueberhaupt noch einen Aufstieg zu starten.
            var rankPoints = MetaSaveSystem.RankPoints(save);
            var tier = RankTable.TierFor(rankPoints);
            var rankPanel = Panel(screen.transform, new Vector2(-92, -82), new Vector2(430, 212), new Vector2(1, 1),
                new Color(0.018f, 0.035f, 0.07f, 0.95f), RankTable.Accent(tier));
            Text(rankPanel.transform, $"SHIFT {save.shiftIndex}  ·  ENDET IN {ShiftCalendar.Countdown(ShiftCalendar.Remaining)}",
                18, TextAnchor.UpperLeft, new Vector2(30, -16), new Vector2(370, 26), new Vector2(0, 1))
                .color = new Color(0.62f, 0.72f, 0.84f);
            var rankName = Text(rankPanel.transform, RankTable.Name(tier), 46, TextAnchor.UpperLeft,
                new Vector2(28, -44), new Vector2(370, 56), new Vector2(0, 1));
            rankName.fontStyle = FontStyle.Bold;
            rankName.color = RankTable.Accent(tier);
            var next = RankTable.IsHighest(tier)
                ? "HOECHSTER RANG"
                : $"{RankTable.PointsToNext(rankPoints):N0} BIS {RankTable.Name((RankTier)((int)tier + 1))}";
            Text(rankPanel.transform,
                $"{rankPoints:N0} RANGPUNKTE\n{next}\n\nSHARDS  {save.shards}     TOKENS  {save.tokens}",
                21, TextAnchor.UpperLeft, new Vector2(30, -104), new Vector2(370, 100), new Vector2(0, 1));

            var profile = Panel(screen.transform, new Vector2(-92, -306), new Vector2(430, 118), new Vector2(1, 1),
                new Color(0.018f, 0.035f, 0.07f, 0.95f), new Color(0.66f, 0.3f, 1f));
            Text(profile.transform, $"BEST FLOOR   {save.bestFloor}\nRUNS   {save.runs}\nBESTER AUFSTIEG   {save.lifetimeBestScore:N0}", 21,
                TextAnchor.MiddleLeft, new Vector2(30, -6), new Vector2(370, 96), new Vector2(0, 1));

            AnnounceShiftRewardOnce();
            Text(screen.transform, "THREE HEROES. THREE COMBAT IDENTITIES. ONE TOWER THAT NEVER STAYS THE SAME.", 20,
                TextAnchor.LowerLeft, new Vector2(122, 72), new Vector2(1180, 40), new Vector2(0, 0)).color = new Color(0.65f, 0.75f, 0.86f);
        }

        /// <summary>
        /// Ist zwischen zwei Sitzungen ein Shift abgelaufen, gibt es die Tokens
        /// dafuer genau einmal zu sehen. <see cref="MetaSaveSystem.ConsumeShiftReward"/>
        /// loescht die Meldung beim Abholen, sonst stuende sie bei jedem Start da.
        /// </summary>
        private void AnnounceShiftRewardOnce()
        {
            if (!MetaSaveSystem.ConsumeShiftReward(out var tier, out var tokens)) return;
            var banner = Panel(screen.transform, new Vector2(0, 184), new Vector2(880, 104), new Vector2(0.5f, 0),
                new Color(0.03f, 0.06f, 0.05f, 0.97f), RankTable.Accent(tier));
            var headline = Text(banner.transform, "SHIFT ABGESCHLOSSEN", 24, TextAnchor.UpperCenter,
                new Vector2(0, -14), new Vector2(820, 30), new Vector2(0.5f, 1));
            headline.color = RankTable.Accent(tier);
            Text(banner.transform, $"RANG {RankTable.Name(tier)}  ·  +{tokens} TOKENS GUTGESCHRIEBEN",
                26, TextAnchor.UpperCenter, new Vector2(0, -50), new Vector2(820, 36), new Vector2(0.5f, 1));
        }

        private void ShowPreparation(RunMode mode)
        {
            config.Mode = mode;
            BeginScreen(new Color(0.006f, 0.014f, 0.035f, 0.88f));
            backAction = ShowHome;
            var accent = PathCatalog.Accent(mode);
            Header($"{PathCatalog.Name(mode)} CLIMB",
                $"{PathCatalog.Summary(mode)}  ·  CHOOSE A HERO AND UP TO THREE RELICS", accent);

            var heroes = new[] { HeroClassId.Ranger, HeroClassId.Guardian, HeroClassId.Arcanist };
            for (var i = 0; i < heroes.Length; i++)
            {
                var hero = heroes[i];
                var selected = config.Hero == hero;
                var panel = Button(screen.transform,
                    $"[{i + 1}]  {HeroCatalog.Name(hero)}\n{HeroCatalog.Role(hero)}\n\n{HeroCatalog.Kit(hero)}\n\nHP {HeroCatalog.BaseHealth(hero):0}",
                    new Vector2(255 + i * 520, -245), new Vector2(450, 270), new Vector2(0, 1),
                    selected ? Color.white : HeroCatalog.Accent(hero), () => { config.Hero = hero; ShowPreparation(mode); });
                var icon = CreateImage(panel.transform, "Class Emblem", Color.white, new Vector2(28, -28), new Vector2(86, 86), new Vector2(0, 1));
                icon.sprite = UiIconFactory.Hero(hero);
                icon.preserveAspect = true;
                Text(panel.transform, selected ? "SELECTED" : "", 17, TextAnchor.UpperRight,
                    new Vector2(-22, -20), new Vector2(130, 28), new Vector2(1, 1)).color = accent;
            }

            Text(screen.transform, $"RELIC LOADOUT  {config.Relics.Count}/3", 27, TextAnchor.MiddleLeft,
                new Vector2(255, -558), new Vector2(620, 42), new Vector2(0, 1)).fontStyle = FontStyle.Bold;
            var relics = (RelicId[])Enum.GetValues(typeof(RelicId));
            for (var i = 0; i < relics.Length; i++)
            {
                var relic = relics[i];
                var selected = config.Relics.Contains(relic);
                var x = 255 + (i % 3) * 520;
                var y = -620 - (i / 3) * 116;
                Button(screen.transform, $"[{i + 4}]  {(selected ? "◆ " : "◇ ")}{RelicCatalog.Name(relic)}\n{RelicCatalog.Description(relic)}",
                    new Vector2(x, y), new Vector2(450, 92), new Vector2(0, 1), selected ? accent : new Color(0.32f, 0.42f, 0.54f),
                    () => ToggleRelic(relic, mode));
            }
            Button(screen.transform, "BACK", new Vector2(255, 64), new Vector2(250, 74), new Vector2(0, 0), new Color(0.34f, 0.42f, 0.52f), ShowHome);
            Button(screen.transform, "BEGIN CLIMB", new Vector2(-255, 64), new Vector2(390, 82), new Vector2(1, 0), accent, BeginRun);
        }

        private void ToggleRelic(RelicId relic, RunMode mode)
        {
            if (config.Relics.Contains(relic)) config.Relics.Remove(relic);
            else if (config.Relics.Count < 3) config.Relics.Add(relic);
            ShowPreparation(mode);
        }

        private void ShowForge()
        {
            BeginScreen(new Color(0.006f, 0.014f, 0.035f, 0.9f));
            backAction = ShowHome;
            var save = MetaSaveSystem.Load();
            Header("META FORGE", $"SHARDS  {save.shards}  ·  PERMANENT, CAPPED BONUSES", new Color(1f, 0.55f, 0.08f));
            var upgrades = new[] { MetaUpgradeId.Vitality, MetaUpgradeId.Might, MetaUpgradeId.Agility };
            var names = new[] { "VITAL CORE", "TEMPERED EDGE", "WIND GLYPH" };
            var effects = new[] { "+5 MAX HP / LEVEL", "+4% DAMAGE / LEVEL", "+2% MOVE SPEED / LEVEL" };
            for (var i = 0; i < upgrades.Length; i++)
            {
                var id = upgrades[i];
                var level = MetaSaveSystem.UpgradeLevel(save, id);
                var cost = MetaSaveSystem.UpgradeCost(save, id);
                Button(screen.transform, $"[{i + 1}]  {names[i]}\n\n{effects[i]}\n\nLEVEL {level} / 10\n{(level >= 10 ? "MAXIMUM" : "UPGRADE  " + cost + " SHARDS")}",
                    new Vector2(270 + i * 510, -300), new Vector2(430, 400), new Vector2(0, 1),
                    i == 0 ? new Color(0.1f, 0.88f, 0.58f) : i == 1 ? new Color(1f, 0.42f, 0.08f) : new Color(0.16f, 0.72f, 1f),
                    () => { MetaSaveSystem.Purchase(id); ShowForge(); });
            }
            Button(screen.transform, "BACK TO TOWER", new Vector2(270, 90), new Vector2(350, 82), new Vector2(0, 0),
                new Color(0.34f, 0.42f, 0.52f), ShowHome);
        }

        private void BeginRun()
        {
            MetaSaveSystem.SaveRelics(config.Relics);
            RunLaunchSettings.Prepare(config);
            Time.timeScale = 1f;
            GameEvents.Reset();
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.name)) SceneManager.LoadScene(scene.name);
        }

        private void Header(string title, string subtitle, Color accent)
        {
            var header = Panel(screen.transform, new Vector2(0, -28), new Vector2(1530, 132), new Vector2(0.5f, 1),
                new Color(0.018f, 0.035f, 0.07f, 0.97f), accent);
            var titleText = Text(header.transform, title, 46, TextAnchor.UpperCenter, new Vector2(0, -18), new Vector2(1200, 56), new Vector2(0.5f, 1));
            titleText.fontStyle = FontStyle.Bold;
            Text(header.transform, subtitle, 20, TextAnchor.UpperCenter, new Vector2(0, -78), new Vector2(1200, 34), new Vector2(0.5f, 1)).color = accent;
        }

        private void BeginScreen(Color shade)
        {
            if (screen) Destroy(screen);
            buttons.Clear();
            screen = CreateImage(canvas.transform, "Front End Screen", shade, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)).gameObject;
            var rect = (RectTransform)screen.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
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

        private Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Vector2 anchor, Color color, Action action)
        {
            var panel = Panel(parent, pos, size, anchor, new Color(color.r * 0.14f, color.g * 0.14f, color.b * 0.14f, 0.97f), color);
            var button = panel.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action?.Invoke());
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(Color.white, color, 0.45f);
            colors.pressedColor = color;
            button.colors = colors;
            var text = Text(panel.transform, label, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            text.fontStyle = FontStyle.Bold;
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
            text.text = value;
            text.fontSize = size;
            text.alignment = align;
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

        private void Invoke(int index)
        {
            if (index >= 0 && index < buttons.Count && buttons[index]) buttons[index].onClick.Invoke();
        }
    }
}
