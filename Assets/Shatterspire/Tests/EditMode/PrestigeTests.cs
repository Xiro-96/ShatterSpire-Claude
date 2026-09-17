using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Prestige je Held, beim Schmied gekauft - wie in Project R.I.S.E.
    ///
    /// Der Anlass: "mehr Richtung R.I.S.E.", und dort werden Helden ueber fuenf Raenge mit je fuenf
    /// Stufen staerker, bezahlt mit Abzeichen von den Etagen-Meilensteinen. Diese Tests halten die
    /// Regeln fest - vor allem die, die sich still verschieben koennten: Freischaltungen an der
    /// falschen Stufe, Kosten, die sinken, und alte Spielstaende, die etwas verlieren.
    /// </summary>
    public sealed class PrestigeTests
    {
        // ── Raenge ──────────────────────────────────────────────────────────

        [Test]
        public void SchritteVerteilenSichAufFuenfRaengeMitJeFuenfStufen()
        {
            Assert.That((HeroPrestige.Rank(0), HeroPrestige.SubStep(0)), Is.EqualTo((0, 0)));
            Assert.That((HeroPrestige.Rank(1), HeroPrestige.SubStep(1)), Is.EqualTo((1, 1)));
            Assert.That((HeroPrestige.Rank(5), HeroPrestige.SubStep(5)), Is.EqualTo((1, 5)));
            Assert.That((HeroPrestige.Rank(6), HeroPrestige.SubStep(6)), Is.EqualTo((2, 1)));
            Assert.That((HeroPrestige.Rank(25), HeroPrestige.SubStep(25)), Is.EqualTo((5, 5)));
            Assert.That(HeroPrestige.Rank(99), Is.EqualTo(5), "Ueber dem Maximum bleibt es beim letzten Rang.");
        }

        [Test]
        public void JederRangHatEinenEigenenNamen()
        {
            var names = new[] { 0, 1, 6, 11, 16, 21 }.Select(HeroPrestige.RankName).ToList();
            Assert.That(names.Distinct().Count(), Is.EqualTo(6), string.Join(", ", names));
        }

        // ── Kosten und Werte ────────────────────────────────────────────────

        [Test]
        public void DieKostenSteigenUndEndenAmMaximum()
        {
            for (var step = 1; step < HeroPrestige.MaximumStep; step++)
            {
                Assert.That(HeroPrestige.ShardCost(step), Is.GreaterThan(HeroPrestige.ShardCost(step - 1)), $"Splitter {step}");
                Assert.That(HeroPrestige.BadgeCost(step), Is.GreaterThanOrEqualTo(HeroPrestige.BadgeCost(step - 1)), $"Abzeichen {step}");
            }
            Assert.That(HeroPrestige.ShardCost(HeroPrestige.MaximumStep), Is.Zero);
            Assert.That(HeroPrestige.BadgeCost(HeroPrestige.MaximumStep), Is.Zero);
        }

        /// <summary>Der ganze Weg in Zahlen - aendert sich einer, soll das eine bewusste Entscheidung sein.</summary>
        [Test]
        public void DerGanzeWegKostetSoVielWieGeplant()
        {
            var shards = 0;
            var badges = 0;
            for (var step = 0; step < HeroPrestige.MaximumStep; step++)
            {
                shards += HeroPrestige.ShardCost(step);
                badges += HeroPrestige.BadgeCost(step);
            }
            Assert.That(shards, Is.EqualTo(7250));
            Assert.That(badges, Is.EqualTo(150));
        }

        [Test]
        public void DieWerteWachsenBisZumMaximumUndNichtDarueber()
        {
            Assert.That(HeroPrestige.HealthBonus(0), Is.Zero);
            Assert.That(HeroPrestige.DamageBonus(0), Is.Zero);
            Assert.That(HeroPrestige.HealthBonus(25), Is.EqualTo(0.375f).Within(0.0001f));
            Assert.That(HeroPrestige.DamageBonus(25), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(HeroPrestige.DamageBonus(40), Is.EqualTo(HeroPrestige.DamageBonus(25)));
        }

        // ── Freischaltungen ─────────────────────────────────────────────────

        [Test]
        public void JederVolleRangSchaltetGenauAnSeinerStufeFrei()
        {
            Assert.That(HeroPrestige.HasStartUpgrade(4), Is.False);
            Assert.That(HeroPrestige.HasStartUpgrade(5), Is.True);

            Assert.That(HeroPrestige.UltimateUnlockFloor(9), Is.EqualTo(UltimateProgression.UnlockFloor));
            Assert.That(HeroPrestige.UltimateUnlockFloor(10), Is.EqualTo(UltimateProgression.UnlockFloor - 1));

            Assert.That(HeroPrestige.UpgradeChoices(14), Is.EqualTo(3));
            Assert.That(HeroPrestige.UpgradeChoices(15), Is.EqualTo(4));

            Assert.That(HeroPrestige.UltimateStartPower(19), Is.EqualTo(UltimateProgression.StartPower));
            Assert.That(HeroPrestige.UltimateStartPower(20), Is.GreaterThan(UltimateProgression.StartPower));

            Assert.That(HeroPrestige.ExtraDashCharges(24), Is.Zero);
            Assert.That(HeroPrestige.ExtraDashCharges(25), Is.EqualTo(1));
        }

        [Test]
        public void JedeFreischaltungHatEinenTextUndWirdAngekuendigt()
        {
            foreach (var step in new[] { 5, 10, 15, 20, 25 })
                Assert.That(HeroPrestige.UnlockAt(step), Is.Not.Empty, $"Schritt {step}");
            Assert.That(HeroPrestige.UnlockAt(7), Is.Empty);
            Assert.That(HeroPrestige.NextUnlockStep(0), Is.EqualTo(5));
            Assert.That(HeroPrestige.NextUnlockStep(5), Is.EqualTo(10));
            Assert.That(HeroPrestige.NextUnlockStep(24), Is.EqualTo(25));
            Assert.That(HeroPrestige.NextUnlockStep(25), Is.Zero);
        }

        /// <summary>Ein Veteran bekommt die Ultimate eine Etage frueher, ein Champion staerker.</summary>
        [Test]
        public void PrestigeVerschiebtDieUltimate()
        {
            Assert.That(UltimateProgression.IsUnlocked(2), Is.False);
            Assert.That(UltimateProgression.IsUnlocked(2, HeroPrestige.UltimateUnlockFloor(10)), Is.True);
            Assert.That(UltimateProgression.Power(2, HeroPrestige.UltimateUnlockFloor(10)),
                Is.EqualTo(UltimateProgression.StartPower).Within(0.0001f));
            Assert.That(UltimateProgression.Power(3, UltimateProgression.UnlockFloor, HeroPrestige.UltimateStartPower(20)),
                Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void EinMeisterWaehltAusVierKarten()
        {
            for (var seed = 0; seed < 100; seed++)
            {
                var roll = PerkCatalog.RollChoices(HeroClassId.Guardian, new HashSet<PerkId>(), new Random(seed), 4, null,
                    true, HeroPrestige.UpgradeChoices(15));
                Assert.That(roll, Has.Count.EqualTo(4), $"Seed {seed}");
                Assert.That(roll.Select(perk => perk.Id).Distinct().Count(), Is.EqualTo(4), $"Seed {seed}");
            }
        }

        // ── Abzeichen ───────────────────────────────────────────────────────

        [Test]
        public void AbzeichenGibtEsNurAnDenBossEtagen()
        {
            Assert.That(HeroPrestige.BadgesForFloor(4), Is.Zero);
            Assert.That(HeroPrestige.BadgesForFloor(5), Is.EqualTo(3));
            Assert.That(HeroPrestige.BadgesForFloor(10), Is.EqualTo(4));
            Assert.That(HeroPrestige.BadgesForFloor(15), Is.EqualTo(5));
            Assert.That(HeroPrestige.BadgesForClimb(4), Is.Zero, "Vor dem ersten Boss nichts.");
            Assert.That(HeroPrestige.BadgesForClimb(14), Is.EqualTo(7));
            Assert.That(HeroPrestige.BadgesForClimb(15), Is.EqualTo(12), "Ein voller Heroic-Aufstieg.");
        }

        [Test]
        public void AbzeichenGehoerenDemHeldenDerSieVerdient()
        {
            var save = new MetaSaveData();
            MetaSaveSystem.AddBadges(save, HeroClassId.Paladin, 7);
            Assert.That(MetaSaveSystem.Badges(save, HeroClassId.Paladin), Is.EqualTo(7));
            Assert.That(MetaSaveSystem.Badges(save, HeroClassId.Ranger), Is.Zero);
        }

        // ── Kaufen ──────────────────────────────────────────────────────────

        [Test]
        public void OhneSplitterUndAbzeichenGibtEsNichts()
        {
            var save = new MetaSaveData { shards = 49 };
            MetaSaveSystem.AddBadges(save, HeroClassId.Bomber, 10);
            Assert.That(MetaSaveSystem.TryBuyPrestige(save, HeroClassId.Bomber), Is.False, "ein Splitter zu wenig");
            save.shards = 500;
            Assert.That(MetaSaveSystem.TryBuyPrestige(save, HeroClassId.Ranger), Is.False, "keine Abzeichen fuer diesen Helden");
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Ranger), Is.Zero);
        }

        [Test]
        public void EinKaufZiehtGenauDieKostenAb()
        {
            var save = new MetaSaveData { shards = 500 };
            MetaSaveSystem.AddBadges(save, HeroClassId.Bomber, 10);
            Assert.That(MetaSaveSystem.TryBuyPrestige(save, HeroClassId.Bomber), Is.True);
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Bomber), Is.EqualTo(1));
            Assert.That(save.shards, Is.EqualTo(500 - HeroPrestige.ShardCost(0)));
            Assert.That(MetaSaveSystem.Badges(save, HeroClassId.Bomber), Is.EqualTo(10 - HeroPrestige.BadgeCost(0)));
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Guardian), Is.Zero, "Nur dieser Held steigt.");
        }

        [Test]
        public void UeberDasMaximumHinausGibtEsNichts()
        {
            var save = new MetaSaveData { shards = 100000, heroPrestige = new[] { HeroPrestige.MaximumStep } };
            MetaSaveSystem.AddBadges(save, HeroClassId.Ranger, 500);
            Assert.That(MetaSaveSystem.CanBuyPrestige(save, HeroClassId.Ranger), Is.False);
            Assert.That(MetaSaveSystem.TryBuyPrestige(save, HeroClassId.Ranger), Is.False);
            Assert.That(save.shards, Is.EqualTo(100000));
        }

        // ── Alte Spielstaende ───────────────────────────────────────────────

        [Test]
        public void AlteHeldenstufenWerdenZuPrestige()
        {
            Assert.That(HeroPrestige.StepsFromLegacyLevel(1), Is.Zero);
            Assert.That(HeroPrestige.StepsFromLegacyLevel(HeroProgress.MaximumLevel), Is.EqualTo(HeroPrestige.MaximumStep),
                "Wer ganz oben war, bleibt ganz oben.");
            var previous = 0;
            for (var level = 1; level <= HeroProgress.MaximumLevel; level++)
            {
                var steps = HeroPrestige.StepsFromLegacyLevel(level);
                Assert.That(steps, Is.GreaterThanOrEqualTo(previous), $"Stufe {level}");
                previous = steps;
            }
        }

        [Test]
        public void DieUmrechnungNimmtNieEtwasWeg()
        {
            var experience = new int[Enum.GetValues(typeof(HeroClassId)).Length];
            experience[(int)HeroClassId.Guardian] = HeroProgress.TotalFor(4);
            var save = new MetaSaveData { heroExperience = experience };
            save.heroPrestige = new int[experience.Length];
            save.heroPrestige[(int)HeroClassId.Arcanist] = 12;

            MetaSaveSystem.GrantPrestigeFromExperience(save);
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Guardian), Is.EqualTo(HeroPrestige.StepsFromLegacyLevel(4)));
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Arcanist), Is.EqualTo(12),
                "Hoeheres Prestige bleibt, auch wenn die alte Stufe niedriger war.");
            Assert.That(MetaSaveSystem.PrestigeStep(save, HeroClassId.Ranger), Is.Zero);
        }
    }
}
