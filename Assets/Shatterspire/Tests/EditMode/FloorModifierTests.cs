using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die Anomalien sind reine Rechnung, also vollstaendig ohne Unity pruefbar.
    ///
    /// Der wichtigste Test ist die Reproduzierbarkeit: die Zuteilung darf nicht aus
    /// <see cref="UnityEngine.Random"/> kommen, weil im Co-op alle drei Spieler dieselben drei
    /// Angebote sehen muessen, ohne sie einander zu schicken.
    /// </summary>
    public sealed class FloorModifierTests
    {
        [Test]
        public void JedeAnomalieIstAbrufbar()
        {
            foreach (FloorModifierId id in Enum.GetValues(typeof(FloorModifierId)))
                Assert.That(FloorModifierCatalog.For(id).Id, Is.EqualTo(id),
                    $"Fuer {id} fehlt ein Eintrag in der Tabelle.");
        }

        [Test]
        public void GleicheEingabenGebenGleicheAnomalie()
        {
            for (var floor = 2; floor <= 20; floor++)
            {
                var first = FloorModifierCatalog.Offer(1234567, floor, RoomKind.Combat);
                var second = FloorModifierCatalog.Offer(1234567, floor, RoomKind.Combat);
                Assert.That(second, Is.EqualTo(first), $"Etage {floor} streut zwischen zwei Abfragen.");
            }
        }

        [Test]
        public void ErsteEtageUndBossBleibenRuhig()
        {
            foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
            {
                Assert.That(FloorModifierCatalog.Offer(99, 1, kind), Is.EqualTo(FloorModifierId.None),
                    "Auf Etage 1 lernt man die Steuerung, dort gehoert keine Anomalie hin.");
                Assert.That(FloorModifierCatalog.Offer(99, 5, RoomKind.Boss), Is.EqualTo(FloorModifierId.None),
                    "Der Warden hat seine Phasen als eigenen Haken.");
            }
        }

        /// <summary>
        /// Immer, nicht meistens. Frueher stand hier "mehr als 150 von 200" - und in 3,8 % der
        /// Wahlen trugen alle drei Karten dieselbe Anomalie. Genau das fiel beim Spielen auf.
        /// </summary>
        [Test]
        public void DieDreiRoutenEinerEtageUnterscheidenSichImmer()
        {
            for (var seed = 0; seed < 2000; seed++)
            for (var floor = 2; floor <= 16; floor++)
            {
                if (PathCatalog.IsBossFloor(floor)) continue;
                var combat = FloorModifierCatalog.Offer(seed, floor, RoomKind.Combat);
                var elite = FloorModifierCatalog.Offer(seed, floor, RoomKind.Elite);
                var third = FloorModifierCatalog.Offer(seed, floor, RoomKind.Mystery);
                Assert.That(combat, Is.Not.EqualTo(elite), $"Seed {seed}, Etage {floor}");
                Assert.That(elite, Is.Not.EqualTo(third), $"Seed {seed}, Etage {floor}");
                Assert.That(combat, Is.Not.EqualTo(third), $"Seed {seed}, Etage {floor}");
            }
        }

        /// <summary>Schatz und Geheimnis stehen auf demselben Platz - sie bekommen dieselbe Anomalie.</summary>
        [Test]
        public void SchatzUndGeheimnisTeilenSichDenDrittenPlatz()
        {
            for (var seed = 0; seed < 200; seed++)
                Assert.That(FloorModifierCatalog.Offer(seed, 4, RoomKind.Treasure),
                    Is.EqualTo(FloorModifierCatalog.Offer(seed, 4, RoomKind.Mystery)));
        }

        /// <summary>Jede zweite Etage hat eine ruhige Route als sichere Wahl, die andere Haelfte keine.</summary>
        [Test]
        public void EtwaJedeZweiteEtageBietetEineRuhigeRoute()
        {
            var calm = 0;
            var floors = 0;
            for (var seed = 0; seed < 400; seed++)
            for (var floor = 2; floor <= 16; floor++)
            {
                floors++;
                if (FloorModifierCatalog.OffersFor(seed, floor).Contains(FloorModifierId.None)) calm++;
            }
            Assert.That(calm / (float)floors, Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void JedeAnomalieKommtVorUndKeineDominiert()
        {
            var counts = new Dictionary<FloorModifierId, int>();
            foreach (FloorModifierId id in Enum.GetValues(typeof(FloorModifierId))) counts[id] = 0;
            var total = 0;
            for (var seed = 0; seed < 400; seed++)
            for (var floor = 2; floor <= 16; floor++)
            foreach (var kind in new[] { RoomKind.Combat, RoomKind.Elite, RoomKind.Treasure })
            {
                counts[FloorModifierCatalog.Offer(seed, floor, kind)]++;
                total++;
            }

            // Sechs verschiedene, drei davon je Etage: jede - auch die Ruhe - auf rund einem Sechstel
            // der Karten. Keine dominiert, keine fehlt.
            foreach (var pair in counts)
                Assert.That(pair.Value / (float)total, Is.EqualTo(1f / 6f).Within(0.03f),
                    $"{pair.Key} kommt zu {pair.Value / (float)total:P1} vor.");
        }

        [Test]
        public void KeineAnomalieIstNurGutOderNurSchlecht()
        {
            foreach (var modifier in FloorModifierCatalog.All)
            {
                if (modifier.IsCalm) continue;
                var helps = modifier.EnemyHealth < 1f || modifier.EnemyDamage < 1f
                            || modifier.EnemySpeed < 1f || modifier.EnemyCount < 1f
                            || modifier.Gold > 1f || modifier.Shards > 1f;
                var hurts = modifier.EnemyHealth > 1f || modifier.EnemyDamage > 1f
                            || modifier.EnemySpeed > 1f || modifier.EnemyCount > 1f;
                Assert.That(helps, Is.True, $"{modifier.Id} hat keinen Vorteil - dann waehlt sie niemand.");
                Assert.That(hurts, Is.True, $"{modifier.Id} hat keinen Nachteil - dann waehlt sie jeder.");
            }
        }

        [Test]
        public void DerWirkungstextNenntGenauDieGeaendertenZahlen()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.English;
                foreach (var modifier in FloorModifierCatalog.All)
                {
                    var text = FloorModifierCatalog.Effects(modifier);
                    if (modifier.IsCalm)
                    {
                        Assert.That(text, Is.EqualTo("NO ANOMALY"));
                        continue;
                    }
                    Expect(text, "ENEMY HEALTH", modifier.EnemyHealth, modifier.Id);
                    Expect(text, "ENEMY DAMAGE", modifier.EnemyDamage, modifier.Id);
                    Expect(text, "ENEMY SPEED", modifier.EnemySpeed, modifier.Id);
                    Expect(text, "ENEMY COUNT", modifier.EnemyCount, modifier.Id);
                    Expect(text, "GOLD", modifier.Gold, modifier.Id);
                    Expect(text, "SHARDS", modifier.Shards, modifier.Id);
                }
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void ProzenteTragenVorzeichenUndStimmen()
        {
            Assert.That(FloorModifierCatalog.Percent(1.5f), Is.EqualTo("+50%"));
            Assert.That(FloorModifierCatalog.Percent(0.75f), Is.EqualTo("-25%"));
            Assert.That(FloorModifierCatalog.Percent(1.35f), Is.EqualTo("+35%"));
            Assert.That(FloorModifierCatalog.Percent(0.5f), Is.EqualTo("-50%"));
            Assert.That(FloorModifierCatalog.Percent(2.3f), Is.EqualTo("+130%"));
        }

        /// <summary>Die Farbe muss sagen, was die Wirkung fuer den Spieler bedeutet - nicht, ob die Zahl steigt.</summary>
        [Test]
        public void DieFarbeFolgtDemSpielerNichtDerZahl()
        {
            Assert.That(FloorModifierCatalog.Verdict("ENEMY HEALTH", 1.5f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Harmful));
            Assert.That(FloorModifierCatalog.Verdict("ENEMY SPEED", 0.75f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Helpful));
            Assert.That(FloorModifierCatalog.Verdict("ENEMY COUNT", 1.85f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Harmful));
            Assert.That(FloorModifierCatalog.Verdict("ENEMY DAMAGE", 0.85f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Helpful));
            Assert.That(FloorModifierCatalog.Verdict("SHARDS", 1.35f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Reward));
            Assert.That(FloorModifierCatalog.Verdict("GOLD", 2.3f), Is.EqualTo(FloorModifierCatalog.EffectVerdict.Reward));
        }

        /// <summary>Ein Faktor steht genau dann im Text, wenn er nicht 1 ist - und mit seinem Wert.</summary>
        private static void Expect(string text, string label, float factor, FloorModifierId id)
        {
            if (Math.Abs(factor - 1f) < 0.0001f)
            {
                Assert.That(text, Does.Not.Contain(label), $"{id} nennt {label}, obwohl der Faktor 1 ist.");
                return;
            }
            var expected = label + " " + FloorModifierCatalog.Percent(factor);
            Assert.That(text, Does.Contain(expected), $"{id}: {expected} fehlt in {text}.");
        }

        [Test]
        public void ZahlenStehenInDerSpracheDerOberflaeche()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.German;
                Assert.That(Loc.Number(1.5f), Is.EqualTo("1,5"));
                Assert.That(Loc.Number(2f), Is.EqualTo("2"));
                Assert.That(Loc.Number(0.55f), Is.EqualTo("0,55"));
                Loc.Language = Language.English;
                Assert.That(Loc.Number(1.5f), Is.EqualTo("1.5"));
            }
            finally
            {
                Loc.Language = before;
            }
        }
    }

    /// <summary>Der Ersatz fuer <see cref="UnityEngine.Random"/> an allen reproduzierbaren Stellen.</summary>
    public sealed class RunRandomTests
    {
        [Test]
        public void GleicheEingabeGleicherWert()
        {
            Assert.That(RunRandom.Hash(7, 3, 11), Is.EqualTo(RunRandom.Hash(7, 3, 11)));
            Assert.That(RunRandom.Hash(7, 3, 11), Is.Not.EqualTo(RunRandom.Hash(7, 4, 11)));
            Assert.That(RunRandom.Hash(7, 3, 11), Is.Not.EqualTo(RunRandom.Hash(7, 3, 12)));
        }

        [Test]
        public void IndexBleibtImBereich()
        {
            for (var seed = 0; seed < 500; seed++)
                Assert.That(RunRandom.Index(seed, seed % 17, 5, 7), Is.InRange(0, 6));
            Assert.That(RunRandom.Index(1, 1, 1, 0), Is.Zero, "Ohne Auswahl gibt es nur den Index 0.");
        }

        [Test]
        public void IndexStreutGleichmaessig()
        {
            var counts = new int[7];
            for (var seed = 0; seed < 7000; seed++) counts[RunRandom.Index(seed, 3, 1, 7)]++;
            foreach (var count in counts)
                Assert.That(count, Is.EqualTo(1000).Within(140),
                    "Die Streuung ist zu schief: " + string.Join(", ", counts));
        }

        [Test]
        public void WahrscheinlichkeitTrifftUndKlemmt()
        {
            Assert.That(RunRandom.Chance(1, 1, 1, 0f), Is.False);
            Assert.That(RunRandom.Chance(1, 1, 1, 1f), Is.True);
            var hits = 0;
            for (var seed = 0; seed < 4000; seed++)
                if (RunRandom.Chance(seed, 4, 97, 0.5f)) hits++;
            Assert.That(hits, Is.EqualTo(2000).Within(160), $"{hits} von 4000 Treffern bei 50 Prozent.");
        }

        [Test]
        public void DasRoutenangebotIstReproduzierbarUndVollstaendig()
        {
            for (var floor = 2; floor <= 24; floor++)
            {
                var routes = PathCatalog.RoutesFor(4711, floor);
                Assert.That(PathCatalog.RoutesFor(4711, floor), Is.EqualTo(routes));
                if (PathCatalog.IsBossFloor(floor))
                {
                    Assert.That(routes, Is.EqualTo(new[] { RoomKind.Boss }));
                    continue;
                }
                Assert.That(routes.Length, Is.EqualTo(3));
                Assert.That(routes[0], Is.EqualTo(RoomKind.Combat));
                Assert.That(routes[1], Is.EqualTo(RoomKind.Elite));
                Assert.That(routes[2], Is.EqualTo(RoomKind.Treasure).Or.EqualTo(RoomKind.Mystery));
            }
        }
    }
}
