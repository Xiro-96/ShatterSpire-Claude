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
        public void RoutesFollowThePolicyByKindNotByPosition()
        {
            var offer = new[] { RoomKind.Combat, RoomKind.Elite, RoomKind.Treasure };
            Assert.AreEqual(2, AutoPilotChoices.RouteFor(offer, 1, RoutePolicy.Safe), "Vorsichtig: der Schatz.");
            Assert.AreEqual(1, AutoPilotChoices.RouteFor(offer, 1, RoutePolicy.Risky), "Gierig: die Elite.");
            Assert.AreEqual(0, AutoPilotChoices.RouteFor(new[] { RoomKind.Combat, RoomKind.Elite }, 1, RoutePolicy.Safe),
                "Ohne Schatz lieber Kampf als Elite.");
            Assert.AreEqual(1, AutoPilotChoices.RouteFor(offer, 1, RoutePolicy.Cycle), "Im Wechsel wie bisher.");
            Assert.AreEqual(-1, AutoPilotChoices.RouteFor(System.Array.Empty<RoomKind>(), 1, RoutePolicy.Safe));
            // Gegenprobe: Elite an erster Stelle - vorsichtig nimmt trotzdem nicht den ersten Knopf.
            Assert.AreEqual(1, AutoPilotChoices.RouteFor(new[] { RoomKind.Elite, RoomKind.Combat }, 0, RoutePolicy.Safe));
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
            Assert.GreaterOrEqual(gap, EnemyBalance.For(EnemyKind.Brute).ColliderRadius + PartyMember.Clearance - 0.001f,
                $"Der Brute steckt noch {gap:0.00} tief im Helden - beim naechsten Schritt schiebt die "
                + "Kollision den Helden heraus.");
            Assert.AreEqual(heroAt, hero.transform.position, "Der Held selbst darf sich nicht bewegen.");
        }

        /// <summary>
        /// Dasselbe, aber bevor der Held sich bewegt statt am Ende des Bildes. War ein Gegner im Bild vor
        /// dem Helden an der Reihe und lief in ihn hinein, kam das Heraustreten sonst zu spaet - der
        /// Selbsttest mass noch Stoesse von 0,5 bis 0,9 Einheiten, etwa einmal in sieben Laeufen.
        /// </summary>
        [Test]
        public void BeforeTheHeroMovesEveryEnemyInsideHimStepsOut()
        {
            var hero = Spawn("XIRO");
            var heroAt = hero.transform.position;
            var brute = Spawn("Brute").AddComponent<EnemyAgent>();
            typeof(EnemyAgent).GetField("stats", Hidden).SetValue(brute, EnemyBalance.For(EnemyKind.Brute));
            var stateField = typeof(EnemyAgent).GetField("state", Hidden);
            stateField.SetValue(brute, System.Enum.Parse(stateField.FieldType, "Chase"));
            brute.transform.position = heroAt + Vector3.right * 0.3f;

            // Gegenprobe: ein Gegner, der nicht angemeldet ist, bleibt, wo er ist.
            EnemyAgent.MakeRoomFor(hero.transform);
            Assert.AreEqual(0.3f, CombatBrain.FlatDistance(brute.transform.position, heroAt), 0.001f);

            typeof(EnemyAgent).GetMethod("OnEnable", Hidden).Invoke(brute, null);
            try
            {
                EnemyAgent.MakeRoomFor(hero.transform);
                var gap = CombatBrain.FlatDistance(brute.transform.position, heroAt);
                Assert.GreaterOrEqual(gap, EnemyBalance.For(EnemyKind.Brute).ColliderRadius + PartyMember.Clearance - 0.001f,
                    $"Der Brute steckt noch {gap:0.00} tief im Helden.");
                Assert.AreEqual(heroAt, hero.transform.position, "Der Held selbst darf sich nicht bewegen.");
            }
            finally
            {
                typeof(EnemyAgent).GetMethod("OnDisable", Hidden).Invoke(brute, null);
            }
        }

        /// <summary>
        /// Heilkugeln holt er wie ein Mensch: nur mit wenig Leben, im Kampf nur ganz nahe, und nur
        /// seine eigenen - eine fremde kann er nicht einsammeln.
        /// </summary>
        [Test]
        public void TheAutopilotWalksToAnOrbOnlyWhenItIsWorthIt()
        {
            var hero = Spawn("REX");
            var pilot = hero.AddComponent<AutoPilot>();
            var other = Spawn("BRAX");
            var near = HealthOrb.Spawn(hero.transform.position + Vector3.right * 3f, 10f, hero.transform);
            var far = HealthOrb.Spawn(hero.transform.position + Vector3.left * 6f, 10f, hero.transform);
            var foreign = HealthOrb.Spawn(hero.transform.position + Vector3.forward * 1f, 10f, other.transform);
            var orbs = new[] { near, far, foreign };
            // Im Editor laeuft OnEnable nicht von selbst - ohne Anmeldung saehe er keine Kugel.
            foreach (var orb in orbs)
            {
                spawned.Add(orb.gameObject);
                typeof(HealthOrb).GetMethod("OnEnable", Hidden).Invoke(orb, null);
            }
            var choose = typeof(AutoPilot).GetMethod("OrbWorthTheWalk", Hidden);
            HealthOrb Pick(float share, bool fighting) => (HealthOrb)choose.Invoke(pilot, new object[] { share, fighting });
            try
            {
                Assert.IsNull(Pick(0.9f, false), "Mit fast vollem Leben lohnt kein Umweg.");
                Assert.AreEqual(near, Pick(0.6f, false), "Die naechste eigene, nicht die fremde direkt daneben.");
                Assert.IsNull(Pick(0.6f, true), "Im Kampf mit mehr als halbem Leben: weiterkaempfen.");
                Assert.AreEqual(near, Pick(0.3f, true), "Im Kampf mit wenig Leben nur die ganz nahe.");
                typeof(HealthOrb).GetMethod("OnDisable", Hidden).Invoke(near, null);
                Assert.IsNull(Pick(0.3f, true), "Sechs Einheiten sind mitten im Kampf zu weit.");
                Assert.AreEqual(far, Pick(0.3f, false), "Ausserhalb des Kampfes schon.");
            }
            finally
            {
                foreach (var orb in orbs) typeof(HealthOrb).GetMethod("OnDisable", Hidden).Invoke(orb, null);
            }
        }

        /// <summary>
        /// Aufstehen verschafft Luft. Vorher stand der Held im selben Pulk wieder auf, in dem er
        /// gefallen war - 30 von 68 Folgestuerzen kamen weniger als 15 Sekunden danach.
        /// </summary>
        [Test]
        public void StandingUpPushesTheCrowdBack()
        {
            EnemyAgent Enemy(EnemyKind kind, Vector3 at)
            {
                var agent = Spawn(kind.ToString()).AddComponent<EnemyAgent>();
                typeof(EnemyAgent).GetField("stats", Hidden).SetValue(agent, EnemyBalance.For(kind));
                typeof(EnemyAgent).GetField("kind", Hidden).SetValue(agent, kind);
                var state = typeof(EnemyAgent).GetField("state", Hidden);
                state.SetValue(agent, System.Enum.Parse(state.FieldType, "Chase"));
                agent.transform.position = at;
                typeof(EnemyAgent).GetMethod("OnEnable", Hidden).Invoke(agent, null);
                return agent;
            }
            Vector3 Shove(EnemyAgent agent) => (Vector3)typeof(EnemyAgent).GetField("knockbackVelocity", Hidden).GetValue(agent);
            float StaggeredUntil(EnemyAgent agent) => (float)typeof(EnemyAgent).GetField("hitStaggerUntil", Hidden).GetValue(agent);

            var near = Enemy(EnemyKind.Crawler, new Vector3(1.5f, 0f, 0f));
            var far = Enemy(EnemyKind.Crawler, new Vector3(EnemyAgent.RiseRadius + 1f, 0f, 0f));
            var boss = Enemy(EnemyKind.IronWarden, new Vector3(0f, 0f, 2f));
            try
            {
                Assert.AreEqual(2, EnemyAgent.ClearRoomAround(Vector3.zero), "Der nahe Crawler und der Warden.");
                Assert.Greater(Shove(near).x, 0f, "Vom Helden weg.");
                Assert.Greater(StaggeredUntil(near), Time.time, "Der nahe Crawler taumelt.");
                Assert.AreEqual(Vector3.zero, Shove(far), "Ausserhalb des Umkreises bleibt alles, wie es ist.");
                Assert.LessOrEqual(StaggeredUntil(far), Time.time);
                Assert.Greater(Shove(boss).z, 0f, "Auch der Waechter weicht - ein Stueck.");
                Assert.LessOrEqual(StaggeredUntil(boss), Time.time, "Aber ein Waechter taumelt nicht.");
            }
            finally
            {
                foreach (var agent in new[] { near, far, boss })
                    typeof(EnemyAgent).GetMethod("OnDisable", Hidden).Invoke(agent, null);
            }
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
