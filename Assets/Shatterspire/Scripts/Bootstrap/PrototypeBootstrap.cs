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
            // Beruehrungen nicht zusaetzlich als Mausklick melden. Sonst loeste ein Tipp im Menue und in der
            // Upgrade-Wahl doppelt aus, und das Halten des Sticks galt als gehaltener Angriff.
            if (Application.isMobilePlatform) Input.simulateMouseWithTouches = false;
            MobileInput.Reset();
            StylizedArt.ConfigureWorld();
            // Die Flaeche laeuft ueber Menue und Aufstieg durch. Sie ist leise und soll den Turm
            // nur tragen, nicht auffallen.
            Sfx.StartAmbience();

            // Automatische Bildkontrolle (-shatterspire-capture): ohne Menue direkt in einen Aufstieg mit Brax.
            // Mit -shatterspire-menu bleibt die Lobby stehen und wird nur abgelichtet - Heldenkarte,
            // Stufe und Relikte sind sonst auf keinem Bild zu pruefen.
            if (CaptureDemo.Requested && CaptureDemo.MenuOnly)
            {
                // Die Lobby zeigt den zuletzt gewaehlten Helden. Damit -shatterspire-hero auch hier
                // wirkt, wird die Wahl vor dem Aufbau hinterlegt.
                MetaSaveSystem.SaveLobbySelection(CaptureDemo.Hero, RunMode.Heroic);
                BuildFrontEnd();
                new GameObject("Menu Capture").AddComponent<MenuCapture>();
                return;
            }
            if (CaptureDemo.Requested && !RunLaunchSettings.HasPendingRun)
                RunLaunchSettings.Prepare(new RunConfig { Hero = CaptureDemo.Hero, Mode = RunMode.Brave });

            // Selbsttest (-shatterspire-autoplay): ohne Menue in den Aufstieg, mit frischem Spielstand,
            // der nie geschrieben wird. Siehe Autoplay.
            if (Autoplay.Requested && !RunLaunchSettings.HasPendingRun)
            {
                MetaSaveSystem.Ephemeral = true;
                RunLaunchSettings.Prepare(new RunConfig { Hero = Autoplay.Hero, Mode = Autoplay.RunPath });
            }

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
            // Lobby: der gewaehlte Held in der Mitte, die zwei Bots daneben - die Party, die gleich klettert.
            var stage = new GameObject("Lobby Stage").AddComponent<LobbyStage>();
            new GameObject("Front End Systems").AddComponent<MainMenuUI>().Configure(stage);
#if UNITY_EDITOR
            EditorCaptureAgent.Attach(new GameObject("Editor Capture"), "menu", false, 2f);
#endif
        }

        private static void BuildRun(RunConfig config)
        {
            var meta = MetaSaveSystem.Load();
            var player = CreateHero(config.Hero, config, meta, local: true, Vector3.zero, PartySlot.Flank);
            var team = CreateTeam(player.transform, config, meta);
            var runCamera = CreateCamera(player.transform);
            var systems = new GameObject("Game Systems");
            var spawner = systems.AddComponent<EnemySpawner>();
            spawner.Configure(player.transform);
            var hud = systems.AddComponent<PrototypeHUD>();
            hud.Configure(player, config, team);
            // Der Selbsttest muss zuhoeren, bevor der Aufstieg beginnt: RunDirector.Configure startet
            // Etage 1 sofort, und ein Protokoll, das danach einsteigt, kennt sie nicht.
            if (Autoplay.Requested)
            {
                var pilot = player.GetComponent<AutoPilot>();
                pilot.Configure(config.Hero, hud);
                systems.AddComponent<PlaytestRecorder>().Configure(player, hud, pilot, config,
                    CaptureDemo.FixedSeed, Autoplay.Folder, Autoplay.Floors, Autoplay.Minutes);
            }
            var run = systems.AddComponent<RunDirector>();
            run.Configure(player.transform, spawner, hud, config, runCamera, team);
#if UNITY_EDITOR
            EditorCaptureAgent.Attach(systems, "run", true, 3f, 12f);
#endif
            systems.AddComponent<SoundToggle>();
            if (CaptureDemo.Requested) systems.AddComponent<CaptureDemo>().Configure(player, runCamera);
        }

        /// <summary>
        /// Die zwei Mitglieder, die niemand steuert. Sie entstehen aus derselben Bauanleitung wie der
        /// Held des Spielers - der einzige Unterschied steht in <see cref="CreateHero"/>: woher die
        /// Knopfdruecke kommen.
        /// </summary>
        private static BotInput[] CreateTeam(Transform player, RunConfig config, MetaSaveData meta)
        {
            var roster = PartyMember.OfflineTeamFor(config.Hero);
            var offsets = new[] { new Vector3(-2.25f, 0f, -1.4f), new Vector3(2.25f, 0f, -1.4f) };
            var bots = new BotInput[roster.Length];
            for (var i = 0; i < roster.Length; i++)
            {
                var hero = CreateHero(roster[i].Hero, config, meta, local: false,
                    player.position + offsets[i], roster[i].Slot);
                var bot = hero.GetComponent<BotInput>();
                bot.Configure(player, roster[i].Hero, offsets[i].x);
                bots[i] = bot;
            }
            return bots;
        }

        /// <summary>
        /// Ein Held. Immer derselbe Bauplan, ob ihn ein Mensch steuert oder nicht.
        ///
        /// Frueher gab es hier zwei Bauplaene: einen fuer den Spieler mit Leben, Klasse und Aktionen,
        /// und einen fuer die Begleiter, die nichts davon hatten. Der Unterschied ist jetzt eine
        /// einzige Zeile - welche Eingabe an den Helden kommt. Genau dort tritt spaeter der
        /// Mitspieler aus dem Netz an die Stelle des Bots.
        /// </summary>
        private static GameObject CreateHero(HeroClassId hero, RunConfig config, MetaSaveData meta,
            bool local, Vector3 position, PartySlot slot)
        {
            var root = new GameObject(HeroCatalog.Name(hero) + (local ? string.Empty : " (Team)"));
            root.transform.position = position;
            var motor = root.AddComponent<CharacterController>();
            motor.center = Vector3.up * 0.9f;
            motor.height = 1.8f;
            motor.radius = PartyMember.BodyRadius;
            motor.skinWidth = PartyMember.SkinWidth;
            var build = root.AddComponent<PlayerBuild>();
            var health = root.AddComponent<Health>();
            var member = root.AddComponent<PartyMember>();
            member.Configure(HeroCatalog.Name(hero), HeroCatalog.Accent(hero), hero, slot, local);

            // Die Eingabe muss vor Steuerung und Waffe stehen: beide holen sie sich in ihrem Awake.
            // Im Selbsttest sitzt am eigenen Helden der Autopilot statt des Daumens - derselbe Held,
            // dieselbe Schnittstelle, nur ein anderer, der drueckt.
            if (local && Autoplay.Requested) root.AddComponent<AutoPilot>();
            else if (local) root.AddComponent<PlayerInputRouter>();
            else root.AddComponent<BotInput>();

            if (local)
            {
                // Gold, Erfahrung und Trefferserie gehoeren dem Spieler. Ein Mitglied, das eine eigene
                // Boerse fuehrt, traegt sie am Ende des Aufstiegs mit ins Nichts.
                root.AddComponent<LevelSystem>();
                root.AddComponent<RunWallet>();
                root.AddComponent<KillStreak>();
            }

            var controller = root.AddComponent<PlayerController>();
            var weapon = root.AddComponent<WeaponSystem>();
            controller.ConfigureClass(hero);
            // Linie am Boden und Ring um das Ziel gibt es nur einmal - fuer den Helden an diesem
            // Geraet. Drei Kreise auf dem Boden, und keiner davon ist noch der eigene.
            weapon.SetLocal(local);
            weapon.ConfigureClass(hero);
            var built = AuthoredArt.TryBuildHero(root.transform, hero, out var muzzle);
            if (!built) muzzle = StylizedArt.BuildRex(root.transform);
            weapon.SetMuzzle(muzzle);
            // Erst nach der Figur: das Fallen braucht die Bewegung, die mit ihr entsteht. Der Held
            // des Spielers braucht es nicht - sein Tod beendet den Aufstieg.
            if (!local) root.AddComponent<FallenHero>();

            build.ConfigureRun(hero, local ? config : AllyConfig(config), meta);
            if (!local) build.ScaleAsAlly();
            // Nach der Figur, damit auch ihre Teile auf der Ebene liegen.
            PartyMember.PassThroughEachOther(root);
            // Prestige: der Aufschlag gilt nur fuer diesen Helden und nur auf sein Grundleben,
            // nicht auf die gemeinsamen Meta-Upgrades - sonst multiplizierten sich zwei Systeme.
            var prestige = MetaSaveSystem.PrestigeStep(meta, hero);
            var baseHealth = HeroCatalog.BaseHealth(hero) * (1f + HeroPrestige.HealthBonus(prestige));
            health.Configure(TeamId.Player, baseHealth + (meta?.vitalityLevel ?? 0) * 5f);
            if (local && prestige > 0)
                Debug.Log($"SHATTERSPIRE Prestige: {hero} auf Schritt {prestige}, "
                          + $"+{HeroPrestige.HealthBonus(prestige):P0} Leben, "
                          + $"+{HeroPrestige.DamageBonus(prestige):P0} Schaden.");
            return root;
        }

        /// <summary>
        /// Der Lauf, wie ihn ein Mitglied sieht: dieselbe Etage und derselbe Pfad, aber ohne die
        /// Relikte des Spielers. Relikte sind gefunden und gehoeren dem, der sie gefunden hat -
        /// dreimal Blutpakt waere dreimal weniger Leben fuer einen Bonus, den einer traegt.
        /// </summary>
        private static RunConfig AllyConfig(RunConfig config)
        {
            var copy = config?.Clone() ?? new RunConfig();
            copy.Relics.Clear();
            return copy;
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

    }
}
