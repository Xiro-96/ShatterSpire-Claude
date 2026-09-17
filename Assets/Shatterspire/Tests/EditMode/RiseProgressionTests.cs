using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der Held waechst in den Turm hinein, statt ihn von Anfang an zu beherrschen.
    ///
    /// Der Anlass: "das Spiel fuehlt sich leicht an, das sollte es am Anfang nicht - spaeter mit
    /// vielen Boni ja". In Project R.I.S.E. schaltet das Aufsteigen die Ultimate frei, die Pfade
    /// werden schwerer und laenger, und der naechste Pfad oeffnet sich erst nach dem vorigen. Diese
    /// Tests halten die drei Regeln fest, die das hier umsetzen.
    /// </summary>
    public sealed class RiseProgressionTests
    {
        private static readonly RunMode[] Paths = { RunMode.Brave, RunMode.Heroic, RunMode.Legendary };

        // ── Die Ultimate wird verdient ──────────────────────────────────────

        [Test]
        public void DieUltimateIstAufDenErstenEtagenGesperrt()
        {
            for (var floor = 1; floor < UltimateProgression.UnlockFloor; floor++)
            {
                Assert.That(UltimateProgression.IsUnlocked(floor), Is.False, $"Etage {floor}");
                Assert.That(UltimateProgression.Power(floor), Is.EqualTo(0f), $"Etage {floor}");
            }
            Assert.That(UltimateProgression.IsUnlocked(UltimateProgression.UnlockFloor), Is.True);
        }

        [Test]
        public void DieUltimateWaechstMitDemLaufUndBleibtBeiVollerStaerke()
        {
            var previous = 0f;
            for (var floor = UltimateProgression.UnlockFloor; floor <= 30; floor++)
            {
                var power = UltimateProgression.Power(floor);
                Assert.That(power, Is.GreaterThanOrEqualTo(previous), $"Etage {floor} schwaecher als davor");
                Assert.That(power, Is.LessThanOrEqualTo(1f), $"Etage {floor} ueber voller Staerke");
                previous = power;
            }
            Assert.That(UltimateProgression.Power(UltimateProgression.UnlockFloor),
                Is.EqualTo(UltimateProgression.StartPower).Within(0.0001f), "Frisch verdient ist sie schwaecher.");
            Assert.That(UltimateProgression.Power(UltimateProgression.FullPowerFloor), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(UltimateProgression.Power(UltimateProgression.FullPowerFloor - 1), Is.LessThan(1f));
        }

        /// <summary>Ein Upgrade fuer eine Ultimate, die es noch nicht gibt, waere eine leere Karte.</summary>
        [Test]
        public void VorDerFreischaltungKommtKeinUltimateUpgrade()
        {
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            for (var seed = 0; seed < 150; seed++)
            {
                var roll = PerkCatalog.RollThree(hero, new HashSet<PerkId>(), new Random(seed), 1, null,
                    ultimateUnlocked: false);
                Assert.That(roll, Has.Count.EqualTo(3), $"{hero} Seed {seed}");
                Assert.That(roll.Any(perk => perk.Slot == ActionSlot.Ultimate), Is.False,
                    $"{hero} Seed {seed}: Ultimate-Upgrade vor der Freischaltung");
            }
        }

        [Test]
        public void NachDerFreischaltungKommenUltimateUpgradesWieder()
        {
            var seen = 0;
            for (var seed = 0; seed < 300; seed++)
                if (PerkCatalog.RollThree(HeroClassId.Ranger, new HashSet<PerkId>(), new Random(seed), 5, null,
                        ultimateUnlocked: true).Any(perk => perk.Slot == ActionSlot.Ultimate))
                    seen++;
            Assert.That(seen, Is.GreaterThan(0));
        }

        // ── Pfade haben ihre eigene Schwierigkeit ───────────────────────────

        [Test]
        public void JederSchwerePfadIstAufJederEtageZaeher()
        {
            for (var floor = 1; floor <= 20; floor++)
            {
                Assert.That(EnemyBalance.HealthScale(RunMode.Heroic, floor),
                    Is.GreaterThan(EnemyBalance.HealthScale(RunMode.Brave, floor)), $"Leben Etage {floor}");
                Assert.That(EnemyBalance.HealthScale(RunMode.Legendary, floor),
                    Is.GreaterThan(EnemyBalance.HealthScale(RunMode.Heroic, floor)), $"Leben Etage {floor}");
                Assert.That(EnemyBalance.DamageScale(RunMode.Heroic, floor),
                    Is.GreaterThan(EnemyBalance.DamageScale(RunMode.Brave, floor)), $"Schaden Etage {floor}");
                Assert.That(EnemyBalance.DamageScale(RunMode.Legendary, floor),
                    Is.GreaterThan(EnemyBalance.DamageScale(RunMode.Heroic, floor)), $"Schaden Etage {floor}");
            }
        }

        /// <summary>Vorher war Etage 1 der Tiefpunkt: Faktor 1, auf jedem Pfad.</summary>
        [Test]
        public void SchonEtageEinsIstHaerterAlsFrueher()
        {
            foreach (var path in Paths)
            {
                Assert.That(EnemyBalance.HealthScale(path, 1), Is.GreaterThan(1f), path.ToString());
                Assert.That(EnemyBalance.DamageScale(path, 1), Is.GreaterThan(1f), path.ToString());
            }
        }

        [Test]
        public void GegnerWerdenMitJederEtageZaeher()
        {
            foreach (var path in Paths)
            for (var floor = 2; floor <= 20; floor++)
            {
                Assert.That(EnemyBalance.HealthScale(path, floor), Is.GreaterThan(EnemyBalance.HealthScale(path, floor - 1)));
                Assert.That(EnemyBalance.DamageScale(path, floor), Is.GreaterThan(EnemyBalance.DamageScale(path, floor - 1)));
            }
        }

        // ── Pfade oeffnen sich nacheinander ─────────────────────────────────

        private static ClimbResult Run(RunMode path, HeroClassId hero, int floors, bool extracted = true)
            => new(floors, floors / 5, floors * 8, 100, extracted, path, hero);

        [Test]
        public void EinNeuerSpielstandKenntNurBrave()
        {
            var save = new MetaSaveData();
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Brave), Is.True);
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Heroic), Is.False);
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Legendary), Is.False);
            Assert.That(PathProgress.Highest(save), Is.EqualTo(RunMode.Brave));
        }

        [Test]
        public void BraveMitEinemHeldenOeffnetHeroic()
        {
            var save = new MetaSaveData();
            var unlocked = PathProgress.Record(save, Run(RunMode.Brave, HeroClassId.Guardian, 5));
            Assert.That(unlocked, Is.EqualTo(RunMode.Heroic), "Die Freischaltung wird gemeldet.");
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Heroic), Is.True);
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Legendary), Is.False);
            Assert.That(PathProgress.Record(save, Run(RunMode.Brave, HeroClassId.Ranger, 5)), Is.Null,
                "Ein zweites Brave oeffnet nichts Neues.");
        }

        [Test]
        public void LegendaryBrauchtHeroicMitZweiVerschiedenenHelden()
        {
            var save = new MetaSaveData();
            PathProgress.Record(save, Run(RunMode.Brave, HeroClassId.Paladin, 5));
            Assert.That(PathProgress.Record(save, Run(RunMode.Heroic, HeroClassId.Paladin, 15)), Is.Null);
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Legendary), Is.False, "Ein Held reicht nicht.");
            PathProgress.Record(save, Run(RunMode.Heroic, HeroClassId.Paladin, 15));
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Legendary), Is.False, "Derselbe Held zweimal zaehlt einmal.");
            Assert.That(PathProgress.Record(save, Run(RunMode.Heroic, HeroClassId.Bomber, 15)), Is.EqualTo(RunMode.Legendary));
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Legendary), Is.True);
        }

        /// <summary>Nach einem Boss aussteigen ist kein geschaffter Pfad - und Sterben erst recht nicht.</summary>
        [Test]
        public void NurDieLetzteEtageZaehltAlsGeschafft()
        {
            var save = new MetaSaveData();
            Assert.That(PathProgress.Record(save, Run(RunMode.Brave, HeroClassId.Ranger, 4)), Is.Null, "vorzeitig ausgestiegen");
            Assert.That(PathProgress.Record(save, Run(RunMode.Brave, HeroClassId.Ranger, 5, extracted: false)), Is.Null, "gefallen");
            Assert.That(PathProgress.Record(save, Run(RunMode.Heroic, HeroClassId.Ranger, 10)), Is.Null, "Heroic nicht zu Ende");
            Assert.That(PathProgress.IsUnlocked(save, RunMode.Heroic), Is.False);
            Assert.That(PathProgress.IsClear(Run(RunMode.Legendary, HeroClassId.Ranger, 60)), Is.False,
                "Legendary hat kein Ende, das man schaffen koennte.");
        }

        /// <summary>
        /// Ein Stand von vor der Sperre darf nichts verlieren, was er sich verdient hat: wer schon
        /// fuenf Etagen weit kam, hat Brave geschafft.
        /// </summary>
        [Test]
        public void AlteSpielstaendeBehaltenIhreFortschritte()
        {
            var beginner = new MetaSaveData { bestFloor = 3, lastHero = (int)HeroClassId.Guardian };
            MetaSaveSystem.GrantLegacyClears(beginner);
            Assert.That(PathProgress.Highest(beginner), Is.EqualTo(RunMode.Brave));

            var veteran = new MetaSaveData { bestFloor = 7, lastHero = (int)HeroClassId.Guardian };
            MetaSaveSystem.GrantLegacyClears(veteran);
            Assert.That(PathProgress.IsUnlocked(veteran, RunMode.Heroic), Is.True);
            Assert.That(PathProgress.HasCleared(veteran, RunMode.Brave, HeroClassId.Guardian), Is.True);

            var master = new MetaSaveData { bestFloor = 15, lastHero = (int)HeroClassId.Bomber };
            MetaSaveSystem.GrantLegacyClears(master);
            Assert.That(PathProgress.HasCleared(master, RunMode.Heroic, HeroClassId.Bomber), Is.True);
            Assert.That(PathProgress.IsUnlocked(master, RunMode.Legendary), Is.False,
                "Der alte Stand kennt nur einen Helden - fuer Legendary braucht es zwei.");
        }

        /// <summary>Ein Stand ohne das neue Feld darf nicht abstuerzen.</summary>
        [Test]
        public void EinStandOhnePfadfeldIstEinfachLeer()
        {
            var save = new MetaSaveData { pathClears = null };
            Assert.That(PathProgress.HeroesCleared(save, RunMode.Brave), Is.EqualTo(0));
            Assert.That(PathProgress.Highest(save), Is.EqualTo(RunMode.Brave));
            PathProgress.MarkCleared(save, RunMode.Heroic, HeroClassId.Paladin);
            Assert.That(PathProgress.HasCleared(save, RunMode.Heroic, HeroClassId.Paladin), Is.True);
        }
    }
}
