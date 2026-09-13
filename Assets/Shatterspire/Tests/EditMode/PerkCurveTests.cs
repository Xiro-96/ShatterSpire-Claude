using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der Aufbau eines Laufs war flach: jedes Upgrade gab es genau einmal, und die Auswahl auf
    /// Etage 14 kam aus demselben Topf mit denselben Chancen wie die auf Etage 1. Nach fuenfzehn
    /// Etagen hatte man fuenfzehn unverbundene Verbesserungen - der Lauf sammelte, statt sich zu
    /// steigern.
    ///
    /// Diese Tests sichern die zwei Gegenmittel: eine Kurve ueber die Etagen, und Upgrades, die
    /// sich vertiefen lassen.
    /// </summary>
    public sealed class PerkCurveTests
    {
        private static PlayerBuild NewBuild()
        {
            // Mit Lebenskomponente: mehrere Upgrades greifen darauf zu, und ein Aufbau ohne sie
            // gibt es im Spiel nicht.
            var probe = new GameObject("Build Probe");
            probe.AddComponent<Health>().Configure(TeamId.Player, 100f);
            var build = probe.AddComponent<PlayerBuild>();
            build.ConfigureRun(HeroClassId.Ranger, null, null);
            return build;
        }

        [TearDown]
        public void Aufraeumen()
        {
            foreach (var probe in UnityEngine.Object.FindObjectsByType<PlayerBuild>(FindObjectsSortMode.None))
                if (probe && probe.name == "Build Probe") UnityEngine.Object.DestroyImmediate(probe.gameObject);
        }

        [Test]
        public void EinVertiefbaresUpgradeSteigtImRangUndWirktStaerker()
        {
            var build = NewBuild();
            var sunder = PerkCatalog.Find(PerkId.Piercing);
            Assert.That(sunder.Stackable, Is.True, "Durchschlag muss sich vertiefen lassen.");
            Assert.That(build.Pierces, Is.Zero);

            build.Apply(sunder);
            Assert.That(build.Rank(PerkId.Piercing), Is.EqualTo(1));
            Assert.That(build.Pierces, Is.EqualTo(1));

            build.Apply(sunder);
            Assert.That(build.Rank(PerkId.Piercing), Is.EqualTo(2),
                "Der zweite Durchschlag muss ein zweiter sein, nicht derselbe noch einmal.");
            Assert.That(build.Pierces, Is.EqualTo(2));
        }

        [Test]
        public void DerHoechsteRangIstEineGrenze()
        {
            var build = NewBuild();
            var perk = PerkCatalog.Find(PerkId.Multishot);
            for (var i = 0; i < perk.MaximumRank + 4; i++) build.Apply(perk);
            Assert.That(build.Rank(perk.Id), Is.EqualTo(perk.MaximumRank),
                "Ueber den hoechsten Rang hinaus darf nichts mehr gezaehlt werden.");
            Assert.That(build.ProjectileCount, Is.EqualTo(1 + perk.MaximumRank));
        }

        [Test]
        public void EinEinmaligesUpgradeBleibtEinmalig()
        {
            var build = NewBuild();
            // Ein Upgrade, dessen Wirkung keine Zahl ist: ein zweiter Rang waere dieselbe Wirkung
            // noch einmal und damit kein Fortschritt.
            var perk = PerkCatalog.Find(PerkId.FireBullet);
            Assert.That(perk.Stackable, Is.False);
            build.Apply(perk);
            build.Apply(perk);
            Assert.That(build.Rank(perk.Id), Is.EqualTo(1));
        }

        [Test]
        public void EinVertiefbaresUpgradeKommtWiederZurAuswahl()
        {
            var build = NewBuild();
            var sunder = PerkCatalog.Find(PerkId.Piercing);
            build.Apply(sunder);
            var owned = new HashSet<PerkId>(build.Perks);

            var offeredAgain = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                var roll = PerkCatalog.RollThree(HeroClassId.Ranger, owned, new System.Random(seed), 6, build);
                if (roll.Exists(perk => perk.Id == PerkId.Piercing)) offeredAgain++;
            }
            TestContext.WriteLine($"KURVE Vertiefung angeboten: {offeredAgain} von 200 Wahlen auf Etage 6");
            Assert.That(offeredAgain, Is.GreaterThan(0),
                "Ein vertiefbares Upgrade, das man hat, muss wiederkommen koennen.");

            // Und spaet haeufiger als frueh: gegen Ende eines Laufs ist Zuspitzen die
            // interessantere Wahl, weil breites Sammeln nichts mehr freischaltet.
            var earlyOffers = OfferRate(build, owned, 2);
            var lateOffers = OfferRate(build, owned, 13);
            TestContext.WriteLine($"KURVE Vertiefung: Etage 2 {earlyOffers} von 200, Etage 13 {lateOffers} von 200");
            Assert.That(lateOffers, Is.GreaterThan(earlyOffers),
                $"Etage 2 bot {earlyOffers}, Etage 13 {lateOffers} - die Vertiefung muss spaet locken.");
        }

        private static int OfferRate(PlayerBuild build, ICollection<PerkId> owned, int floor)
        {
            var hits = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                var roll = PerkCatalog.RollThree(HeroClassId.Ranger, owned, new System.Random(seed), floor, build);
                if (roll.Exists(perk => perk.Id == PerkId.Piercing)) hits++;
            }
            return hits;
        }

        [Test]
        public void EinAusgereiztesUpgradeKommtNichtMehr()
        {
            var build = NewBuild();
            var sunder = PerkCatalog.Find(PerkId.Piercing);
            for (var i = 0; i < sunder.MaximumRank; i++) build.Apply(sunder);
            var owned = new HashSet<PerkId>(build.Perks);

            for (var seed = 0; seed < 200; seed++)
            {
                var roll = PerkCatalog.RollThree(HeroClassId.Ranger, owned, new System.Random(seed), 9, build);
                Assert.That(roll.Exists(perk => perk.Id == PerkId.Piercing), Is.False,
                    $"Seed {seed}: ein ausgereiztes Upgrade darf nicht mehr angeboten werden.");
            }
        }

        [Test]
        public void SpaeteEtagenBietenHaeufigerSeltenesAnAlsFruehe()
        {
            var early = RarePortion(1);
            var late = RarePortion(14);
            TestContext.WriteLine($"KURVE Anteil Episch/Legendaer: Etage 1 {early:P1}, Etage 14 {late:P1}");
            Assert.That(late, Is.GreaterThan(early + 0.03f),
                $"Etage 1 bietet {early:P1}, Etage 14 {late:P1} - das ist keine Kurve.");
            // Und die fruehe Etage darf nicht leer ausgehen: eine Wahl ohne jedes Seltene waere
            // vierzehn Etagen lang dieselbe Wahl.
            Assert.That(early, Is.GreaterThan(0.05f));
        }

        private static float RarePortion(int floor)
        {
            var rare = 0;
            var total = 0;
            for (var seed = 0; seed < 600; seed++)
            {
                foreach (var perk in PerkCatalog.RollThree(HeroClassId.Ranger, null, new System.Random(seed), floor, null))
                {
                    if (perk.Rarity is PerkRarity.Epic or PerkRarity.Legendary) rare++;
                    total++;
                }
            }
            return rare / (float)total;
        }

        [Test]
        public void DieKarteSagtWasSieFuerDenAufbauBedeutet()
        {
            var build = NewBuild();
            var sunder = PerkCatalog.Find(PerkId.Piercing);
            Assert.That(PerkCatalog.MeaningFor(sunder, build), Is.Empty,
                "Ein noch nicht genommenes Upgrade ohne Fusion hat nichts zu sagen.");

            build.Apply(sunder);
            Assert.That(PerkCatalog.MeaningFor(sunder, build), Does.Contain("II"),
                "Beim zweiten Mal muss der naechste Rang auf der Karte stehen.");

            // Fusion: Feuer plus Sprengschuss ergibt Inferno.
            var fresh = NewBuild();
            fresh.Apply(PerkCatalog.Find(PerkId.ExplosiveShot));
            Assert.That(PerkCatalog.FusionCompletedBy(PerkId.FireBullet, fresh), Is.EqualTo("INFERNO"));
            Assert.That(PerkCatalog.FusionCompletedBy(PerkId.IceBullet, fresh), Is.Null);
        }

        [Test]
        public void EinGanzerLaufFuehrtZuEinemTieferenAufbauAlsVorher()
        {
            // Fuenfzehn Etagen, jedes Mal die erste Karte genommen. Frueher waren das fuenfzehn
            // verschiedene Upgrades mit Rang 1; jetzt darf sich derselbe Aufbau zuspitzen.
            var build = NewBuild();
            var random = new System.Random(4711);
            for (var floor = 1; floor <= 15; floor++)
            {
                var roll = PerkCatalog.RollThree(HeroClassId.Ranger, new HashSet<PerkId>(build.Perks),
                    random, floor, build);
                Assert.That(roll.Count, Is.EqualTo(3), $"Etage {floor} bot nur {roll.Count} Karten an.");
                build.Apply(roll[0]);
            }
            Assert.That(build.TotalRanks, Is.EqualTo(15), "Fuenfzehn Etagen, fuenfzehn Verbesserungen.");
            TestContext.WriteLine($"KURVE Nach 15 Etagen: {build.Perks.Count} verschiedene Upgrades, "
                                  + $"{build.TotalRanks} Raenge, Schaden x{build.DamageMultiplier:0.00}");
        }
    }
}
