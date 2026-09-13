using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Auf Etage 5, 10 und 15 stand dreimal derselbe Iron Warden - dreimal derselbe Hoehepunkt je
    /// Aufstieg. Diese Tests halten fest, dass ein Heroic-Pfad jetzt drei verschiedene Kaempfe hat
    /// und dass jeder Waechter vollstaendig eingetragen ist.
    /// </summary>
    public sealed class BossRotationTests
    {
        private static IEnumerable<int> BossFloors(int upTo)
        {
            for (var floor = 1; floor <= upTo; floor++)
                if (PathCatalog.IsBossFloor(floor)) yield return floor;
        }

        [Test]
        public void EinHeroicAufstiegZeigtDreiVerschiedeneWaechter()
        {
            var seen = new List<EnemyKind>();
            foreach (var floor in BossFloors(PathCatalog.FloorCount(RunMode.Heroic)))
                seen.Add(PathCatalog.BossFor(floor));
            Assert.That(seen.Count, Is.EqualTo(3), "Heroic hat drei Boss-Etagen.");
            CollectionAssert.AllItemsAreUnique(seen,
                "Dreimal derselbe Waechter ist dreimal derselbe Hoehepunkt: " + string.Join(", ", seen));
        }

        [Test]
        public void DieFolgeHaengtAnDerEtageUndNichtAmZufall()
        {
            // Dass Etage 10 der Zwilling ist, soll man lernen und sich darauf einstellen koennen.
            for (var floor = 5; floor <= 60; floor += 5)
                Assert.That(PathCatalog.BossFor(floor), Is.EqualTo(PathCatalog.BossFor(floor)));
            Assert.That(PathCatalog.BossFor(5), Is.EqualTo(EnemyKind.IronWarden));
            Assert.That(PathCatalog.BossFor(10), Is.EqualTo(EnemyKind.RiftTwin));
            Assert.That(PathCatalog.BossFor(15), Is.EqualTo(EnemyKind.ChoirWarden));
            // Ein Aufstieg ohne Ende dreht die Folge weiter, statt auszugehen.
            Assert.That(PathCatalog.BossFor(20), Is.EqualTo(EnemyKind.IronWarden));
        }

        [Test]
        public void JederWaechterGiltAlsBossUndKeinGewoehnlicherGegner()
        {
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var isBoss = EnemyKinds.IsBoss(kind);
                var usedAsBoss = false;
                for (var floor = 5; floor <= 30; floor += 5)
                    if (PathCatalog.BossFor(floor) == kind) usedAsBoss = true;
                Assert.That(isBoss, Is.EqualTo(usedAsBoss),
                    $"{kind}: IsBoss={isBoss}, wird als Boss gestellt={usedAsBoss}. Beides muss "
                    + "uebereinstimmen, sonst fehlt ihm Groesse, Kollider oder die fehlende Leine.");
            }
        }

        [Test]
        public void JederWaechterHatEigeneZahlenUndIstHaerterAlsJederNormaleGegner()
        {
            var toughestNormal = 0f;
            foreach (var stats in EnemyBalance.All)
                if (!EnemyKinds.IsBoss(stats.Kind)) toughestNormal = Math.Max(toughestNormal, stats.Health);

            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                if (!EnemyKinds.IsBoss(kind)) continue;
                var stats = EnemyBalance.For(kind);
                Assert.That(stats.Kind, Is.EqualTo(kind), $"Fuer {kind} fehlt ein eigener Statblock.");
                Assert.That(stats.Health, Is.GreaterThan(toughestNormal * 2f),
                    $"{kind} hat {stats.Health} Leben, der zaeheste normale Gegner {toughestNormal}.");
                Assert.That(RunWallet.RewardFor(kind, 1), Is.GreaterThan(100),
                    $"{kind} zahlt zu wenig fuer einen Waechter.");
            }
        }

        [Test]
        public void JederWaechterIstUebersetzt()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.German;
                var missing = new List<string>();
                for (var floor = 5; floor <= 15; floor += 5)
                {
                    var name = PathCatalog.BossName(PathCatalog.BossFor(floor));
                    if (Loc.T(name) == name && !Loc.IsProperName(name)) missing.Add(name);
                    var objective = "DEFEAT " + name;
                    if (Loc.T(objective) == objective) missing.Add(objective);
                }
                Assert.That(missing, Is.Empty, "Nicht uebersetzt: " + string.Join(", ", missing));
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void JederWaechterHatEinenEigenenNamen()
        {
            var names = new HashSet<string>();
            for (var floor = 5; floor <= 15; floor += 5)
                names.Add(PathCatalog.BossName(PathCatalog.BossFor(floor)));
            Assert.That(names.Count, Is.EqualTo(3), "Drei Waechter, drei Namen: " + string.Join(", ", names));
        }
    }
}
