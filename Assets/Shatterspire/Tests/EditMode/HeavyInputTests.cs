using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der schwere Angriff, getrieben ueber die echte Eingabe - nicht ueber eine Nachrechnung.
    ///
    /// Der Anlass: "Richturteil laeuft nicht. Wird automatisch eingesetzt, ohne dass ich es
    /// druecke." Beides war derselbe Fehler, und keine meiner bisherigen Rechnungen konnte ihn
    /// finden, weil keine davon einen Knopf drueckt.
    ///
    /// Auf dem Telefon ist ein Tipp <b>Druck und Loslassen im selben Bild</b>. Die Waffe startete
    /// daraufhin die Ladung und loeste sie im selben Aufruf wieder aus - bei Ladung 0. Ergebnis: der
    /// schwaechste Schlag, den es gibt, der volle Balken weg, und weder Ladebalken noch Fenster zu
    /// sehen. Von aussen sieht das aus, als sei die Faehigkeit von allein losgegangen.
    ///
    /// Diese Tests treiben <see cref="WeaponSystem"/> Bild fuer Bild mit echten Knopfdruecken.
    /// </summary>
    public sealed class HeavyInputTests
    {
        private static readonly MethodInfo RouterTick =
            typeof(PlayerInputRouter).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo HeavyTick =
            typeof(WeaponSystem).GetMethod("UpdateHeavyAttack", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject hero;
        private PlayerInputRouter router;
        private WeaponSystem weapon;

        [SetUp]
        public void Build()
        {
            MobileInput.Reset();
            hero = new GameObject("XIRO");
            var build = hero.AddComponent<PlayerBuild>();
            var health = hero.AddComponent<Health>();
            router = hero.AddComponent<PlayerInputRouter>();
            weapon = hero.AddComponent<WeaponSystem>();

            // Awake laeuft ausserhalb des Spiels nicht von selbst.
            Invoke(health, "Awake");
            Invoke(build, "Awake");
            Invoke(weapon, "Awake");
            health.Configure(TeamId.Player, 160f);
            // Ohne Anzeigen: die bauen Objekte und Materialien, die hier nichts zu suchen haben.
            weapon.SetIndicatorsEnabled(false);
            weapon.ConfigureClass(HeroClassId.Paladin);
            SetPrivate(weapon, "heavyMeter", 100f);
        }

        [TearDown]
        public void Clear()
        {
            MobileInput.Reset();
            if (hero) Object.DestroyImmediate(hero);
        }

        private static void Invoke(object target, string method)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);

        private static void SetPrivate(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static T GetPrivate<T>(object target, string field)
            => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        /// <summary>Ein Bild: die Eingabe liest die Knoepfe, dann arbeitet die Waffe damit.</summary>
        private void Frame()
        {
            RouterTick.Invoke(router, null);
            HeavyTick.Invoke(weapon, null);
        }

        /// <summary>
        /// Setzt die Ladung. Achtung: jedes <see cref="Frame"/> legt danach noch ein
        /// <c>Time.deltaTime</c> drauf - im Editor sind das rund 60 ms, also gut 5 % der Ladung.
        /// Werte dicht an einer Schwelle rutschen dadurch darueber.
        /// </summary>
        private void Charged(float normalized)
            => SetPrivate(weapon, "heavyCharge", normalized * ActionBalance.HeavyChargeSeconds);

        // ── Der Tipp ────────────────────────────────────────────────────────

        [Test]
        public void ATapDoesNotFireTheHeavyAtZeroCharge()
        {
            // Genau ein Tipp: Druck und Loslassen, bevor die Eingabe das naechste Mal liest.
            MobileInput.SetHeavy(true);
            MobileInput.SetHeavy(false);
            Frame();

            Assert.IsTrue(weapon.ChargingHeavy,
                "Ein Tipp hat den schweren Angriff sofort ausgeloest. Auf dem Telefon ist jeder "
                + "Tipp Druck und Loslassen in einem Bild - so verpufft der volle Balken bei "
                + "Ladung 0, ohne dass man je einen Ladebalken sieht.");
            Assert.Less(weapon.HeavyChargeNormalized, ActionBalance.PerfectStart,
                "Nach einem Tipp steht die Ladung noch am Anfang.");
        }

        [Test]
        public void ATapKeepsChargingUntilTheChargeIsFull()
        {
            MobileInput.SetHeavy(true);
            MobileInput.SetHeavy(false);
            Frame();
            Assert.IsTrue(weapon.ChargingHeavy);

            // Mitten in der Ladung laeuft sie noch, am Ende loest sie aus.
            Charged(0.8f);
            Frame();
            Assert.IsTrue(weapon.ChargingHeavy, "Vor dem Ende darf sie nicht weg sein.");

            Charged(1f);
            Frame();
            Assert.IsFalse(weapon.ChargingHeavy, "Bei voller Ladung loest sie von selbst aus.");
        }

        // ── Halten und im Fenster loslassen ────────────────────────────────

        [Test]
        public void ReleasingInsideTheWindowFires()
        {
            MobileInput.SetHeavy(true);
            Frame();
            Assert.IsTrue(weapon.ChargingHeavy);

            Charged((ActionBalance.PerfectStart + ActionBalance.PerfectEnd) * 0.5f);
            Frame();
            Assert.AreEqual(HeavyTiming.Perfect, weapon.HeavyTimingNow,
                "Mitten im Fenster muss der Moment als perfekt gelten.");

            MobileInput.SetHeavy(false);
            Frame();
            Assert.IsFalse(weapon.ChargingHeavy, "Im Fenster loslassen muss ausloesen.");
        }

        [Test]
        public void ReleasingTooEarlyIsIgnoredAndTheChargeGoesOn()
        {
            MobileInput.SetHeavy(true);
            Frame();

            Charged(0.1f);
            MobileInput.SetHeavy(false);
            Frame();

            Assert.IsTrue(weapon.ChargingHeavy,
                "Zu frueh loslassen darf den Balken nicht verpulvern - es bringt nichts und kostet "
                + "alles.");
        }

        [Test]
        public void TheMeterIsSpentOnlyWhenTheAttackActuallyFires()
        {
            MobileInput.SetHeavy(true);
            MobileInput.SetHeavy(false);
            Frame();
            Assert.AreEqual(1f, weapon.HeavyMeterNormalized, 0.001f,
                "Der Balken gehoert erst dem Schlag, wenn er faellt.");
        }

        // ── Der Schritt ins Ziel ist kein Dash ─────────────────────────────

        [Test]
        public void TheStepIntoASwingIsNeverFasterThanRunning()
        {
            foreach (var hero in System.Enum.GetValues(typeof(HeroClassId)).Cast<HeroClassId>()
                         .Where(HeroCatalog.IsMelee))
            {
                var run = HeroCatalog.BaseSpeed(hero);
                foreach (var motion in new[] { AttackMotion.Swing, AttackMotion.Smash, AttackMotion.Spin })
                {
                    var windup = ChampionAnimationDriver.StrikeSecondsOf(motion);
                    for (var distance = 0f; distance <= MeleeApproach.MaxEngage; distance += 0.1f)
                    {
                        var step = MeleeApproach.StepDistance(distance, windup, hero, run);
                        var speed = step / windup;
                        Assert.LessOrEqual(speed, run + 0.001f,
                            $"{HeroCatalog.Name(hero)} legt beim {motion} aus {distance:0.0} Einheiten "
                            + $"{step:0.00} in {windup:0.000} s zurueck - das sind {speed:0.0} je "
                            + $"Sekunde, er laeuft aber nur {run:0.0}. Das ist ein Dash, kein Schritt.");
                    }
                }
            }
        }

        [Test]
        public void NobodyStepsWhenTheTargetIsAlreadyInReach()
        {
            foreach (var hero in System.Enum.GetValues(typeof(HeroClassId)).Cast<HeroClassId>()
                         .Where(HeroCatalog.IsMelee))
            {
                var run = HeroCatalog.BaseSpeed(hero);
                var windup = ChampionAnimationDriver.StrikeSecondsOf(AttackMotion.Swing);
                var gap = MeleeApproach.IdealGapFor(hero);
                Assert.AreEqual(0f, MeleeApproach.StepDistance(gap, windup, hero, run), 0.0001f,
                    $"{HeroCatalog.Name(hero)} setzt nach, obwohl er schon nah genug steht.");
                Assert.Less(gap, ActionBalance.MeleeReach(hero),
                    $"{HeroCatalog.Name(hero)} laeuft bis unter seine eigene Klinge. Er trifft ab "
                    + $"{ActionBalance.MeleeReach(hero):0.00} und geht auf {gap:0.00} heran.");
            }
        }
    }
}
