using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der Selbsttest und was er gefunden hat.
    ///
    /// Der erste Lauf mit XIRO zeigte vier Dinge, die keine Rechnung vorher gesehen hatte: Schritte
    /// beim Angreifen mit bis zu 8,0 Einheiten je Sekunde bei 4,9 Lauftempo, eine Zeile im
    /// Ende-Bildschirm, die ueber den Knoepfen lag, ein unuebersetztes "YOU HAVE", und ein Protokoll,
    /// dem die erste Etage fehlte. Hier stehen die Teile, die sich ohne laufendes Spiel pruefen lassen.
    /// </summary>
    public sealed class PlaytestTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        // ── Die Knoepfe des Autopiloten ─────────────────────────────────────

        [Test]
        public void ThePilotAnswersEveryWindowThatStopsTheGame()
        {
            Assert.AreEqual(0, AutoPilotChoices.ButtonFor(ModalKind.Perk, 3, 1), "Erstes Upgrade.");
            Assert.AreEqual(8, AutoPilotChoices.ButtonFor(ModalKind.Shop, 9, 1),
                "Beim Haendler der letzte Knopf - weiter, ohne zu kaufen.");
            Assert.AreEqual(1, AutoPilotChoices.ButtonFor(ModalKind.Ascension, 2, 5), "Weiterklettern.");
            Assert.AreEqual(0, AutoPilotChoices.ButtonFor(ModalKind.Ascension, 1, 5),
                "Gibt es nur das Aussteigen, dann das.");
        }

        [Test]
        public void ThePilotLeavesTheEndAndThePauseAlone()
        {
            Assert.AreEqual(-1, AutoPilotChoices.ButtonFor(ModalKind.RunEnd, 2, 3),
                "Das Ende bedient das Protokoll - drueckt der Autopilot 'nochmal', beginnt ein neuer Lauf.");
            Assert.AreEqual(-1, AutoPilotChoices.ButtonFor(ModalKind.Pause, 3, 3));
            Assert.AreEqual(-1, AutoPilotChoices.ButtonFor(ModalKind.None, 0, 3));
            Assert.AreEqual(-1, AutoPilotChoices.ButtonFor(ModalKind.Perk, 0, 3), "Ohne Knoepfe nichts druecken.");
        }

        [Test]
        public void RoutesTakeTurnsAndStayOnTheButtons()
        {
            var picked = new HashSet<int>();
            for (var floor = 1; floor <= 9; floor++)
            {
                var index = AutoPilotChoices.ButtonFor(ModalKind.Routes, 3, floor);
                Assert.That(index, Is.InRange(0, 2));
                picked.Add(index);
            }
            Assert.AreEqual(3, picked.Count, "Ueber einen Aufstieg soll mehr als eine Raumart vorkommen.");
        }

        // ── Die Rechnungen des Protokolls ───────────────────────────────────

        [Test]
        public void PercentileReadsASortedList()
        {
            var sorted = Enumerable.Range(1, 100).Select(i => (float)i).ToList();
            Assert.AreEqual(1f, PlaytestMath.Percentile(sorted, 0f));
            Assert.AreEqual(100f, PlaytestMath.Percentile(sorted, 1f));
            Assert.AreEqual(95f, PlaytestMath.Percentile(sorted, 0.95f), 1f);
            Assert.AreEqual(0f, PlaytestMath.Percentile(new List<float>(), 0.5f));
        }

        [Test]
        public void AFastStepIsClearlyAboveRunning()
        {
            Assert.Greater(PlaytestMath.FastStepLimit(4.9f), 4.9f, "Laufen selbst darf nie auffallen.");
            Assert.Less(PlaytestMath.FastStepLimit(4.9f), 4.9f * 1.5f,
                "Ein Satz mit anderthalbfachem Lauftempo muss auffallen.");
        }

        // ── Das Schritt-Budget ──────────────────────────────────────────────

        [Test]
        public void TheStepNeverAddsUpToMoreThanRunning()
        {
            Assert.AreEqual(0f, MeleeApproach.StepBudget(1f, 4.9f, 4.9f, 1f / 60f), 0.0001f,
                "Wer schon voll laeuft, hat keinen Platz fuer einen Schritt.");
            Assert.AreEqual(0.01f, MeleeApproach.StepBudget(0.01f, 4.9f, 0f, 1f / 60f), 0.0001f,
                "Was ins Budget passt, geht ganz durch.");
            Assert.AreEqual((4.9f - 1.47f) / 60f, MeleeApproach.StepBudget(1f, 4.9f, 1.47f, 1f / 60f), 0.0001f,
                "Gebunden laeuft XIRO mit 30 % - der Schritt bekommt den Rest.");
        }

        /// <summary>
        /// Der Schritt ins Ziel, Bild fuer Bild, so wie PlayerController.LungeRoutine ihn geht - mit
        /// dem Laufen, das gleichzeitig weiterlaeuft. Gibt das hoechste Tempo ueber 0,1 s zurueck,
        /// also dieselbe Groesse, die der Selbsttest misst.
        /// </summary>
        private static float PeakSpeed(float distance, float seconds, float run, float walking, bool budget)
        {
            const float dt = 1f / 60f;
            var positions = new List<float> { 0f };
            float moved = 0f, elapsed = 0f, position = 0f;
            for (var frame = 0; frame < 60; frame++)
            {
                position += walking * dt;
                if (elapsed < seconds)
                {
                    elapsed += dt;
                    var wanted = distance * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
                    var step = budget ? MeleeApproach.StepBudget(wanted - moved, run, walking, dt) : wanted - moved;
                    position += step;
                    moved += step;
                }
                positions.Add(position);
            }
            var peak = 0f;
            for (var i = 6; i < positions.Count; i++) peak = Mathf.Max(peak, (positions[i] - positions[i - 6]) / (6 * dt));
            return peak;
        }

        [Test]
        public void TheLungeWithWalkingStaysAtRunningSpeed()
        {
            // XIRO: 4,9 Lauftempo, gebunden laeuft er mit 30 %, der Hieb holt 0,126 s aus.
            const float run = 4.9f, walking = 4.9f * 0.3f, windup = 0.126f;
            var distance = run * windup;
            var peak = PeakSpeed(distance, windup, run, walking, budget: true);
            Assert.LessOrEqual(peak, run + 0.01f,
                $"Schritt und Laufen kommen zusammen auf {peak:0.0} je Sekunde - mehr als Laufen, und damit "
                + "genau das, was als Dash auf den Gegner zu gemeldet wurde.");
        }

        [Test]
        public void WithoutTheBudgetTheSameLungeWasFasterThanRunning()
        {
            // Die Gegenrechnung, damit der Test oben nicht nur deshalb gruen ist, weil der Schritt
            // gar nicht erst schnell wird. So war es bis zum ersten Selbsttest.
            const float run = 4.9f, walking = 4.9f * 0.3f, windup = 0.126f;
            var peak = PeakSpeed(run * windup, windup, run, walking, budget: false);
            Assert.Greater(peak, PlaytestMath.FastStepLimit(run),
                $"Ohne Budget kam der Schritt auf {peak:0.0} - so hat der Selbsttest ihn gemessen.");
        }

        // ── Wer wen verdraengt ──────────────────────────────────────────────

        [Test]
        public void AnEnemyInsideAHeroStepsOutTheHeroStaysPut()
        {
            // Gefunden vom Selbsttest: der Held ruckte um bis zu 1,07 Einheiten in 0,13 s, ohne dass
            // jemand etwas drueckte - ein Gegnerkoerper war in ihn hineingelaufen, und die Kollision
            // schob den Helden heraus.
            var hero = Spawn("XIRO");
            hero.AddComponent<Health>().Configure(TeamId.Player, 160f);
            hero.AddComponent<PartyMember>().Configure("XIRO", Color.white, HeroClassId.Paladin,
                PartySlot.Flank, local: true);
            var heroAt = hero.transform.position;

            var brute = Spawn("Brute").AddComponent<EnemyAgent>();
            typeof(EnemyAgent).GetField("stats", Hidden).SetValue(brute, EnemyBalance.For(EnemyKind.Brute));
            var stateField = typeof(EnemyAgent).GetField("state", Hidden);
            stateField.SetValue(brute, System.Enum.Parse(stateField.FieldType, "Chase"));
            brute.transform.position = heroAt + Vector3.right * 0.3f;

            typeof(EnemyAgent).GetMethod("LateUpdate", Hidden).Invoke(brute, null);

            var gap = CombatBrain.FlatDistance(brute.transform.position, heroAt);
            Assert.GreaterOrEqual(gap, EnemyBalance.For(EnemyKind.Brute).ColliderRadius + PartyMember.BodyRadius - 0.001f,
                $"Der Brute steckt noch {gap:0.00} tief im Helden - beim naechsten Schritt schiebt die "
                + "Kollision den Helden heraus.");
            Assert.AreEqual(heroAt, hero.transform.position, "Der Held selbst darf sich nicht bewegen.");
        }

        // ── Zwei Spielweisen, eine Kampflogik ───────────────────────────────

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void Clear()
        {
            foreach (var go in spawned)
                if (go && go.TryGetComponent<Health>(out var body))
                    typeof(Health).GetMethod("OnDisable", Hidden)?.Invoke(body, null);
            foreach (var go in spawned) if (go) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        /// <summary>XIRO mit vollem schweren Balken und einer Faehigkeit, die noch abklingt.</summary>
        private WeaponSystem Xiro()
        {
            var hero = Spawn("XIRO");
            var build = hero.AddComponent<PlayerBuild>();
            var health = hero.AddComponent<Health>();
            hero.AddComponent<PlayerInputRouter>();
            var weapon = hero.AddComponent<WeaponSystem>();
            foreach (var component in new MonoBehaviour[] { health, build, weapon })
                component.GetType().GetMethod("Awake", Hidden)?.Invoke(component, null);
            health.Configure(TeamId.Player, 160f);
            weapon.SetLocal(false);
            weapon.ConfigureClass(HeroClassId.Paladin);
            typeof(WeaponSystem).GetField("heavyMeter", Hidden).SetValue(weapon, 100f);
            typeof(WeaponSystem).GetField("skillReadyAt", Hidden).SetValue(weapon, Time.time + 60f);
            return weapon;
        }

        private Health LoneEnemy(Vector3 at)
        {
            var enemy = Spawn("Crawler").AddComponent<Health>();
            enemy.Configure(TeamId.Enemy, 500f);
            typeof(Health).GetMethod("OnEnable", Hidden).Invoke(enemy, null);
            enemy.transform.position = at;
            return enemy;
        }

        [Test]
        public void ACompanionSavesItsHeavyForACrowdThePlayerDoesNot()
        {
            var weapon = Xiro();
            var enemy = LoneEnemy(weapon.transform.position + Vector3.forward * 1.5f);
            var health = weapon.GetComponent<Health>();

            var companion = new CombatIntent();
            new CombatBrain(CombatBrain.Companion).Fight(weapon.transform, health, weapon, HeroClassId.Paladin,
                enemy, 1.5f, ref companion);
            Assert.IsFalse(companion.HeavyPress,
                "Ein Begleiter verbraucht seinen schweren Angriff nicht an einem einzelnen Laeufer - "
                + "sonst faellt XIROs Urteil wieder ununterbrochen.");
            Assert.IsTrue(companion.Attack, "Stattdessen schlaegt er normal zu.");

            var player = new CombatIntent();
            new CombatBrain(CombatBrain.Player).Fight(weapon.transform, health, weapon, HeroClassId.Paladin,
                enemy, 1.5f, ref player);
            Assert.IsTrue(player.HeavyPress, "Wer am Knopf sitzt, setzt ihn ein, sobald er bereit ist.");
        }

        [Test]
        public void LowOnHealthEitherBrainDashesAwayFromTheThreat()
        {
            var weapon = Xiro();
            var enemy = LoneEnemy(weapon.transform.position + Vector3.forward * 1.5f);
            var health = weapon.GetComponent<Health>();
            health.Drain(health.Maximum * 0.8f);

            var intent = new CombatIntent();
            new CombatBrain(CombatBrain.Player).Fight(weapon.transform, health, weapon, HeroClassId.Paladin,
                enemy, 1.5f, ref intent);
            Assert.IsTrue(intent.Dash, "Wenig Leben und ein Gegner auf der Haut: weg.");
            Assert.Less(intent.DashMove.y, 0f, "Vom Gegner weg, nicht auf ihn zu.");
        }

        /// <summary>
        /// Wann der Kopf rollt. Vorher im ersten Bild der Ankuendigung - die Unverwundbarkeit war vorbei,
        /// bevor der Schlag landete, und bei einem Armbrustschuss folgte die Linie der Rolle einfach nach.
        /// </summary>
        [Test]
        public void DieRolleDecktDenEinschlagJederGegnerart()
        {
            foreach (var stats in EnemyBalance.All)
            {
                var roll = CombatBrain.DodgeMoment(0f, stats.TelegraphSeconds);
                Assert.That(roll, Is.GreaterThanOrEqualTo(CombatBrain.ReactionSeconds),
                    $"{stats.Kind}: schneller als ein Mensch reagieren kann");
                Assert.That(roll, Is.LessThanOrEqualTo(stats.TelegraphSeconds),
                    $"{stats.Kind}: erst nach dem Einschlag gerollt");
                Assert.That(roll + PlayerController.RollInvulnerableSeconds, Is.GreaterThanOrEqualTo(stats.TelegraphSeconds),
                    $"{stats.Kind}: die Unverwundbarkeit ist vorbei, bevor die Ankuendigung endet");
            }
            // Die Linie des Armbrustschuetzen folgt dem Ziel auf den ersten 62 % - wer vorher rollt,
            // wird trotzdem getroffen.
            var marksman = EnemyBalance.For(EnemyKind.Marksman).TelegraphSeconds;
            Assert.That(CombatBrain.DodgeMoment(0f, marksman), Is.GreaterThan(marksman * 0.62f),
                "vor dem Einrasten der Armbrustlinie gerollt");
            // Gegenprobe: wer wie vorher im ersten Bild rollt, ist beim Schildstoss laengst wieder verwundbar.
            Assert.That(0f + PlayerController.RollInvulnerableSeconds,
                Is.LessThan(EnemyBalance.For(EnemyKind.Shieldbearer).TelegraphSeconds));
        }

        [Test]
        public void VorEinemSchussZurSeiteVorEinemSchlagZurueck()
        {
            var self = new Vector3(0f, 0f, -6f);
            var enemy = Vector3.zero;
            var line = (self - enemy).normalized;

            var shot = CombatBrain.DodgeDirection(self, enemy, true, 1f);
            Assert.That(Mathf.Abs(Vector3.Dot(shot, line)), Is.LessThan(0.01f), "aus der Schusslinie heraus, nicht in ihr zurueck");
            Assert.That(shot.magnitude, Is.EqualTo(1f).Within(0.001f));

            var blow = CombatBrain.DodgeDirection(self, enemy, false, 1f);
            Assert.That(Vector3.Dot(blow, line), Is.GreaterThan(0.8f), "vom Schlag weg");
            Assert.That(Mathf.Abs(Vector3.Dot(blow, Vector3.Cross(Vector3.up, line))), Is.GreaterThan(0.3f), "und ein Stueck zur Seite");

            var other = CombatBrain.DodgeDirection(self, enemy, true, -1f);
            Assert.That(Vector3.Dot(shot, other), Is.LessThan(-0.99f), "beide Seiten kommen vor");
        }
    }
}
