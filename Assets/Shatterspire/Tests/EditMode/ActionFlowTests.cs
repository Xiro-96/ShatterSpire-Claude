using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Wie sich die Aktionen aneinanderreihen: was ein Druck im falschen Moment bewirkt, was ein
    /// Dash abbricht, und wann die Welt kurz anhaelt.
    ///
    /// Drei Befunde aus dem Code, alle drei in Auftrag gegeben mit "Alles 3":
    ///
    /// 1. Die Faehigkeit loeste beim Loslassen nur aus, wenn sie in genau diesem Bild bereit war.
    ///    Ein Loslassen 0,2 s zu frueh ging verloren - kein Schlag, kein Hinweis. Der normale
    ///    Angriff hatte laengst einen Puffer.
    /// 2. Ein Hieb, der schon ausgeholt hatte, landete auch nach einem Dash noch - an der neuen
    ///    Stelle, weil der Treffer erst beim Einschlag berechnet wird.
    /// 3. Jeder leichte Hieb hielt die ganze Welt an. XIRO allein lief damit 12 % der Zeit in
    ///    Zeitlupe, bei 2,8 Stopps je Sekunde.
    /// </summary>
    public sealed class ActionFlowTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly MethodInfo RouterTick = typeof(PlayerInputRouter).GetMethod("Update", Hidden);
        private static readonly MethodInfo WeaponTick = typeof(WeaponSystem).GetMethod("Update", Hidden);

        private GameObject[] spawned = new GameObject[0];
        private WeaponSystem weapon;
        private PlayerInputRouter router;

        [SetUp]
        public void Build()
        {
            MobileInput.Reset();
            Time.timeScale = 1f;
            var hero = Spawn("XIRO");
            var build = hero.AddComponent<PlayerBuild>();
            var health = hero.AddComponent<Health>();
            router = hero.AddComponent<PlayerInputRouter>();
            weapon = hero.AddComponent<WeaponSystem>();
            foreach (var component in new MonoBehaviour[] { health, build, weapon })
                component.GetType().GetMethod("Awake", Hidden)?.Invoke(component, null);
            health.Configure(TeamId.Player, 160f);
            // Ohne Ringe und Linien einrichten, dann als eigenen Helden fuehren.
            weapon.SetLocal(false);
            weapon.ConfigureClass(HeroClassId.Paladin);
            weapon.SetLocal(true);
        }

        [TearDown]
        public void Clear()
        {
            MobileInput.Reset();
            Time.timeScale = 1f;
            // OnDisable laeuft hier ebenso wenig - von Hand abmelden, sonst liegt der Gegner fuer
            // die naechsten Tests noch in Health.Active.
            foreach (var go in spawned)
                if (go && go.TryGetComponent<Health>(out var body))
                    typeof(Health).GetMethod("OnDisable", Hidden)?.Invoke(body, null);
            foreach (var go in spawned) if (go) Object.DestroyImmediate(go);
            spawned = new GameObject[0];
            var stop = GameObject.Find("Hitstop");
            if (stop) Object.DestroyImmediate(stop);
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned = spawned.Append(go).ToArray();
            return go;
        }

        private void Set(string field, object value) => typeof(WeaponSystem).GetField(field, Hidden).SetValue(weapon, value);

        /// <summary>Ein Bild: erst liest die Eingabe die Knoepfe, dann handelt die Waffe.</summary>
        private void Frame()
        {
            RouterTick.Invoke(router, null);
            WeaponTick.Invoke(weapon, null);
        }

        private bool SkillFired => weapon.SkillCooldownRemaining > 1f;

        // ── 1. Die Faehigkeit wird gepuffert ────────────────────────────────

        [Test]
        public void ASkillReleasedJustBeforeReadyFiresWhenReady()
        {
            Set("skillReadyAt", Time.time + 0.2f);
            MobileInput.SetSkill(true);
            Frame();
            MobileInput.SetSkill(false);
            Frame();
            Assert.IsFalse(SkillFired, "Vor dem Ende der Abklingzeit darf sie noch nicht losgehen.");

            Set("skillReadyAt", Time.time - 0.01f);
            Frame();
            Assert.IsTrue(SkillFired,
                "Losgelassen 0,2 s vor dem Ende - und die Faehigkeit kam nie. Genau dieser Druck ging "
                + "bisher verloren, ohne jeden Hinweis.");
        }

        [Test]
        public void ASkillReleasedFarTooEarlyIsRefusedVisibly()
        {
            Set("skillReadyAt", Time.time + 3f);
            MobileInput.SetSkill(true);
            Frame();
            MobileInput.SetSkill(false);
            Frame();

            Set("skillReadyAt", Time.time - 0.01f);
            Frame();
            Assert.IsFalse(SkillFired,
                "Drei Sekunden zu frueh ist kein Timing mehr - die Faehigkeit darf spaeter nicht "
                + "unverhofft losgehen.");
            Assert.Greater(weapon.LastSkillRefused, 0f,
                "Der abgewiesene Druck muss gemeldet werden, damit der Knopf antworten kann.");
        }

        [Test]
        public void AQueuedSkillKeepsTheDirectionOfTheRelease()
        {
            // Nach rechts gezogen und losgelassen, kurz vor Ende der Abklingzeit.
            Set("skillReadyAt", Time.time + 0.2f);
            MobileInput.SetSkill(true);
            MobileInput.ActionAim = Vector2.right;
            Frame();
            MobileInput.ReleaseActionAim();
            MobileInput.SetSkill(false);
            Frame();

            // Inzwischen zielt der Held nach links - etwa weil er zurueckweicht.
            router.ScriptedAim = Vector3.left * 5f;
            Frame();

            Set("skillReadyAt", Time.time - 0.01f);
            Frame();
            Assert.IsTrue(SkillFired, "Der Aufbau stimmt nicht: die Faehigkeit ist nicht losgegangen.");
            Assert.Less(Vector3.Angle(weapon.transform.forward, Vector3.right), 1f,
                "Die gepufferte Faehigkeit ging in die Richtung von jetzt statt in die vom Loslassen. "
                + "Wer beim Zurueckweichen loslaesst, wirft sie sonst nach hinten.");
        }

        // ── 2. Der Dash bricht ab ───────────────────────────────────────────

        private IEnumerator Swing(System.Action landed)
            => (IEnumerator)typeof(WeaponSystem).GetMethod("MeleeImpact", Hidden)
                .Invoke(weapon, new object[] { 0.1f, landed });

        [Test]
        public void ADashCancelsASwingThatHasNotLandedYet()
        {
            var landed = false;
            var swing = Swing(() => landed = true);
            swing.MoveNext();   // holt aus
            weapon.OnDashStarted(weapon.transform.position, Vector3.forward);
            swing.MoveNext();   // waere der Treffer
            Assert.IsFalse(landed,
                "Der Hieb ist nach dem Dash doch noch gelandet - an der neuen Stelle. Wer aus einem "
                + "Schlag herausrollt, soll nicht trotzdem zuschlagen.");
        }

        [Test]
        public void WithoutADashTheSwingLands()
        {
            // Die Gegenrichtung: sonst waere der Test oben auch gruen, wenn nie etwas landet.
            var landed = false;
            var swing = Swing(() => landed = true);
            swing.MoveNext();
            swing.MoveNext();
            Assert.IsTrue(landed);
        }

        [Test]
        public void ADashCancelsAHeavyChargeWithoutCostOrLecture()
        {
            Set("heavyMeter", 100f);
            MobileInput.SetHeavy(true);
            Frame();
            Assert.IsTrue(weapon.ChargingHeavy, "Der Aufbau stimmt nicht: es wird nicht geladen.");

            weapon.OnDashStarted(weapon.transform.position, Vector3.forward);
            Assert.IsFalse(weapon.ChargingHeavy, "Der Dash muss die Ladung abbrechen.");
            Assert.AreEqual(1f, weapon.HeavyMeterNormalized, 0.001f,
                "Ein Dash aus der Ladung heraus darf den Balken nicht kosten.");
            Assert.Less(weapon.LastHeavyCancel, 0f,
                "Wer mit dem Dash abbricht, hat es so gewollt - die Erklaerung des Griffs gehoert "
                + "nur zum zu frueh losgelassenen Knopf.");
        }

        // ── 3. Nur schwere Hiebe halten die Welt an ────────────────────────

        private void StrikeAt(Vector3 point, bool heavyImpact)
            => typeof(WeaponSystem).GetMethod("Strike", Hidden).Invoke(weapon, new object[]
            {
                point, 1.5f, 10f, DamageType.Physical, 4f, false, false, true, heavyImpact
            });

        [Test]
        public void LightHitsDoNotFreezeTheWorldHeavyHitsDo()
        {
            var enemy = Spawn("Crawler").AddComponent<Health>();
            enemy.Configure(TeamId.Enemy, 100000f);
            // Ausserhalb des Spiels laeuft OnEnable nicht - ohne das steht der Gegner nicht in
            // Health.Active, kein Hieb trifft ihn, und die erste Pruefung waere nur zufaellig gruen.
            typeof(Health).GetMethod("OnEnable", Hidden).Invoke(enemy, null);
            var point = weapon.transform.position + Vector3.forward;
            enemy.transform.position = point;

            StrikeAt(point, heavyImpact: false);
            Assert.Less(enemy.Normalized, 1f, "Der Aufbau stimmt nicht: der leichte Hieb hat nicht getroffen.");
            Assert.AreEqual(1f, Time.timeScale, 0.0001f,
                "Ein leichter Hieb hat die ganze Welt angehalten, auch die eigene Figur. Bei XIRO "
                + "hiess das 12 % der Zeit Zeitlupe.");

            StrikeAt(point, heavyImpact: true);
            Assert.Less(Time.timeScale, 1f, "Der schwere Hieb, der das Kombo beendet, soll haengen bleiben.");
        }
    }
}
