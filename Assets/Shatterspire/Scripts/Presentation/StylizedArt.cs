using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>
    /// Builds the first SHATTERSPIRE art pass from inexpensive procedural pieces.
    /// The silhouettes, palette and props are original and intentionally optimized
    /// for mobile readability. Authored meshes can replace these rigs later without
    /// changing gameplay code.
    /// </summary>
    public static class StylizedArt
    {
        public static readonly Color Ink = new(0.055f, 0.075f, 0.12f);
        public static readonly Color Slate = new(0.2f, 0.25f, 0.31f);
        public static readonly Color Stone = new(0.38f, 0.45f, 0.4f);
        public static readonly Color StoneLight = new(0.61f, 0.64f, 0.57f);
        public static readonly Color Cyan = new(0.04f, 0.88f, 0.8f);
        public static readonly Color Gold = new(1f, 0.68f, 0.12f);
        public static readonly Color Coral = new(0.98f, 0.25f, 0.2f);
        public static readonly Color Violet = new(0.66f, 0.3f, 0.94f);
        public static readonly Color Skin = new(0.78f, 0.49f, 0.31f);

        public static void ConfigureWorld()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.3f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.18f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.045f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.035f, 0.055f, 0.12f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 46f;
            QualitySettings.shadowDistance = 36f;

            var keyGo = new GameObject("Warm Spire Sun");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.9f, 0.76f);
            key.intensity = 1.08f;
            key.shadows = LightShadows.Soft;
            key.shadowResolution = LightShadowResolution.Medium;
            keyGo.transform.rotation = Quaternion.Euler(51f, -34f, 0f);

            var fillGo = new GameObject("Cool Spire Fill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.45f, 0.7f, 1f);
            fill.intensity = 0.32f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(58f, 142f, 0f);

            // Kantenlicht von schraeg hinten. Das ist der Unterschied zwischen
            // "Figur steht auf dem Boden" und "Figur klebt am Boden": aus der
            // Distanz trennt erst die helle Kante die Silhouette vom Untergrund.
            var rimGo = new GameObject("Rim Light");
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.62f, 0.82f, 1f);
            rim.intensity = 0.85f;
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(14f, 196f, 0f);

            PostFx.Build();
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.orthographic = true;
            // Keep the whole combat formation readable on a 16:9 display. The
            // previous framing made the hero attractive in screenshots, but hid
            // incoming waves and made the arena feel smaller than it really is.
            // Naeher heran. Bei 7,4 lag im Spielbild rund ein Viertel der Flaeche
            // ausserhalb der Arena und damit schwarz brach.
            camera.orthographicSize = 6.35f;
            camera.fieldOfView = 36f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 110f;
            camera.backgroundColor = new Color(0.022f, 0.035f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = true;
            PostFx.EnableOn(camera);
        }

        public static void BuildArena()
        {
            if (AuthoredArt.TryBuildArena()) return;

            var root = new GameObject("THE VERDANT FORGE").transform;
            Part(root, PrimitiveType.Cube, "Floating Foundation", new Vector3(0f, -0.48f, 0f),
                new Vector3(34f, 0.85f, 34f), Slate, smoothness: 0.18f, keepCollider: true);

            for (var x = 0; x < 8; x++)
            for (var z = 0; z < 8; z++)
            {
                var tileColor = (x + z) % 3 == 0 ? StoneLight : Stone;
                var tile = Part(root, PrimitiveType.Cube, "Forge Tile", new Vector3(-14f + x * 4f, 0f, -14f + z * 4f),
                    new Vector3(3.86f, 0.18f, 3.86f), tileColor, smoothness: 0.12f);
                Object.Destroy(tile.GetComponent<Collider>());
            }

            var medallion = Part(root, PrimitiveType.Cylinder, "Spire Medallion", new Vector3(0f, 0.13f, 0f),
                new Vector3(6.4f, 0.08f, 6.4f), Ink, smoothness: 0.3f, metallic: 0.22f);
            Object.Destroy(medallion.GetComponent<Collider>());
            var core = Part(root, PrimitiveType.Cylinder, "Medallion Core", new Vector3(0f, 0.19f, 0f),
                new Vector3(3.8f, 0.035f, 3.8f), new Color(0.08f, 0.47f, 0.47f), emissive: true);
            Object.Destroy(core.GetComponent<Collider>());

            for (var i = 0; i < 4; i++)
            {
                var rotation = Quaternion.Euler(0f, i * 90f, 0f);
                var direction = rotation * Vector3.forward;
                var vein = Part(root, PrimitiveType.Cube, "Spire Vein", direction * 8.6f + Vector3.up * 0.14f,
                    new Vector3(0.16f, 0.045f, 10.5f), Cyan, new Vector3(0f, i * 90f, 0f), true);
                Object.Destroy(vein.GetComponent<Collider>());
            }

            CreateBoundary(root, new Vector3(0f, 0.55f, -16.25f), new Vector3(33.5f, 1.15f, 0.85f));
            CreateBoundary(root, new Vector3(0f, 0.55f, 16.25f), new Vector3(33.5f, 1.15f, 0.85f));
            CreateBoundary(root, new Vector3(-16.25f, 0.55f, 0f), new Vector3(0.85f, 1.15f, 33.5f));
            CreateBoundary(root, new Vector3(16.25f, 0.55f, 0f), new Vector3(0.85f, 1.15f, 33.5f));

            CreateCrystalCluster(root, new Vector3(-15.8f, 0.45f, -15.6f), Cyan, 0);
            CreateCrystalCluster(root, new Vector3(15.8f, 0.45f, -15.6f), Gold, 1);
            CreateCrystalCluster(root, new Vector3(-15.8f, 0.45f, 15.6f), Gold, 2);
            CreateCrystalCluster(root, new Vector3(15.8f, 0.45f, 15.6f), Cyan, 3);
        }

        public static Transform BuildRex(Transform root)
        {
            if (AuthoredArt.TryBuildRex(root, out var authoredMuzzle)) return authoredMuzzle;

            var model = new GameObject("Rex · Rift Gunslinger").transform;
            model.SetParent(root, false);

            Part(model, PrimitiveType.Sphere, "Coat", new Vector3(0f, 1.05f, 0f), new Vector3(1.15f, 1.08f, 0.8f),
                new Color(0.04f, 0.47f, 0.66f), smoothness: 0.26f);
            Part(model, PrimitiveType.Cylinder, "Energy Belt", new Vector3(0f, 0.78f, 0f), new Vector3(0.92f, 0.12f, 0.68f), Ink, metallic: 0.3f);
            Part(model, PrimitiveType.Cube, "Belt Core", new Vector3(0f, 0.79f, 0.37f), new Vector3(0.28f, 0.22f, 0.12f), Gold, emissive: true);

            for (var side = -1; side <= 1; side += 2)
            {
                Part(model, PrimitiveType.Capsule, "Leg", new Vector3(side * 0.29f, 0.43f, 0f), new Vector3(0.3f, 0.42f, 0.3f), Ink);
                Part(model, PrimitiveType.Sphere, "Boot", new Vector3(side * 0.3f, 0.17f, 0.14f), new Vector3(0.4f, 0.25f, 0.58f), Slate);
                Part(model, PrimitiveType.Sphere, "Shoulder Guard", new Vector3(side * 0.68f, 1.22f, 0f), new Vector3(0.45f, 0.42f, 0.55f),
                    side < 0 ? Cyan : Gold, emissive: true);
                Part(model, PrimitiveType.Capsule, "Arm", new Vector3(side * 0.7f, 0.93f, 0.13f), new Vector3(0.22f, 0.42f, 0.22f),
                    new Color(0.05f, 0.32f, 0.49f), new Vector3(0f, 0f, side * -16f));
                Part(model, PrimitiveType.Sphere, "Glove", new Vector3(side * 0.77f, 0.68f, 0.31f), new Vector3(0.28f, 0.24f, 0.3f), Skin);
                BuildPistol(model, side);
            }

            Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.72f, 0.08f), new Vector3(0.82f, 0.78f, 0.75f), Skin, smoothness: 0.42f);
            Part(model, PrimitiveType.Sphere, "Hair Mass", new Vector3(0f, 1.94f, -0.08f), new Vector3(0.9f, 0.5f, 0.76f), Ink);
            for (var i = -2; i <= 2; i++)
                Part(model, PrimitiveType.Cube, "Hair Spike", new Vector3(i * 0.16f, 2.15f - Mathf.Abs(i) * 0.04f, -0.02f),
                    new Vector3(0.19f, 0.42f, 0.22f), Ink, new Vector3(0f, 0f, -i * 10f));
            Part(model, PrimitiveType.Cube, "Goggle Strap", new Vector3(0f, 1.78f, 0.38f), new Vector3(0.85f, 0.16f, 0.13f), Slate, smoothness: 0.2f);
            for (var side = -1; side <= 1; side += 2)
                Part(model, PrimitiveType.Sphere, "Amber Goggle", new Vector3(side * 0.2f, 1.79f, 0.47f), new Vector3(0.3f, 0.24f, 0.12f), Gold, emissive: true);
            Part(model, PrimitiveType.Cylinder, "Rift Scarf", new Vector3(0f, 1.38f, 0f), new Vector3(0.72f, 0.11f, 0.62f), Cyan, emissive: true);

            CreateShadow(root, 1.35f, new Color(0.035f, 0.07f, 0.1f));
            CreateSelectionRing(root, 1.45f, Cyan);
            root.gameObject.AddComponent<StylizedCharacterMotion>().Configure(model, 11f);

            var muzzle = new GameObject("Dual Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 0.78f, 1.24f);
            return muzzle;
        }

        public static void BuildEnemy(Transform root, EnemyKind kind)
        {
            if (AuthoredArt.TryBuildEnemy(root, kind)) return;

            var model = new GameObject(kind + " Stylized Rig").transform;
            model.SetParent(root, false);
            switch (kind)
            {
                case EnemyKind.Crawler: BuildCrawler(model); break;
                case EnemyKind.Shooter: BuildShooter(model); break;
                case EnemyKind.Brute: BuildBrute(model, false); break;
                case EnemyKind.Elite: BuildBrute(model, true); break;
                case EnemyKind.IronWarden: BuildWarden(model); break;
            }

            var radius = kind == EnemyKind.IronWarden ? 1.65f : kind is EnemyKind.Brute or EnemyKind.Elite ? 1.05f : 0.7f;
            CreateShadow(root, radius, new Color(0.04f, 0.07f, 0.08f));
            root.gameObject.AddComponent<StylizedCharacterMotion>().Configure(model, kind == EnemyKind.IronWarden ? 5f : 8f);
        }

        public static void AddHealthBar(GameObject enemy, EnemyKind kind)
        {
            var height = kind == EnemyKind.IronWarden ? 4.85f : kind == EnemyKind.Elite ? 3.2f
                : kind == EnemyKind.Brute ? 2.88f : 2.02f;
            var width = kind == EnemyKind.IronWarden ? 3.5f : kind is EnemyKind.Brute or EnemyKind.Elite ? 2.05f : 1.42f;
            var color = kind == EnemyKind.IronWarden ? Gold : kind == EnemyKind.Elite
                ? new Color(1f, 0.18f, 0.64f) : new Color(0.96f, 0.22f, 0.2f);
            enemy.AddComponent<WorldHealthBar>().Configure(width, height, color,
                kind is EnemyKind.Elite or EnemyKind.IronWarden);
        }

        private static void BuildPistol(Transform model, int side)
        {
            Part(model, PrimitiveType.Cube, "Arc Pistol", new Vector3(side * 0.77f, 0.69f, 0.66f),
                new Vector3(0.22f, 0.24f, 0.74f), Ink, new Vector3(0f, side * 3f, 0f), smoothness: 0.32f, metallic: 0.72f);
            Part(model, PrimitiveType.Cylinder, "Pistol Coil", new Vector3(side * 0.77f, 0.7f, 0.94f),
                new Vector3(0.17f, 0.24f, 0.17f), side < 0 ? Cyan : Gold, new Vector3(90f, 0f, 0f), true, 0.5f, 0.15f);
        }

        private static void BuildCrawler(Transform model)
        {
            Part(model, PrimitiveType.Sphere, "Shell", new Vector3(0f, 0.62f, -0.08f), new Vector3(0.95f, 0.62f, 1.2f), Coral);
            Part(model, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.72f, 0.52f), new Vector3(0.78f, 0.62f, 0.68f), new Color(0.73f, 0.12f, 0.2f));
            for (var side = -1; side <= 1; side += 2)
            {
                Part(model, PrimitiveType.Capsule, "Front Claw", new Vector3(side * 0.55f, 0.35f, 0.52f), new Vector3(0.2f, 0.47f, 0.2f), Ink,
                    new Vector3(30f, 0f, side * -58f));
                Part(model, PrimitiveType.Capsule, "Rear Claw", new Vector3(side * 0.58f, 0.32f, -0.35f), new Vector3(0.18f, 0.42f, 0.18f), Ink,
                    new Vector3(-25f, 0f, side * -62f));
                Part(model, PrimitiveType.Sphere, "Eye", new Vector3(side * 0.22f, 0.83f, 0.84f), new Vector3(0.17f, 0.15f, 0.11f), Gold, emissive: true);
            }
            Part(model, PrimitiveType.Cube, "Shell Stripe", new Vector3(0f, 0.94f, -0.12f), new Vector3(0.18f, 0.12f, 0.9f), Gold, emissive: true);
        }

        private static void BuildShooter(Transform model)
        {
            Part(model, PrimitiveType.Sphere, "Caster Coat", new Vector3(0f, 0.72f, 0f), new Vector3(0.9f, 1.1f, 0.78f), Violet);
            Part(model, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.28f, -0.02f), new Vector3(0.82f, 0.78f, 0.7f), new Color(0.25f, 0.12f, 0.48f));
            Part(model, PrimitiveType.Sphere, "Mask", new Vector3(0f, 1.25f, 0.45f), new Vector3(0.58f, 0.48f, 0.22f), Ink);
            for (var side = -1; side <= 1; side += 2)
                Part(model, PrimitiveType.Sphere, "Eye", new Vector3(side * 0.16f, 1.29f, 0.58f), new Vector3(0.11f, 0.1f, 0.08f), Cyan, emissive: true);
            Part(model, PrimitiveType.Cylinder, "Arc Cannon", new Vector3(0.58f, 0.87f, 0.38f), new Vector3(0.27f, 0.62f, 0.27f), Slate,
                new Vector3(90f, 0f, -8f), smoothness: 0.25f, metallic: 0.65f);
            Part(model, PrimitiveType.Sphere, "Cannon Crystal", new Vector3(0.58f, 0.89f, 0.83f), new Vector3(0.31f, 0.31f, 0.31f), Cyan, emissive: true);
        }

        private static void BuildBrute(Transform model, bool elite)
        {
            var armor = elite ? new Color(0.55f, 0.08f, 0.42f) : new Color(0.62f, 0.27f, 0.1f);
            var accent = elite ? new Color(1f, 0.16f, 0.72f) : Gold;
            Part(model, PrimitiveType.Sphere, "Heavy Torso", new Vector3(0f, 1.15f, 0f), new Vector3(1.65f, 1.42f, 1.12f), armor);
            Part(model, PrimitiveType.Sphere, "Helm", new Vector3(0f, 1.9f, 0.13f), new Vector3(0.9f, 0.8f, 0.78f), Ink, metallic: 0.55f);
            Part(model, PrimitiveType.Cube, "Face Plate", new Vector3(0f, 1.84f, 0.57f), new Vector3(0.7f, 0.34f, 0.16f), accent, emissive: true);
            for (var side = -1; side <= 1; side += 2)
            {
                Part(model, PrimitiveType.Sphere, "Boulder Shoulder", new Vector3(side * 0.98f, 1.35f, 0f), new Vector3(0.8f, 0.78f, 0.82f), Slate, metallic: 0.35f);
                Part(model, PrimitiveType.Capsule, "Heavy Arm", new Vector3(side * 1.02f, 0.87f, 0.12f), new Vector3(0.38f, 0.65f, 0.38f), armor);
                Part(model, PrimitiveType.Sphere, "Fist", new Vector3(side * 1.06f, 0.4f, 0.35f), new Vector3(0.62f, 0.52f, 0.68f), accent);
                Part(model, PrimitiveType.Capsule, "Heavy Leg", new Vector3(side * 0.45f, 0.4f, 0f), new Vector3(0.42f, 0.52f, 0.42f), Ink);
            }
            Part(model, PrimitiveType.Sphere, "Reactor", new Vector3(0f, 1.15f, 0.88f), new Vector3(0.45f, 0.45f, 0.22f), accent, emissive: true);
            if (elite)
            {
                Part(model, PrimitiveType.Cube, "Elite Horn L", new Vector3(-0.45f, 2.45f, 0f), new Vector3(0.2f, 0.75f, 0.2f), accent,
                    new Vector3(0f, 0f, -24f), true);
                Part(model, PrimitiveType.Cube, "Elite Horn R", new Vector3(0.45f, 2.45f, 0f), new Vector3(0.2f, 0.75f, 0.2f), accent,
                    new Vector3(0f, 0f, 24f), true);
            }
        }

        private static void BuildWarden(Transform model)
        {
            var iron = new Color(0.16f, 0.2f, 0.27f);
            var bronze = new Color(0.58f, 0.31f, 0.12f);
            Part(model, PrimitiveType.Sphere, "Warden Core Body", new Vector3(0f, 1.75f, 0f), new Vector3(2.35f, 2.35f, 1.65f), iron, metallic: 0.72f);
            Part(model, PrimitiveType.Cube, "Warden Chest Plate", new Vector3(0f, 1.76f, 0.86f), new Vector3(1.65f, 1.15f, 0.26f), bronze, metallic: 0.55f);
            Part(model, PrimitiveType.Sphere, "Warden Reactor", new Vector3(0f, 1.78f, 1.08f), new Vector3(0.72f, 0.72f, 0.28f), Gold, emissive: true);
            Part(model, PrimitiveType.Sphere, "Warden Helm", new Vector3(0f, 2.85f, 0.1f), new Vector3(1.25f, 1.05f, 1.02f), Ink, metallic: 0.78f);
            Part(model, PrimitiveType.Cube, "Warden Visor", new Vector3(0f, 2.78f, 0.83f), new Vector3(1.05f, 0.3f, 0.2f), Coral, emissive: true);
            for (var side = -1; side <= 1; side += 2)
            {
                Part(model, PrimitiveType.Sphere, "Warden Pauldron", new Vector3(side * 1.48f, 2.05f, 0f), new Vector3(1.25f, 1.05f, 1.25f), bronze, metallic: 0.62f);
                Part(model, PrimitiveType.Capsule, "Warden Arm", new Vector3(side * 1.5f, 1.25f, 0.1f), new Vector3(0.58f, 0.9f, 0.58f), iron, metallic: 0.65f);
                Part(model, PrimitiveType.Capsule, "Warden Leg", new Vector3(side * 0.68f, 0.58f, 0f), new Vector3(0.62f, 0.8f, 0.62f), iron, metallic: 0.65f);
            }
            Part(model, PrimitiveType.Cylinder, "Hammer Shaft", new Vector3(1.82f, 1.05f, 0.48f), new Vector3(0.22f, 1.25f, 0.22f), Ink,
                new Vector3(20f, 0f, -18f), metallic: 0.75f);
            Part(model, PrimitiveType.Cube, "Hammer Head", new Vector3(2.1f, 0.22f, 0.72f), new Vector3(1.45f, 0.75f, 0.8f), bronze,
                new Vector3(0f, 0f, -8f), metallic: 0.65f);
            Part(model, PrimitiveType.Cube, "Hammer Rune", new Vector3(2.1f, 0.23f, 1.14f), new Vector3(0.72f, 0.3f, 0.09f), Coral, emissive: true);
        }

        private static void CreateBoundary(Transform root, Vector3 position, Vector3 scale)
        {
            Part(root, PrimitiveType.Cube, "Carved Boundary", position, scale, Ink, smoothness: 0.18f, metallic: 0.12f, keepCollider: true);
            var capScale = new Vector3(scale.x > scale.z ? scale.x : 0.2f, 0.12f, scale.z > scale.x ? scale.z : 0.2f);
            var cap = Part(root, PrimitiveType.Cube, "Boundary Rune", position + Vector3.up * 0.62f, capScale, Gold, emissive: true);
            Object.Destroy(cap.GetComponent<Collider>());
        }

        private static void CreateCrystalCluster(Transform root, Vector3 position, Color color, int seed)
        {
            Part(root, PrimitiveType.Cylinder, "Forge Plinth", position, new Vector3(1.7f, 0.45f, 1.7f), Ink, metallic: 0.35f);
            for (var i = 0; i < 3; i++)
            {
                var crystal = Part(root, PrimitiveType.Cube, "World Crystal", position + new Vector3((i - 1) * 0.48f, 0.8f + i * 0.18f, (i % 2) * 0.28f),
                    new Vector3(0.46f, 1.5f + i * 0.25f, 0.46f), color, new Vector3(14f - i * 9f, seed * 31f + i * 18f, 45f), true, 0.48f);
                Object.Destroy(crystal.GetComponent<Collider>());
            }
        }

        private static void CreateShadow(Transform root, float size, Color color)
        {
            var shadow = Part(root, PrimitiveType.Cylinder, "Ground Shadow", new Vector3(0f, 0.025f, 0f),
                new Vector3(size, 0.025f, size * 0.72f), color, smoothness: 0f);
            Object.Destroy(shadow.GetComponent<Collider>());
        }

        private static void CreateSelectionRing(Transform root, float size, Color color)
        {
            var disc = Part(root, PrimitiveType.Cylinder, "Champion Ring", new Vector3(0f, 0.04f, 0f),
                new Vector3(size, 0.02f, size), color, emissive: true);
            Object.Destroy(disc.GetComponent<Collider>());
            var inner = Part(root, PrimitiveType.Cylinder, "Ring Inset", new Vector3(0f, 0.055f, 0f),
                new Vector3(size * 0.72f, 0.025f, size * 0.72f), Stone, smoothness: 0.05f);
            Object.Destroy(inner.GetComponent<Collider>());
        }

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 localPosition,
            Vector3 localScale, Color color, Vector3 localEuler = default, bool emissive = false,
            float smoothness = 0.3f, float metallic = 0f, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(color, emissive, smoothness, metallic);
            if (!keepCollider) PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go;
        }
    }

    public sealed class StylizedCharacterMotion : MonoBehaviour
    {
        private Transform model;
        private Vector3 modelOrigin;
        private Vector3 modelBaseScale = Vector3.one;
        private Vector3 previousPosition;
        private float stepFrequency;
        private float recoil;
        private Vector2 lean;
        private ChampionAnimationDriver authoredAnimation;
        private bool authored;

        public void Configure(Transform visual, float frequency)
        {
            model = visual;
            modelOrigin = visual.localPosition;
            modelBaseScale = visual.localScale;
            stepFrequency = frequency;
            previousPosition = transform.position;
        }

        public void ConfigureAuthored(Transform visual, Animator animator, float topSpeed = 6f)
        {
            model = visual;
            modelOrigin = visual.localPosition;
            modelBaseScale = visual.localScale;
            previousPosition = transform.position;
            authored = true;
            if (animator)
            {
                authoredAnimation = gameObject.AddComponent<ChampionAnimationDriver>();
                authoredAnimation.Configure(animator, topSpeed);
            }
        }

        public void PulseAttack(float strength = 1f)
        {
            recoil = Mathf.Max(recoil, strength);
            authoredAnimation?.PulseAttack(strength);
        }

        public void PulseDash() => authoredAnimation?.PulseDash();
        public void PulseUltimate() => authoredAnimation?.PulseUltimate();
        public void PulseHit() => authoredAnimation?.PulseHit();

        private void LateUpdate()
        {
            if (!model) return;
            var delta = transform.position - previousPosition;
            var speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            previousPosition = transform.position;
            var movement = Mathf.Clamp01(speed / 3f);
            recoil = Mathf.MoveTowards(recoil, 0f, 7f * Time.deltaTime);
            if (authored)
            {
                // Vorher 0,055 Einheiten Versatz und 3,5 Grad Neigung - das lag
                // unter der Wahrnehmungsschwelle, der Effekt war praktisch
                // unsichtbar. Jetzt mit echtem Squash: beim Schlag kurz tiefer
                // und breiter, Volumen bleibt dabei etwa erhalten.
                var squash = 1f - recoil * 0.11f;
                var stretch = 1f + recoil * 0.07f;

                // Neigung in die Laufrichtung. Kostet nichts und ist der
                // Unterschied zwischen "gleitet" und "laeuft".
                var local = transform.InverseTransformDirection(
                    new Vector3(delta.x, 0f, delta.z) / Mathf.Max(0.0001f, Time.deltaTime));
                var desiredLean = new Vector2(
                    Mathf.Clamp(local.z / 7f, -1f, 1f),
                    Mathf.Clamp(local.x / 7f, -1f, 1f));
                lean = Vector2.Lerp(lean, desiredLean, 1f - Mathf.Exp(-9f * Time.deltaTime));

                model.localPosition = modelOrigin + new Vector3(0f, 0f, -recoil * 0.17f);
                model.localRotation = Quaternion.Euler(
                    recoil * -11f + lean.x * 5.5f, 0f, lean.y * -5.5f);
                model.localScale = new Vector3(
                    modelBaseScale.x * stretch,
                    modelBaseScale.y * squash,
                    modelBaseScale.z * stretch);
                return;
            }
            var bob = Mathf.Abs(Mathf.Sin(Time.time * stepFrequency)) * 0.075f * movement;
            var sway = Mathf.Sin(Time.time * stepFrequency * 0.5f) * 3.5f * movement;
            model.localPosition = modelOrigin + new Vector3(0f, bob, -recoil * 0.08f);
            model.localRotation = Quaternion.Euler(recoil * -7f, 0f, sway);
        }
    }

    public sealed class WorldHealthBar : MonoBehaviour
    {
        private Health health;
        private Transform pivot;
        private RectTransform fill;
        private RectTransform chip;
        private float pixelWidth;
        private float chipNormalized = 1f;
        private Camera worldCamera;
        private bool alwaysVisible;
        private static Sprite roundedSprite;
        public static Sprite RoundedUiSprite => RoundedSprite();

        public void Configure(float barWidth, float height, Color color, bool persistent = false)
        {
            health = GetComponent<Health>();
            alwaysVisible = persistent;
            pixelWidth = Mathf.Clamp(barWidth * 56f, 68f, 188f);
            var canvasObject = new GameObject("Floating Health Bar", typeof(RectTransform), typeof(Canvas));
            pivot = canvasObject.transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * height;
            pivot.localScale = Vector3.one * 0.0095f;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 24;
            worldCamera = Camera.main;
            canvas.worldCamera = worldCamera;
            var canvasRect = (RectTransform)pivot;
            canvasRect.sizeDelta = new Vector2(pixelWidth, 10f);

            var shadow = CreateBarImage(canvasRect, "Health Soft Shadow", new Color(0f, 0f, 0f, 0.28f));
            Stretch(shadow.rectTransform, -2f, -3f, 2f, 3f);
            shadow.raycastTarget = false;

            var back = CreateBarImage(canvasRect, "Health Dark Capsule", new Color(0.025f, 0.045f, 0.065f, 0.96f));
            Stretch(back.rectTransform, 0f, 0f, 0f, 0f);
            back.raycastTarget = false;

            var innerTrack = CreateBarImage(canvasRect, "Health Inner Track", new Color(0.18f, 0.2f, 0.22f, 0.92f));
            SetLeftBar(innerTrack.rectTransform, pixelWidth - 6f, 5f, 3f);
            innerTrack.raycastTarget = false;

            var chipImage = CreateBarImage(canvasRect, "Health Damage Chip", new Color(1f, 0.74f, 0.24f, 0.92f));
            chip = chipImage.rectTransform;
            SetLeftBar(chip, pixelWidth - 6f, 5f, 3f);
            chipImage.raycastTarget = false;

            var fillImage = CreateBarImage(canvasRect, "Health Color Fill", color);
            fill = fillImage.rectTransform;
            SetLeftBar(fill, pixelWidth - 6f, 5f, 3f);
            fillImage.raycastTarget = false;

            var shine = CreateBarImage(fill, "Health Highlight", new Color(1f, 1f, 1f, 0.22f));
            var shineRect = shine.rectTransform;
            shineRect.anchorMin = new Vector2(0.06f, 0.63f);
            shineRect.anchorMax = new Vector2(0.94f, 0.88f);
            shineRect.offsetMin = Vector2.zero;
            shineRect.offsetMax = Vector2.zero;
            shine.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (!health || !pivot || !fill) return;
            if (!worldCamera) worldCamera = Camera.main;
            if (worldCamera) pivot.rotation = worldCamera.transform.rotation;
            var normalized = health.Normalized;
            chipNormalized = normalized >= chipNormalized
                ? normalized
                : Mathf.MoveTowards(chipNormalized, normalized, Time.unscaledDeltaTime * 0.42f);
            if (pivot.gameObject.activeSelf != (alwaysVisible || normalized < 0.995f))
                pivot.gameObject.SetActive(alwaysVisible || normalized < 0.995f);
            if (chip) chip.sizeDelta = new Vector2(Mathf.Max(1f, (pixelWidth - 6f) * chipNormalized), 5f);
            fill.sizeDelta = new Vector2(Mathf.Max(1f, (pixelWidth - 6f) * normalized), 5f);
        }

        private static Image CreateBarImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void SetLeftBar(RectTransform rect, float width, float height, float leftInset)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(leftInset, 0f);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Sprite RoundedSprite()
        {
            if (roundedSprite) return roundedSprite;
            const int width = 64;
            const int height = 16;
            const float radius = 7f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "SS Rounded Health Bar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var nearestX = Mathf.Clamp(x + 0.5f, radius, width - radius);
                var nearestY = Mathf.Clamp(y + 0.5f, radius, height - radius);
                var dx = x + 0.5f - nearestX;
                var dy = y + 0.5f - nearestY;
                var alpha = dx * dx + dy * dy <= radius * radius ? (byte)255 : (byte)0;
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            roundedSprite.name = "SS Rounded Health Bar Sprite";
            return roundedSprite;
        }
    }
}
