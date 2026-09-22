using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Schreibt mit, was im Selbsttest passiert, und haelt an, was auffaellt.
    ///
    /// Jede der Pruefungen hier steht fuer einen Fehler, den bisher nur der Spieler auf dem Telefon
    /// gefunden hat:
    /// - <b>Schritt schneller als Laufen</b>: "XIRO dasht auf Monster zu" - der Schritt ins Ziel lag
    ///   ueber dem Lauftempo, bei BRAX schon vorher.
    /// - <b>Schwerer Angriff ohne Loslassen</b>: "Richturteil wird automatisch eingesetzt".
    /// - <b>Zeitlupe</b>: die Begleiter hielten die ganze Welt an, 13 bis 17 % der Zeit.
    /// - <b>Festhaengen</b>: ein Held, der sein Ziel hat und nicht vom Fleck kommt.
    /// - <b>Fehlermeldungen</b>: jede Ausnahme, die das Spiel wirft, mit den ersten Zeilen des Stapels.
    ///
    /// Am Ende liegen im Ordner <c>summary.txt</c> (zum Lesen), <c>summary.json</c> (zum Vergleichen),
    /// <c>events.log</c> (alle SHATTERSPIRE-Zeilen) und Bilder in regelmaessigem Abstand und von jeder
    /// Auffaelligkeit.
    /// </summary>
    public sealed class PlaytestRecorder : MonoBehaviour
    {
        /// <summary>Schaden von einer Quelle auf einer Etage.</summary>
        [Serializable]
        public sealed class HitRecord
        {
            public string source;
            public int hits;
            public float amount;
            /// <summary>Treffer, die bis 0,6 s nach einem Ausweichen trotzdem ankamen.</summary>
            public int afterDodge;
        }

        [Serializable]
        public sealed class FloorRecord
        {
            public int floor;
            public string kind;
            public string anomaly;
            public float seconds;
            /// <summary>Leben beim Betreten, als Anteil. Wer mit einem Viertel ankommt, faellt anders.</summary>
            public float healthAtStart = 1f;
            public float damageTaken;
            public List<HitRecord> hits = new();
            /// <summary>Schaden, den der eigene Held austeilte (ohne Overkill).</summary>
            public float damageDealt;
            /// <summary>Schaden, den die Begleiter austeilten.</summary>
            public float allyDamageDealt;
            /// <summary>
            /// Sekunden, in denen der Held ein Ziel in Kampfweite hatte. Ausgeteilt je Etagensekunde
            /// hing an den festen Verteidigungszeiten und an dem, was es zu toeten gab - wer ohnehin
            /// fast alles toetete, zeigte dieselbe Zahl, egal wie hart er traf (REX: 83 % des
            /// Gruppenschadens vor und nach einem Fuenftel weniger Grundschaden). Je Kampfsekunde
            /// misst, wie hart er trifft.
            /// </summary>
            public float fightSeconds;
            public int orbsCollected;
            public int orbsExpired;
            public float orbHealing;
            public float lowestHealth = 1f;
            public int kills;
            public int heavy;
            public int perfect;
            public int good;
            public int skills;
            public int ultimates;
            public int companionFalls;
            public int playerDowns;
        }

        [Serializable]
        public sealed class EventRecord
        {
            public float time;
            public int floor;
            public string kind;
            public string detail;
        }

        [Serializable]
        public sealed class Summary
        {
            public string hero;
            public string path;
            public int seed;
            public int floorsWanted;
            public int floorsReached;
            public string endReason;
            public float realSeconds;
            public float fpsMean;
            public float fpsLow5;
            public float worstFrameMs;
            public float slowShare;
            public float stopsPerMinute;
            public int fastSteps;
            public float fastStepMax;
            public float runSpeed;
            public int fastStepsAttacking;
            public int heavyFired;
            public int heavyIssued;
            public int heavyWithoutInput;
            public int stuck;
            public int unstickDashes;
            public int dodges;
            public int errors;
            public int exceptions;
            public List<FloorRecord> floors = new();
            public List<EventRecord> events = new();
            public List<string> errorSamples = new();
        }

        /// <summary>Alle so viele Sekunden ein Bild.</summary>
        private const float ShotEvery = 15f;

        private readonly Summary summary = new();
        private readonly List<float> frames = new();
        private readonly Queue<(float Time, Vector3 Position, float[] Sources, float Run)> recent = new();
        private readonly Queue<(float Time, Vector3 Position)> stuckSamples = new();
        private readonly StringBuilder log = new();
        private readonly HashSet<string> seenErrors = new();

        private GameObject hero;
        private Health playerHealth;
        private WeaponSystem weapon;
        private PlayerController controller;
        private PlayerBuild build;
        private PrototypeHUD hud;
        private AutoPilot pilot;
        private string folder;
        private float limitSeconds;
        private float started;
        private FloorRecord floor;
        private float floorStarted;
        private float slowSeconds;
        private float activeSeconds;
        private int stops;
        private bool wasSlow;
        private float nextShot;
        private float lastExcused = -99f;
        private bool inFastStep;
        private float fastStepPeak;
        private bool fastStepAttacking;
        private string fastStepCause = string.Empty;
        private float nextStuckCheck;
        private float nextStuckReport;
        private int shotCount;
        private int anomalyShots;
        private string runEndReason;
        private bool finished;

        public void Configure(GameObject player, PrototypeHUD hudReference, AutoPilot autoPilot, RunConfig config,
            int seed, string outputFolder, int floorsWanted, float minutes)
        {
            hero = player;
            playerHealth = player.GetComponent<Health>();
            weapon = player.GetComponent<WeaponSystem>();
            controller = player.GetComponent<PlayerController>();
            build = player.GetComponent<PlayerBuild>();
            hud = hudReference;
            pilot = autoPilot;
            folder = outputFolder;
            limitSeconds = minutes * 60f;
            summary.hero = HeroCatalog.Name(config.Hero);
            summary.path = config.Mode.ToString();
            summary.seed = seed;
            summary.floorsWanted = floorsWanted;
            Directory.CreateDirectory(Path.Combine(folder, "shots"));
            started = Time.realtimeSinceStartup;
            nextShot = ShotEvery;

            // Drei Fenster nebeneinander, die gleichzeitig spielen: ohne das waeren alle laut.
            AudioListener.volume = 0f;
            Application.runInBackground = true;

            Application.logMessageReceived += OnLog;
            GameEvents.RoomStarted += OnRoomStarted;
            GameEvents.EntityDied += OnEntityDied;
            GameEvents.RunEnded += OnRunEnded;
            GameEvents.OrbEnded += OnOrbEnded;
            GameEvents.DamageApplied += OnDamageApplied;
            if (playerHealth) playerHealth.Damaged += OnPlayerDamaged;
            if (weapon)
            {
                weapon.HeavyFired += OnHeavyFired;
                weapon.SkillFired += OnSkillFired;
                weapon.UltimateFired += OnUltimateFired;
            }
            Note("START", $"{summary.hero}, {summary.path}, Seed {seed}, {floorsWanted} Etagen, "
                          + $"hoechstens {minutes:0} min");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            GameEvents.RoomStarted -= OnRoomStarted;
            GameEvents.EntityDied -= OnEntityDied;
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.OrbEnded -= OnOrbEnded;
            GameEvents.DamageApplied -= OnDamageApplied;
            if (playerHealth) playerHealth.Damaged -= OnPlayerDamaged;
            if (!weapon) return;
            weapon.HeavyFired -= OnHeavyFired;
            weapon.SkillFired -= OnSkillFired;
            weapon.UltimateFired -= OnUltimateFired;
        }

        private float Real => Time.realtimeSinceStartup - started;

        private void Update()
        {
            if (finished || !hero) return;
            var dt = Time.unscaledDeltaTime;
            // Die ersten drei Sekunden laden noch - sie wuerden die Bildrate verfaelschen.
            if (Real > 3f) frames.Add(dt);

            var scale = Time.timeScale;
            if (scale > 0f)
            {
                activeSeconds += dt;
                var slow = scale < 0.999f;
                if (slow) slowSeconds += dt;
                if (slow && !wasSlow) stops++;
                wasSlow = slow;
                WatchSteps();
                WatchStuck();
                if (floor != null && pilot && pilot.Fighting) floor.fightSeconds += Time.deltaTime;
            }
            if (floor != null && playerHealth) floor.lowestHealth = Mathf.Min(floor.lowestHealth, playerHealth.Normalized);

            if (Real >= nextShot)
            {
                nextShot = Real + ShotEvery;
                StartCoroutine(Shot($"t{Mathf.RoundToInt(Real):0000}"));
            }

            if (hud && hud.OpenModal == ModalKind.RunEnd) Finish(runEndReason ?? "Aufstieg beendet");
            else if (Real >= limitSeconds) Finish($"Zeitgrenze nach {limitSeconds / 60f:0} min");
        }

        // ── Pruefungen ──────────────────────────────────────────────────────

        /// <summary>
        /// Ein Schritt, der schneller ist als Laufen. Gemessen ueber 0,1 s Spielzeit, damit ein
        /// einzelnes Bild nicht zaehlt. Dash und Sprung sind ausgenommen, Versetzungen ueber drei
        /// Einheiten auch - das sind Etagenwechsel.
        /// </summary>
        private void WatchSteps()
        {
            if (!controller) return;
            var now = Time.time;
            var position = hero.transform.position;
            position.y = 0f;
            if (controller.Rolling || controller.IsAirborne || controller.Charging) lastExcused = now;
            var sources = new float[6];
            for (var i = 0; i < sources.Length; i++) sources[i] = controller.Travelled((MoveSource)i);
            var runNow = HeroCatalog.BaseSpeed(build ? build.HeroClass : HeroClassId.Ranger)
                         * (build ? build.MoveSpeedMultiplier : 1f);
            recent.Enqueue((now, position, sources, runNow));
            while (recent.Count > 1 && now - recent.Peek().Time > 0.15f) recent.Dequeue();
            var (oldTime, oldPosition, oldSources, _) = recent.Peek();
            var age = now - oldTime;
            if (age < 0.1f || now - lastExcused < 0.3f) return;
            var distance = Vector3.Distance(position, oldPosition);
            if (distance > 3f)
            {
                recent.Clear();
                return;
            }
            // Das hoechste Tempo im Fenster, nicht das jetzige: laeuft ein Tempo-Schub (Jaegerblick,
            // Adrenalin, Vergeltung) mitten im Fenster ab, war der Schritt davor nicht zu schnell. So
            // meldete der Selbsttest am 22.09. einen Schritt von REX, der nur aus Laufen bestand.
            var run = runNow;
            foreach (var sample in recent) run = Mathf.Max(run, sample.Run);
            summary.runSpeed = runNow;
            var speed = distance / age;
            if (speed > PlaytestMath.FastStepLimit(run))
            {
                if (speed > fastStepPeak)
                {
                    // Woher die Strecke in diesem Fenster kam - das ist die Frage, die der erste Lauf
                    // nicht beantworten konnte.
                    var parts = new StringBuilder();
                    var named = 0f;
                    string[] labels = { "Laufen", "Schritt", "Ansturm", "Rolle", "Randkorrektur", "Verdraengung" };
                    for (var i = 0; i < sources.Length; i++)
                    {
                        var part = sources[i] - oldSources[i];
                        named += part;
                        parts.Append(labels[i]).Append(' ').Append(part.ToString("0.00", CultureInfo.InvariantCulture)).Append(", ");
                    }
                    parts.Append("sonst ").Append(Mathf.Max(0f, distance - named).ToString("0.00", CultureInfo.InvariantCulture));
                    fastStepCause = $"in {age:0.00} s: {parts}";
                }
                fastStepPeak = Mathf.Max(fastStepPeak, speed);
                fastStepAttacking |= weapon && weapon.AimEngaged;
                inFastStep = true;
                return;
            }
            if (!inFastStep) return;
            inFastStep = false;
            summary.fastSteps++;
            summary.fastStepMax = Mathf.Max(summary.fastStepMax, fastStepPeak);
            if (fastStepAttacking) summary.fastStepsAttacking++;
            var detail = $"{fastStepPeak:0.0} je Sekunde statt hoechstens {run:0.0}"
                         + (fastStepAttacking ? ", beim Angreifen" : ", ohne Angriff")
                         + " - " + fastStepCause;
            Anomaly("SCHRITT", detail, "schritt");
            fastStepPeak = 0f;
            fastStepAttacking = false;
        }

        /// <summary>
        /// Festhaengen: der Autopilot hat ein Ziel, kaempft nicht, will laufen, und ist in sechs
        /// Sekunden keinen Meter weit gekommen. Ohne "will laufen" zaehlte jeder Kern, den er im Ring
        /// verteidigte, und jeder Begleiter, den er aufhob - am 22.09. waren das die meisten Meldungen.
        /// </summary>
        private void WatchStuck()
        {
            if (Real < nextStuckCheck || !pilot) return;
            nextStuckCheck = Real + 1f;
            var position = hero.transform.position;
            stuckSamples.Enqueue((Real, position));
            while (stuckSamples.Count > 7) stuckSamples.Dequeue();
            if (!pilot.WantsToMove) stuckSamples.Clear();
            if (stuckSamples.Count < 7 || !pilot.HasGoal || pilot.Fighting) return;
            var moved = CombatBrain.FlatDistance(position, stuckSamples.Peek().Position);
            if (moved >= 1f || Real < nextStuckReport) return;
            nextStuckReport = Real + 20f;
            summary.stuck++;
            Anomaly("FEST", $"bei ({position.x:0.0} / {position.z:0.0}), {moved:0.00} Einheiten in 6 s, "
                            + $"Autopilot: {pilot.Doing}", "fest");
        }

        // ── Ereignisse ──────────────────────────────────────────────────────

        private void OnRoomStarted(int index, RoomKind kind)
        {
            CloseFloor();
            summary.floorsReached = Mathf.Max(summary.floorsReached, index);
            if (index > summary.floorsWanted)
            {
                Finish($"Ziel erreicht: {summary.floorsWanted} Etagen");
                return;
            }
            floor = new FloorRecord
            {
                floor = index, kind = kind.ToString(), anomaly = pendingAnomaly,
                healthAtStart = playerHealth ? playerHealth.Normalized : 1f
            };
            pendingAnomaly = "None";
            floorStarted = Time.time;
            Note("ETAGE", $"{index}, {kind}, Leben {floor.healthAtStart * 100f:0} %");
            // Nach der Blende: das Bild im Moment des Wechsels war bei jedem Lauf schwarz.
            StartCoroutine(Shot($"etage{index:00}_{kind}", 1.6f));
        }

        private void CloseFloor()
        {
            if (floor == null) return;
            floor.seconds = Time.time - floorStarted;
            summary.floors.Add(floor);
            floor = null;
        }

        private void OnEntityDied(Health value)
        {
            if (!value || floor == null) return;
            if (value.Team == TeamId.Enemy)
            {
                floor.kills++;
                return;
            }
            if (value == playerHealth)
            {
                floor.playerDowns++;
                Anomaly("GEFALLEN", "der eigene Held", "gefallen");
                return;
            }
            floor.companionFalls++;
            var member = value.GetComponent<PartyMember>();
            Note("BEGLEITER", (member ? member.DisplayName : value.name) + " gefallen");
        }

        private void OnRunEnded(bool victory, int shards)
            => runEndReason = victory ? $"Aufstieg geschafft, {shards} Splitter" : $"Aufstieg verloren, {shards} Splitter";

        private void OnPlayerDamaged(DamageInfo damage)
        {
            if (floor == null) return;
            floor.damageTaken += damage.Amount;
            var source = PlaytestMath.SourceName(damage.Source);
            var entry = floor.hits.Find(h => h.source == source);
            if (entry == null)
            {
                entry = new HitRecord { source = source };
                floor.hits.Add(entry);
            }
            entry.hits++;
            entry.amount += damage.Amount;
            if (pilot && Time.time - pilot.LastDodgeAt < 0.6f) entry.afterDodge++;
        }

        private string pendingAnomaly = "None";

        private void OnLogAnomaly(string condition)
        {
            // "SHATTERSPIRE Anomalie: Etage 2, Elite, Swarm." - kommt vor oder mit dem Etagenstart.
            const string marker = "SHATTERSPIRE Anomalie: ";
            if (!condition.StartsWith(marker)) return;
            var parts = condition.Substring(marker.Length).TrimEnd('.').Split(',');
            if (parts.Length < 3) return;
            var number = parts[0].Replace("Etage", string.Empty).Trim();
            if (floor != null && number == floor.floor.ToString(CultureInfo.InvariantCulture))
                floor.anomaly = parts[2].Trim();
            else
                pendingAnomaly = parts[2].Trim();
        }

        private void OnDamageApplied(Health target, DamageInfo damage, float dealt)
        {
            if (floor == null || !target || target.Team != TeamId.Enemy) return;
            if (damage.Source == hero) floor.damageDealt += dealt;
            else if (PartyMember.IsOtherHero(damage.Source)) floor.allyDamageDealt += dealt;
        }

        private void OnOrbEnded(float heal, bool collected)
        {
            if (floor == null) return;
            if (!collected)
            {
                floor.orbsExpired++;
                return;
            }
            floor.orbsCollected++;
            floor.orbHealing += heal;
        }

        private void OnHeavyFired(HeavyTiming timing)
        {
            summary.heavyFired++;
            if (floor == null) return;
            floor.heavy++;
            if (timing == HeavyTiming.Perfect) floor.perfect++;
            else if (timing == HeavyTiming.Good) floor.good++;
        }

        private void OnSkillFired()
        {
            if (floor != null) floor.skills++;
        }

        private void OnUltimateFired()
        {
            if (floor != null) floor.ultimates++;
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            if (condition.StartsWith("SHATTERSPIRE") && log.Length < 400000)
                log.Append(Real.ToString("0.0", CultureInfo.InvariantCulture)).Append("  ").AppendLine(condition);
            OnLogAnomaly(condition);
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            if (type == LogType.Exception) summary.exceptions++;
            else summary.errors++;
            if (!seenErrors.Add(condition) || summary.errorSamples.Count >= 12) return;
            var lines = (stack ?? string.Empty).Split('\n');
            var head = string.Join(" | ", lines, 0, Mathf.Min(3, lines.Length)).Trim();
            summary.errorSamples.Add($"[{type}] {condition}  @ {head}");
            Anomaly(type == LogType.Exception ? "AUSNAHME" : "FEHLER", condition, "fehler");
        }

        // ── Protokoll ───────────────────────────────────────────────────────

        private void Note(string kind, string detail)
        {
            summary.events.Add(new EventRecord { time = Real, floor = floor?.floor ?? 0, kind = kind, detail = detail });
            log.Append(Real.ToString("0.0", CultureInfo.InvariantCulture)).Append("  [").Append(kind).Append("] ")
                .AppendLine(detail);
        }

        private void Anomaly(string kind, string detail, string shotName)
        {
            Note(kind, detail);
            // Hoechstens zwanzig Bilder von Auffaelligkeiten - bei einem Fehler je Bild waere der Ordner sonst voll.
            if (anomalyShots >= 20) return;
            anomalyShots++;
            StartCoroutine(Shot($"{shotName}_{Mathf.RoundToInt(Real):0000}"));
        }

        private IEnumerator Shot(string name, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            yield return new WaitForEndOfFrame();
            Texture2D image = null;
            try
            {
                image = ScreenCapture.CaptureScreenshotAsTexture();
                if (image && image.width > 8)
                {
                    File.WriteAllBytes(Path.Combine(folder, "shots", $"{shotCount++:000}_{name}.jpg"),
                        image.EncodeToJPG(80));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("SHATTERSPIRE Selbsttest: Bild nicht moeglich - " + exception.Message);
            }
            finally
            {
                if (image) Destroy(image);
            }
        }

        private void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            CloseFloor();
            summary.endReason = reason;
            summary.realSeconds = Real;
            frames.Sort();
            if (frames.Count > 0)
            {
                var total = 0f;
                foreach (var frame in frames) total += frame;
                summary.fpsMean = frames.Count / Mathf.Max(0.001f, total);
                summary.fpsLow5 = 1f / Mathf.Max(0.0001f, PlaytestMath.Percentile(frames, 0.95f));
                summary.worstFrameMs = frames[frames.Count - 1] * 1000f;
            }
            summary.slowShare = activeSeconds <= 0f ? 0f : slowSeconds / activeSeconds;
            summary.stopsPerMinute = activeSeconds <= 0f ? 0f : stops / activeSeconds * 60f;
            summary.heavyIssued = pilot ? pilot.HeavyReleasesIssued : 0;
            summary.heavyWithoutInput = Mathf.Max(0, summary.heavyFired - summary.heavyIssued);
            summary.unstickDashes = pilot ? pilot.UnstickDashes : 0;
            summary.dodges = pilot ? pilot.Dodges : 0;
            if (summary.heavyWithoutInput > 0)
                Note("HEAVY", $"{summary.heavyWithoutInput} schwere Angriffe ohne Loslassen");
            Note("ENDE", reason);

            File.WriteAllText(Path.Combine(folder, "summary.json"), JsonUtility.ToJson(summary, true));
            File.WriteAllText(Path.Combine(folder, "summary.txt"), PlaytestMath.Describe(summary));
            File.WriteAllText(Path.Combine(folder, "events.log"), log.ToString());
            Debug.Log($"SHATTERSPIRE Selbsttest fertig: {reason}. Protokoll in {folder}");
            StartCoroutine(QuitAfterShot());
        }

        private IEnumerator QuitAfterShot()
        {
            yield return Shot("ende");
            yield return null;
            Application.Quit();
        }
    }

    /// <summary>Die Rechnungen des Selbsttests, ohne Unity nachpruefbar.</summary>
    public static class PlaytestMath
    {
        /// <summary>
        /// Ab welchem Tempo ein Schritt auffaellt. Ein Fuenftel ueber dem Lauftempo: darunter liegen
        /// Bremsweg und Rundung, darueber ein Satz, den das Auge als Sprung liest.
        /// </summary>
        public static float FastStepLimit(float runSpeed) => runSpeed * 1.2f;

        /// <summary>Wer einen Treffer ausgeteilt hat, als kurzer Name fuer den Bericht.</summary>
        public static string SourceName(GameObject source)
        {
            if (!source) return "ohne Quelle";
            var enemy = source.GetComponent<EnemyAgent>();
            if (enemy) return enemy.Kind.ToString();
            var member = source.GetComponent<PartyMember>();
            return member ? "Gruppe" : source.name;
        }

        /// <summary>Wert bei diesem Anteil einer aufsteigend sortierten Liste.</summary>
        public static float Percentile(IReadOnlyList<float> sorted, float fraction)
        {
            if (sorted == null || sorted.Count == 0) return 0f;
            var index = Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[index];
        }

        /// <summary>Der Bericht zum Lesen.</summary>
        public static string Describe(PlaytestRecorder.Summary s)
        {
            var text = new StringBuilder();
            var de = CultureInfo.GetCultureInfo("de-DE");
            text.AppendLine($"SHATTERSPIRE Selbsttest · {s.hero} · {s.path} · Seed {s.seed}");
            text.AppendLine($"{s.realSeconds / 60f:0.0} min gespielt, Etage {s.floorsReached} von {s.floorsWanted} erreicht. "
                            + $"Ende: {s.endReason}.");
            text.AppendLine();
            text.AppendLine(string.Format(de, "Bildrate      Mittel {0:0} fps, langsamste 5 % {1:0} fps, schlimmstes Bild {2:0} ms",
                s.fpsMean, s.fpsLow5, s.worstFrameMs));
            text.AppendLine(string.Format(de, "Zeitlupe      {0:0.0} % der Spielzeit, {1:0} Stopps je Minute",
                s.slowShare * 100f, s.stopsPerMinute));
            text.AppendLine();
            text.AppendLine("Etage  Art        Dauer  Leben zu Beginn  Schaden  tiefstes Leben  Kills  Heavy (perfekt/gut)  Faehigk.  Ult.  Begleiter gefallen  selbst gefallen");
            foreach (var f in s.floors)
                text.AppendLine(string.Format(de,
                    "{0,5}  {1,-9} {2,5:0}s  {3,13:0} %  {4,7:0}  {5,13:0} %  {6,5}  {7,5} ({8}/{9}){10,12}  {11,8}  {12,4}  {13,18}  {14,15}",
                    f.floor, f.kind, f.seconds, f.healthAtStart * 100f, f.damageTaken, f.lowestHealth * 100f, f.kills,
                    f.heavy, f.perfect, f.good, string.Empty, f.skills, f.ultimates, f.companionFalls, f.playerDowns));
            text.AppendLine();
            text.AppendLine("Ausgeteilt (eigener Held: Summe, je Kampfsekunde, Anteil an der Gruppe)");
            foreach (var f in s.floors)
                if (f.seconds > 1f)
                    text.AppendLine(string.Format(de, "{0,5}  {1,6:0}  {2,5:0.0}/s im Kampf ({3:0} s)  Anteil {4:0} %", f.floor,
                        f.damageDealt, f.damageDealt / Mathf.Max(1f, f.fightSeconds), f.fightSeconds,
                        100f * f.damageDealt / Mathf.Max(1f, f.damageDealt + f.allyDamageDealt)));
            text.AppendLine();
            text.AppendLine("Heilkugeln (eingesammelt/verfallen, geheilt)");
            foreach (var f in s.floors)
                if (f.orbsCollected + f.orbsExpired > 0)
                    text.AppendLine(string.Format(de, "{0,5}  {1}/{2}, {3:0}", f.floor, f.orbsCollected, f.orbsExpired, f.orbHealing));
            text.AppendLine();
            text.AppendLine("Schaden nach Quelle (Treffer, Summe, davon kurz nach dem Ausweichen)");
            foreach (var f in s.floors)
            {
                if (f.hits.Count == 0) continue;
                var line = new StringBuilder();
                line.Append(string.Format(de, "{0,5}  {1,-8}", f.floor, string.IsNullOrEmpty(f.anomaly) ? "None" : f.anomaly));
                f.hits.Sort((a, b) => b.amount.CompareTo(a.amount));
                foreach (var h in f.hits)
                    line.Append(string.Format(de, "  {0} {1}x/{2:0}{3}", h.source, h.hits, h.amount,
                        h.afterDodge > 0 ? $" ({h.afterDodge} n.A.)" : string.Empty));
                text.AppendLine(line.ToString());
            }
            text.AppendLine();
            text.AppendLine("Auffaelligkeiten");
            text.AppendLine(string.Format(de, "  Schritt schneller als Laufen   {0}x (hoechstens {1:0.0} je s, Lauftempo {2:0.0}), davon {3} beim Angreifen",
                s.fastSteps, s.fastStepMax, s.runSpeed, s.fastStepsAttacking));
            text.AppendLine($"  Schwerer Angriff ohne Loslassen {s.heavyWithoutInput}x ({s.heavyFired} ausgeloest, {s.heavyIssued} losgelassen)");
            text.AppendLine($"  Festgehangen                    {s.stuck}x, freigedasht {s.unstickDashes}x");
            text.AppendLine($"  Ausgewichen                     {s.dodges}x");
            text.AppendLine($"  Fehler / Ausnahmen              {s.errors} / {s.exceptions}");
            foreach (var sample in s.errorSamples) text.AppendLine("    " + sample);
            return text.ToString();
        }
    }
}
