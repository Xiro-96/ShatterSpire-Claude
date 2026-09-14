using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Shatterspire
{
    /// <summary>
    /// Runtime bridge for authored FBX content. Gameplay code only knows about
    /// roots and colliders, so visual assets can continue to be replaced later.
    /// </summary>
    public static partial class AuthoredArt
    {
        private const string RexModel = "Art3D/KayKit/Characters/Ranger";
        private const string ForgeModels = "Art3D/Forge/Models/";
        private const string ForgeTextures = "Art3D/Forge/Textures/";
        private const string RexTextures = "Art3D/Rex/";
        private const string HeroModels = "Art3D/Heroes/";
        private const string HeroTextures = "Art3D/Heroes/";
        private const string KayCharacters = "Art3D/KayKit/Characters/";
        private const string KayWeapons = "Art3D/KayKit/Weapons/";
        private const string SkeletonCharacters = "Art3D/KayKit/Skeletons/Characters/";
        private const string SkeletonWeapons = "Art3D/KayKit/Skeletons/Weapons/";
        private const string SkeletonTexture = SkeletonCharacters + "skeleton_texture";
        private const string DungeonModels = "Art3D/KayKit/Dungeon/";
        private const string DungeonTexture = DungeonModels + "dungeon_texture";

        private static readonly Dictionary<string, GameObject> ModelCache = new();
        private static readonly Dictionary<string, Material> MaterialCache = new();
        private static Mesh crystalMesh;

        private const float CourtTileSpacing = 4.22f;

        /// <summary>
        /// Helligkeitsverlauf des Hofbodens: hell in der Mitte, dunkler zum Rand. Liegt hier
        /// als einzige Quelle, weil ApplyFloorTheme den Verlauf vorher auf jeder Etage mit
        /// einer einzigen hellen Farbe ueberschrieben hat - der Boden war dadurch von Kante
        /// zu Kante ein Farbwert und Hindernisse hoben sich nicht ab.
        /// </summary>
        private static Color CourtTileTint(int x, int z)
        {
            var distanceFromCenter = Mathf.Max(Mathf.Abs(x - 3), Mathf.Abs(z - 3));
            // Warm und nah an Weiss: die KayKit-Modelle sind auf ihre eigenen Atlasfarben
            // ausgelegt - Sandstein, Terrakotta, Holz. Kuehles Einfaerben hat die ganze
            // Szene in einen einzigen Blauton gedrueckt. Stimmung kommt jetzt aus dem Licht.
            var tint = distanceFromCenter <= 1 ? new Color(0.94f, 0.9f, 0.84f)
                : distanceFromCenter == 2 ? new Color(0.82f, 0.78f, 0.72f)
                : new Color(0.66f, 0.62f, 0.58f);
            // Leichte Unruhe im Muster, damit der Boden nicht wie eine Fliesenflaeche wirkt.
            if ((x + z) % 4 == 0) tint = Color.Lerp(tint, new Color(0.96f, 0.84f, 0.68f), 0.3f);
            // Deterministische Streuung je Kachel: ohne sie ist jede Ringstufe exakt gleich hell,
            // und der Boden liest sich als Flaeche statt als verlegter Stein.
            var hash = (x * 73856093) ^ (z * 19349663);
            var jitter = ((hash & 0xFF) / 255f - 0.5f) * 0.12f;
            tint = new Color(Mathf.Clamp01(tint.r + jitter), Mathf.Clamp01(tint.g + jitter * 0.9f),
                Mathf.Clamp01(tint.b + jitter * 0.75f));
            return tint;
        }

        public static bool TryBuildArena()
        {
            if (!LoadModel(DungeonModels + "floor_tile_large")) return false;

            // V14 art target: chunky fantasy silhouettes over a saturated magical-tech
            // court. Large color regions and landmarks read clearly on a phone screen.
            var root = new GameObject("THE FORGOTTEN COURT · V15 TOWER FOUNDATIONS").transform;
            var abyss = new Color(0.05f, 0.045f, 0.06f);
            var rim = new Color(0.22f, 0.17f, 0.14f);
            var deepStone = new Color(0.66f, 0.62f, 0.58f);
            var cyan = new Color(0.03f, 0.92f, 1f);
            var violet = new Color(0.72f, 0.22f, 1f);
            var ember = new Color(1f, 0.48f, 0.08f);

            ArenaPart(root, PrimitiveType.Cylinder, "Floating Court Island", new Vector3(0f, -1.02f, 0f),
                new Vector3(36.4f, 0.9f, 36.4f), abyss, false, 0.02f, false);
            ArenaPart(root, PrimitiveType.Cube, "Court Foundation", new Vector3(0f, -0.2f, 0f),
                new Vector3(30.4f, 0.32f, 30.4f), rim, false, 0.04f, false);

            const int tileCount = 7;
            const float spacing = CourtTileSpacing;
            for (var x = 0; x < tileCount; x++)
            for (var z = 0; z < tileCount; z++)
            {
                var edge = x == 0 || z == 0 || x == tileCount - 1 || z == tileCount - 1;
                // Grates nur noch als Eckdetail. Vorher lagen fuenf davon im Feld, zwei direkt
                // gestapelt in der Mitte - mit ihren hohen Rahmen dominierten sie jedes Spielbild
                // und liessen die Arena wie einen Prototyp aus Kloetzen wirken. Verzierte
                // Kacheln im Inneren geben dem Boden stattdessen Struktur.
                var tileName = (x == 0 && z == 0) || (x == tileCount - 1 && z == tileCount - 1)
                    ? "floor_tile_big_grate"
                    : edge && (x + z) % 3 == 0 ? "floor_tile_large_rocks"
                    : !edge && (x * 3 + z * 5) % 7 == 0 ? "floor_tile_small_decorated"
                    : "floor_tile_large";
                var position = new Vector3((x - 3) * spacing, 0f, (z - 3) * spacing);
                var tint = CourtTileTint(x, z);
                SpawnDungeonModel(root, tileName, position, ((x + z) & 1) * 90f, 4.34f, FitAxis.Horizontal,
                    tint);
            }

            BuildCourtReactor(root, cyan, violet, ember);

            for (var i = 0; i < 4; i++)
            {
                var yaw = i * 90f;
                var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var channel = ArenaPart(root, PrimitiveType.Cube, "Court Energy Channel",
                    direction * 8.25f + Vector3.up * 0.105f, new Vector3(0.075f, 0.016f, 7.9f),
                    i % 2 == 0 ? cyan : violet, true, 0.22f, false);
                channel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            CreateInvisibleBoundary(root, new Vector3(0f, 0.7f, -16.1f), new Vector3(33f, 1.4f, 0.65f));
            CreateInvisibleBoundary(root, new Vector3(0f, 0.7f, 16.1f), new Vector3(33f, 1.4f, 0.65f));
            CreateInvisibleBoundary(root, new Vector3(-16.1f, 0.7f, 0f), new Vector3(0.65f, 1.4f, 33f));
            CreateInvisibleBoundary(root, new Vector3(16.1f, 0.7f, 0f), new Vector3(0.65f, 1.4f, 33f));

            BuildDungeonPortal(root, new Vector3(0f, 0.02f, 14.15f), 180f, cyan);
            BuildDungeonPortal(root, new Vector3(14.15f, 0.02f, 0f), 270f, ember);
            BuildDungeonPortal(root, new Vector3(-14.15f, 0.02f, 0f), 90f, violet);
            BuildCourtBastion(root, new Vector3(-13.1f, 0.02f, -12.7f), 24f, violet);
            BuildCourtBastion(root, new Vector3(13.1f, 0.02f, -12.7f), -24f, ember);
            BuildCourtBastion(root, new Vector3(-12.9f, 0.02f, 11.8f), 154f, cyan);
            BuildCourtBastion(root, new Vector3(12.9f, 0.02f, 11.8f), 206f, cyan);
            BuildCourtEdge(root, deepStone);

            var console = SpawnModel(ForgeModels + "Prop_Computer", root,
                new Vector3(10.8f, 0.04f, 12.6f), 210f, 1.75f, FitAxis.Horizontal);
            ApplyForgeMaterials(console);
            var cables = SpawnModel(ForgeModels + "Prop_Cable_1", root,
                new Vector3(-9.8f, 0.025f, 12.8f), 12f, 3.2f, FitAxis.Horizontal);
            ApplyForgeMaterials(cables);

            CreateAccentLight(root, new Vector3(0f, 2.1f, 0f), cyan, 8.5f, 1.15f);
            root.gameObject.AddComponent<CourtAmbientMotion>();
            return true;
        }

        public static bool TryBuildRex(Transform root, out Transform muzzle)
        {
            muzzle = null;
            var source = LoadModel(RexModel);
            if (!source) return false;

            var model = UnityEngine.Object.Instantiate(source, root);
            model.name = "Rex · Authored Rift Ranger";
            ResetTransform(model.transform);
            FitAndPlace(model, root.position, 2.58f, FitAxis.Height);
            var animator = model.GetComponentInChildren<Animator>();
            Reground(model, root.position);
            ApplyKayKitMaterials(model, KayCharacters + "ranger_texture", new Color(1f, 1f, 1f));
            StylizeHumanoidProportions(animator, 1.1f, 1.08f);

            var motion = root.gameObject.AddComponent<StylizedCharacterMotion>();
            motion.ConfigureAuthored(model.transform, animator, HeroCatalog.BaseSpeed(HeroClassId.Ranger));

            CreateGroundShadow(root, 1.42f);
            CreateSelectionRing(root, 1.46f, new Color(0.06f, 0.92f, 0.96f));
            AttachRexScarf(animator, model.transform);
            AttachKayKitCrossbows(animator);

            muzzle = new GameObject("Rift Crossbow Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 1.12f, 0.92f);
            return true;
        }

        public static bool TryBuildHero(Transform root, HeroClassId hero, out Transform muzzle)
        {
            if (hero == HeroClassId.Ranger) return TryBuildRex(root, out muzzle);
            var role = hero == HeroClassId.Guardian || hero == HeroClassId.Paladin
                ? CompanionRole.Guardian
                : CompanionRole.Support;
            // KORR bekommt die Schleicherfigur: klein, beweglich, mit Beutel. XIRO den Ritter mit
            // Schild - sie ist die einzige mit einem Schild in der Hand, und genau darum geht es
            // bei ihr. Beide sind dadurch auf einen Blick von Brax, Rex und Orion zu unterscheiden.
            var model3D = hero switch
            {
                HeroClassId.Bomber => "Rogue_Hooded",
                HeroClassId.Paladin => "Knight",
                _ => null
            };
            var texture = hero switch
            {
                HeroClassId.Bomber => "rogue_texture",
                HeroClassId.Paladin => "knight_texture",
                _ => null
            };
            if (!TryBuildCompanion(root, role, HeroCatalog.Accent(hero), out muzzle,
                    HeroCatalog.BaseSpeed(hero), model3D, texture,
                    oathGear: hero == HeroClassId.Paladin)) return false;
            var model = root.childCount > 0 ? root.GetChild(0) : null;
            if (model) model.name = HeroCatalog.Name(hero) + " · " + HeroCatalog.Role(hero);
            return true;
        }

        public static void ApplyFloorTheme(int floor)
        {
            var theme = FloorCatalog.ThemeFor(floor);
            var arenaObject = GameObject.Find("THE FORGOTTEN COURT · V15 TOWER FOUNDATIONS");
            if (arenaObject)
            {
                // Das Thema verschiebt nur den Farbton des warmen Verlaufs. Stimmung kommt aus
                // dem Licht, nicht aus dunkel eingefaerbtem Stein.
                var themeTint = theme switch
                {
                    FloorTheme.EmberFoundry => new Color(1f, 0.84f, 0.72f),
                    FloorTheme.AstralArchive => new Color(0.9f, 0.86f, 1f),
                    _ => new Color(1f, 0.98f, 0.94f)
                };
                for (var i = 0; i < arenaObject.transform.childCount; i++)
                {
                    var child = arenaObject.transform.GetChild(i);
                    if (!child.name.Contains("floor_tile")) continue;
                    var x = Mathf.RoundToInt(child.localPosition.x / CourtTileSpacing) + 3;
                    var z = Mathf.RoundToInt(child.localPosition.z / CourtTileSpacing) + 3;
                    ApplyKayKitMaterials(child.gameObject, DungeonTexture, CourtTileTint(x, z) * themeTint);
                }
            }

            RenderSettings.fogColor = theme switch
            {
                FloorTheme.EmberFoundry => new Color(0.18f, 0.09f, 0.06f),
                FloorTheme.AstralArchive => new Color(0.12f, 0.08f, 0.18f),
                _ => new Color(0.1f, 0.13f, 0.2f)
            };
            // Der Himmelsanteil des Umgebungslichts faerbt die Schatten. Kuehl gegen die
            // warme Sonne ist genau der Kontrast, der dem Bild bisher fehlte.
            RenderSettings.ambientSkyColor = theme switch
            {
                FloorTheme.EmberFoundry => new Color(0.5f, 0.44f, 0.52f),
                FloorTheme.AstralArchive => new Color(0.42f, 0.42f, 0.76f),
                _ => new Color(0.36f, 0.48f, 0.74f)
            };
            if (Camera.main) Camera.main.backgroundColor = RenderSettings.fogColor;
        }

        /// <summary>
        /// <paramref name="oathGear"/> tauscht den Kriegshammer der Waechter-Rolle gegen Schwert
        /// und Schild. XIRO hat die Rolle des Waechters, aber nicht seine Waffe - ein Paladin mit
        /// Kriegshammer waere ein Barbar in Ruestung.
        /// </summary>
        public static bool TryBuildCompanion(Transform root, CompanionRole role, Color accent,
            out Transform muzzle, float topSpeed = 6.5f, string modelName = null, string textureName = null,
            bool oathGear = false)
        {
            muzzle = null;
            // Mit modelName laesst sich eine andere Figur aufsetzen, ohne eine neue Bot-Rolle zu
            // erfinden - gebraucht wird das fuer KORR, der kein Wachter, Jaeger oder Magier ist.
            var resource = modelName != null
                ? KayCharacters + modelName
                : role switch
                {
                    CompanionRole.Guardian => KayCharacters + "Barbarian",
                    CompanionRole.Ranger => KayCharacters + "Ranger",
                    _ => KayCharacters + "Mage"
                };
            var source = LoadModel(resource);
            if (!source) return false;

            var model = UnityEngine.Object.Instantiate(source, root);
            model.name = role switch
            {
                CompanionRole.Guardian => "Brax · Forge Guardian",
                CompanionRole.Ranger => "Rex · Rift Ranger",
                _ => "Mira · Dawn Weaver"
            };
            ResetTransform(model.transform);
            FitAndPlace(model, root.position, role == CompanionRole.Guardian ? 3.02f : 2.52f, FitAxis.Height);
            var animator = model.GetComponentInChildren<Animator>();
            Reground(model, root.position);
            ApplyKayKitMaterials(model,
                KayCharacters + (textureName ?? (role == CompanionRole.Guardian ? "barbarian_texture"
                    : role == CompanionRole.Ranger ? "ranger_texture" : "mage_texture")),
                // XIROs Ruestung faellt ins Goldene. Ein Tonwert auf der vorhandenen Textur, kein
                // eigenes Bild: die Ruestung soll geweiht aussehen, nicht neu gebaut sein.
                oathGear ? new Color(1f, 0.88f, 0.58f) : Color.white);
            StylizeHumanoidProportions(animator, role == CompanionRole.Guardian ? 1.08f : 1.12f, 1.08f);

            var motion = root.gameObject.AddComponent<StylizedCharacterMotion>();
            motion.ConfigureAuthored(model.transform, animator, topSpeed);
            CreateGroundShadow(root, role == CompanionRole.Guardian ? 1.72f : 1.34f);
            CreateSelectionRing(root, role == CompanionRole.Guardian ? 1.76f : 1.38f, accent);

            if (oathGear) AttachOathGear(animator, model.transform, accent);
            else if (role == CompanionRole.Guardian) AttachGuardianHammer(animator, model.transform, accent);
            else if (role == CompanionRole.Ranger) AttachKayKitCrossbows(animator);
            else
            {
                AttachKayKitStaff(animator, model.transform);
                AttachSupportFocus(animator, model.transform, accent);
            }

            // Klingenspur, aber nur fuer die beiden, die wirklich zuschlagen. Der Zweihaender zieht
            // weiter aussen als der Hammer.
            if (oathGear || role == CompanionRole.Guardian)
            {
                var grip = FindNamedBone(animator ? animator.transform : model.transform, "handslot.r");
                if (!grip && animator && animator.isHuman) grip = animator.GetBoneTransform(HumanBodyBones.RightHand);
                motion.AttachBladeTrail(grip, accent, oathGear ? 1.45f : 1.1f);
            }

            muzzle = new GameObject(role == CompanionRole.Guardian ? "Hammer Impact" : "Dawn Focus").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = role == CompanionRole.Guardian
                ? new Vector3(0f, 0.65f, 1.35f)
                : role == CompanionRole.Ranger ? new Vector3(0f, 1.05f, 0.92f)
                : new Vector3(0.45f, 1.35f, 0.72f);
            return true;
        }

        public static bool TryBuildEnemy(Transform root, EnemyKind kind)
        {
            var modelName = kind switch
            {
                EnemyKind.Crawler => "Skeleton_Rogue",
                EnemyKind.Shooter => "Skeleton_Mage",
                EnemyKind.Brute => "Skeleton_Warrior",
                EnemyKind.Elite => "Skeleton_Warrior",
                EnemyKind.IronWarden => "Skeleton_Warrior",
                // Der Zwilling ist der schnelle Waechter: die schlanke Schleicherfigur statt des
                // Kriegers. Der Chorwaechter ruft, also traegt er den Stab des Magiers.
                EnemyKind.RiftTwin => "Skeleton_Rogue",
                EnemyKind.ChoirWarden => "Skeleton_Mage",
                EnemyKind.Shieldbearer => "Skeleton_Warrior",
                EnemyKind.Marksman => "Skeleton_Rogue",
                _ => "Skeleton_Minion"
            };
            var source = LoadModel(SkeletonCharacters + modelName);
            if (!source) return false;

            var model = UnityEngine.Object.Instantiate(source, root);
            model.name = kind + " · Authored Creature";
            ResetTransform(model.transform);
            var height = kind switch
            {
                EnemyKind.Crawler => 1.52f,
                EnemyKind.Shooter => 1.78f,
                EnemyKind.Brute => 2.22f,
                EnemyKind.Elite => 2.58f,
                EnemyKind.IronWarden => 4.05f,
                EnemyKind.RiftTwin => 3.3f,
                EnemyKind.ChoirWarden => 3.8f,
                EnemyKind.Shieldbearer => 2.02f,
                EnemyKind.Marksman => 1.72f,
                _ => 1.65f
            };
            FitAndPlace(model, root.position, height, FitAxis.Height);
            var widthScale = EnemyKinds.IsBoss(kind) ? 1.22f
                : kind is EnemyKind.Brute or EnemyKind.Elite ? 1.14f
                : kind == EnemyKind.Shieldbearer ? 1.1f : 1f;
            model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(widthScale, 1f, widthScale));
            Reground(model, root.position);

            var primary = kind switch
            {
                EnemyKind.Crawler => new Color(0.88f, 0.16f, 0.16f),
                EnemyKind.Shooter => new Color(0.48f, 0.18f, 0.82f),
                EnemyKind.Brute => new Color(0.8f, 0.34f, 0.08f),
                EnemyKind.Elite => new Color(0.82f, 0.08f, 0.55f),
                EnemyKind.IronWarden => new Color(0.12f, 0.2f, 0.3f),
                // Jeder Waechter hat seine eigene Farbe - auf einer Boss-Etage steht nur einer im
                // Raum, und man soll auf dem ersten Bild wissen, welcher.
                EnemyKind.RiftTwin => new Color(0.24f, 0.1f, 0.42f),
                EnemyKind.ChoirWarden => new Color(0.08f, 0.3f, 0.26f),
                // Stahlblau und Giftgruen: beide Rollen sollen sich im Getuemmel auf einen Blick
                // von den roten Crawlern und den violetten Shootern unterscheiden.
                EnemyKind.Shieldbearer => new Color(0.2f, 0.44f, 0.76f),
                EnemyKind.Marksman => new Color(0.34f, 0.74f, 0.26f),
                _ => Color.gray
            };
            var accent = EnemyKinds.IsBoss(kind) ? new Color(1f, 0.45f, 0.08f) : Color.Lerp(primary, Color.white, 0.45f);
            var corruptionTint = EnemyKinds.IsBoss(kind)
                ? new Color(0.62f, 0.72f, 0.88f)
                : Color.Lerp(Color.white, primary, kind switch
                {
                    EnemyKind.Shooter => 0.2f,
                    EnemyKind.Elite => 0.26f,
                    EnemyKind.Brute => 0.15f,
                    EnemyKind.Shieldbearer => 0.34f,
                    EnemyKind.Marksman => 0.32f,
                    _ => 0.12f
                });
            ApplyKayKitMaterials(model, SkeletonTexture, corruptionTint);

            var visualRig = new GameObject(kind + " · Riftborn Silhouette").transform;
            visualRig.SetParent(root, false);
            model.transform.SetParent(visualRig, true);
            AddRiftbornSignature(visualRig, kind, primary, accent);

            var motion = root.gameObject.AddComponent<StylizedCharacterMotion>();
            var animator = model.GetComponentInChildren<Animator>();
            StylizeHumanoidProportions(animator, EnemyKinds.IsBoss(kind) ? 1.13f : 1.09f,
                kind is EnemyKind.Brute or EnemyKind.Elite ? 1.12f : 1.06f);
            AttachSkeletonLoadout(animator, kind);
            var footprint = EnemyKinds.IsBoss(kind) ? 1.85f
                : kind is EnemyKind.Brute or EnemyKind.Elite ? 1.18f
                : kind == EnemyKind.Shieldbearer ? 0.95f : 0.78f;
            CreateGroundShadow(root, footprint);
            CreateSelectionRing(root, footprint * 1.08f, Color.Lerp(primary, new Color(0.3f, 0.02f, 0.04f), 0.24f));
            if (animator) motion.ConfigureAuthored(visualRig, animator, EnemyBalance.For(kind).Speed, true);
            else motion.Configure(visualRig, EnemyKinds.IsBoss(kind) ? 4.2f : 7.5f);
            return true;
        }

        public static bool BuildRoomDecor(Transform root, int index, RoomKind kind, Color accent)
        {
            if (!LoadModel(DungeonModels + "pillar_decorated")) return false;

            BuildThemeDecor(root, index);

            if (kind == RoomKind.Treasure)
            {
                SpawnDungeonModel(root, "chest_gold", new Vector3(0f, 0.04f, 3.5f), 180f, 2.5f,
                    FitAxis.Horizontal, Color.white);
                CreateAccentLight(root, new Vector3(0f, 1.1f, 3.5f), accent, 5f, 2.2f);
            }
            else if (kind == RoomKind.Mystery)
            {
                SpawnDungeonModel(root, "barrel_large_decorated", new Vector3(0f, 0.04f, 4.8f), 180f, 2.15f,
                    FitAxis.Height, new Color(0.86f, 0.82f, 0.94f));
                CreateAccentLight(root, new Vector3(0f, 1.5f, 4.4f), accent, 5f, 1.8f);
            }

            var obstacleCount = kind == RoomKind.Boss ? 4 : 3;
            var safePositions = new[]
            {
                new Vector3(-12.4f, 0.03f, -10.5f),
                new Vector3(12.3f, 0.03f, -1.2f),
                new Vector3(-12.2f, 0.03f, 8.5f)
            };
            for (var i = 0; i < obstacleCount; i++)
            {
                var angle = i * Mathf.PI * 2f / obstacleCount + index * 0.57f;
                var radius = kind == RoomKind.Boss ? 10.4f : 6.2f + (i % 2) * 2.25f;
                var position = kind == RoomKind.Boss
                    ? new Vector3(Mathf.Cos(angle) * radius, 0.03f, Mathf.Sin(angle) * radius)
                    : safePositions[i];
                var propName = kind == RoomKind.Boss || i == 0 ? "pillar_decorated"
                    : i == 1 ? "crates_stacked" : "rubble_large";
                var target = propName == "pillar_decorated" ? 2.9f : 2.35f;
                SpawnDungeonModel(root, propName, position, i * 47f + index * 19f, target,
                    propName == "pillar_decorated" ? FitAxis.Height : FitAxis.Horizontal,
                    i % 2 == 0 ? Color.white : new Color(0.96f, 0.92f, 0.86f));
                CrystalPart(root, "Prop Rift Crystal", position + new Vector3(0f, propName == "pillar_decorated" ? 2.25f : 1.1f, 0.36f),
                    new Vector3(0.14f, 0.46f, 0.14f), new Vector3(0f, 0f, 0f), accent);
                CreateObstacle(root, new Vector3(position.x, 0.9f, position.z), new Vector3(1.15f, 1.8f, 1.15f));
            }
            return true;
        }

        private static void BuildThemeDecor(Transform root, int floor)
        {
            var theme = FloorCatalog.ThemeFor(floor);
            if (theme == FloorTheme.EmberFoundry)
            {
                var positions = new[] { new Vector3(-11.8f, 0.03f, 10.8f), new Vector3(11.9f, 0.03f, 10.6f), new Vector3(11.7f, 0.03f, -10.8f) };
                var models = new[] { "Column_Pipes", "Prop_Barrel_Large", "Prop_Crate4" };
                for (var i = 0; i < positions.Length; i++)
                {
                    var model = SpawnModel(ForgeModels + models[i], root, positions[i], i * 73f, i == 0 ? 3.8f : 2.5f,
                        i == 0 ? FitAxis.Height : FitAxis.Horizontal);
                    ApplyForgeMaterials(model);
                    CreateAccentLight(root, positions[i] + Vector3.up * 1.1f, new Color(1f, 0.24f, 0.025f), 5.2f, 1.25f);
                }
                return;
            }

            if (theme == FloorTheme.AstralArchive)
            {
                var violet = new Color(0.68f, 0.18f, 1f);
                var cyan = new Color(0.08f, 0.88f, 1f);
                for (var i = 0; i < 7; i++)
                {
                    var angle = i * 360f / 7f + 18f;
                    var position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 13.2f;
                    CrystalPart(root, "Astral Memory Crystal", position + Vector3.up * 0.65f,
                        new Vector3(0.42f, 1.3f + (i % 3) * 0.28f, 0.42f), new Vector3(0f, angle, 0f), i % 2 == 0 ? violet : cyan);
                    CreateAccentLight(root, position + Vector3.up * 1.2f, i % 2 == 0 ? violet : cyan, 4.5f, 0.8f);
                }
            }
        }

        private static GameObject ArenaPart(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 scale, Color color, bool emissive, float smoothness, bool keepCollider)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = SolidMaterial(color, emissive, smoothness, 0f);
            var primitiveCollider = part.GetComponent<Collider>();
            if (!keepCollider) RemoveVisualCollider(primitiveCollider);
            return part;
        }

        private static void BuildGardenPath(Transform root, Vector3 axis, float length)
        {
            var horizontal = Mathf.Abs(axis.x) > 0.5f;
            var scale = horizontal ? new Vector3(length, 0.055f, 3.55f) : new Vector3(3.55f, 0.055f, length);
            ArenaPart(root, PrimitiveType.Cube, "Ivory Garden Walk", new Vector3(0f, 0.045f, 0f), scale,
                new Color(0.48f, 0.51f, 0.47f), false, 0.04f, false);

            var stepCount = 9;
            for (var i = 0; i < stepCount; i++)
            {
                var distance = Mathf.Lerp(-length * 0.43f, length * 0.43f, i / (float)(stepCount - 1));
                var position = horizontal ? new Vector3(distance, 0.085f, 0f) : new Vector3(0f, 0.085f, distance);
                var stoneScale = horizontal ? new Vector3(1.45f, 0.035f, 3.25f) : new Vector3(3.25f, 0.035f, 1.45f);
                ArenaPart(root, PrimitiveType.Cube, "Carved Path Stone", position, stoneScale,
                    i % 2 == 0 ? new Color(0.62f, 0.64f, 0.58f) : new Color(0.54f, 0.57f, 0.52f), false, 0.04f, false);
            }
        }

        private static void BuildRiftgardenBoundary(Transform root)
        {
            // Invisible square bounds retain the reliable gameplay limits. The visible
            // edge uses short rounded stones so the player sees a garden island, not a box.
            CreateInvisibleBoundary(root, new Vector3(0f, 0.7f, -16.1f), new Vector3(33f, 1.4f, 0.65f));
            CreateInvisibleBoundary(root, new Vector3(0f, 0.7f, 16.1f), new Vector3(33f, 1.4f, 0.65f));
            CreateInvisibleBoundary(root, new Vector3(-16.1f, 0.7f, 0f), new Vector3(0.65f, 1.4f, 33f));
            CreateInvisibleBoundary(root, new Vector3(16.1f, 0.7f, 0f), new Vector3(0.65f, 1.4f, 33f));

            for (var i = 0; i < 20; i++)
            {
                var angle = i * Mathf.PI * 2f / 20f;
                var position = new Vector3(Mathf.Cos(angle) * 15.55f, 0.28f, Mathf.Sin(angle) * 15.55f);
                var stone = ArenaPart(root, PrimitiveType.Capsule, "Riftgarden Edge Stone", position,
                    new Vector3(1.15f + (i % 3) * 0.12f, 0.48f, 0.72f),
                    i % 2 == 0 ? new Color(0.38f, 0.44f, 0.39f) : new Color(0.46f, 0.49f, 0.43f), false, 0.04f, false);
                stone.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 90f);
            }
        }

        private static void CreateInvisibleBoundary(Transform root, Vector3 position, Vector3 size)
        {
            var boundary = new GameObject("Riftgarden Boundary");
            boundary.transform.SetParent(root, false);
            boundary.transform.localPosition = position;
            boundary.AddComponent<BoxCollider>().size = size;
        }

        private static void BuildRiftgardenPortal(Transform root, Vector3 position, float yaw, Color accent)
        {
            var frame = SpawnModel(ForgeModels + "Door_Frame_A", root, position, yaw, 3.35f, FitAxis.Height);
            ApplyTempleMaterials(frame, accent);
            var pad = ArenaPart(root, PrimitiveType.Cylinder, "Portal Garden Pad", position + Vector3.up * 0.05f,
                new Vector3(3.5f, 0.05f, 3.5f), new Color(0.22f, 0.29f, 0.38f), false, 0.04f, false);
            pad.transform.localScale = new Vector3(3.5f, 0.05f, 2.7f);
            ArenaPart(root, PrimitiveType.Cylinder, "Portal Rune", position + Vector3.up * 0.105f,
                new Vector3(1.85f, 0.022f, 1.85f), accent, true, 0.3f, false);
        }

        private static void BuildDungeonPortal(Transform root, Vector3 position, float yaw, Color accent)
        {
            SpawnDungeonModel(root, "wall_doorway", position, yaw, 4.25f, FitAxis.Horizontal,
                new Color(0.96f, 0.93f, 0.88f));
            var pad = ArenaPart(root, PrimitiveType.Cylinder, "Portal Socket", position + Vector3.up * 0.045f,
                new Vector3(2.75f, 0.035f, 2.25f), new Color(0.2f, 0.15f, 0.12f), false, 0.05f, false);
            pad.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ArenaPart(root, PrimitiveType.Cylinder, "Portal Rune", position + Vector3.up * 0.09f,
                new Vector3(1.1f, 0.016f, 1.1f), accent, true, 0.28f, false);
        }

        private static void BuildCourtReactor(Transform root, Color cyan, Color violet, Color ember)
        {
            var platform = SpawnModel(ForgeModels + "Platform_Round1", root,
                new Vector3(0f, 0.02f, 0f), 0f, 5.4f, FitAxis.Horizontal);
            ApplyForgeMaterials(platform);

            ArenaPart(root, PrimitiveType.Cylinder, "Reactor Shadow Socket", new Vector3(0f, 0.075f, 0f),
                new Vector3(5.7f, 0.05f, 5.7f), new Color(0.035f, 0.055f, 0.12f), false, 0.08f, false);
            ArenaPart(root, PrimitiveType.Cylinder, "Reactor Cyan Core", new Vector3(0f, 0.135f, 0f),
                new Vector3(2.15f, 0.018f, 2.15f), cyan, true, 0.3f, false);
            ArenaPart(root, PrimitiveType.Cylinder, "Reactor Dark Iris", new Vector3(0f, 0.16f, 0f),
                new Vector3(1.38f, 0.018f, 1.38f), new Color(0.035f, 0.06f, 0.13f), false, 0.12f, false);

            var orbitA = new GameObject("Reactor Orbit A").transform;
            orbitA.SetParent(root, false);
            orbitA.localPosition = new Vector3(0f, 0.2f, 0f);
            var orbitB = new GameObject("Reactor Orbit B").transform;
            orbitB.SetParent(root, false);
            orbitB.localPosition = new Vector3(0f, 0.2f, 0f);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI * 2f / 6f;
                var parent = i % 2 == 0 ? orbitA : orbitB;
                CrystalPart(parent, "Orbiting Court Shard",
                    new Vector3(Mathf.Cos(angle) * 2.2f, 0.22f + (i % 2) * 0.18f, Mathf.Sin(angle) * 2.2f),
                    new Vector3(0.15f, 0.52f, 0.15f), new Vector3(18f, -i * 60f, 38f),
                    i % 3 == 0 ? ember : i % 2 == 0 ? cyan : violet);
            }
        }

        private static void BuildCourtBastion(Transform root, Vector3 position, float yaw, Color accent)
        {
            SpawnDungeonModel(root, "pillar_decorated", position, yaw, 3.3f,
                FitAxis.Height, new Color(0.94f, 0.9f, 0.84f));
            var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            SpawnDungeonModel(root, "torch_lit", position + direction * 0.72f + Vector3.up * 1.38f,
                yaw + 180f, 1.15f, FitAxis.Height, new Color(1f, 0.78f, 0.52f));
            CrystalPart(root, "Bastion Rift Crystal", position + Vector3.up * 2.48f,
                new Vector3(0.2f, 0.62f, 0.2f), new Vector3(12f, yaw, 42f), accent);
            CreateAccentLight(root, position + Vector3.up * 1.7f, accent, 5.8f, 1.15f);
        }

        private static void BuildCourtEdge(Transform root, Color tint)
        {
            var wallPositions = new[]
            {
                new Vector3(-8.6f, 0.02f, 14.45f), new Vector3(8.6f, 0.02f, 14.45f),
                new Vector3(-8.6f, 0.02f, -14.45f), new Vector3(8.6f, 0.02f, -14.45f),
                new Vector3(-14.45f, 0.02f, -7.4f), new Vector3(-14.45f, 0.02f, 7.4f),
                new Vector3(14.45f, 0.02f, -7.4f), new Vector3(14.45f, 0.02f, 7.4f)
            };
            for (var i = 0; i < wallPositions.Length; i++)
            {
                var sideWall = i >= 4;
                var model = i % 3 == 0 ? "wall_broken" : "barrier";
                SpawnDungeonModel(root, model, wallPositions[i], sideWall ? 90f : 0f,
                    model == "barrier" ? 3.7f : 4.1f, FitAxis.Horizontal,
                    i % 2 == 0 ? tint : Color.Lerp(tint, Color.white, 0.18f));
            }
        }

        private static void BuildGardenCluster(Transform root, Vector3 center, int seed, Color foliage)
        {
            for (var i = 0; i < 5; i++)
            {
                var angle = (seed * 47f + i * 72f) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle) * (0.5f + (i % 2) * 0.55f), 0f,
                    Mathf.Sin(angle) * (0.45f + (i % 3) * 0.3f));
                var leaf = ArenaPart(root, PrimitiveType.Sphere, "Chunky Garden Foliage", center + offset + Vector3.up * (0.35f + (i % 2) * 0.18f),
                    new Vector3(0.78f, 0.62f + (i % 2) * 0.18f, 0.72f),
                    i % 2 == 0 ? foliage : Color.Lerp(foliage, new Color(0.55f, 0.82f, 0.28f), 0.38f), false, 0.04f, false);
                leaf.transform.localRotation = Quaternion.Euler(0f, i * 29f, 0f);
            }
            for (var i = 0; i < 2; i++)
            {
                var rock = ArenaPart(root, PrimitiveType.Sphere, "Warm Garden Rock",
                    center + new Vector3((i * 2 - 1) * 0.82f, 0.24f, -0.32f + i * 0.48f),
                    new Vector3(0.8f, 0.52f, 0.68f), new Color(0.62f, 0.65f, 0.57f), false, 0.04f, false);
                rock.transform.localRotation = Quaternion.Euler(i * 17f, seed * 31f, i * -12f);
            }
        }

        private static void BuildRiftCrystalCluster(Transform root, Vector3 center, Color accent, int seed)
        {
            var darkStone = new Color(0.13f, 0.17f, 0.24f);
            for (var i = 0; i < 4; i++)
            {
                var angle = (seed * 41f + i * 82f) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(angle) * (0.35f + i * 0.16f), 0.28f,
                    Mathf.Sin(angle) * (0.3f + i * 0.14f));
                CrystalPart(root, "Court Rift Crystal", center + offset,
                    new Vector3(0.32f + i * 0.07f, 0.7f + (i % 3) * 0.28f, 0.32f + i * 0.05f),
                    new Vector3(i * 7f, seed * 31f + i * 23f, (i - 1.5f) * 7f),
                    i % 2 == 0 ? accent : Color.Lerp(accent, Color.white, 0.28f));
            }
            var baseStone = ArenaPart(root, PrimitiveType.Cylinder, "Crystal Cluster Base", center,
                new Vector3(1.55f, 0.22f, 1.25f), darkStone, false, 0.04f, false);
            baseStone.transform.localRotation = Quaternion.Euler(0f, seed * 27f, 0f);
        }

        private static void BuildEdgeForgeProp(Transform root, string model, Vector3 position, float yaw, float size)
        {
            var prop = SpawnModel(ForgeModels + model, root, position, yaw, size, FitAxis.Horizontal);
            ApplyTempleMaterials(prop, new Color(0.2f, 0.84f, 0.74f));
        }

        private static void ApplyTempleMaterials(GameObject root, Color accent)
        {
            if (!root) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
                var materials = new Material[materialCount];
                for (var i = 0; i < materialCount; i++)
                    materials[i] = i == materialCount - 1 && materialCount > 1
                        ? SolidMaterial(accent, true, 0.22f, 0.05f)
                        : SolidMaterial(i % 2 == 0 ? new Color(0.7f, 0.71f, 0.63f) : new Color(0.42f, 0.45f, 0.4f), false, 0.08f, 0.02f);
                renderer.sharedMaterials = materials;
            }
            SetRendering(root);
        }

        private static void BuildWalls(Transform root)
        {
            for (var i = -3; i <= 3; i++)
            {
                if (i != 0)
                {
                    var north = SpawnModel(ForgeModels + (i % 2 == 0 ? "WallAstra_Straight_Broken" : "WallAstra_Straight"), root,
                        new Vector3(i * 4.45f, 0f, 16.25f), 0f, 4.6f, FitAxis.Horizontal);
                    ApplyForgeMaterials(north);
                }

                if (Mathf.Abs(i) > 1)
                {
                    var south = SpawnModel(ForgeModels + "ShortWall_AccentStrip_Straight", root,
                        new Vector3(i * 4.45f, 0f, -16.25f), 0f, 4.6f, FitAxis.Horizontal);
                    ApplyForgeMaterials(south);
                }

                if (i != 0)
                {
                    var east = SpawnModel(ForgeModels + "WallAstra_Straight", root,
                        new Vector3(16.25f, 0f, i * 4.45f), 90f, 4.6f, FitAxis.Horizontal);
                    var west = SpawnModel(ForgeModels + "WallAstra_Straight", root,
                        new Vector3(-16.25f, 0f, i * 4.45f), 90f, 4.6f, FitAxis.Horizontal);
                    ApplyForgeMaterials(east);
                    ApplyForgeMaterials(west);
                }
            }
        }

        private static void BuildDoors(Transform root)
        {
            BuildDoor(root, new Vector3(0f, 0f, 16.15f), 0f, new Color(0.1f, 0.92f, 0.86f));
            BuildDoor(root, new Vector3(16.15f, 0f, 0f), 90f, new Color(1f, 0.55f, 0.12f));
            BuildDoor(root, new Vector3(-16.15f, 0f, 0f), 90f, new Color(0.55f, 0.25f, 1f));
        }

        private static void BuildDoor(Transform root, Vector3 position, float yaw, Color color)
        {
            var frame = SpawnModel(ForgeModels + "Door_Frame_A", root, position, yaw, 4.2f, FitAxis.Height);
            ApplyForgeMaterials(frame);
            var panel = SpawnModel(ForgeModels + "Door_DarkMetal", root, position + Vector3.up * 0.04f, yaw, 3.35f, FitAxis.Height);
            ApplyForgeMaterials(panel);
            CreateAccentLight(root, position + Vector3.up * 1.8f, color, 5.5f, 1.55f);
        }

        private static void BuildPermanentProps(Transform root)
        {
            var positions = new[]
            {
                new Vector3(-12.8f, 0.02f, 12.2f), new Vector3(12.5f, 0.02f, 11.6f),
                new Vector3(-13.2f, 0.02f, -10.8f), new Vector3(13f, 0.02f, -11.1f)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                var column = SpawnModel(ForgeModels + (i % 2 == 0 ? "Column_Astra" : "Column_Pipes"), root,
                    positions[i], i * 90f, 3.8f, FitAxis.Height);
                ApplyForgeMaterials(column);
            }

            var cables = SpawnModel(ForgeModels + "Prop_Cable_3", root, new Vector3(-8.5f, 0.025f, 13.5f), 18f, 4.2f, FitAxis.Horizontal);
            ApplyForgeMaterials(cables);
            var console = SpawnModel(ForgeModels + "Prop_Computer", root, new Vector3(11.9f, 0.03f, 13f), 205f, 2.1f, FitAxis.Horizontal);
            ApplyForgeMaterials(console);
        }

        private static void BuildVerdantGrowth(Transform root)
        {
            var centers = new[]
            {
                new Vector3(-14.2f, 0.18f, -12.4f), new Vector3(13.8f, 0.18f, -8.2f),
                new Vector3(-13.6f, 0.18f, 9.5f), new Vector3(12.8f, 0.18f, 12.2f)
            };
            for (var cluster = 0; cluster < centers.Length; cluster++)
            for (var i = 0; i < 7; i++)
            {
                var angle = (i * 51f + cluster * 23f) * Mathf.Deg2Rad;
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = "Forge Garden";
                leaf.transform.SetParent(root, false);
                leaf.transform.position = centers[cluster] + new Vector3(Mathf.Cos(angle) * 1.2f, i % 3 * 0.12f,
                    Mathf.Sin(angle) * 0.85f);
                leaf.transform.localScale = new Vector3(0.82f, 0.52f, 0.72f) * (0.75f + (i % 3) * 0.16f);
                var color = i % 3 == 0 ? new Color(0.34f, 0.72f, 0.25f)
                    : i % 3 == 1 ? new Color(0.14f, 0.5f, 0.24f) : new Color(0.46f, 0.78f, 0.32f);
                leaf.GetComponent<Renderer>().sharedMaterial = SolidMaterial(color, false, 0.05f, 0f);
                RemoveVisualCollider(leaf.GetComponent<Collider>());
            }
        }

        private static void AttachPulsePistols(Animator animator)
        {
            if (!animator || !animator.isHuman) return;
            AttachPulsePistol(animator.GetBoneTransform(HumanBodyBones.LeftHand), true);
            AttachPulsePistol(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
        }

        private static void AttachKayKitCrossbows(Animator animator)
        {
            if (!animator || !animator.isHuman) return;
            AttachKayKitWeapon(animator.GetBoneTransform(HumanBodyBones.LeftHand),
                KayWeapons + "crossbow_1handed", "Rex Left Rift Blaster",
                new Vector3(-0.02f, 0.01f, 0.09f), new Vector3(82f, 5f, 2f), 0.66f,
                KayWeapons + "ranger_texture");
            AttachKayKitWeapon(animator.GetBoneTransform(HumanBodyBones.RightHand),
                KayWeapons + "crossbow_1handed", "Rex Right Rift Blaster",
                new Vector3(0.02f, 0.01f, 0.09f), new Vector3(82f, -5f, -2f), 0.66f,
                KayWeapons + "ranger_texture");
        }

        private static void AttachKayKitStaff(Animator animator, Transform fallback)
        {
            var hand = animator && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : fallback;
            AttachKayKitWeapon(hand, KayWeapons + "staff", "Mira Rift Staff",
                new Vector3(0.02f, -0.08f, 0.06f), new Vector3(8f, 0f, 92f), 1.42f,
                KayWeapons + "mage_texture");
        }

        private static void AttachSkeletonLoadout(Animator animator, EnemyKind kind)
        {
            if (!animator || !animator.isHuman) return;
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            switch (kind)
            {
                case EnemyKind.Crawler:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Blade", "Riftbone Blade",
                        new Vector3(0.02f, 0f, 0.08f), new Vector3(88f, 0f, 0f), 0.8f, SkeletonTexture);
                    break;
                case EnemyKind.Shooter:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Staff", "Riftbone Focus",
                        new Vector3(0.02f, -0.04f, 0.05f), new Vector3(8f, 0f, 92f), 1.34f, SkeletonTexture);
                    break;
                case EnemyKind.Brute:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Axe", "Riftbone Cleaver",
                        new Vector3(0.02f, -0.03f, 0.07f), new Vector3(4f, 0f, 92f), 1.22f, SkeletonTexture);
                    AttachKayKitWeapon(leftHand, SkeletonWeapons + "Skeleton_Shield_Large_A", "Riftbone Shield",
                        new Vector3(0f, 0.02f, 0.07f), new Vector3(88f, 0f, 0f), 1.05f, SkeletonTexture);
                    break;
                case EnemyKind.Shieldbearer:
                    // Der Schild ist absichtlich uebergross: er ist die Mechanik und muss von weitem
                    // als Flaeche lesbar sein, hinter der die Figur verschwindet.
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Blade", "Bulwark Blade",
                        new Vector3(0.02f, 0f, 0.08f), new Vector3(88f, 0f, 0f), 0.82f, SkeletonTexture);
                    AttachKayKitWeapon(leftHand, SkeletonWeapons + "Skeleton_Shield_Large_A", "Bulwark Shield",
                        new Vector3(0f, 0.03f, 0.09f), new Vector3(88f, 0f, 0f), 1.18f, SkeletonTexture);
                    break;
                case EnemyKind.Marksman:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Crossbow", "Riftbone Crossbow",
                        new Vector3(0.03f, -0.02f, 0.07f), new Vector3(0f, 0f, 92f), 0.95f, SkeletonTexture);
                    break;
                case EnemyKind.ChoirWarden:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Staff", "Choir Staff",
                        new Vector3(0.02f, -0.02f, 0.07f), new Vector3(0f, 0f, 92f), 1.7f, SkeletonTexture);
                    break;
                case EnemyKind.RiftTwin:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Blade", "Twin Blade",
                        new Vector3(0.02f, -0.02f, 0.07f), new Vector3(0f, 0f, 92f), 1.5f, SkeletonTexture);
                    AttachKayKitWeapon(leftHand, SkeletonWeapons + "Skeleton_Blade", "Twin Offhand",
                        new Vector3(0f, -0.02f, 0.07f), new Vector3(0f, 0f, 92f), 1.4f, SkeletonTexture);
                    break;
                case EnemyKind.Elite:
                case EnemyKind.IronWarden:
                    AttachKayKitWeapon(rightHand, SkeletonWeapons + "Skeleton_Axe", "Warden Cleaver",
                        new Vector3(0.02f, -0.03f, 0.08f), new Vector3(4f, 0f, 92f),
                        EnemyKinds.IsBoss(kind) ? 1.68f : 1.36f, SkeletonTexture);
                    AttachKayKitWeapon(leftHand, SkeletonWeapons + "Skeleton_Shield_Large_A", "Warden Shield",
                        new Vector3(0f, 0.02f, 0.08f), new Vector3(88f, 0f, 0f),
                        EnemyKinds.IsBoss(kind) ? 1.48f : 1.16f, SkeletonTexture);
                    break;
            }
        }

        private static void AttachKayKitWeapon(Transform hand, string resource, string name,
            Vector3 localPosition, Vector3 localEuler, float targetSize, string texture)
        {
            if (!hand) return;
            var source = LoadModel(resource);
            if (!source) return;
            var weapon = UnityEngine.Object.Instantiate(source, hand);
            weapon.name = name;
            ResetTransform(weapon.transform);
            FitToWorldSize(weapon, targetSize, FitAxis.Largest);
            weapon.transform.localPosition = localPosition;
            weapon.transform.localRotation = Quaternion.Euler(localEuler);
            ApplyKayKitMaterials(weapon, texture, Color.white);
        }

        private static void AttachRexScarf(Animator animator, Transform fallback)
        {
            var anchor = animator && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Neck)
                : fallback;
            if (!anchor) return;
            var orange = new Color(1f, 0.36f, 0.08f);
            GearPart(anchor, PrimitiveType.Sphere, "Rex Scarf Collar", new Vector3(0f, -0.07f, 0.01f),
                new Vector3(0.72f, 0.18f, 0.58f), Vector3.zero, orange, false, 0.16f);
            GearPart(anchor, PrimitiveType.Cube, "Rex Scarf Tail A", new Vector3(-0.18f, -0.26f, -0.16f),
                new Vector3(0.18f, 0.58f, 0.08f), new Vector3(14f, 8f, -24f), orange, false, 0.12f);
            GearPart(anchor, PrimitiveType.Cube, "Rex Scarf Tail B", new Vector3(0.1f, -0.24f, -0.2f),
                new Vector3(0.15f, 0.46f, 0.07f), new Vector3(18f, -8f, 16f), new Color(0.86f, 0.2f, 0.04f), false, 0.12f);
        }

        /// <summary>
        /// Brax' Streithammer am Waffenhalter-Knochen der KayKit-Figur (handslot.r). Dort sitzen Waffen ohne
        /// geschaetzte Winkel, der Griff zeigt entlang der lokalen Y-Achse. Masse in Welt-Einheiten: die Figur ist
        /// skaliert, und Groessen in Knochen-Einheiten liessen den Kopf riesig werden.
        /// Vorher hing der Hammer am Handknochen, steckte in Ruhe im Koerper und ragte beim Schlag als Klotz
        /// zur Seite - die Kampfclips sahen dadurch aus wie "nur so tun".
        /// </summary>
        /// <summary>
        /// XIROs Ausruestung: ein Zweihaender, sonst nichts.
        ///
        /// Die erste Fassung gab ihm Schwert und Rundschild. Der Zweihaender ist die staerkere
        /// Ansage: er haelt Schlaege nicht hinter einem Schild ab, sondern hinter der flach
        /// gestellten Klinge - dieselbe Mechanik, aber die Waffe erzaehlt sie mit.
        ///
        /// Die Klinge wird in der rechten Hand gefuehrt; die Zweihand-Clips aus dem KayKit-Satz
        /// legen die linke ohnehin an den Griff.
        /// </summary>
        private static void AttachOathGear(Animator animator, Transform fallback, Color accent)
        {
            var steel = new Color(0.84f, 0.86f, 0.9f);
            var gold = new Color(0.95f, 0.78f, 0.3f);

            var right = FindNamedBone(animator ? animator.transform : fallback, "handslot.r");
            if (!right) right = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : fallback;
            if (!right) return;

            var blade = new GameObject("Xiro Greatsword").transform;
            blade.SetParent(right, false);
            blade.localPosition = Vector3.zero;
            blade.localRotation = Quaternion.identity;
            blade.localScale = Vector3.one / Mathf.Max(0.0001f, right.lossyScale.x);

            // Langer Griff fuer zwei Haende, schwerer Knauf als Gegengewicht.
            GearPart(blade, PrimitiveType.Cube, "Xiro Grip", new Vector3(0f, -0.16f, 0f),
                new Vector3(0.08f, 0.42f, 0.08f), Vector3.zero, new Color(0.24f, 0.15f, 0.11f), false, 0.2f, 0f);
            GearPart(blade, PrimitiveType.Sphere, "Xiro Pommel", new Vector3(0f, -0.4f, 0f),
                new Vector3(0.15f, 0.14f, 0.15f), Vector3.zero, gold, false, 0.42f, 0.65f);
            GearPart(blade, PrimitiveType.Cube, "Xiro Crossguard", new Vector3(0f, 0.1f, 0f),
                new Vector3(0.62f, 0.08f, 0.12f), Vector3.zero, gold, false, 0.45f, 0.65f);
            for (var side = -1; side <= 1; side += 2)
                GearPart(blade, PrimitiveType.Cube, "Xiro Guard Wing", new Vector3(side * 0.26f, 0.17f, 0f),
                    new Vector3(0.12f, 0.12f, 0.1f), new Vector3(0f, 0f, side * 32f), gold, false, 0.45f, 0.65f);
            // Die Klinge selbst: breiter und laenger als eine Einhandwaffe.
            GearPart(blade, PrimitiveType.Cube, "Xiro Blade", new Vector3(0f, 0.98f, 0f),
                new Vector3(0.19f, 1.72f, 0.05f), Vector3.zero, steel, false, 0.66f, 0.78f);
            GearPart(blade, PrimitiveType.Cube, "Xiro Blade Fuller", new Vector3(0f, 0.98f, 0f),
                new Vector3(0.06f, 1.62f, 0.07f), Vector3.zero, accent, true, 0.5f, 0.1f);
            GearPart(blade, PrimitiveType.Cube, "Xiro Blade Tip", new Vector3(0f, 1.9f, 0f),
                new Vector3(0.19f, 0.2f, 0.05f), new Vector3(0f, 0f, 45f), steel, false, 0.66f, 0.78f);
        }

        private static void AttachGuardianHammer(Animator animator, Transform fallback, Color accent)
        {
            var slot = FindNamedBone(animator ? animator.transform : fallback, "handslot.r");
            if (!slot) slot = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : fallback;
            if (!slot) return;

            var hammer = new GameObject("Brax War Hammer").transform;
            hammer.SetParent(slot, false);
            hammer.localPosition = Vector3.zero;
            hammer.localRotation = Quaternion.identity;
            var boneScale = Mathf.Max(0.0001f, slot.lossyScale.x);
            hammer.localScale = Vector3.one / boneScale;

            var wood = new Color(0.36f, 0.22f, 0.12f);
            var iron = new Color(0.3f, 0.32f, 0.36f);
            // Stiel ueber und unter der Hand, damit auch die zweite Hand greifen kann. Zylinder sind 2 Einheiten hoch.
            GearPart(hammer, PrimitiveType.Cylinder, "Brax Hammer Shaft", new Vector3(0f, 0.36f, 0f),
                new Vector3(0.085f, 0.63f, 0.085f), Vector3.zero, wood, false, 0.15f, 0f);
            GearPart(hammer, PrimitiveType.Sphere, "Brax Hammer Pommel", new Vector3(0f, -0.29f, 0f),
                new Vector3(0.14f, 0.12f, 0.14f), Vector3.zero, iron, false, 0.3f, 0.5f);
            GearPart(hammer, PrimitiveType.Cube, "Brax Hammer Head", new Vector3(0f, 1.06f, 0f),
                new Vector3(0.56f, 0.32f, 0.32f), Vector3.zero, iron, false, 0.28f, 0.55f);
            GearPart(hammer, PrimitiveType.Cube, "Brax Hammer Band", new Vector3(0f, 1.06f, 0f),
                new Vector3(0.2f, 0.36f, 0.36f), Vector3.zero, new Color(0.55f, 0.36f, 0.14f), false, 0.3f, 0.4f);
            GearPart(hammer, PrimitiveType.Cube, "Brax Hammer Amber Face L", new Vector3(-0.29f, 1.06f, 0f),
                new Vector3(0.04f, 0.24f, 0.24f), Vector3.zero, accent, true, 0.34f, 0.12f);
            GearPart(hammer, PrimitiveType.Cube, "Brax Hammer Amber Face R", new Vector3(0.29f, 1.06f, 0f),
                new Vector3(0.04f, 0.24f, 0.24f), Vector3.zero, accent, true, 0.34f, 0.12f);
        }

        private static Transform FindNamedBone(Transform root, string boneName)
        {
            if (!root) return null;
            if (root.name == boneName) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindNamedBone(root.GetChild(i), boneName);
                if (found) return found;
            }
            return null;
        }

        private static void AttachSupportFocus(Animator animator, Transform fallback, Color accent)
        {
            var hand = animator && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.LeftHand)
                : fallback;
            if (!hand) return;
            GearPart(hand, PrimitiveType.Cube, "Mira Dawn Prism", new Vector3(0f, 0.05f, 0.34f),
                new Vector3(0.34f, 0.52f, 0.34f), new Vector3(22f, 45f, 18f), accent, true, 0.52f, 0.05f);
            GearPart(hand, PrimitiveType.Sphere, "Mira Prism Halo A", new Vector3(-0.28f, 0.12f, 0.34f),
                new Vector3(0.12f, 0.12f, 0.12f), Vector3.zero, Color.white, true, 0.45f);
            GearPart(hand, PrimitiveType.Sphere, "Mira Prism Halo B", new Vector3(0.26f, -0.14f, 0.32f),
                new Vector3(0.09f, 0.09f, 0.09f), Vector3.zero, accent, true, 0.45f);
        }

        private static void AttachPulsePistol(Transform hand, bool left)
        {
            if (!hand) return;
            var side = left ? -1f : 1f;
            var dark = new Color(0.055f, 0.09f, 0.14f);
            var metal = new Color(0.16f, 0.28f, 0.36f);
            var energy = left ? new Color(0.08f, 0.9f, 1f) : new Color(0.22f, 0.68f, 1f);
            GearPart(hand, PrimitiveType.Cube, left ? "Rex Left Pistol Body" : "Rex Right Pistol Body",
                new Vector3(side * 0.015f, 0.02f, 0.28f), new Vector3(0.2f, 0.22f, 0.62f),
                new Vector3(0f, side * 4f, 0f), dark, false, 0.28f, 0.46f);
            GearPart(hand, PrimitiveType.Cube, "Rex Pistol Upper Rail", new Vector3(side * 0.015f, 0.13f, 0.27f),
                new Vector3(0.13f, 0.08f, 0.48f), Vector3.zero, metal, false, 0.24f, 0.38f);
            GearPart(hand, PrimitiveType.Cube, "Rex Pistol Grip", new Vector3(side * 0.01f, -0.16f, 0.04f),
                new Vector3(0.13f, 0.32f, 0.15f), new Vector3(-16f, 0f, 0f), metal, false, 0.22f, 0.28f);
            GearPart(hand, PrimitiveType.Cylinder, "Rex Pistol Coil", new Vector3(side * 0.015f, 0.02f, 0.57f),
                new Vector3(0.13f, 0.18f, 0.13f), new Vector3(90f, 0f, 0f), energy, true, 0.48f, 0.12f);
            GearPart(hand, PrimitiveType.Sphere, "Rex Pistol Core", new Vector3(side * 0.015f, 0.02f, 0.43f),
                new Vector3(0.18f, 0.18f, 0.18f), Vector3.zero, energy, true, 0.56f, 0.08f);
        }

        private static GameObject SpawnModel(string resource, Transform parent, Vector3 worldPosition, float yaw, float targetSize, FitAxis axis)
        {
            var source = LoadModel(resource);
            if (!source) return null;
            var model = UnityEngine.Object.Instantiate(source, parent);
            model.name = source.name;
            ResetTransform(model.transform);
            model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            FitAndPlace(model, worldPosition, targetSize, axis);
            SetRendering(model);
            return model;
        }

        private static GameObject SpawnDungeonModel(Transform parent, string modelName, Vector3 worldPosition,
            float yaw, float targetSize, FitAxis axis, Color tint)
        {
            var model = SpawnModel(DungeonModels + modelName, parent, worldPosition, yaw, targetSize, axis);
            if (model) ApplyKayKitMaterials(model, DungeonTexture, tint);
            return model;
        }

        private static GameObject LoadModel(string resource)
        {
            if (ModelCache.TryGetValue(resource, out var cached) && cached) return cached;
            var loaded = Resources.Load<GameObject>(resource);
            ModelCache[resource] = loaded;
            return loaded;
        }

        private static void FitAndPlace(GameObject model, Vector3 worldPosition, float targetSize, FitAxis axis)
        {
            FitToWorldSize(model, targetSize, axis);
            if (!TryGetBounds(model, out var bounds))
            {
                model.transform.position = worldPosition;
                return;
            }
            model.transform.position += new Vector3(worldPosition.x - bounds.center.x, worldPosition.y - bounds.min.y,
                worldPosition.z - bounds.center.z);
        }

        private static void Reground(GameObject model, Vector3 worldPosition)
        {
            if (!TryGetBounds(model, out var bounds)) return;
            model.transform.position += new Vector3(worldPosition.x - bounds.center.x,
                worldPosition.y - bounds.min.y, worldPosition.z - bounds.center.z);
        }

        private static void FitToWorldSize(GameObject model, float targetSize, FitAxis axis)
        {
            if (!TryGetBounds(model, out var bounds)) return;
            var sourceSize = axis switch
            {
                FitAxis.Height => bounds.size.y,
                FitAxis.Horizontal => Mathf.Max(bounds.size.x, bounds.size.z),
                _ => Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
            };
            if (sourceSize <= 0.0001f) return;
            model.transform.localScale *= targetSize / sourceSize;
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            var found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static void ApplyRexMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var replacements = new Material[Mathf.Max(1, originals.Length)];
                for (var i = 0; i < replacements.Length; i++)
                {
                    var name = originals.Length > i && originals[i] ? originals[i].name : string.Empty;
                    replacements[i] = name.ToLowerInvariant().Contains("ranger")
                        ? TexturedMaterial("RexRanger", RexTextures + "T_Ranger_3_BaseColor", RexTextures + "T_Ranger_Normal",
                            new Color(0.28f, 0.52f, 0.78f), 0.04f, 0.22f)
                        : TexturedMaterial("RexSkin", RexTextures + "T_Regular_Male_Dark_BaseColor", RexTextures + "T_Regular_Male_Normal",
                            new Color(1f, 0.86f, 0.72f), 0f, 0.3f);
                }
                renderer.sharedMaterials = replacements;
            }
            SetRendering(root);
        }

        private static void ApplyCompanionMaterials(GameObject root, CompanionRole role, Color accent)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var replacements = new Material[Mathf.Max(1, originals.Length)];
                for (var i = 0; i < replacements.Length; i++)
                {
                    var name = originals.Length > i && originals[i] ? originals[i].name.ToLowerInvariant() : string.Empty;
                    var skin = name.Contains("regular") || name.Contains("skin") || name.Contains("base");
                    if (skin)
                    {
                        var prefix = role == CompanionRole.Support ? "T_Regular_Female" : "T_Regular_Male";
                        replacements[i] = TexturedMaterial(role + "Skin", HeroTextures + prefix + "_Dark_BaseColor",
                            HeroTextures + prefix + "_Normal", new Color(1f, 0.9f, 0.8f), 0f, 0.34f);
                    }
                    else
                    {
                        var prefix = role == CompanionRole.Support ? "T_Ranger_3" : "T_Peasant_2";
                        var normal = role == CompanionRole.Support ? "T_Ranger_Normal" : "T_Peasant_Normal";
                        var outfitBase = role == CompanionRole.Support
                            ? new Color(0.48f, 0.82f, 0.7f)
                            : new Color(0.58f, 0.45f, 0.27f);
                        var outfitTint = Color.Lerp(outfitBase, accent, 0.14f);
                        replacements[i] = TexturedMaterial(role + "Outfit", HeroTextures + prefix + "_BaseColor",
                            HeroTextures + normal, outfitTint, 0.04f, 0.22f);
                    }
                }
                renderer.sharedMaterials = replacements;
            }
            SetRendering(root);
        }

        private static void ApplyKayKitMaterials(GameObject root, string texturePath, Color tint)
        {
            if (!root) return;
            var material = KayKitMaterial(texturePath, tint);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var count = Mathf.Max(1, renderer.sharedMaterials.Length);
                var replacements = new Material[count];
                for (var i = 0; i < count; i++) replacements[i] = material;
                renderer.sharedMaterials = replacements;
            }
            SetRendering(root);
        }

        private static Material KayKitMaterial(string texturePath, Color tint)
        {
            var key = "KayKit:" + texturePath + ":" + tint.GetHashCode();
            if (MaterialCache.TryGetValue(key, out var cached) && cached) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = "SHATTERSPIRE · KayKit " + texturePath.Substring(texturePath.LastIndexOf('/') + 1),
                color = tint,
                enableInstancing = true
            };
            var texture = Resources.Load<Texture2D>(texturePath);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            MaterialCache[key] = material;
            return material;
        }

        // internal statt private: der Tower Lift in FloorObjectiveController baut
        // dasselbe Platform_Round1-Modell auf und konnte die Materialien bisher
        // nicht zuweisen - er blieb deshalb mit dem untexturierten FBX-Standard
        // als grosse weisse Scheibe im Bild.
        internal static void ApplyForgeMaterials(GameObject root)
        {
            if (!root) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var replacements = new Material[Mathf.Max(1, originals.Length)];
                for (var i = 0; i < replacements.Length; i++)
                {
                    var name = originals.Length > i && originals[i] ? originals[i].name.ToLowerInvariant() : string.Empty;
                    if (name.Contains("padded"))
                        replacements[i] = TexturedMaterial("ForgePadded", ForgeTextures + "T_PaddedWall_BaseColor", ForgeTextures + "T_PaddedWall_Normal",
                            new Color(0.43f, 0.57f, 0.47f), 0.04f, 0.24f);
                    else if (name.Contains("trim_03") || name.Contains("cable"))
                        replacements[i] = TexturedMaterial("ForgeTrim03", ForgeTextures + "T_Trim_03_BaseColor", ForgeTextures + "T_Trim_03_Normal",
                            new Color(0.72f, 0.54f, 0.25f), 0.24f, 0.3f);
                    else if (name.Contains("trim_02"))
                        replacements[i] = TexturedMaterial("ForgeTrim02", ForgeTextures + "T_Trim_02_BaseColor_Blue", ForgeTextures + "T_Trim_02_Normal",
                            new Color(0.28f, 0.68f, 0.58f), 0.25f, 0.29f);
                    else
                        replacements[i] = TexturedMaterial("ForgeTrim01", ForgeTextures + "T_Trim_01_BaseColor", ForgeTextures + "T_Trim_01_Normal",
                            new Color(0.57f, 0.55f, 0.43f), 0.26f, 0.25f);
                }
                renderer.sharedMaterials = replacements;
            }
            SetRendering(root);
        }

        private static void ApplyCreatureMaterials(GameObject root, Color primary, Color accent)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var count = Mathf.Max(1, originals.Length);
                var replacements = new Material[count];
                for (var i = 0; i < count; i++)
                {
                    var sourceName = originals.Length > i && originals[i] ? originals[i].name : string.Empty;
                    var semantic = (renderer.name + " " + sourceName).ToLowerInvariant();
                    var isEnergy = semantic.Contains("eye") || semantic.Contains("glow") || semantic.Contains("emiss") || semantic.Contains("core");
                    var shade = Mathf.Abs((renderer.name.GetHashCode() + i * 17) % 3);
                    var bodyColor = shade == 0
                        ? Color.Lerp(primary, new Color(0.07f, 0.09f, 0.12f), 0.2f)
                        : shade == 1 ? primary : Color.Lerp(primary, Color.white, 0.13f);
                    replacements[i] = isEnergy
                        ? SolidMaterial(accent, true, 0.42f, 0.04f)
                        : SolidMaterial(bodyColor, false, 0.12f, 0.03f);
                }
                renderer.sharedMaterials = replacements;
            }
            SetRendering(root);
        }

        private static Material TexturedMaterial(string id, string albedoPath, string normalPath, Color tint, float metallic, float smoothness)
        {
            var key = id + tint.GetHashCode();
            if (MaterialCache.TryGetValue(key, out var cached) && cached) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "SS " + id, color = tint, enableInstancing = true };
            var albedo = Resources.Load<Texture2D>(albedoPath);
            var normal = Resources.Load<Texture2D>(normalPath);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (albedo)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            }
            if (normal)
            {
                if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            MaterialCache[key] = material;
            return material;
        }

        private static Material SolidMaterial(Color color, bool emissive, float smoothness, float metallic)
        {
            var key = "Solid" + color.GetHashCode() + emissive + Mathf.RoundToInt(smoothness * 100f) + Mathf.RoundToInt(metallic * 100f);
            if (MaterialCache.TryGetValue(key, out var cached) && cached) return cached;
            var material = PrototypeFactory.CreateMaterial(color, emissive, smoothness, metallic);
            MaterialCache[key] = material;
            return material;
        }

        private static void SetRendering(GameObject root)
        {
            if (!root) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                RemoveVisualCollider(collider);
        }

        private static void StylizeHumanoidProportions(Animator animator, float headScale, float handScale)
        {
            if (!animator || !animator.isHuman) return;
            ScaleBone(animator.GetBoneTransform(HumanBodyBones.Head), headScale);
            ScaleBone(animator.GetBoneTransform(HumanBodyBones.LeftHand), handScale);
            ScaleBone(animator.GetBoneTransform(HumanBodyBones.RightHand), handScale);
        }

        private static void ScaleBone(Transform bone, float scale)
        {
            if (bone) bone.localScale = bone.localScale * scale;
        }

        private static Transform GearPart(Transform parent, PrimitiveType type, string name, Vector3 worldOffset,
            Vector3 worldScale, Vector3 localEuler, Color color, bool emissive, float smoothness, float metallic = 0f)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.position = parent.position + parent.rotation * worldOffset;
            part.transform.rotation = parent.rotation * Quaternion.Euler(localEuler);
            part.transform.SetParent(parent, true);
            SetWorldScale(part.transform, worldScale);
            part.GetComponent<Renderer>().sharedMaterial = SolidMaterial(color, emissive, smoothness, metallic);
            RemoveVisualCollider(part.GetComponent<Collider>());
            return part.transform;
        }

        private static void SetWorldScale(Transform value, Vector3 worldScale)
        {
            var parentScale = value.parent ? value.parent.lossyScale : Vector3.one;
            value.localScale = new Vector3(
                worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private static GameObject CrystalPart(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Vector3 localEuler, Color color, bool emissive = true)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            part.AddComponent<MeshFilter>().sharedMesh = GetCrystalMesh();
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SolidMaterial(color, emissive, 0.26f, 0.02f);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part;
        }

        private static Mesh GetCrystalMesh()
        {
            if (crystalMesh) return crystalMesh;
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(24);
            var top = new Vector3(0f, 0.62f, 0f);
            var bottom = new Vector3(0f, -0.5f, 0f);
            var ring = new[]
            {
                new Vector3(-0.5f, -0.08f, -0.38f), new Vector3(0.5f, -0.08f, -0.38f),
                new Vector3(0.5f, -0.08f, 0.38f), new Vector3(-0.5f, -0.08f, 0.38f)
            };
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                var start = vertices.Count;
                vertices.Add(top);
                vertices.Add(ring[next]);
                vertices.Add(ring[i]);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);

                start = vertices.Count;
                vertices.Add(bottom);
                vertices.Add(ring[i]);
                vertices.Add(ring[next]);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            crystalMesh = new Mesh { name = "SHATTERSPIRE Faceted Crystal" };
            crystalMesh.SetVertices(vertices);
            crystalMesh.SetTriangles(triangles, 0);
            crystalMesh.RecalculateNormals();
            crystalMesh.RecalculateBounds();
            return crystalMesh;
        }

        private static void AddRiftbornSignature(Transform rig, EnemyKind kind, Color primary, Color accent)
        {
            var dark = Color.Lerp(primary, new Color(0.035f, 0.055f, 0.075f), 0.68f);
            switch (kind)
            {
                case EnemyKind.Crawler:
                    CrystalPart(rig, "Riftborn Heart", new Vector3(0f, 0.72f, 0.5f),
                        new Vector3(0.2f, 0.2f, 0.13f), new Vector3(18f, 45f, 18f), accent);
                    for (var i = -1; i <= 1; i += 2)
                    {
                        CrystalPart(rig, "Crawler Crystal Spine",
                            new Vector3(i * 0.22f, 0.92f, -0.16f),
                            new Vector3(0.1f, 0.32f, 0.1f),
                            new Vector3(38f, i * 16f, 45f + i * 8f), accent);
                    }
                    break;

                case EnemyKind.Shooter:
                    var orbit = new GameObject("Cyclop Orbiting Shards").transform;
                    orbit.SetParent(rig, false);
                    orbit.localPosition = Vector3.up * 1.08f;
                    for (var i = 0; i < 3; i++)
                    {
                        var angle = i * Mathf.PI * 2f / 3f;
                        CrystalPart(orbit, "Orbit Shard",
                            new Vector3(Mathf.Cos(angle) * 0.68f, (i - 1) * 0.1f, Mathf.Sin(angle) * 0.52f),
                            new Vector3(0.1f, 0.3f, 0.1f), new Vector3(18f, i * 120f, 45f), accent);
                    }
                    orbit.gameObject.AddComponent<RiftbornOrbit>();
                    break;

                case EnemyKind.Brute:
                case EnemyKind.Elite:
                    var elite = kind == EnemyKind.Elite;
                    CrystalPart(rig, elite ? "Elite Rift Reactor" : "Brute Rift Reactor",
                        new Vector3(0f, 1.18f, 0.58f), new Vector3(0.26f, 0.26f, 0.13f),
                        new Vector3(0f, 0f, 45f), accent);
                    if (elite)
                    {
                        for (var side = -1; side <= 1; side += 2)
                        {
                            CrystalPart(rig, "Elite Crown Shard", new Vector3(side * 0.3f, 2.18f, 0f),
                                new Vector3(0.11f, 0.4f, 0.11f), new Vector3(0f, 0f, side * 24f), accent);
                        }
                    }
                    break;

                case EnemyKind.Shieldbearer:
                    // Der Kristall sitzt seitlich am Schildarm: von vorn verdeckt ihn der Schild, von
                    // der Seite leuchtet er. Damit zeigt die Silhouette selbst, wo der Gegner offen ist.
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CrystalPart(rig, "Bulwark Flank Crystal", new Vector3(side * 0.52f, 1.12f, -0.1f),
                            new Vector3(0.12f, 0.34f, 0.12f), new Vector3(12f, 0f, side * 26f), accent);
                    }
                    CrystalPart(rig, "Bulwark Core", new Vector3(0f, 1.42f, -0.32f),
                        new Vector3(0.2f, 0.2f, 0.11f), new Vector3(0f, 0f, 45f), accent);
                    break;

                case EnemyKind.Marksman:
                    CrystalPart(rig, "Marksman Sight Crystal", new Vector3(0f, 1.5f, 0.34f),
                        new Vector3(0.15f, 0.15f, 0.09f), new Vector3(0f, 45f, 45f), accent);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CrystalPart(rig, "Marksman Quiver Shard", new Vector3(side * 0.26f, 1.05f, -0.36f),
                            new Vector3(0.08f, 0.3f, 0.08f), new Vector3(-28f, 0f, side * 14f), accent);
                    }
                    break;

                case EnemyKind.RiftTwin:
                    // Zwei Splitter ueber den Schultern - das Zeichen dafuer, dass er sich teilt.
                    for (var side = -1; side <= 1; side += 2)
                        CrystalPart(rig, "Twin Rift Shard", new Vector3(side * 0.62f, 2.6f, -0.1f),
                            new Vector3(0.22f, 0.82f, 0.22f), new Vector3(0f, 0f, side * 34f), accent);
                    break;

                case EnemyKind.ChoirWarden:
                    // Ein Ring aus Splittern: der Chor, den er ruft, haengt sichtbar ueber ihm.
                    for (var i = 0; i < 5; i++)
                    {
                        var angle = i * Mathf.PI * 2f / 5f;
                        CrystalPart(rig, "Choir Shard",
                            new Vector3(Mathf.Cos(angle) * 0.78f, 2.72f, Mathf.Sin(angle) * 0.78f),
                            new Vector3(0.16f, 0.5f, 0.16f), new Vector3(0f, i * 72f, 20f), accent);
                    }
                    break;

                case EnemyKind.IronWarden:
                    CrystalPart(rig, "Warden Crown Reactor", new Vector3(0f, 2.28f, 0.82f),
                        new Vector3(0.48f, 0.48f, 0.2f), new Vector3(0f, 0f, 45f), accent);
                    for (var side = -1; side <= 1; side += 2)
                    {
                        CrystalPart(rig, "Warden Crown Shard", new Vector3(side * 0.65f, 3.55f, -0.05f),
                            new Vector3(0.18f, 0.7f, 0.18f), new Vector3(0f, 0f, side * 22f), accent);
                    }
                    break;
            }
        }

        private static void CreateObstacle(Transform parent, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Forge Obstacle Collider");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void AddCreatureCore(Transform parent, float height, Color color, float size)
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Rift Core";
            core.transform.SetParent(parent, false);
            core.transform.localPosition = new Vector3(0f, height * 0.52f, 0.38f);
            core.transform.localScale = Vector3.one * size;
            core.GetComponent<Renderer>().sharedMaterial = SolidMaterial(color, true, 0.55f, 0.12f);
            RemoveVisualCollider(core.GetComponent<Collider>());
        }

        private static void CreateGroundShadow(Transform parent, float size)
        {
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "Champion Ground Shadow";
            shadow.transform.SetParent(parent, false);
            shadow.transform.localPosition = Vector3.up * 0.02f;
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(size * 1.3f, size * 0.92f, 1f);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(new Color(0.015f, 0.025f, 0.035f, 0.42f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RemoveVisualCollider(shadow.GetComponent<Collider>());
        }

        private static void CreateSelectionRing(Transform parent, float size, Color color)
        {
            var outer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outer.name = "Champion Selection Ring";
            outer.transform.SetParent(parent, false);
            outer.transform.localPosition = Vector3.up * 0.035f;
            outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            outer.transform.localScale = new Vector3(size * 1.18f, size * 1.18f, 1f);
            color.a = 0.84f;
            var renderer = outer.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(color, 0.68f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RemoveVisualCollider(outer.GetComponent<Collider>());
        }

        private static void CreateEnergyDisc(Transform parent, Vector3 position, float diameter, Color color)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Forge Energy Inlay";
            disc.transform.SetParent(parent, false);
            disc.transform.position = position;
            disc.transform.localScale = new Vector3(diameter, 0.018f, diameter);
            disc.GetComponent<Renderer>().sharedMaterial = SolidMaterial(color, true, 0.22f, 0.04f);
            RemoveVisualCollider(disc.GetComponent<Collider>());
        }

        private static void CreateAccentLight(Transform parent, Vector3 position, Color color, float range, float intensity)
        {
            var lightObject = new GameObject("Forge Accent Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private static void RemoveVisualCollider(Collider collider)
        {
            if (!collider) return;
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
        }

        private static void ResetTransform(Transform value)
        {
            value.localPosition = Vector3.zero;
            value.localRotation = Quaternion.identity;
            value.localScale = Vector3.one;
        }

        private enum FitAxis { Height, Horizontal, Largest }
    }

    public sealed class CourtAmbientMotion : MonoBehaviour
    {
        private Transform orbitA;
        private Transform orbitB;
        private Light[] accentLights;
        private float[] baseIntensity;

        private void Awake()
        {
            orbitA = transform.Find("Reactor Orbit A");
            orbitB = transform.Find("Reactor Orbit B");
            accentLights = GetComponentsInChildren<Light>(true);
            baseIntensity = new float[accentLights.Length];
            for (var i = 0; i < accentLights.Length; i++)
                baseIntensity[i] = accentLights[i].intensity;
        }

        private void Update()
        {
            if (orbitA) orbitA.Rotate(0f, 18f * Time.deltaTime, 0f, Space.Self);
            if (orbitB) orbitB.Rotate(0f, -13f * Time.deltaTime, 0f, Space.Self);
            for (var i = 0; i < accentLights.Length; i++)
            {
                if (!accentLights[i]) continue;
                accentLights[i].intensity = baseIntensity[i] *
                    (0.88f + Mathf.Sin(Time.time * 2.1f + i * 1.37f) * 0.12f);
            }
        }
    }

    public sealed class RiftbornOrbit : MonoBehaviour
    {
        private float phase;

        private void Awake() => phase = transform.position.x * 0.37f + transform.position.z * 0.21f;

        private void Update()
        {
            transform.localRotation = Quaternion.Euler(0f, Time.time * 62f, 0f);
            transform.localPosition = new Vector3(0f, 1.08f + Mathf.Sin(Time.time * 3.4f + phase) * 0.08f, 0f);
        }
    }

    /// <summary>
    /// Plays retargeted humanoid clips without requiring an Animator Controller.
    /// The graph is kept on the gameplay root so it survives visual recoil offsets.
    /// </summary>
    public sealed class ChampionAnimationDriver : MonoBehaviour
    {
        // Blendzeiten. Kurz genug, dass die Steuerung direkt bleibt, lang genug,
        // dass kein Schnitt mehr sichtbar ist.
        private const float LocomotionBlendSeconds = 0.16f;
        /// <summary>Strecke je Schritt. Passt zum Laufclip, sonst trippelt oder schlurft es.</summary>
        private const float StrideLength = 1.85f;
        private const float ActionFadeInSeconds = 0.07f;
        private const float ActionFadeOutSeconds = 0.17f;

        private Animator animator;
        private PlayableGraph graph;
        private AnimationPlayableOutput output;

        // Aufbau des Graphen:
        //   output -> layerMixer [0] locomotion [0] idle [1] move     (Basis, ganzer Koerper)
        //                        [1] fullMixer  [0] Slot A [1] Slot B (ganzer Koerper)
        //                        [2] upperMixer [0] Slot A [1] Slot B (nur Oberkoerper, maskiert)
        // Zwei Action-Slots, weil sonst ein Combo-Schlag in den naechsten schneiden
        // wuerde statt hinueberzublenden. Zwei Ebenen, weil ein Schlag im Laufen nur
        // die Arme betreffen darf - sonst bleiben die Beine stehen und die Figur
        // rutscht ueber den Boden. Beide Ebenen spielen dieselbe Bewegung als zwei
        // gleich getaktete Kopien; welche zaehlt, entscheidet das Lauftempo.
        private AnimationLayerMixerPlayable layerMixer;
        private AnimationMixerPlayable fullMixer;
        private AnimationMixerPlayable upperMixer;
        private AnimationMixerPlayable locomotion;
        private AnimationClipPlayable idlePlayable;
        private AnimationClipPlayable movePlayable;
        private readonly AnimationClipPlayable[] fullPlayables = new AnimationClipPlayable[2];
        private readonly AnimationClipPlayable[] upperPlayables = new AnimationClipPlayable[2];
        private readonly float[] actionWeights = new float[2];
        private readonly bool[] actionUpperBody = new bool[2];
        private AvatarMask upperBodyMask;
        private int activeSlot = -1;
        private float actionHoldUntil;
        private bool actionIsAttack;

        private AnimationClip idle;
        private AnimationClip move;
        private AnimationClip attack;
        private AnimationClip roll;
        private AnimationClip ultimate;
        private AnimationClip hit;
        private AnimationClip swing;
        private AnimationClip smash;
        private AnimationClip spin;
        private AnimationClip shot;
        private AnimationClip cast;
        private AnimationClip channel;
        private AnimationClip leap;
        private AnimationClip summon;
        private AnimationClip stab;
        private AnimationClip draw;
        private AnimationClip release;
        private AnimationClip guard;
        private AnimationClip guardBreak;
        private AnimationClip dodgeBack;
        private AnimationClip dodgeLeft;
        private AnimationClip dodgeRight;
        private AnimationClip spawnGround;
        private AnimationClip awakenFloor;
        private AnimationClip awakenStanding;
        private AnimationClip inactiveFloor;
        private AnimationClip inactiveStanding;
        private AnimationClip taunt;
        private AnimationClip tauntLong;
        private AnimationClip death;
        private Vector3 previousPosition;
        private float moveBlend;
        private float referenceSpeed = 6f;
        private bool ready;
        /// <summary>Zurueckgelegte Strecke seit dem letzten Schritt. Ton haengt am Weg, nicht an der Uhr.</summary>
        private float strideDistance;

        public void Configure(Animator target, float topSpeed = 6f, bool undead = false)
        {
            animator = target;
            referenceSpeed = Mathf.Max(0.5f, topSpeed);
            if (!animator || !animator.avatar || !animator.avatar.isValid) return;

            var clipList = new List<AnimationClip>();
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_General"));
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_MovementBasic"));
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/Animations/UAL2_Standard"));
            // KayKit Character Animations 1.1, dasselbe Rig_Medium wie General und MovementBasic.
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_CombatMelee"));
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_CombatRanged"));
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_MovementAdvanced"));
            clipList.AddRange(Resources.LoadAll<AnimationClip>("Art3D/KayKit/Animations/Rig_Medium_Special"));
            var clips = clipList.ToArray();
            // Die Namen muessen exakt zu den AnimStacks in den FBX passen. Die
            // vorherige Liste suchte nach Melee_Hook, OverhandThrow, Slide_Start,
            // Sword_Dash und Shield_OneShot - keiner dieser Clips existiert im
            // Projekt. Angriff, Dash und Ultimate waren dadurch seit jeher stumm,
            // ohne dass irgendwo eine Meldung aufgetaucht waere.
            idle = undead
                ? FindClip(clips, "Skeletons_Idle", "Idle_A", "Idle_B", "Idle_No_Loop")
                : FindClip(clips, "Idle_A", "Idle_B", "Idle_No_Loop");
            move = FindClip(clips, "Running_A", "Running_B", "Walking_A");
            attack = FindClip(clips, "Throw", "Use_Item", "Interact");
            roll = FindClip(clips, "Dodge_Forward", "Jump_Start", "Jump_Full_Short", "Jump_Full_Long");
            dodgeBack = FindClip(clips, "Dodge_Backward");
            dodgeLeft = FindClip(clips, "Dodge_Left");
            dodgeRight = FindClip(clips, "Dodge_Right");
            spawnGround = FindClip(clips, "Skeletons_Spawn_Ground", "Spawn_Ground");
            awakenFloor = FindClip(clips, "Skeletons_Awaken_Floor", "Skeletons_Awaken_Floor_Long");
            awakenStanding = FindClip(clips, "Skeletons_Awaken_Standing");
            inactiveFloor = FindClip(clips, "Skeletons_Inactive_Floor_Pose");
            inactiveStanding = FindClip(clips, "Skeletons_Inactive_Standing_Pose");
            taunt = FindClip(clips, "Skeletons_Taunt");
            tauntLong = FindClip(clips, "Skeletons_Taunt_Longer", "Skeletons_Taunt");
            death = undead ? FindClip(clips, "Skeletons_Death", "Death_A") : FindClip(clips, "Death_A");
            ultimate = FindClip(clips, "Spawn_Ground", "Spawn_Air", "Throw");
            hit = FindClip(clips, "Hit_A", "Hit_B", "Hit_Knockback");
            // Kampfclips aus KayKit Character Animations. Fehlen sie, bleiben Use_Item und Throw als Ersatz,
            // und StylizedCharacterMotion ergaenzt die Bewegung mit einer Drehung des Oberkoerpers.
            swing = FindClip(clips, "Melee_2H_Attack_Slice", "Melee_1H_Attack_Slice_Horizontal", "Use_Item", "Interact", "Throw");
            smash = FindClip(clips, "Melee_2H_Attack_Chop", "Melee_1H_Attack_Chop", "Throw", "Use_Item");
            spin = FindClip(clips, "Melee_2H_Attack_Spin", "Melee_2H_Attack_Spinning");
            shot = FindClip(clips, "Ranged_1H_Shoot", "Ranged_2H_Shoot");
            cast = FindClip(clips, "Ranged_Magic_Shoot", "Ranged_Magic_Spellcasting");
            channel = FindClip(clips, "Ranged_Magic_Spellcasting", "Ranged_Magic_Shoot");
            leap = FindClip(clips, "Melee_1H_Attack_Jump_Chop", "Melee_2H_Attack_Chop");
            summon = FindClip(clips, "Ranged_Magic_Summon", "Ranged_Magic_Raise");
            // Schildtraeger und Armbruster. Alle vier Clips liegen in CombatMelee bzw. CombatRanged;
            // ohne sie bleiben Stoss, Spannen und Loesen bei den bisherigen Ersatzbewegungen.
            stab = FindClip(clips, "Melee_Block_Attack", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Chop");
            draw = FindClip(clips, "Ranged_Bow_Draw", "Ranged_Bow_Aiming_Idle", "Ranged_1H_Aiming");
            release = FindClip(clips, "Ranged_Bow_Release", "Ranged_1H_Shoot", "Ranged_2H_Shoot");
            guard = FindClip(clips, "Melee_Blocking", "Melee_Block");
            guardBreak = FindClip(clips, "Melee_Block_Hit", "Hit_A");
            WarnAboutMissingClips();
            if (!idle) return;

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            graph = PlayableGraph.Create("Shatterspire Humanoid Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "Pose", animator);

            idlePlayable = CreateLoop(idle);
            movePlayable = CreateLoop(move ? move : idle);
            locomotion = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(idlePlayable, 0, locomotion, 0);
            graph.Connect(movePlayable, 0, locomotion, 1);
            locomotion.SetInputWeight(0, 1f);
            locomotion.SetInputWeight(1, 0f);

            fullMixer = AnimationMixerPlayable.Create(graph, 2);
            upperMixer = AnimationMixerPlayable.Create(graph, 2);
            layerMixer = AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(locomotion, 0, layerMixer, 0);
            graph.Connect(fullMixer, 0, layerMixer, 1);
            graph.Connect(upperMixer, 0, layerMixer, 2);
            layerMixer.SetInputWeight(0, 1f);
            layerMixer.SetInputWeight(1, 0f);
            layerMixer.SetInputWeight(2, 0f);
            upperBodyMask = BuildUpperBodyMask();
            layerMixer.SetLayerMaskFromAvatarMask(2, upperBodyMask);

            output.SetSourcePlayable(layerMixer);
            graph.Play();
            previousPosition = transform.position;
            ready = true;
        }

        public void PulseAttack(float strength) => PlayAction(attack, 0.28f + strength * 0.08f, 1.15f, upperBody: true);

        /// <summary>
        /// Bewegungen, die sich auf den Oberkoerper beschraenken lassen. Nur Wirbel und
        /// Sprungschlag brauchen den ganzen Koerper; alles andere darf im Laufen passieren.
        /// </summary>
        private static bool UsesUpperBodyOnly(AttackMotion kind) =>
            kind != AttackMotion.Spin && kind != AttackMotion.Leap;
        private AnimationClip ClipFor(AttackMotion kind) => kind switch
        {
            AttackMotion.Swing => swing,
            AttackMotion.Smash => smash,
            AttackMotion.Spin => spin ? spin : smash,
            AttackMotion.Shot => shot ? shot : swing,
            AttackMotion.Cast => cast ? cast : smash,
            AttackMotion.Channel => channel ? channel : smash,
            AttackMotion.Leap => leap ? leap : smash,
            AttackMotion.Stab => stab ? stab : swing,
            AttackMotion.Draw => draw ? draw : cast,
            AttackMotion.Release => release ? release : shot,
            _ => summon ? summon : ultimate
        };

        /// <summary>True, wenn fuer die Bewegung ein echter Kampfclip existiert - dann braucht es keine Ersatzdrehung.</summary>
        public bool HasCombatClip(AttackMotion kind)
        {
            var clip = ClipFor(kind);
            return clip && (clip.name.Contains("Melee_") || clip.name.Contains("Ranged_"));
        }

        /// <summary>Dauer einer Bewegung. Echte Clips bekommen mehr Zeit, sonst wirken sie gehetzt.</summary>
        public float DurationFor(AttackMotion kind) => DurationFor(kind, HasCombatClip(kind));

        /// <summary>Dieselbe Tabelle ohne laufenden Graphen, damit Tests sie nachrechnen koennen.</summary>
        public static float DurationFor(AttackMotion kind, bool combat)
        {
            return kind switch
            {
                // Kurz und durchgehend in Bewegung. Vorher waren es 0,42 bis 0,55 s, von denen die
                // Haelfte eine gehaltene Pose war: die Figur holte aus, stand, schlug, stand.
                AttackMotion.Swing => combat ? 0.3f : 0.3f,
                AttackMotion.Smash => combat ? 0.4f : 0.38f,
                AttackMotion.Spin => combat ? 0.45f : 0.36f,
                AttackMotion.Cast => combat ? 0.4f : 0.3f,
                AttackMotion.Channel => combat ? 0.7f : 0.4f,
                AttackMotion.Leap => combat ? 0.6f : 0.45f,
                AttackMotion.Summon => combat ? 0.9f : 0.6f,
                AttackMotion.Stab => combat ? 0.36f : 0.32f,
                AttackMotion.Draw => combat ? 0.6f : 0.4f,
                AttackMotion.Release => combat ? 0.35f : 0.24f,
                _ => combat ? 0.3f : 0.2f
            };
        }

        /// <summary>
        /// Das Abspielfenster je Bewegung, als Anteil des Clips: von wo bis wo, und wo darin die
        /// Waffe trifft.
        ///
        /// Die Zahlen sind gemessen, nicht geschaetzt - SHATTERSPIRE > Kampfclips vermessen faehrt
        /// jeden Clip auf dem Rig ab und meldet, wann die Klinge am schnellsten ist. Beispiel
        /// Melee_2H_Attack_Chop: 1,63 s lang, der Hammer trifft bei 0,53 des Clips, und der eigentliche
        /// Schwung dauert von 0,39 bis 0,53. Alles davor ist Ausholen, alles danach Nachschwingen.
        ///
        /// Die erste Fassung hat die Hand verfolgt statt die Klinge. Das ging beim Hieb knapp auf,
        /// aber beim Wirbel liegt die schnellste Hand bei 0,56 und die schnellste Klinge bei 0,29 -
        /// das Fenster schnitt den eigentlichen Sweep weg. Gemessen wird, was man sieht.
        ///
        /// Vorher lief jeder Kampfclip von 0 bis 0,92 - also einschliesslich des langen Ausholens -
        /// und wurde in die kurze Aktionsdauer gequetscht: der Hieb mit 2,4-fachem, Schmettern und
        /// Wirbel mit 2,8-fachem Tempo, und die beiden letzten wurden zusaetzlich abgeschnitten. Das
        /// war kein Schlag mehr, sondern ein Zucken. Mit dem Fenster um den Schwung herum laeuft
        /// derselbe Clip in derselben Zeit mit 1,0- bis 1,4-fachem Tempo.
        /// </summary>
        public static (float From, float To, float Strike) WindowFor(AttackMotion kind) => kind switch
        {
            AttackMotion.Swing => (0.24f, 0.60f, 0.391f),
            AttackMotion.Smash => (0.34f, 0.68f, 0.528f),
            AttackMotion.Spin => (0.18f, 0.46f, 0.288f),
            AttackMotion.Stab => (0.42f, 0.70f, 0.523f),
            AttackMotion.Leap => (0.40f, 0.78f, 0.564f),
            AttackMotion.Shot => (0f, 0.34f, 0.075f),
            AttackMotion.Cast => (0f, 0.40f, 0.086f),
            AttackMotion.Release => (0f, 0.26f, 0.045f),
            AttackMotion.Draw => (0f, 0.50f, 0.068f),
            _ => (0f, 0.92f, 0.4f)
        };

        /// <summary>
        /// Wann die Waffe trifft, in Sekunden nach dem Start der Bewegung. Der Schaden soll dann
        /// fallen und nicht beim Tastendruck - sonst sieht es aus, als wuerde nur so getan.
        /// </summary>
        public float StrikeSecondsFor(AttackMotion kind)
        {
            var duration = DurationFor(kind);
            if (!HasCombatClip(kind)) return duration * 0.4f;
            var window = WindowFor(kind);
            var span = Mathf.Max(0.01f, window.To - window.From);
            return duration * Mathf.Clamp01((window.Strike - window.From) / span);
        }

        /// <summary>Echte Kampfclips im gemessenen Fenster um den Schwung, Ersatzclips in ihrem Kern.</summary>
        public void PlayMotion(AttackMotion kind, float duration)
        {
            var clip = ClipFor(kind);
            if (!ready || !clip || clip.length <= 0f) return;
            var (from, to) = HasCombatClip(kind) ? (WindowFor(kind).From, WindowFor(kind).To) : kind switch
            {
                AttackMotion.Swing => (0.1f, 0.7f),
                AttackMotion.Shot => (0.2f, 0.55f),
                AttackMotion.Spin => (0.25f, 0.7f),
                _ => (0.12f, 0.72f)
            };
            var span = clip.length * (to - from);
            PlayAction(clip, duration, Mathf.Clamp(span / Mathf.Max(0.05f, duration), 0.5f, 2.8f), clip.length * from,
                       UsesUpperBodyOnly(kind));
            actionIsAttack = true;
        }

        /// <summary>Ausweichschritt passend zur Richtung relativ zur Blickrichtung: vor, zurueck oder seitlich.</summary>
        public void PulseDash(Vector3 localDirection)
        {
            var clip = roll;
            if (Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.z))
                clip = localDirection.x > 0f ? (dodgeRight ? dodgeRight : roll) : (dodgeLeft ? dodgeLeft : roll);
            else if (localDirection.z < 0f && dodgeBack) clip = dodgeBack;
            if (!clip || clip.length <= 0f) return;
            // Der Dash selbst dauert 0,22 s; der Clip bekommt etwas mehr, damit das Aufkommen zu sehen ist.
            PlayAction(clip, 0.36f, Mathf.Clamp(clip.length * 0.85f / 0.36f, 0.8f, 3f));
        }

        private AnimationClip ClipFor(PresenceMotion kind) => kind switch
        {
            PresenceMotion.SpawnGround => spawnGround,
            PresenceMotion.AwakenFloor => awakenFloor ? awakenFloor : awakenStanding,
            PresenceMotion.AwakenStanding => awakenStanding,
            PresenceMotion.InactiveFloor => inactiveFloor,
            PresenceMotion.InactiveStanding => inactiveStanding,
            PresenceMotion.Taunt => taunt,
            PresenceMotion.TauntLong => tauntLong,
            PresenceMotion.Guard => guard,
            PresenceMotion.GuardBreak => guardBreak ? guardBreak : hit,
            _ => death
        };

        /// <summary>
        /// Spielt einen Auftritt - Auftauchen, Aufstehen, Provozieren, Tod - hoechstens maxSeconds lang und gibt
        /// die tatsaechliche Dauer zurueck, damit das Verhalten so lange wartet. 0, wenn der Clip fehlt.
        /// </summary>
        public float PlayPresence(PresenceMotion kind, float preferredSpeed, float maxSeconds)
        {
            var clip = ClipFor(kind);
            if (!ready || !clip || clip.length <= 0f) return 0f;
            var speed = Mathf.Max(Mathf.Max(0.1f, preferredSpeed), clip.length / Mathf.Max(0.1f, maxSeconds));
            var duration = clip.length / speed;
            PlayAction(clip, duration, speed);
            return duration;
        }

        /// <summary>Haelt eine Pose, bis die naechste Aktion sie abloest - etwa ein Skelett, das reglos am Boden liegt.</summary>
        /// <summary>
        /// Haelt eine Pose, bis die naechste Aktion sie abloest - etwa ein Skelett, das reglos am Boden
        /// liegt, oder ein Schildtraeger, der in Deckung geht. Mit <paramref name="upperBody"/> nur fuer
        /// Rumpf und Arme, sodass die Beine weiter ihre Fortbewegung spielen.
        /// </summary>
        public bool HoldPose(PresenceMotion kind, bool upperBody = false)
        {
            var clip = ClipFor(kind);
            if (!ready || !clip) return false;
            // Die Deckung wird gehalten, also aus der Mitte des Clips heraus, wo der Schild oben ist.
            var pose = kind == PresenceMotion.Guard
                ? clip.length * 0.5f
                : Mathf.Max(0f, clip.length - 0.01f);
            PlayAction(clip, float.PositiveInfinity, 0f, pose, upperBody);
            return true;
        }

        public void PulseDash() => PlayAction(roll, 0.34f, 1.45f);
        public void PulseUltimate() => PlayAction(ultimate, 0.72f, 1.05f);
        public void PulseHit()
        {
            // Ein eingesteckter Treffer bricht keinen laufenden Angriff ab. Im Nahkampf wird Brax staendig
            // getroffen - vorher kam sein Schwung dadurch nie zu Ende.
            if (actionIsAttack && activeSlot >= 0 && Time.time < actionHoldUntil) return;
            PlayAction(hit, 0.2f, 1.4f, upperBody: true);
        }

        private void Update()
        {
            if (!ready || !graph.IsValid()) return;
            var delta = Time.deltaTime;

            var displacement = transform.position - previousPosition;
            displacement.y = 0f;
            previousPosition = transform.position;
            var speed = delta > 0f ? displacement.magnitude / delta : 0f;

            // Locomotion ist ein kontinuierlicher Blend, kein Schalter: bei halbem
            // Stick sieht die Figur auch halb so schnell aus.
            var desired = Mathf.Clamp01(speed / referenceSpeed);
            moveBlend = Mathf.MoveTowards(moveBlend, desired, delta / LocomotionBlendSeconds);
            locomotion.SetInputWeight(0, 1f - moveBlend);
            locomotion.SetInputWeight(1, moveBlend);
            // Clip-Tempo mitziehen, sonst rutschen die Fuesse ueber den Boden.
            movePlayable.SetSpeed(Mathf.Lerp(0.75f, 1.35f, moveBlend));

            // Schritte aus der tatsaechlichen Strecke: bei halbem Tempo kommen sie von selbst
            // halb so oft, ohne dass irgendwo ein Takt gepflegt werden muesste.
            if (moveBlend > 0.25f)
            {
                strideDistance += displacement.magnitude;
                if (strideDistance >= StrideLength)
                {
                    strideDistance = 0f;
                    Sfx.Play(Sound.Footstep, transform.position, 0.55f);
                }
            }
            else
            {
                // Fast voll: der erste Schritt nach dem Losgehen soll sofort kommen.
                strideDistance = StrideLength * 0.75f;
            }

            WrapLoop(idlePlayable);
            WrapLoop(movePlayable);

            if (activeSlot >= 0 && Time.time >= actionHoldUntil) activeSlot = -1;

            for (var slot = 0; slot < actionWeights.Length; slot++)
            {
                var rising = slot == activeSlot;
                var seconds = rising ? ActionFadeInSeconds : ActionFadeOutSeconds;
                actionWeights[slot] = Mathf.MoveTowards(actionWeights[slot], rising ? 1f : 0f, delta / seconds);
                if (!rising && actionWeights[slot] <= 0f) ReleaseSlot(slot);
            }

            // Explizit normalisieren: der Mixer rechnet Gewichte nicht selbst auf
            // eins, und beim schnellen Combo-Wechsel koennte die Summe kurz
            // darueber liegen.
            var first = actionWeights[0];
            var second = actionWeights[1];
            var total = first + second;
            if (total > 1f)
            {
                first /= total;
                second /= total;
            }

            // Die maskierte Ebene traegt immer voll - der Oberkoerper zeigt den Schlag
            // unverwaessert. Die Ganzkoerper-Ebene nimmt ab, je schneller die Figur laeuft:
            // im Stand bleibt der volle Ausfallschritt, im Lauf tragen die Beine weiter.
            var fullFirst = first * (actionUpperBody[0] ? 1f - moveBlend : 1f);
            var fullSecond = second * (actionUpperBody[1] ? 1f - moveBlend : 1f);
            var upperFirst = actionUpperBody[0] ? first : 0f;
            var upperSecond = actionUpperBody[1] ? second : 0f;
            Distribute(fullMixer, fullFirst, fullSecond);
            Distribute(upperMixer, upperFirst, upperSecond);
            layerMixer.SetInputWeight(1, Mathf.Clamp01(fullFirst + fullSecond));
            layerMixer.SetInputWeight(2, Mathf.Clamp01(upperFirst + upperSecond));
        }

        private void PlayAction(AnimationClip clip, float holdSeconds, float speed, float startTime = 0f,
                                bool upperBody = false)
        {
            if (!ready || !clip || !graph.IsValid()) return;
            actionIsAttack = false;
            // In den jeweils anderen Slot legen, damit der laufende Schlag
            // ausblenden kann statt abgeschnitten zu werden.
            var slot = activeSlot == 0 ? 1 : 0;
            ReleaseSlot(slot);
            var layered = upperBody && upperBodyMask;
            fullPlayables[slot] = ConnectAction(fullMixer, slot, clip, speed, startTime);
            // Zweite Kopie desselben Clips, gleich getaktet, fuer die maskierte Ebene.
            if (layered) upperPlayables[slot] = ConnectAction(upperMixer, slot, clip, speed, startTime);
            actionUpperBody[slot] = layered;
            activeSlot = slot;
            actionHoldUntil = Time.time + Mathf.Max(0.05f, holdSeconds);
        }

        private AnimationClipPlayable ConnectAction(AnimationMixerPlayable mixer, int slot, AnimationClip clip,
                                                    float speed, float startTime)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(speed);
            if (startTime > 0f) playable.SetTime(startTime);
            graph.Connect(playable, 0, mixer, slot);
            return playable;
        }

        /// <summary>Verteilt zwei Slotgewichte innerhalb eines Mixers; die Gesamtstaerke traegt die Ebene.</summary>
        private static void Distribute(AnimationMixerPlayable mixer, float first, float second)
        {
            var sum = first + second;
            if (sum <= 0.0001f)
            {
                mixer.SetInputWeight(0, 0f);
                mixer.SetInputWeight(1, 0f);
                return;
            }
            mixer.SetInputWeight(0, first / sum);
            mixer.SetInputWeight(1, second / sum);
        }

        private void ReleaseSlot(int slot)
        {
            ReleaseFrom(fullMixer, fullPlayables, slot);
            ReleaseFrom(upperMixer, upperPlayables, slot);
            actionWeights[slot] = 0f;
            actionUpperBody[slot] = false;
        }

        private void ReleaseFrom(AnimationMixerPlayable mixer, AnimationClipPlayable[] store, int slot)
        {
            if (!store[slot].IsValid()) return;
            if (graph.IsValid())
            {
                graph.Disconnect(mixer, slot);
                mixer.SetInputWeight(slot, 0f);
            }
            store[slot].Destroy();
            store[slot] = default;
        }

        /// <summary>
        /// Maske fuer die obere Ebene: Rumpf, Kopf, Arme und Finger folgen dem Angriff,
        /// Huefte und Beine bleiben bei der Fortbewegung.
        /// </summary>
        private static AvatarMask BuildUpperBodyMask()
        {
            var mask = new AvatarMask { name = "Shatterspire Oberkoerper" };
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
            return mask;
        }

        private AnimationClipPlayable CreateLoop(AnimationClip clip)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            playable.SetApplyPlayableIK(false);
            return playable;
        }

        private static void WrapLoop(AnimationClipPlayable playable)
        {
            if (!playable.IsValid()) return;
            var clip = playable.GetAnimationClip();
            if (!clip || clip.length <= 0f) return;
            var time = playable.GetTime();
            // Modulo statt auf null setzen, damit an der Naht kein Frame verloren geht.
            if (time >= clip.length) playable.SetTime(time % clip.length);
        }

        /// <summary>
        /// Ein fehlender Clip macht die betroffene Aktion stumm, ohne dass irgendwo
        /// etwas auffaellt — genau so waren Angriff, Dash und Ultimate lange Zeit
        /// unbemerkt tot. Deshalb wird das Fehlen jetzt einmal pro Figur gemeldet.
        /// </summary>
        private void WarnAboutMissingClips()
        {
            var missing = new List<string>();
            if (!idle) missing.Add("Idle");
            if (!move) missing.Add("Laufen");
            if (!attack) missing.Add("Angriff");
            if (!roll) missing.Add("Dash");
            if (!ultimate) missing.Add("Ultimate");
            if (!hit) missing.Add("Trefferreaktion");
            if (missing.Count == 0) return;
            Debug.LogWarning($"ChampionAnimationDriver auf '{name}': kein Clip für " +
                             string.Join(", ", missing) + ". Diese Aktionen bleiben unanimiert.", this);
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] candidates)
        {
            foreach (var candidate in candidates)
            foreach (var clip in clips)
            {
                var name = clip.name;
                var separator = name.LastIndexOf('|');
                if (separator >= 0) name = name.Substring(separator + 1);
                if (name == candidate) return clip;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (upperBodyMask) Destroy(upperBodyMask);
        }
    }
}
