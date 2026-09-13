using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Nur mit dem Startparameter -shatterspire-capture [Ordner]: startet einen Aufstieg mit Brax, spielt seine
    /// Hammer-Kombo automatisch vor, nimmt eine Bildfolge auf und beendet das Spiel. So laesst sich eine Animation
    /// pruefen, ohne dass jemand spielen und Screenshots schicken muss. Ohne den Parameter wird die Klasse nie erzeugt.
    /// </summary>
    public sealed class CaptureDemo : MonoBehaviour
    {
        private const string Flag = "-shatterspire-capture";
        private Transform player;
        private PlayerInputRouter input;
        private Camera view;
        private string folder;
        private bool framing;
        private float frameSize = 2.1f;
        private Transform frameOn;
        private bool overview;
        private Vector3? overviewPoint;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        /// <summary>Nur die Lobby ablichten, statt in einen Aufstieg zu springen (-shatterspire-menu).</summary>
        public static bool MenuOnly
            => Array.IndexOf(Environment.GetCommandLineArgs(), "-shatterspire-menu") >= 0;

        /// <summary>Wohin die Bilder gehen. Auch der Menue-Modus braucht das, ohne eine Instanz zu haben.</summary>
        public static string Folder
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, Flag);
                return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-")
                    ? args[index + 1]
                    : Path.Combine(Application.persistentDataPath, "captures");
            }
        }

        /// <summary>
        /// Welche Route die Vorfuehrung am Aufzug nimmt, aus -shatterspire-route. Ohne Angabe die
        /// erste. Anders kaeme nie ein Bild aus Schatzkammer oder Raetselraum zustande.
        /// </summary>
        public static int RouteChoice
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-shatterspire-route");
                return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var route)
                    ? Mathf.Max(0, route)
                    : 0;
            }
        }

        /// <summary>
        /// Salze der beiden Vorfuehrgegner. Sie stehen ausserhalb dessen, was der Spawner vergibt
        /// (siehe <see cref="EnemySpawner.EnemySalt"/>), und unterscheiden sich voneinander, damit
        /// nicht beide dieselbe Eigenschaft ziehen.
        /// </summary>
        private const int DemoSaltAhead = 7000001;
        private const int DemoSaltSideways = 7000002;

        /// <summary>
        /// Fester Lauf-Seed aus -shatterspire-seed. Ohne ihn waere jede Bildfolge eine andere Etage
        /// mit anderen Anomalien, und zwei Aufnahmen liessen sich nicht vergleichen.
        /// Gibt 0 zurueck, wenn keiner angegeben ist.
        /// </summary>
        public static int FixedSeed
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-shatterspire-seed");
                return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var seed)
                    ? seed
                    : 0;
            }
        }

        /// <summary>
        /// Welcher Held vorgefuehrt wird. Mit -shatterspire-hero Bomber laeuft die Vorfuehrung als
        /// KORR, sonst als Brax. So lassen sich beide Handschriften pruefen, ohne zwei Builds.
        /// </summary>
        public static HeroClassId Hero
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-shatterspire-hero");
                if (index < 0 || index + 1 >= args.Length) return HeroClassId.Guardian;
                return Enum.TryParse<HeroClassId>(args[index + 1], true, out var parsed)
                    ? parsed
                    : HeroClassId.Guardian;
            }
        }

        public void Configure(GameObject hero, CameraController runCamera)
        {
            player = hero.transform;
            input = hero.GetComponent<PlayerInputRouter>();
            view = runCamera ? runCamera.GetComponent<Camera>() : Camera.main;
            folder = Folder;
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            // Bots ausblenden, eigene Kamera flach und nah: Arme, Hammer und Drehung sollen erkennbar sein.
            foreach (var bot in FindObjectsByType<CompanionBot>(FindObjectsSortMode.None)) bot.gameObject.SetActive(false);
            if (view && view.TryGetComponent<CameraController>(out var follow)) follow.enabled = false;
            framing = true;
            input.ScriptedAim = new Vector3(0.75f, 0f, -1f);
            yield return new WaitForSecondsRealtime(0.8f);

            // Uebersicht von oben ueber einen Kampfraum - der Startraum hat bewusst keine Deckung.
            var director = FindFirstObjectByType<RunDirector>();
            var layout = director && director.Navigation != null ? director.Navigation.Layout : null;
            frameSize = 13f;
            overview = true;
            foreach (var room in layout != null ? layout.Rooms : new System.Collections.Generic.List<LayoutRoom>())
            {
                if (room.Cover.Count == 0) continue;
                overviewPoint = room.Bounds.Center;
                break;
            }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("map");
            overview = false;
            overviewPoint = null;
            frameSize = 2.1f;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("idle");

            input.ScriptedAttack = true;
            var start = Time.unscaledTime;
            for (var i = 0; i < 16; i++)
            {
                while (Time.unscaledTime - start < i * 0.1f) yield return null;
                yield return Shot($"f{i:00}");
            }
            input.ScriptedAttack = false;
            yield return new WaitForSecondsRealtime(0.6f);

            // Zweiter Durchgang: schlagen waehrend des Laufens. Genau hier zeigt sich, ob die
            // Beine weiterlaufen oder ob die Figur im Schlag einfriert und ueber den Boden rutscht.
            input.ScriptedMove = new Vector2(0.6f, -0.8f); // gleiche Richtung wie das Ziel, damit der Lauf natuerlich liest
            yield return new WaitForSecondsRealtime(0.5f);
            input.ScriptedAttack = true;
            start = Time.unscaledTime;
            for (var i = 0; i < 12; i++)
            {
                while (Time.unscaledTime - start < i * 0.1f) yield return null;
                yield return Shot($"m{i:00}");
            }
            input.ScriptedAttack = false;
            input.ScriptedMove = null;
            yield return new WaitForSecondsRealtime(0.6f);

            if (Hero == HeroClassId.Bomber) yield return ShowBomberHeavy();
            if (Hero == HeroClassId.Paladin) yield return ShowPaladinBrace();
            yield return ShowFloorPace();
            yield return ShowStreakAndOrbs();
            yield return ShowThreatMarker();
            yield return ShowForgePlunge();
            yield return ShowShieldbearer();
            yield return ShowMarksman();
            // Zuletzt, weil die Fahrt die Etage auswechselt und danach nichts mehr zu zeigen ist.
            yield return ShowLiftRide();

            Debug.Log("SHATTERSPIRE Capture fertig: " + folder);
            Application.Quit();
        }

        /// <summary>
        /// Schildtraeger: haelt die Deckung, waehrend er laeuft, faengt Treffer von vorn ab und bricht
        /// erst auf, wenn ein schwerer Schlag landet. Genau das soll auf der Bildfolge zu sehen sein.
        /// </summary>
        private IEnumerator ShowShieldbearer()
        {
            var enemy = SpawnDemoEnemy(EnemyKind.Shieldbearer, 7f);
            if (!enemy) yield break;
            frameOn = enemy.transform;
            frameSize = 3.2f;
            input.ScriptedAim = enemy.transform.position - player.position;
            yield return new WaitForSecondsRealtime(1.3f);

            // Erst der Anmarsch: Schild oben, Beine laufen. Dann der Schlagabtausch.
            var start = Time.unscaledTime;
            for (var i = 0; i < 4; i++)
            {
                while (Time.unscaledTime - start < i * 0.22f) yield return null;
                yield return Shot($"g{i:00}");
            }
            input.ScriptedAttack = true;
            start = Time.unscaledTime;
            for (var i = 4; i < 10; i++)
            {
                while (Time.unscaledTime - start < (i - 4) * 0.16f) yield return null;
                yield return Shot($"g{i:00}");
            }
            input.ScriptedAttack = false;
            if (enemy) Destroy(enemy.gameObject);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>Armbruster: sichtbares Spannen, mitwandernde Ziellinie, dann der Schuss.</summary>
        private IEnumerator ShowMarksman()
        {
            // Weit genug weg, dass er nicht sofort zurueckweicht, und lange genug, dass ein voller
            // Zyklus aus Spannen und Loesen auf die Bildfolge passt.
            var enemy = SpawnDemoEnemy(EnemyKind.Marksman, 8.5f);
            if (!enemy) yield break;
            frameOn = enemy.transform;
            frameSize = 3f;
            input.ScriptedAim = enemy.transform.position - player.position;
            var start = Time.unscaledTime;
            for (var i = 0; i < 10; i++)
            {
                while (Time.unscaledTime - start < i * 0.34f) yield return null;
                yield return Shot($"a{i:00}");
            }
            if (enemy) Destroy(enemy.gameObject);
            frameOn = null;
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>
        /// Serie und Lebenskugeln: mehrere Gegner dicht beieinander fallen kurz hintereinander. Die
        /// Serienanzeige muss hochzaehlen, und Kugeln muessen liegen bleiben.
        /// </summary>
        private IEnumerator ShowStreakAndOrbs()
        {
            for (var i = 0; i < 6; i++)
            {
                var victim = SpawnDemoEnemy(EnemyKind.Crawler, 2.6f + i * 0.5f);
                if (victim && victim.TryGetComponent<Health>(out var health))
                    health.TakeDamage(new DamageInfo(9999f, DamageType.Physical, player.gameObject,
                        victim.transform.position, Vector3.zero));
            }
            frameOn = null;
            overviewPoint = null;
            frameSize = 4.5f;
            var start = Time.unscaledTime;
            for (var i = 0; i < 6; i++)
            {
                while (Time.unscaledTime - start < i * 0.25f) yield return null;
                yield return Shot($"s{i:00}");
            }
        }

        /// <summary>
        /// Gefahr von ausserhalb des Bildes: ein Armbruster weit weg spannt, die Kamera bleibt beim
        /// Helden. Am Bildrand muss eine Marke stehen - sonst kommt der Bolzen aus dem Nichts.
        /// </summary>
        private IEnumerator ShowThreatMarker()
        {
            // Zur Seite, nicht nach vorn. Bei 28 Grad Kameraneigung deckt die Ansicht laengs rund
            // zehn Meter Boden ab - ein Gegner in neun Meter Entfernung nach vorn ist noch im Bild.
            // Quer ist die Ansicht nur gut vier Meter breit, dort steht er wirklich draussen.
            var far = SpawnDemoEnemySideways(EnemyKind.Marksman, 9f);
            if (!far) yield break;
            frameOn = null;
            overviewPoint = null;
            frameSize = 2.4f;
            input.ScriptedAim = far.transform.position - player.position;
            var start = Time.unscaledTime;
            for (var i = 0; i < 10; i++)
            {
                while (Time.unscaledTime - start < i * 0.3f) yield return null;
                yield return Shot($"t{i:00}");
            }
            if (far) Destroy(far.gameObject);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        /// <summary>Die Aufzugsfahrt: Abheben, Blende, Ankunft auf der naechsten Etage.</summary>
        private IEnumerator ShowLiftRide()
        {
            var director = FindFirstObjectByType<RunDirector>();
            if (!director) yield break;
            frameOn = null;
            overviewPoint = null;
            frameSize = 6f;
            yield return new WaitForSecondsRealtime(0.4f);
            director.RideLiftForCapture();
            var start = Time.unscaledTime;
            for (var i = 0; i < 10; i++)
            {
                while (Time.unscaledTime - start < i * 0.22f) yield return null;
                yield return Shot($"l{i:00}");
            }
            yield return ShowRouteChoice();
        }

        /// <summary>
        /// Die Kette zwischen zwei Etagen: Verbesserung, Haendler, Wahl der Route - und dann die
        /// neue Etage mit ihrer Anomalie im HUD.
        ///
        /// Ohne diese Folge gibt es von der Wahl am Aufzug kein Bild: alle drei Modals halten die
        /// Zeit an und brauchen eine Eingabe, die in der Stapelverarbeitung niemand gibt. Genau
        /// dort steht aber der Text, der zweimal englisch geblieben ist.
        /// </summary>
        private IEnumerator ShowRouteChoice()
        {
            var hud = FindFirstObjectByType<PrototypeHUD>();
            if (!hud) yield break;

            // Auf das erste Modal warten. Die Blende braucht ihre halbe Sekunde.
            var waited = 0f;
            while (hud.OpenModal == ModalKind.None && waited < 6f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var step = 0;
            // Hoechstens acht Schritte: mehrere Stufenaufstiege koennen mehrere Verbesserungen
            // hintereinander zeigen, endlos darf es aber nicht werden.
            for (var guard = 0; guard < 8 && hud.OpenModal != ModalKind.None; guard++)
            {
                var kind = hud.OpenModal;
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot($"r{step++:00}");
                if (kind == ModalKind.Routes)
                {
                    Debug.Log($"SHATTERSPIRE Routenwahl aufgenommen: {hud.OpenModalButtons} Knopf/Knoepfe.");
                    hud.PressModalForCapture(Mathf.Min(RouteChoice, hud.OpenModalButtons - 1));
                    break;
                }
                // Beim Haendler ist der letzte Knopf "weiter", bei der Verbesserung der erste.
                hud.PressModalForCapture(kind == ModalKind.Shop ? hud.OpenModalButtons - 1 : 0);
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // Die neue Etage steht: Blende auf, Anomalie-Schild im Kopf.
            yield return new WaitForSecondsRealtime(0.9f);
            for (var i = 0; i < 3; i++)
            {
                yield return Shot($"r{step++:00}");
                yield return new WaitForSecondsRealtime(0.4f);
            }
            yield return ShowNewRoom();
        }

        /// <summary>
        /// Was die gewaehlte Route aus dem Raum macht: in der Schatzkammer zum naechsten Hort
        /// laufen, im Raetselraum auf den Altar mit dem hohen Einsatz zugehen. Ohne das zeigt die
        /// Bildfolge nur einen Raum mit Einrichtung, nicht das, was darin passiert.
        /// </summary>
        private IEnumerator ShowNewRoom()
        {
            var playerController = player.GetComponent<PlayerController>();
            var caches = FindObjectsByType<TreasureCache>(FindObjectsSortMode.None);
            var altars = FindObjectsByType<MysteryAltar>(FindObjectsSortMode.None);
            Transform goal = Nearest(caches);
            // Ohne Horte und ohne Altaere bleibt der Haendlerstand im Aufzugsraum - auch der ist
            // neu und soll auf ein Bild.
            if (!goal && altars.Length == 0)
            {
                var stall = FindFirstObjectByType<TraderStall>();
                if (stall) goal = stall.transform;
            }
            if (!goal && altars.Length > 0)
            {
                // Der grosse Einsatz steht rechts; ihn zu waehlen zeigt beide Seiten der Wette.
                foreach (var altar in altars)
                    if (altar.Offer.Stake == WagerStake.Large) goal = altar.transform;
                if (!goal) goal = altars[0].transform;
            }
            if (goal && Vector3.Distance(player.position, goal.position) > 12f)
            {
                // Zu weit fuer die Vorfuehrung, die nicht pfadfinden kann: heransetzen.
                var navigation = FindFirstObjectByType<RunDirector>()?.Navigation;
                var approach = goal.position + new Vector3(-5.5f, 0f, -4.5f);
                if (navigation != null) approach = navigation.ClampToWalkable(approach, 0.6f);
                if (playerController) playerController.Teleport(approach);
                else player.position = approach;
                yield return new WaitForSecondsRealtime(0.3f);
            }
            if (!goal && caches.Length > 0)
            {
                // Kein Hort ist geradeaus erreichbar: sie liegen in den Folgeraeumen, und dorthin
                // fuehrt ein Weg durch Tueren, den die Vorfuehrung nicht laufen kann. Fuer das Bild
                // wird der Held deshalb in die Naehe des ersten Hortes gesetzt. Das ist ein Griff
                // der Aufnahme, kein Weg des Spiels - im Spiel laeuft man hin.
                var target = caches[0];
                var navigation = FindFirstObjectByType<RunDirector>()?.Navigation;
                var approach = target.transform.position + new Vector3(-5.5f, 0f, -2.5f);
                if (navigation != null) approach = navigation.ClampToWalkable(approach, 0.6f);
                if (playerController) playerController.Teleport(approach);
                else player.position = approach;
                yield return new WaitForSecondsRealtime(0.3f);
                goal = Nearest(caches) ?? target.transform;
                Debug.Log($"SHATTERSPIRE Aufnahme: Held an den Hort gesetzt, "
                          + $"Abstand {Vector3.Distance(player.position, goal.position):0.0}.");
            }
            if (!goal) yield break;

            frameOn = null;
            overviewPoint = null;
            frameSize = 7f;
            var step = 0;
            var start = Time.unscaledTime;
            // Hinlaufen und dabei alle 0,45 s ein Bild: Annaeherung, Ausloesen, Folge.
            while (Time.unscaledTime - start < 16f && step < 20)
            {
                // Ist der Hort geholt, zum naechsten weiterlaufen: erst dann zeigt die Bildfolge
                // die Uhr, die laeuft, und den Zaehler, der steigt.
                if (!goal || (goal.TryGetComponent<TreasureCache>(out var reached) && reached.Opened))
                    goal = Nearest(caches) ?? goal;
                if (goal)
                {
                    var to = goal.position - player.position;
                    to.y = 0f;
                    input.ScriptedMove = new Vector2(to.x, to.z).normalized;
                }
                else
                {
                    input.ScriptedMove = null;
                }
                if (Time.unscaledTime - start >= step * 0.45f)
                {
                    var gap = goal ? Vector3.Distance(
                        new Vector3(player.position.x, 0f, player.position.z),
                        new Vector3(goal.position.x, 0f, goal.position.z)) : -1f;
                    Debug.Log($"SHATTERSPIRE Sonde n{step:00}: Abstand {gap:0.00}, Held "
                              + $"{player.position.x:0.0}/{player.position.z:0.0}");
                    yield return Shot($"n{step++:00}");
                }
                yield return null;
            }
            input.ScriptedMove = null;
        }

        /// <summary>
        /// Der naechste noch verschlossene Hort, zu dem eine freie Bahn fuehrt.
        ///
        /// Die Luftlinie genuegt nicht: der erste Aufnahmelauf schickte den Helden auf einen Hort
        /// zu, der 2,3 Einheiten entfernt im Nachbarraum lag - er lief in die Wand und blieb sieben
        /// Sekunden davor stehen. Ein Mensch geht um die Wand herum; die Vorfuehrung kann das nicht,
        /// also nimmt sie nur Ziele, die geradeaus erreichbar sind.
        /// </summary>
        private Transform Nearest(TreasureCache[] caches)
        {
            var navigation = FindFirstObjectByType<RunDirector>()?.Navigation;
            Transform best = null;
            var bestDistance = float.MaxValue;
            foreach (var cache in caches)
            {
                if (!cache || cache.Opened) continue;
                var offset = cache.transform.position - player.position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= bestDistance) continue;
                if (navigation != null)
                {
                    var reached = navigation.FurthestWalkableAlong(player.position, cache.transform.position, 0.5f);
                    if ((reached - cache.transform.position).sqrMagnitude > 2.25f) continue;
                }
                bestDistance = offset.sqrMagnitude;
                best = cache.transform;
            }
            return best;
        }

        /// <summary>
        /// KORRs schwerer Angriff von Anfang bis Ende: werfen, bis der Balken voll ist, halten,
        /// loslassen.
        ///
        /// Der Grund fuer diese Bildfolge: der Balken fuellt sich aus leichten Treffern, und KORRs
        /// Wurfladungen haben ihren Treffer nie gemeldet. Sein Balken blieb damit auf null, die
        /// rechte Maustaste tat gar nichts, und kein Test hat es bemerkt - die Kette laeuft ueber
        /// Zuendschnur, Explosion und Trefferzahl und ist nur im laufenden Spiel zu pruefen.
        /// </summary>
        private IEnumerator ShowBomberHeavy()
        {
            var weapon = player.GetComponent<WeaponSystem>();
            if (!weapon) yield break;
            var victim = SpawnDemoEnemy(EnemyKind.Crawler, 6f);
            frameOn = null;
            overviewPoint = null;
            frameSize = 6.5f;
            input.ScriptedAim = victim ? (victim.transform.position - player.position).normalized : Vector3.forward;
            yield return new WaitForSecondsRealtime(0.4f);

            // Werfen, bis der Balken voll ist. Hoechstens zehn Sekunden - bleibt er leer, ist der
            // Fehler wieder da, und das soll im Log stehen statt die Aufnahme haengen zu lassen.
            input.ScriptedAttack = true;
            var waited = 0f;
            while (!weapon.HeavyReady && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                if (victim && !victim.GetComponent<Health>().IsAlive) victim = SpawnDemoEnemy(EnemyKind.Crawler, 6f);
                yield return null;
            }
            input.ScriptedAttack = false;
            Debug.Log($"SHATTERSPIRE KORR schwer: Balken nach {waited:0.0} s bei "
                      + $"{weapon.HeavyMeterNormalized:P0}, bereit {weapon.HeavyReady}.");
            if (!weapon.HeavyReady) yield break;

            yield return Shot("h00");
            input.ScriptedHeavy = true;
            var start = Time.unscaledTime;
            for (var i = 1; i < 6; i++)
            {
                while (Time.unscaledTime - start < i * 0.22f) yield return null;
                yield return Shot($"h{i:00}");
            }
            Debug.Log($"SHATTERSPIRE KORR schwer: laedt bei {weapon.HeavyChargeNormalized:P0}, "
                      + $"perfekt {weapon.HeavyPerfect}.");
            input.ScriptedHeavy = false;
            for (var i = 6; i < 12; i++)
            {
                while (Time.unscaledTime - start < i * 0.22f) yield return null;
                yield return Shot($"h{i:00}");
            }
            Debug.Log($"SHATTERSPIRE KORR schwer: nach dem Loslassen Balken {weapon.HeavyMeterNormalized:P0}, "
                      + $"{TimedBomb.Active.Count} Ladung(en) scharf.");
            if (victim) Destroy(victim.gameObject);
            input.ScriptedAim = new Vector3(0.75f, 0f, -1f);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>
        /// Eine echte Core-Verteidigung, um den Takt zu messen.
        ///
        /// Die Zahl, um die es geht, steht im Log: wie lange waehrend einer laufenden Verteidigung
        /// kein Gegner am Leben war. Das ist der Stillstand, den man als "zaeh" empfindet, und er
        /// laesst sich nur im laufenden Spiel messen - im Grundriss steht er nicht.
        ///
        /// Der Held bleibt dafuer unverwundbar: gemessen wird, wann die Wellen eintreffen, nicht ob
        /// eine automatische Vorfuehrung einen Kampf gewinnt.
        /// </summary>
        private IEnumerator ShowFloorPace()
        {
            var spawner = FindFirstObjectByType<EnemySpawner>();
            var heroHealth = player.GetComponent<Health>();
            if (!spawner || !heroHealth) yield break;

            frameOn = null;
            overviewPoint = null;
            frameSize = 9f;
            var forward = new Vector3(0.75f, 0f, -1f).normalized;
            input.ScriptedAim = forward;
            spawner.SpawnObjectiveEncounter(player.position + forward * 6f, 1, 0, RoomKind.Combat);
            yield return new WaitForSecondsRealtime(0.3f);

            var start = Time.unscaledTime;
            var step = 0;
            input.ScriptedAttack = true;
            // Grosszuegig: die Schleife endet ohnehin, sobald die Verteidigung vorbei ist. Die
            // Obergrenze ist nur die Reissleine. Sie zaehlt Wanduhr-Zeit und ist deshalb ungenau,
            // wenn das Fenster den Fokus verliert - gemessen wird die Leerlaufzeit im Spawner, die
            // aus Bildzeit kommt.
            while (Time.unscaledTime - start < 150f && (spawner.IsSpawning || spawner.EncounterCount > 0))
            {
                heroHealth.SetInvulnerable(2f);
                if (Time.unscaledTime - start >= step * 2.5f && step < 8) yield return Shot($"t{step++:00}");
                yield return null;
            }
            input.ScriptedAttack = false;
            Debug.Log($"SHATTERSPIRE Takt: Verteidigung nach {Time.unscaledTime - start:0.0} s vorbei.");
            input.ScriptedAim = new Vector3(0.75f, 0f, -1f);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>
        /// LYRAs Schildstand: einstecken und zurueckgeben.
        ///
        /// Ihr ganzes Versprechen steht in zwei Zahlen - wie viel der Schild gehalten hat und wie
        /// viel davon zurueckgeht. Beides ist nur zu pruefen, wenn waehrend des Ladens wirklich
        /// jemand auf sie einschlaegt, also stellt diese Folge ihr Gegner vor die Nase.
        /// </summary>
        private IEnumerator ShowPaladinBrace()
        {
            var weapon = player.GetComponent<WeaponSystem>();
            if (!weapon) yield break;
            frameOn = null;
            overviewPoint = null;
            frameSize = 6.5f;

            // Erst den schweren Balken fuellen - er kommt aus leichten Treffern.
            var victim = SpawnDemoEnemy(EnemyKind.Crawler, 3.2f);
            input.ScriptedAim = victim ? (victim.transform.position - player.position).normalized : Vector3.forward;
            yield return new WaitForSecondsRealtime(0.4f);
            input.ScriptedAttack = true;
            var waited = 0f;
            while (!weapon.HeavyReady && waited < 12f)
            {
                waited += Time.unscaledDeltaTime;
                if (victim && !victim.GetComponent<Health>().IsAlive) victim = SpawnDemoEnemy(EnemyKind.Crawler, 3.2f);
                yield return null;
            }
            input.ScriptedAttack = false;
            Debug.Log($"SHATTERSPIRE LYRA: Balken nach {waited:0.0} s, bereit {weapon.HeavyReady}.");
            if (!weapon.HeavyReady) yield break;

            // Zwei Angreifer von vorn, damit der Schild etwas zu halten hat. Sie bekommen Zeit,
            // heranzukommen, bevor der Schild hochgeht: der Balken laedt in gut einer Sekunde voll
            // und loest dann von selbst aus - im ersten Aufnahmelauf stand die Ladung schon wieder,
            // als der erste Gegner ankam, und der Schild hielt folgerichtig null.
            var first = SpawnDemoEnemy(EnemyKind.Brute, 2.2f);
            var second = SpawnDemoEnemy(EnemyKind.Crawler, 2.6f);
            yield return new WaitForSecondsRealtime(1.8f);
            var before = player.GetComponent<Health>().Current;
            input.ScriptedHeavy = true;
            var start = Time.unscaledTime;
            for (var i = 0; i < 7; i++)
            {
                while (Time.unscaledTime - start < i * 0.3f) yield return null;
                yield return Shot($"p{i:00}");
            }
            var held = before - player.GetComponent<Health>().Current;
            Debug.Log($"SHATTERSPIRE LYRA: waehrend des Schildstands {held:0} Leben verloren.");
            input.ScriptedHeavy = false;
            for (var i = 7; i < 13; i++)
            {
                while (Time.unscaledTime - start < i * 0.3f) yield return null;
                yield return Shot($"p{i:00}");
            }

            // Und die Faehigkeit: geweihter Boden.
            input.ScriptedSkill = true;
            yield return new WaitForSecondsRealtime(0.2f);
            input.ScriptedSkill = false;
            for (var i = 13; i < 18; i++)
            {
                while (Time.unscaledTime - start < i * 0.3f) yield return null;
                yield return Shot($"p{i:00}");
            }
            Debug.Log($"SHATTERSPIRE LYRA: {FindObjectsByType<HallowedGround>(FindObjectsSortMode.None).Length} "
                      + "geweihte Flaeche(n) im Raum.");

            if (first) Destroy(first.gameObject);
            if (second) Destroy(second.gameObject);
            if (victim) Destroy(victim.gameObject);
            input.ScriptedAim = new Vector3(0.75f, 0f, -1f);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>
        /// Brax' Ultimate: Absprung, Flug, Aufschlag, Krater. Genau hier greift die Ultimate in den
        /// CharacterController ein - wenn der Held haengen bleibt oder unter dem Boden landet, sieht
        /// man es auf diesen Bildern und nirgends sonst.
        /// </summary>
        private IEnumerator ShowForgePlunge()
        {
            var weapon = player.GetComponent<WeaponSystem>();
            if (!weapon) yield break;
            // In die Raummitte zielen. Zielt man nach aussen, ist gar kein Platz zum Springen -
            // dann prueft die Bildfolge einen Sonderfall statt der Ultimate.
            var director = FindFirstObjectByType<RunDirector>();
            var layout = director && director.Navigation != null ? director.Navigation.Layout : null;
            var inward = player.forward;
            if (layout != null)
            {
                var room = director.Navigation.RoomAt(player.position);
                if (room >= 0)
                {
                    var toCentre = layout.Rooms[room].Bounds.Center - player.position;
                    toCentre.y = 0f;
                    if (toCentre.sqrMagnitude > 0.5f) inward = toCentre.normalized;
                }
            }
            input.ScriptedMove = null;
            var victim = SpawnDemoEnemy(EnemyKind.Crawler, 7f);
            frameOn = null;
            overviewPoint = player.position + inward * 4f;
            frameSize = 9.5f;
            overview = true;
            input.ScriptedAim = inward;
            yield return new WaitForSecondsRealtime(0.6f);

            // Erst ein paar Ladungen legen. Beim Bomber lebt die Ultimate davon, dass das Feld
            // vorbereitet ist - ohne das prueft die Bildfolge nur den Notnagel.
            input.ScriptedAttack = true;
            yield return new WaitForSecondsRealtime(1.1f);
            input.ScriptedAttack = false;

            weapon.FillUltimateForCapture();
            input.ScriptedUltimate = true;
            var start = Time.unscaledTime;
            for (var i = 0; i < 12; i++)
            {
                while (Time.unscaledTime - start < i * 0.13f) yield return null;
                yield return Shot($"u{i:00}");
            }
            overview = false;
            overviewPoint = null;
            if (victim) Destroy(victim.gameObject);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>Wie <see cref="SpawnDemoEnemy"/>, aber seitlich neben dem Helden.</summary>
        private EnemyAgent SpawnDemoEnemySideways(EnemyKind kind, float distance)
        {
            var director = FindFirstObjectByType<RunDirector>();
            var navigation = director ? director.Navigation : null;
            var side = Vector3.Cross(Vector3.up, player.forward).normalized;
            var spot = player.position + side * distance;
            if (navigation != null) spot = navigation.ClampToWalkable(spot, 0.6f);
            // Fester Seed und festes Salz: zwei Aufnahmen sollen denselben Gegner zeigen.
            var enemy = EnemyFactory.Create(kind, spot, player, 1, FixedSeed, DemoSaltSideways);
            enemy.SetBehaviour(navigation, spot, false);
            return enemy;
        }

        private EnemyAgent SpawnDemoEnemy(EnemyKind kind, float distance)
        {
            var director = FindFirstObjectByType<RunDirector>();
            var navigation = director ? director.Navigation : null;
            var spot = player.position + player.forward * distance;
            if (navigation != null) spot = navigation.ClampToWalkable(spot, 0.6f);
            var enemy = EnemyFactory.Create(kind, spot, player, 1, FixedSeed, DemoSaltAhead);
            enemy.SetBehaviour(navigation, spot, false);
            return enemy;
        }

        private void LateUpdate()
        {
            if (!framing || !view || !player) return;
            // Bei den Gegner-Vorfuehrungen steiler von oben: flach schiebt sich staendig eine Wand
            // oder ein Gelaender zwischen Kamera und Motiv.
            var focus = overviewPoint ?? (frameOn ? frameOn.position : player.position);
            view.orthographicSize = frameSize;
            view.transform.rotation = Quaternion.Euler(overview ? 62f : frameOn ? 42f : 28f, 0f, 0f);
            view.transform.position = focus + Vector3.up * 1.35f - view.transform.forward * 16f;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Debug.Log("SHATTERSPIRE Capture: " + name);
        }
    }
}
