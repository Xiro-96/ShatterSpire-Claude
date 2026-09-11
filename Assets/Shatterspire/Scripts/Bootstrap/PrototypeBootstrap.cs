using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shatterspire
{
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSceneBootstrap()
        {
            // RuntimeInitializeOnLoadMethod(AfterSceneLoad) only runs for the first
            // loaded scene. Runs deliberately reload the clean prototype scene, so
            // listen for every scene load and rebuild the selected game state there.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __) => Build();

        /// <summary>
        /// Baut Menue oder Aufstieg neu auf. Steht die aktive Szene in den Build Settings, wird sie neu
        /// geladen. Sonst geht das nicht - etwa bei der unbenannten Szene, mit der Unity nach einem
        /// Batchmode-Lauf startet. Dann entsteht eine frische leere Szene und die alte wird entladen.
        /// Vorher lief LoadScene mit leerem Namen ins Leere, und BEGIN CLIMB tat nichts.
        /// </summary>
        public static void Reload()
        {
            Time.timeScale = 1f;
            GameEvents.Reset();
            var active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
                return;
            }
            var fresh = SceneManager.CreateScene("SHATTERSPIRE " + Time.frameCount);
            SceneManager.SetActiveScene(fresh);
            var unload = SceneManager.UnloadSceneAsync(active);
            if (unload == null)
            {
                Debug.LogWarning($"SHATTERSPIRE: Szene '{active.name}' liess sich nicht entladen, Neuaufbau abgebrochen.");
                return;
            }
            unload.completed += _ => Build();
        }

        /// <summary>
        /// Das Spiel baut Kamera und Licht selbst. Bringt die Szene eigene mit - die unbenannte
        /// Standardszene hat "Main Camera" und "Directional Light" -, uebernimmt deren Kamera
        /// Camera.main (Zielen, Labels) und ein zweites Licht verfaelscht das Bild.
        /// </summary>
        private static void DisableForeignSceneObjects()
        {
            var active = SceneManager.GetActiveScene();
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != active) continue;
                Debug.Log($"SHATTERSPIRE: mitgebrachte Kamera '{camera.name}' abgeschaltet, das Spiel baut seine eigene.");
                camera.gameObject.SetActive(false);
            }
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != active) continue;
                Debug.Log($"SHATTERSPIRE: mitgebrachtes Licht '{light.name}' abgeschaltet, das Spiel baut sein eigenes.");
                light.gameObject.SetActive(false);
            }
        }

        private static void Build()
        {
            if (Object.FindAnyObjectByType<RunDirector>() || Object.FindAnyObjectByType<MainMenuUI>()) return;
            DisableForeignSceneObjects();
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            MobileInput.Reset();
            StylizedArt.ConfigureWorld();

            if (!RunLaunchSettings.HasPendingRun)
            {
                BuildFrontEnd();
                return;
            }

            BuildRun(RunLaunchSettings.Consume() ?? new RunConfig());
        }

        private static void BuildFrontEnd()
        {
            // Die Hof-Arena ist nur noch Kulisse fuers Menue. Aufstiege bauen ihre Etagen aus Raeumen.
            StylizedArt.BuildArena();
            AuthoredArt.ApplyFloorTheme(1);
            CreatePreviewHero(HeroClassId.Ranger, new Vector3(-3.5f, 0f, 1.1f), 15f);
            CreatePreviewHero(HeroClassId.Guardian, new Vector3(0f, 0f, 1.8f), 0f);
            CreatePreviewHero(HeroClassId.Arcanist, new Vector3(3.5f, 0f, 1.1f), -15f);
            CreateMenuCamera();
            new GameObject("Front End Systems").AddComponent<MainMenuUI>().Configure();
#if UNITY_EDITOR
            EditorCaptureAgent.Attach(new GameObject("Editor Capture"), "menu", false, 2f);
#endif
        }

        private static void BuildRun(RunConfig config)
        {
            var player = CreatePlayer(config);
            var team = CreateOfflineTeam(player.transform, config.Hero);
            var runCamera = CreateCamera(player.transform);
            var systems = new GameObject("Game Systems");
            var spawner = systems.AddComponent<EnemySpawner>();
            spawner.Configure(player.transform);
            var hud = systems.AddComponent<PrototypeHUD>();
            hud.Configure(player, config);
            var run = systems.AddComponent<RunDirector>();
            run.Configure(player.transform, spawner, hud, config, runCamera, team);
#if UNITY_EDITOR
            EditorCaptureAgent.Attach(systems, "run", true, 3f, 12f);
#endif
        }

        private static GameObject CreatePlayer(RunConfig config)
        {
            var root = new GameObject(HeroCatalog.Name(config.Hero));
            root.transform.position = Vector3.zero;
            var motor = root.AddComponent<CharacterController>();
            motor.center = Vector3.up * 0.9f;
            motor.height = 1.8f;
            motor.radius = 0.45f;
            root.AddComponent<PlayerInputRouter>();
            var build = root.AddComponent<PlayerBuild>();
            var health = root.AddComponent<Health>();
            root.AddComponent<LevelSystem>();
            var controller = root.AddComponent<PlayerController>();
            var weapon = root.AddComponent<WeaponSystem>();

            controller.ConfigureClass(config.Hero);
            weapon.ConfigureClass(config.Hero);
            var built = AuthoredArt.TryBuildHero(root.transform, config.Hero, out var muzzle);
            if (!built) muzzle = StylizedArt.BuildRex(root.transform);
            weapon.SetMuzzle(muzzle);

            var meta = MetaSaveSystem.Load();
            build.ConfigureRun(config.Hero, config, meta);
            health.Configure(TeamId.Player, HeroCatalog.BaseHealth(config.Hero) + meta.vitalityLevel * 5f);
            return root;
        }

        private static void CreatePreviewHero(HeroClassId hero, Vector3 position, float yaw)
        {
            var root = new GameObject(HeroCatalog.Name(hero) + " · Menu Preview");
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!AuthoredArt.TryBuildHero(root.transform, hero, out _)) StylizedArt.BuildRex(root.transform);
        }

        private static void CreateMenuCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            StylizedArt.ConfigureCamera(camera);
            camera.orthographicSize = 7.4f;
            go.transform.position = new Vector3(0f, 8.8f, -10.8f);
            go.transform.rotation = Quaternion.Euler(43f, 0f, 0f);
            go.AddComponent<AudioListener>();
        }

        private static CameraController CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            StylizedArt.ConfigureCamera(camera);
            var controller = go.AddComponent<CameraController>();
            controller.Configure(target);
            go.transform.position = target.position + new Vector3(0f, 11.4f, -10.2f);
            go.transform.rotation = Quaternion.Euler(51.5f, 0f, 0f);
            go.AddComponent<AudioListener>();
            return controller;
        }

        private static CompanionBot[] CreateOfflineTeam(Transform player, HeroClassId selected)
        {
            var first = selected != HeroClassId.Guardian
                ? CreateCompanion(player, new Vector3(-2.25f, 0f, -1.4f), CompanionRole.Guardian,
                    new Color(1f, 0.54f, 0.12f), "BRAX · GUARDIAN")
                : CreateCompanion(player, new Vector3(-2.25f, 0f, -1.4f), CompanionRole.Ranger,
                    new Color(0.05f, 0.9f, 0.92f), "REX · RANGER");
            var second = selected != HeroClassId.Arcanist
                ? CreateCompanion(player, new Vector3(2.25f, 0f, -1.4f), CompanionRole.Support,
                    new Color(0.28f, 1f, 0.58f), "MIRA · SUPPORT")
                : CreateCompanion(player, new Vector3(2.25f, 0f, -1.4f), CompanionRole.Ranger,
                    new Color(0.05f, 0.9f, 0.92f), "REX · RANGER");
            return new[] { first, second };
        }

        private static CompanionBot CreateCompanion(Transform player, Vector3 offset, CompanionRole role, Color accent, string label)
        {
            var companion = new GameObject(label + " Bot");
            companion.transform.position = player.position + offset;
            var bot = companion.AddComponent<CompanionBot>();
            bot.Configure(player, offset, role, accent, label);
            return bot;
        }
    }
}
