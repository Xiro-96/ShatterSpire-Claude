using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Score, Rang und Shift sind reine Rechnung ohne Szene — also genau der Teil,
    /// der vollstaendig ohne Play-Mode nachweisbar ist. Wenn hier alles gruen ist,
    /// stimmt die Wertung, unabhaengig davon wie das HUD sie anzeigt.
    /// </summary>
    public sealed class RankAndShiftTests
    {
        private static ClimbResult Climb(int floors, int bosses, int enemies, int shards, bool extracted)
            => new(floors, bosses, enemies, shards, extracted, RunMode.Heroic, HeroClassId.Ranger);

        // ── Score ────────────────────────────────────────────────

        [Test]
        public void ScoreSetztSichAusDenGenanntenPostenZusammen()
        {
            var result = Climb(floors: 4, bosses: 1, enemies: 20, shards: 50, extracted: true);
            var raw = 4 * ClimbScore.PerFloor + 1 * ClimbScore.PerBoss
                      + 20 * ClimbScore.PerEnemy + 50 * ClimbScore.PerShard;
            var expected = (int)Math.Round(raw * (double)ClimbScore.ExtractedMultiplier,
                MidpointRounding.AwayFromZero);
            Assert.That(ClimbScore.Evaluate(result), Is.EqualTo(expected).Within(1));
        }

        [Test]
        public void ExtrahierenZaehltMehrAlsFallen()
        {
            var extracted = ClimbScore.Evaluate(Climb(6, 1, 40, 60, true));
            var fallen = ClimbScore.Evaluate(Climb(6, 1, 40, 60, false));
            Assert.That(extracted, Is.GreaterThan(fallen),
                "Extract muss sich lohnen, sonst ist die Entscheidung am Boss-Ende bedeutungslos.");
        }

        [Test]
        public void SummeDerAufschluesselungEntsprichtDemRohwert()
        {
            var result = Climb(7, 1, 33, 41, true);
            var sum = 0;
            foreach (var entry in ClimbScore.Breakdown(result)) sum += entry.Points;
            // Der Rohwert vor dem Multiplikator muss exakt der Aufschluesselung
            // entsprechen, sonst zeigt der Endbildschirm eine Rechnung, die nicht aufgeht.
            var raw = 7 * ClimbScore.PerFloor + ClimbScore.PerBoss
                      + 33 * ClimbScore.PerEnemy + 41 * ClimbScore.PerShard;
            Assert.That(sum, Is.EqualTo(raw));
        }

        [Test]
        public void EinLeererAufstiegGibtKeinePunkte()
            => Assert.That(ClimbScore.Evaluate(Climb(0, 0, 0, 0, false)), Is.Zero);

        // ── Rang ─────────────────────────────────────────────────

        [Test]
        public void RangPunkteNehmenNurDieDreiBestenAufstiege()
        {
            var scores = new List<int> { 5000, 100, 9000, 200, 7000, 50 };
            Assert.That(RankTable.PointsFrom(scores), Is.EqualTo(9000 + 7000 + 5000));
        }

        [Test]
        public void EinSchlechterAufstiegVerschlechtertDenRangNicht()
        {
            var good = new List<int> { 20000, 18000, 16000 };
            var before = RankTable.PointsFrom(good);
            good.Add(120);
            Assert.That(RankTable.PointsFrom(good), Is.EqualTo(before),
                "Ein misslungener Run darf nur nicht zaehlen - er darf nichts wegnehmen.");
        }

        [Test]
        public void RangstufenSteigenMonotonMitDenPunkten()
        {
            var previous = -1;
            for (var points = 0; points <= 140000; points += 500)
            {
                var tier = (int)RankTable.TierFor(points);
                Assert.That(tier, Is.GreaterThanOrEqualTo(previous),
                    $"Bei {points} Punkten faellt die Rangstufe wieder ab.");
                previous = tier;
            }
        }

        [Test]
        public void JedeRangstufeIstErreichbarUndTraegtEigeneBelohnung()
        {
            var seenTokens = new HashSet<int>();
            foreach (RankTier tier in Enum.GetValues(typeof(RankTier)))
            {
                Assert.That(RankTable.TierFor(RankTable.ThresholdOf(tier)), Is.EqualTo(tier),
                    $"Die Schwelle von {RankTable.Name(tier)} landet nicht auf dieser Stufe.");
                Assert.That(seenTokens.Add(RankTable.TokensFor(tier)), Is.True,
                    $"{RankTable.Name(tier)} zahlt dieselben Tokens wie eine andere Stufe.");
            }
        }

        [Test]
        public void FortschrittImRangLaeuftVonNullBisEins()
        {
            Assert.That(RankTable.ProgressInTier(0), Is.EqualTo(0f).Within(0.001f));
            var ember = RankTable.ThresholdOf(RankTier.Ember);
            var iron = RankTable.ThresholdOf(RankTier.Iron);
            Assert.That(RankTable.ProgressInTier((ember + iron) / 2), Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(RankTable.ProgressInTier(RankTable.ThresholdOf(RankTier.Spire) + 50000),
                Is.EqualTo(1f).Within(0.001f), "Im hoechsten Rang ist der Balken voll.");
            Assert.That(RankTable.PointsToNext(RankTable.ThresholdOf(RankTier.Spire)), Is.Zero);
        }

        [Test]
        public void DreiVolleDurchgaengeErreichenDenHoechstenRang()
        {
            // Kalibrierungsnachweis: ein kompletter Standard-Spire bis Etage 15 mit
            // drei Bossen soll dreimal gespielt an die Spitze der Leiter fuehren.
            // Waere das unerreichbar, waere der hoechste Rang Dekoration.
            var full = ClimbScore.Evaluate(Climb(15, 3, 200, 300, true));
            var points = RankTable.PointsFrom(new List<int> { full, full, full });
            Assert.That(RankTable.TierFor(points), Is.EqualTo(RankTier.Spire),
                $"Drei Volldurchgaenge ergeben {points} Punkte, das reicht nicht fuer SPIRE.");
        }

        [Test]
        public void EinKurzerAufstiegBleibtImUnterstenRang()
        {
            var short1 = ClimbScore.Evaluate(Climb(2, 0, 12, 12, false));
            Assert.That(RankTable.TierFor(RankTable.PointsFrom(new List<int> { short1 })),
                Is.EqualTo(RankTier.Splinter));
        }

        // ── Shift ────────────────────────────────────────────────

        [Test]
        public void ShiftIndexWaechstJedeSiebenTage()
        {
            var start = ShiftCalendar.StartOf(0);
            Assert.That(ShiftCalendar.IndexFor(start), Is.EqualTo(0));
            Assert.That(ShiftCalendar.IndexFor(start.AddDays(6)), Is.EqualTo(0));
            Assert.That(ShiftCalendar.IndexFor(start.AddDays(7)), Is.EqualTo(1));
            Assert.That(ShiftCalendar.IndexFor(start.AddDays(20)), Is.EqualTo(2));
        }

        [Test]
        public void ShiftBeginntAmMontag()
        {
            Assert.That(ShiftCalendar.StartOf(0).DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(ShiftCalendar.StartOf(37).DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        }

        [Test]
        public void VorDerEpocheGibtEsKeineNegativenShifts()
            => Assert.That(ShiftCalendar.IndexFor(ShiftCalendar.StartOf(0).AddDays(-90)), Is.Zero);

        [Test]
        public void RestlaufzeitFaelltInnerhalbEinesShiftsMonotonAufNull()
        {
            var start = ShiftCalendar.StartOf(5);
            var full = ShiftCalendar.RemainingIn(start);
            Assert.That(full.TotalDays, Is.EqualTo(ShiftCalendar.DaysPerShift).Within(0.001));
            Assert.That(ShiftCalendar.RemainingIn(start.AddDays(3)).TotalDays,
                Is.LessThan(full.TotalDays));
            Assert.That(ShiftCalendar.RemainingIn(ShiftCalendar.EndOf(5)).TotalSeconds,
                Is.EqualTo(ShiftCalendar.DaysPerShift * 86400d).Within(1d),
                "Genau am Wechsel laeuft bereits der naechste Shift.");
        }

        [Test]
        public void CountdownWirdNieNegativAngezeigt()
            => Assert.That(ShiftCalendar.Countdown(TimeSpan.FromSeconds(-500)), Is.EqualTo("ENDED"));
    }
}
