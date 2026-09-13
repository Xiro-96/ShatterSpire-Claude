using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der Raetselraum als Wette: zwei Altaere, einer davon, Einsatz sichtbar und Inhalt nicht.
    ///
    /// Vorher entschied ein <see cref="UnityEngine.Random"/>-Wurf im Spawner, ob der Raum ueberhaupt
    /// Verteidiger bekommt - eine "unbekannte Begegnung", die nichts versprach und nichts hielt.
    /// </summary>
    public sealed class WagerTests
    {
        [Test]
        public void JedeWetteIstAbrufbar()
        {
            foreach (WagerId id in Enum.GetValues(typeof(WagerId)))
                Assert.That(WagerCatalog.For(id).Id, Is.EqualTo(id),
                    $"Fuer {id} fehlt ein Eintrag in der Tabelle.");
        }

        [Test]
        public void SegenUndFluchWerdenAusDerZahlAbgeleitet()
        {
            foreach (var wager in WagerCatalog.All)
            {
                if (wager.IsNothing) continue;
                var expected = wager.IsMultiplier ? wager.Amount > 1f : wager.Amount > 0f;
                Assert.That(wager.IsBoon, Is.EqualTo(expected),
                    $"{wager.Id} behauptet {(wager.IsBoon ? "Segen" : "Fluch")} bei {wager.Amount}.");
            }
        }

        [Test]
        public void JederEinsatzHatGenugAufBeidenSeiten()
        {
            foreach (WagerStake stake in Enum.GetValues(typeof(WagerStake)))
            {
                Assert.That(WagerCatalog.Of(stake, true).Count, Is.GreaterThanOrEqualTo(2),
                    $"{stake} hat zu wenige Segen - die Ziehung waere vorhersehbar.");
                Assert.That(WagerCatalog.Of(stake, false).Count, Is.GreaterThanOrEqualTo(2),
                    $"{stake} hat zu wenige Fluechte.");
            }
        }

        [Test]
        public void DerGrosseEinsatzSchlaegtWeiterAusAlsDerKleine()
        {
            // Sonst waere "hoher Einsatz" nur ein Wort auf dem Stein. Verglichen wird je Wirkung,
            // weil ein Faktor und ein Lebensbetrag keine gemeinsame Einheit haben.
            foreach (WagerEffect effect in Enum.GetValues(typeof(WagerEffect)))
            {
                var small = MaxSwing(WagerStake.Small, effect);
                var large = MaxSwing(WagerStake.Large, effect);
                if (small <= 0f || large <= 0f) continue;
                Assert.That(large, Is.GreaterThan(small),
                    $"{effect}: grosser Einsatz schlaegt {large}, kleiner {small}.");
            }
        }

        private static float MaxSwing(WagerStake stake, WagerEffect effect)
        {
            var best = 0f;
            foreach (var wager in WagerCatalog.All)
            {
                if (wager.IsNothing || wager.Stake != stake || wager.Effect != effect) continue;
                var swing = wager.IsMultiplier ? Mathf.Abs(wager.Amount - 1f) : Mathf.Abs(wager.Amount);
                best = Mathf.Max(best, swing);
            }
            return best;
        }

        [Test]
        public void GleicheEingabeGibtDieselbeWette()
        {
            for (var floor = 1; floor <= 20; floor++)
            foreach (WagerStake stake in Enum.GetValues(typeof(WagerStake)))
            {
                var first = WagerCatalog.Draw(9182736, floor, stake, 1);
                Assert.That(WagerCatalog.Draw(9182736, floor, stake, 1), Is.EqualTo(first),
                    $"Etage {floor}, {stake} streut zwischen zwei Abfragen.");
            }
        }

        [Test]
        public void DieZiehungTrifftDenAngekuendigtenSegensanteil()
        {
            var boons = 0;
            var total = 0;
            for (var seed = 0; seed < 600; seed++)
            for (var floor = 1; floor <= 12; floor++)
            foreach (WagerStake stake in Enum.GetValues(typeof(WagerStake)))
            {
                if (WagerCatalog.For(WagerCatalog.Draw(seed, floor, stake, 1)).IsBoon) boons++;
                total++;
            }
            Assert.That(boons / (float)total, Is.EqualTo(WagerCatalog.BoonChance).Within(0.03f),
                $"{boons} von {total} Ziehungen waren ein Segen.");
        }

        [Test]
        public void DieBeidenAltaereEinesRaumsZiehenGetrennt()
        {
            // Ohne getrenntes Salz haetten beide Altaere denselben Inhalt, und der Einsatz waere
            // die einzige Information - dann waere die Wahl keine.
            var differing = 0;
            for (var seed = 0; seed < 200; seed++)
            {
                var small = WagerCatalog.Draw(seed, 3, WagerStake.Small, 1);
                var large = WagerCatalog.Draw(seed, 3, WagerStake.Large, 2);
                if (WagerCatalog.For(small).IsBoon != WagerCatalog.For(large).IsBoon) differing++;
            }
            Assert.That(differing, Is.GreaterThan(60),
                $"Nur {differing} von 200 Raeumen boten Segen und Fluch gemischt an.");
        }

        [Test]
        public void DerTextNenntGenauDieZahl()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.English;
                foreach (var wager in WagerCatalog.All)
                {
                    var text = WagerCatalog.Describe(wager);
                    if (wager.IsNothing)
                    {
                        Assert.That(text, Is.EqualTo("NOTHING HAPPENS"));
                        continue;
                    }
                    var number = wager.IsMultiplier
                        ? Loc.Number(wager.Amount)
                        : Loc.Number(Mathf.Abs(wager.Effect == WagerEffect.HealthShare
                            || wager.Effect == WagerEffect.GoldShare
                            ? wager.Amount * 100f
                            : wager.Amount));
                    Assert.That(text, Does.Contain(number), $"{wager.Id}: {number} fehlt in '{text}'.");
                    if (!wager.IsMultiplier)
                        Assert.That(text, Does.Contain(wager.IsBoon ? "+" : "−"),
                            $"{wager.Id}: Vorzeichen fehlt in '{text}'.");
                }
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void JedeWetteIstUebersetzt()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.German;
                var missing = new List<string>();
                foreach (var wager in WagerCatalog.All)
                {
                    if (Loc.T(wager.Name) == wager.Name) missing.Add($"{wager.Id}: '{wager.Name}'");
                    var text = WagerCatalog.Describe(wager);
                    foreach (var english in new[] { "DAMAGE", "SPEED", "HEALTH", "NOTHING" })
                        if (text.Contains(english)) missing.Add($"{wager.Id}: '{english}' in '{text}'");
                }
                Assert.That(missing, Is.Empty, "Nicht uebersetzt: " + string.Join(" | ", missing));
            }
            finally
            {
                Loc.Language = before;
            }
        }
    }

    /// <summary>
    /// Die Schatzkammer: Horte im Raum, ein Alarm nach dem ersten, und was man nicht schafft,
    /// sinkt ein. Geprueft wird die Platzierung - ein Hort in einer Deckung waere sichtbar und
    /// unerreichbar, und das ist schlimmer als gar keiner.
    /// </summary>
    public sealed class TreasureVaultTests
    {
        private static IEnumerable<FloorLayout> SampleFloors()
        {
            for (var seed = 1; seed <= 30; seed++)
            for (var floor = 1; floor <= 9; floor++)
            {
                if (PathCatalog.IsBossFloor(floor)) continue;
                yield return FloorLayoutGenerator.Generate(seed * 7919 + floor, floor, RoomKind.Treasure);
            }
        }

        [Test]
        public void GoldSteigtMitDerEtage()
        {
            Assert.That(TreasureVault.GoldFor(1), Is.EqualTo(TreasureVault.BaseGold));
            var previous = 0;
            for (var floor = 1; floor <= 20; floor++)
            {
                var gold = TreasureVault.GoldFor(floor);
                Assert.That(gold, Is.GreaterThanOrEqualTo(previous), $"Etage {floor} zahlt weniger als {floor - 1}.");
                previous = gold;
            }
            Assert.That(TreasureVault.GoldFor(0), Is.GreaterThan(0), "Auch ein unsinniger Wert muss Gold geben.");
        }

        [Test]
        public void JedeEtageHatHorteUndKeinenImStartraum()
        {
            foreach (var layout in SampleFloors())
            {
                var spots = TreasureVault.Spots(layout);
                Assert.That(spots.Count, Is.GreaterThan(0), $"Etage {layout.Floor} ohne Horte.");
                var start = layout.Rooms[0];
                foreach (var spot in spots)
                    Assert.That(start.Bounds.Contains(spot), Is.False,
                        "Im Startraum steht kein Hort - dort beginnt der Weg, nicht die Beute.");
            }
        }

        [Test]
        public void KeinHortLiegtInEinerDeckung()
        {
            var checkedSpots = 0;
            foreach (var layout in SampleFloors())
            {
                foreach (var room in layout.Rooms)
                {
                    if (room.Role == RoomRole.Start) continue;
                    if (!TreasureVault.TrySpot(layout.Seed, room, out var spot)) continue;
                    checkedSpots++;
                    // Negativer Rand = Deckung um die Griffweite vergroessert. Mit positivem Rand
                    // fragte dieser Test dasselbe Falsche wie der Code und ging deshalb durch.
                    foreach (var cover in room.Cover)
                        Assert.That(cover.Contains(spot, -TreasureVault.Clearance), Is.False,
                            $"Etage {layout.Floor}, Raum {room.Index}: Hort steht zu nah an einer Deckung.");
                    Assert.That(room.Bounds.Contains(spot), Is.True,
                        $"Etage {layout.Floor}, Raum {room.Index}: Hort liegt ausserhalb des Raums.");
                }
            }
            Assert.That(checkedSpots, Is.GreaterThan(100), "Zu wenige Horte geprueft, der Test sagt nichts.");
        }

        [Test]
        public void DieselbeEtageLegtDieHorteAnDieselbeStelle()
        {
            var layout = FloorLayoutGenerator.Generate(31337, 4, RoomKind.Treasure);
            var first = TreasureVault.Spots(layout);
            var second = TreasureVault.Spots(FloorLayoutGenerator.Generate(31337, 4, RoomKind.Treasure));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var i = 0; i < first.Count; i++)
                Assert.That((second[i] - first[i]).magnitude, Is.LessThan(0.001f),
                    $"Hort {i} liegt beim zweiten Aufbau woanders.");
        }

        /// <summary>
        /// Das langsamste Tempo im Spiel, mit Abschlag fuer Umwege um Deckung. Danach bemisst sich,
        /// was in der Alarmzeit zu schaffen ist - nicht nach dem schnellsten Helden auf gerader Bahn.
        /// </summary>
        private static float SlowestPace => HeroCatalog.BaseSpeed(HeroClassId.Guardian) * 0.8f;

        /// <summary>Weg vom ersten Hort durch die uebrigen. Vorher laeuft die Uhr noch nicht.</summary>
        private static List<float> LegsAfterAlarm(FloorLayout layout)
        {
            var spots = TreasureVault.Spots(layout);
            var legs = new List<float>();
            for (var i = 1; i < spots.Count; i++) legs.Add((spots[i] - spots[i - 1]).magnitude);
            return legs;
        }

        [Test]
        public void AufJederEtageIstMindestensDieHaelfteZuSchaffen()
        {
            // Die Kammer ist ein Wettlauf, kein Abhaken: alles zu bekommen soll die Ausnahme sein.
            // Aber wenn nicht einmal die Haelfte erreichbar ist, ist es kein Rennen mehr, sondern
            // eine feste Abgabe - und genau das war die erste Fassung mit 22 Sekunden.
            foreach (var layout in SampleFloors())
            {
                var legs = LegsAfterAlarm(layout);
                if (legs.Count == 0) continue;
                var half = Mathf.Max(1, (legs.Count + 1) / 2);
                var walk = 0f;
                for (var i = 0; i < half; i++) walk += legs[i];
                var needed = walk / SlowestPace;
                Assert.That(TreasureVault.SealSeconds, Is.GreaterThanOrEqualTo(needed),
                    $"Etage {layout.Floor}, Seed {layout.Seed}: die Haelfte der Horte liegt "
                    + $"{walk:0.0} Einheiten auseinander, das braucht {needed:0.0} s bei "
                    + $"{TreasureVault.SealSeconds} s Alarm.");
            }
        }

        [Test]
        public void AufDenMeistenEtagenSindAlleZuSchaffen()
        {
            // Sonst waere die Kammer nur frustrierend. Zwei Drittel ist die Grenze: haeufig genug,
            // dass gutes Laufen belohnt wird, selten genug, dass es nicht selbstverstaendlich ist.
            var total = 0;
            var complete = 0;
            foreach (var layout in SampleFloors())
            {
                var legs = LegsAfterAlarm(layout);
                if (legs.Count == 0) continue;
                total++;
                var walk = 0f;
                foreach (var leg in legs) walk += leg;
                if (walk / SlowestPace <= TreasureVault.SealSeconds) complete++;
            }
            Assert.That(complete / (float)total, Is.GreaterThan(0.66f),
                $"Nur auf {complete} von {total} Etagen waere die Kammer ganz zu raeumen.");
        }
    }
}
