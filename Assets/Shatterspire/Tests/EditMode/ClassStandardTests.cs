using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Alle fuenf Klassen auf demselben Stand.
    ///
    /// Der Anlass: "Der Paladin laesst sich sehr schlecht spielen. Aschewelle fliegt sonst wo hin,
    /// Autohits sehr schlecht als recht, Richturteil selten einsetzbar." Dazu, klassenuebergreifend:
    /// "man sieht nicht gut genug, wann man perfekt hittet".
    ///
    /// Gemessen war XIRO auf zwei Achsen der Ausreisser: 2,51 Einheiten Kampftempo gegen einen
    /// Crawler mit 3,50, und mit 2,60 die kuerzeste Reichweite von allen - kuerzer als der
    /// Kriegshammer mit 3,25. Er blieb 1,11 s an einem weglaufenden Gegner, also gut drei Hiebe,
    /// und danach schlug er ins Leere. Der Balken des schweren Angriffs brauchte je Klasse
    /// verschieden lang: REX 1,80 s, BRAX 2,90 s.
    ///
    /// Diese Tests halten die Norm fest, nicht die Einzelwerte: jeder Nahkaempfer muss lange genug
    /// am Gegner bleiben, jeder Held muss gleich schnell an seinen schweren Angriff kommen, und das
    /// perfekte Fenster muss treffbar sein.
    /// </summary>
    public sealed class ClassStandardTests
    {
        private static IEnumerable<HeroClassId> AllHeroes
            => System.Enum.GetValues(typeof(HeroClassId)).Cast<HeroClassId>();

        private static IEnumerable<HeroClassId> Melee => AllHeroes.Where(HeroCatalog.IsMelee);

        /// <summary>
        /// Gebundener Anteil eines Kombos: wie viel der Zeit ein Nahkaempfer beim Angreifen
        /// festgenagelt ist. Dieselbe Rechnung wie <see cref="MeleeApproach.BoundSeconds"/>, mit den
        /// Ausholzeiten aus den gemessenen Clipfenstern.
        /// </summary>
        private static float BoundShare(HeroClassId hero)
        {
            var combo = ComboOf(hero);
            var cycle = combo.Sum(step => step.Gap);
            var bound = combo.Sum(step =>
                MeleeApproach.BoundSeconds(ChampionAnimationDriver.StrikeSecondsOf(step.Motion), step.Gap));
            return cycle <= 0f ? 0f : bound / cycle;
        }

        private static (AttackMotion Motion, float Gap)[] ComboOf(HeroClassId hero) => hero switch
        {
            HeroClassId.Guardian => new[]
            {
                (AttackMotion.Swing, 0.38f), (AttackMotion.Smash, 0.46f), (AttackMotion.Spin, 0.64f)
            },
            HeroClassId.Paladin => new[]
            {
                (AttackMotion.Swing, 0.3f), (AttackMotion.Swing, 0.3f), (AttackMotion.Smash, 0.42f)
            },
            _ => new (AttackMotion, float)[0]
        };

        // ── Nahkampf: lange genug am Gegner ─────────────────────────────────

        [Test]
        public void EveryBrawlerStaysOnARunnerLongEnough()
        {
            foreach (var hero in Melee)
            {
                var speed = ActionBalance.CombatSpeed(HeroCatalog.BaseSpeed(hero), BoundShare(hero));
                var contact = ActionBalance.ContactSeconds(ActionBalance.MeleeReach(hero), speed,
                    ActionBalance.RunnerSpeed);
                Assert.GreaterOrEqual(contact, ActionBalance.MinimumContactSeconds,
                    $"{HeroCatalog.Name(hero)} bleibt nur {contact:0.00} s an einem Crawler "
                    + $"(Kampftempo {speed:0.00}, Reichweite {ActionBalance.MeleeReach(hero):0.00}). "
                    + "Er schlaegt danach ins Leere.");
            }
        }

        [Test]
        public void TheTwoHanderOutreachesTheHammer()
        {
            // Nicht Geschmack, sondern Konsistenz: eine laengere Waffe reicht weiter. XIROs
            // Zweihaender reichte 2,60, BRAX' Kriegshammer 3,25.
            Assert.Greater(ActionBalance.MeleeReach(HeroClassId.Paladin),
                ActionBalance.MeleeReach(HeroClassId.Guardian),
                "Der Zweihaender muss weiter reichen als der Kriegshammer.");
        }

        [Test]
        public void TheSlowestBrawlerHasTheLongestReach()
        {
            var slowest = Melee.OrderBy(HeroCatalog.BaseSpeed).First();
            var longest = Melee.OrderByDescending(ActionBalance.MeleeReach).First();
            Assert.AreEqual(longest, slowest,
                $"{HeroCatalog.Name(slowest)} ist der langsamste Nahkaempfer, reicht aber nicht am "
                + "weitesten. Langsam und kurz zugleich ist unspielbar.");
        }

        [Test]
        public void RangedHeroesHaveNoMeleeReach()
        {
            foreach (var hero in AllHeroes.Where(h => !HeroCatalog.IsMelee(h)))
                Assert.AreEqual(0f, ActionBalance.MeleeReach(hero), 0.0001f,
                    $"{HeroCatalog.Name(hero)} kaempft auf Abstand und braucht keine Schlagweite.");
        }

        [Test]
        public void BotsStandInsideTheWeaponsReach()
        {
            foreach (var hero in Melee)
            {
                var engage = HeroCatalog.EngageRange(hero);
                var reach = ActionBalance.MeleeReach(hero);
                Assert.Less(engage, reach,
                    $"Ein Bot mit {HeroCatalog.Name(hero)} haelt {engage:0.00} Einheiten Abstand, "
                    + $"die Waffe reicht {reach:0.00}. Er stellt sich ausserhalb seiner Schlagweite auf.");
                Assert.Greater(engage, reach * 0.6f,
                    $"{HeroCatalog.Name(hero)} geht unnoetig weit heran und steht dem Spieler im Weg.");
            }
        }

        [Test]
        public void TheGroundShapeMatchesWhatTheSwingHits()
        {
            // Die Meldung von damals war "Linie und Schuss sind nicht synchron". Dasselbe galt am
            // Boden: der Keil war kuerzer als der Schlag.
            foreach (var hero in Melee)
            {
                var shape = AimCatalog.Describe(hero, ActionSlot.Light, 0, false, 15f, 13f);
                Assert.AreEqual(ActionBalance.MeleeReach(hero), shape.Range, 0.01f,
                    $"Der Keil von {HeroCatalog.Name(hero)} zeigt {shape.Range:0.00}, "
                    + $"getroffen wird bis {ActionBalance.MeleeReach(hero):0.00}.");
            }
        }

        // ── Die Bindung trifft alle gleich ──────────────────────────────────

        [Test]
        public void BindNeverEatsMoreThanItsShareOfTheSwing()
        {
            foreach (var gap in new[] { 0.2f, 0.3f, 0.42f, 0.46f, 0.64f, 1f })
            foreach (var motion in new[] { AttackMotion.Swing, AttackMotion.Smash, AttackMotion.Spin })
            {
                var windup = ChampionAnimationDriver.StrikeSecondsOf(motion);
                var bound = MeleeApproach.BoundSeconds(windup, gap);
                Assert.LessOrEqual(bound, gap * MeleeApproach.MaxBoundShare + 0.0001f,
                    $"{motion} bei {gap:0.00} s Pause bindet {bound:0.000} s - mehr als der Anteil erlaubt.");
                Assert.LessOrEqual(bound, MeleeApproach.MaxBoundSeconds + 0.0001f);
            }
        }

        [Test]
        public void FasterSwingsAreNotBoundProportionallyLonger()
        {
            // Genau das war der Fehler: die Bindung war eine absolute Zahl, also fiel sie bei
            // kurzen Pausen anteilig schwerer ins Gewicht. XIRO war 70 % gebunden, BRAX 51 %.
            var shares = Melee.ToDictionary(h => h, BoundShare);
            foreach (var pair in shares)
                Assert.LessOrEqual(pair.Value, MeleeApproach.MaxBoundShare + 0.0001f,
                    $"{HeroCatalog.Name(pair.Key)} ist {pair.Value:0%} der Zeit gebunden.");
            var spread = shares.Values.Max() - shares.Values.Min();
            Assert.Less(spread, 0.12f,
                $"Der gebundene Anteil geht {spread:0%} auseinander - die Nahkaempfer stehen nicht "
                + "auf demselben Stand.");
        }

        // ── Der schwere Angriff kommt bei allen gleich schnell ──────────────

        [Test]
        public void EveryHeroReachesItsHeavyInTheSameTime()
        {
            foreach (var hero in AllHeroes)
                Assert.AreEqual(ActionBalance.HeavyFillSeconds, ActionBalance.SecondsToHeavy(hero), 0.05f,
                    $"{HeroCatalog.Name(hero)} braucht {ActionBalance.SecondsToHeavy(hero):0.00} s bis "
                    + "zum schweren Angriff. Der perfekte Moment ist die interessanteste Eingabe im "
                    + "Spiel - niemand darf seltener hinkommen als die anderen.");
        }

        [Test]
        public void SlowerHeroesFillMorePerHit()
        {
            // Die Fuellung muss dem Tempo folgen, sonst kippt die Gleichheit beim naechsten
            // Zahlendreher. BRAX schlaegt langsamer als REX, also fuellt jeder seiner Treffer mehr.
            Assert.Greater(ActionBalance.HeavyFillPerHit(HeroClassId.Guardian),
                ActionBalance.HeavyFillPerHit(HeroClassId.Ranger));
        }

        // ── Das perfekte Fenster ────────────────────────────────────────────

        [Test]
        public void TheWindowIsWideEnoughToHitOnAPhone()
        {
            Assert.GreaterOrEqual(ActionBalance.WindowSeconds, 0.3f,
                $"Das Fenster ist {ActionBalance.WindowSeconds * 1000f:0} ms breit. Auf einem Telefon, "
                + "mit einem Daumen auf dem Knopf, ist das nicht mehr Timing, sondern Glueck.");
            Assert.LessOrEqual(ActionBalance.WindowSeconds, 0.55f,
                "So breit ist es kein Fenster mehr, sondern die halbe Ladung.");
        }

        [Test]
        public void TheWindowOpensLateEnoughToReactTo()
        {
            // Eine einfache Reaktion auf einen Ton dauert rund 160 ms. Wer erst beim Aufgehen
            // anfaengt zu reagieren, muss noch im Fenster ankommen.
            Assert.GreaterOrEqual(ActionBalance.WindowOpensAt, 0.4f,
                "Das Fenster geht zu frueh auf, um darauf zu reagieren.");
            Assert.Greater(ActionBalance.WindowSeconds, 0.16f,
                "Das Fenster ist kuerzer als eine Reaktion auf den Ton, der es ankuendigt.");
        }

        [Test]
        public void JudgeSortsTheThreeTiers()
        {
            Assert.AreEqual(HeavyTiming.Perfect, ActionBalance.Judge(ActionBalance.PerfectStart));
            Assert.AreEqual(HeavyTiming.Perfect, ActionBalance.Judge(ActionBalance.PerfectEnd));
            Assert.AreEqual(HeavyTiming.Perfect,
                ActionBalance.Judge((ActionBalance.PerfectStart + ActionBalance.PerfectEnd) * 0.5f));

            Assert.AreEqual(HeavyTiming.Good,
                ActionBalance.Judge(ActionBalance.PerfectStart - ActionBalance.GoodMargin * 0.5f));
            Assert.AreEqual(HeavyTiming.Good,
                ActionBalance.Judge(ActionBalance.PerfectEnd + ActionBalance.GoodMargin * 0.5f));

            Assert.AreEqual(HeavyTiming.Loose, ActionBalance.Judge(0f));
            Assert.AreEqual(HeavyTiming.Loose, ActionBalance.Judge(1f));
        }

        [Test]
        public void TimingIsAlwaysWorthMoreThanWaiting()
        {
            var perfect = ActionBalance.Multiplier((ActionBalance.PerfectStart + ActionBalance.PerfectEnd) * 0.5f);
            var good = ActionBalance.Multiplier(ActionBalance.PerfectEnd + ActionBalance.GoodMargin * 0.5f);
            var held = ActionBalance.Multiplier(1f);

            Assert.Greater(perfect, good, "Perfekt muss mehr bringen als knapp daneben.");
            Assert.Greater(good, held,
                "Knapp daneben muss mehr bringen als einfach durchhalten - sonst ist das Fenster "
                + "eine Falle fuer die, die es versuchen.");
            Assert.Greater(perfect / held, 1.4f,
                $"Perfekt bringt nur das {perfect / held:0.00}-fache von Durchhalten. Dafuer schaut "
                + "niemand auf den Ring.");
        }

        [Test]
        public void MultiplierNeverDropsWhileCharging()
        {
            // Laenger halten darf nie schlechter sein als kuerzer halten, ausser beim Verlassen des
            // Fensters. Sonst liest sich der Balken falsch.
            var loose = new List<float>();
            for (var n = 0f; n < ActionBalance.PerfectStart - ActionBalance.GoodMargin; n += 0.02f)
                loose.Add(ActionBalance.Multiplier(n));
            for (var i = 1; i < loose.Count; i++)
                Assert.GreaterOrEqual(loose[i], loose[i - 1] - 0.0001f,
                    "Vor dem Fenster muss laenger halten immer mindestens so gut sein.");
        }
    }
}
