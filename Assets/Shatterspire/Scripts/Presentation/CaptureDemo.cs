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
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, Flag);
            folder = index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-")
                ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "captures");
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
            var enemy = EnemyFactory.Create(kind, spot, player, 1);
            enemy.SetBehaviour(navigation, spot, false);
            return enemy;
        }

        private EnemyAgent SpawnDemoEnemy(EnemyKind kind, float distance)
        {
            var director = FindFirstObjectByType<RunDirector>();
            var navigation = director ? director.Navigation : null;
            var spot = player.position + player.forward * distance;
            if (navigation != null) spot = navigation.ClampToWalkable(spot, 0.6f);
            var enemy = EnemyFactory.Create(kind, spot, player, 1);
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
