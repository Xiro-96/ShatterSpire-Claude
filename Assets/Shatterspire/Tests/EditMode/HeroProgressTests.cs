using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Dauerhafter Fortschritt je Held: die Kurve, der Aufschlag und der Weg in den Spielstand.
    ///
    /// Der Anlass: aller Fortschritt zwischen den Laeufen war gemeinsam - wer den Waechter spielte,
    /// machte damit auch den Jaeger staerker. Ein Lieblingsheld hatte keine Spur im Spielstand.
    /// </summary>
    public sealed class HeroProgressTests
    {
        [Test]
        public void DieKurveSteigtUndEndetAufDerHoechstenStufe()
        {
            var previous = 0;
            for (var level = 1; level < HeroProgress.MaximumLevel; level++)
            {
                var cost = HeroProgress.StepCost(level);
                Assert.That(cost, Is.GreaterThan(previous),
                    $"Stufe {level} kostet {cost}, Stufe {level - 1} kostete {previous}.");
                previous = cost;
            }
            Assert.That(HeroProgress.StepCost(HeroProgress.MaximumLevel), Is.Zero,
                "Ueber der hoechsten Stufe gibt es nichts mehr zu kaufen.");
            Assert.That(HeroProgress.StepCost(0), Is.EqualTo(HeroProgress.StepCost(1)),
                "Ein unsinniger Wert darf nicht in negative Kosten laufen.");
        }

        [Test]
        public void StufeUndErfahrungPassenZueinander()
        {
            Assert.That(HeroProgress.LevelFor(0), Is.EqualTo(1));
            Assert.That(HeroProgress.LevelFor(-500), Is.EqualTo(1), "Negatives darf nicht durchschlagen.");
            for (var level = 1; level <= HeroProgress.MaximumLevel; level++)
            {
                var total = HeroProgress.TotalFor(level);
                Assert.That(HeroProgress.LevelFor(total), Is.EqualTo(level),
                    $"Bei genau {total} Erfahrung muss Stufe {level} gelten.");
                if (level > 1)
                    Assert.That(HeroProgress.LevelFor(total - 1), Is.EqualTo(level - 1),
                        $"Eine Erfahrung unter der Schwelle darf Stufe {level} noch nicht geben.");
            }
            Assert.That(HeroProgress.LevelFor(int.MaxValue / 2), Is.EqualTo(HeroProgress.MaximumLevel));
        }

        [Test]
        public void DerBalkenLaeuftVonNullBisEinsUndBleibtObenStehen()
        {
            for (var level = 1; level < HeroProgress.MaximumLevel; level++)
            {
                var total = HeroProgress.TotalFor(level);
                Assert.That(HeroProgress.ProgressInLevel(total), Is.EqualTo(0f).Within(0.001f),
                    $"Frisch auf Stufe {level} muss der Balken leer sein.");
                var almost = total + HeroProgress.StepCost(level) - 1;
                Assert.That(HeroProgress.ProgressInLevel(almost), Is.GreaterThan(0.95f));
                Assert.That(HeroProgress.ToNextLevel(total), Is.EqualTo(HeroProgress.StepCost(level)));
            }
            var top = HeroProgress.TotalFor(HeroProgress.MaximumLevel);
            Assert.That(HeroProgress.ProgressInLevel(top), Is.EqualTo(1f));
            Assert.That(HeroProgress.ToNextLevel(top), Is.Zero);
        }

        [Test]
        public void DerAufschlagBleibtBescheiden()
        {
            // Der Unterschied soll spuerbar sein und kein Ersatz fuers Spielen. Waere er groesser,
            // haenge der Erfolg eines Laufs mehr an der Stufe als an der Hand am Bildschirm.
            Assert.That(HeroProgress.HealthBonus(1), Is.Zero);
            Assert.That(HeroProgress.DamageBonus(1), Is.Zero);
            Assert.That(HeroProgress.HealthBonus(HeroProgress.MaximumLevel), Is.LessThanOrEqualTo(0.4f));
            Assert.That(HeroProgress.DamageBonus(HeroProgress.MaximumLevel), Is.LessThanOrEqualTo(0.35f));
            Assert.That(HeroProgress.HealthBonus(99), Is.EqualTo(HeroProgress.HealthBonus(HeroProgress.MaximumLevel)),
                "Ueber der hoechsten Stufe darf nichts weiterwachsen.");
        }

        [Test]
        public void JederRangHatEinenEigenenNamenUndEineEigeneFarbe()
        {
            var titles = new HashSet<string>();
            for (var level = 1; level <= HeroProgress.MaximumLevel; level++) titles.Add(HeroProgress.Title(level));
            Assert.That(titles.Count, Is.EqualTo(5), "Fuenf Raenge: " + string.Join(", ", titles));
            Assert.That(HeroProgress.Title(1), Is.EqualTo("NOVICE"));
            Assert.That(HeroProgress.Title(HeroProgress.MaximumLevel), Is.EqualTo("LEGEND"));
            var colors = new HashSet<Color>();
            foreach (var level in new[] { 1, 3, 5, 10, HeroProgress.MaximumLevel })
                colors.Add(HeroProgress.TitleColor(level));
            Assert.That(colors.Count, Is.EqualTo(5), "Jeder Rang braucht seine eigene Farbe.");
        }

        [Test]
        public void EinVollerAufstiegBringtEineSpuerbareStufe()
        {
            // Kalibriert am Standard-Spire: ein Durchgang bis Etage 15 mit drei Bossen liegt bei
            // rund 32 000 Punkten. Bleibt die Erfahrung daraus zu klein, ist die Stufe unerreichbar;
            // wird sie zu gross, ist die Kurve nach zwei Laeufen ausgereizt.
            var full = new ClimbResult(15, 3, 400, 600, true, RunMode.Heroic, HeroClassId.Guardian, 0, 0);
            var gained = HeroProgress.ExperienceFor(full);
            var level = HeroProgress.LevelFor(gained);
            TestContext.WriteLine($"STUFEN Voller Aufstieg: {gained} Erfahrung, Stufe {level}, "
                                  + $"ganzer Weg {HeroProgress.TotalFor(HeroProgress.MaximumLevel)}");
            Assert.That(level, Is.InRange(2, 5),
                $"Ein voller Aufstieg bringt Stufe {level} - zu wenig oder zu viel auf einmal.");
            var runsToMax = HeroProgress.TotalFor(HeroProgress.MaximumLevel) / (float)Mathf.Max(1, gained);
            TestContext.WriteLine($"STUFEN Volle Aufstiege bis zur Hoechststufe: {runsToMax:0.0}");
            Assert.That(runsToMax, Is.InRange(8f, 60f),
                $"{runsToMax:0} volle Aufstiege bis Stufe {HeroProgress.MaximumLevel}.");
        }

        [Test]
        public void EinGescheiterterLaufBringtWenigerAlsEinGelungener()
        {
            var won = new ClimbResult(10, 2, 200, 300, true, RunMode.Heroic, HeroClassId.Ranger, 0, 0);
            var lost = new ClimbResult(10, 2, 200, 300, false, RunMode.Heroic, HeroClassId.Ranger, 0, 0);
            Assert.That(HeroProgress.ExperienceFor(lost), Is.LessThan(HeroProgress.ExperienceFor(won)));
            var nothing = new ClimbResult(0, 0, 0, 0, false, RunMode.Brave, HeroClassId.Ranger, 0, 0);
            Assert.That(HeroProgress.ExperienceFor(nothing), Is.Zero);
        }

        [Test]
        public void ErfahrungLandetBeimRichtigenHelden()
        {
            var data = new MetaSaveData();
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
                Assert.That(MetaSaveSystem.HeroExperience(data, hero), Is.Zero,
                    "Ein frischer Stand kennt noch keinen Helden.");
            Assert.That(MetaSaveSystem.HeroLevel(data, HeroClassId.Guardian), Is.EqualTo(1));

            // Ein alter Stand kennt neuere Helden nicht: die Liste ist kuerzer als die Aufzaehlung.
            data.heroExperience = new[] { 5000 };
            Assert.That(MetaSaveSystem.HeroExperience(data, HeroClassId.Ranger), Is.EqualTo(5000));
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            {
                if (hero == HeroClassId.Ranger) continue;
                Assert.That(MetaSaveSystem.HeroExperience(data, hero), Is.Zero,
                    $"{hero} steht nicht in der Liste und muss trotzdem sauber 0 liefern.");
            }
        }
    }
}
