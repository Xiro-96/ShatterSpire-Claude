using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Alles, was drei Spieler im selben Lauf gleich vorfinden muessen, kommt aus
    /// <see cref="RunRandom"/> und nicht aus <see cref="UnityEngine.Random"/>.
    ///
    /// Geprueft wird hier die eine Eigenschaft, auf die es dabei ankommt: gleiche Eingaben, gleiches
    /// Ergebnis - und trotzdem genug Streuung, damit die Etage nicht in immer derselben Form
    /// dasteht. Reine Rechnung, also ohne Szene und ohne Gegnerobjekte pruefbar.
    /// </summary>
    public sealed class RunDeterminismTests
    {
        [Test]
        public void VerteidigerEinesCoresBleibenGleich()
        {
            for (var floor = 1; floor <= 20; floor++)
            {
                for (var core = 0; core < 4; core++)
                {
                    var first = EnemySpawner.EncounterHasDefenders(RoomKind.Mystery, 4711, floor, core);
                    var second = EnemySpawner.EncounterHasDefenders(RoomKind.Mystery, 4711, floor, core);
                    Assert.That(second, Is.EqualTo(first),
                        $"Etage {floor}, Core {core + 1} entscheidet sich zwischen zwei Abfragen neu.");
                }
            }
        }

        [Test]
        public void SchatzraumHatNieVerteidigerUndKampfraumImmer()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                Assert.That(EnemySpawner.EncounterHasDefenders(RoomKind.Treasure, seed, 5, 0), Is.False,
                    "Der Schatzraum ist die Pause im Aufstieg, dort steht nichts.");
                Assert.That(EnemySpawner.EncounterHasDefenders(RoomKind.Combat, seed, 5, 0), Is.True,
                    "Ein Kampfraum ohne Verteidiger waere ein geschenkter Core.");
                Assert.That(EnemySpawner.EncounterHasDefenders(RoomKind.Elite, seed, 5, 0), Is.True);
            }
        }

        [Test]
        public void MysterienFallenMalSoUndMalSoAus()
        {
            // Das Mysterium lebt davon, dass man es nicht weiss. Kaeme immer dieselbe Antwort,
            // waere das Salz wirkungslos und die Route nur noch ein zweiter Schatzraum.
            var withDefenders = 0;
            for (var seed = 0; seed < 400; seed++)
                if (EnemySpawner.EncounterHasDefenders(RoomKind.Mystery, seed, 6, 0)) withDefenders++;
            Assert.That(withDefenders, Is.InRange(140, 260),
                $"{withDefenders} von 400 Mysterien mit Verteidigern - das streut nicht wie eine Haelfte.");
        }

        [Test]
        public void ZweiGegnerEinerEtageHabenNieDasselbeSalz()
        {
            // Gleiches Salz hiesse gleicher Wurf: zwei Nachbarn im Lager waeren bis auf die Art
            // derselbe Gegner, und das Lager schluege wie ein Mann zu.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (var origin = 1; origin <= 3; origin++)
                for (var group = 0; group < 20; group++)
                    for (var slot = 0; slot < 20; slot++)
                        Assert.That(seen.Add(EnemySpawner.EnemySalt(origin, group, slot)), Is.True,
                            $"Salz doppelt vergeben: Herkunft {origin}, Gruppe {group}, Platz {slot}.");
        }

        [Test]
        public void EliteBleibtDerselbeElite()
        {
            var salt = EnemySpawner.EnemySalt(2, 1, 0);
            for (var floor = 1; floor <= 20; floor++)
                Assert.That(EnemyAgent.EliteIsExplosive(2024, floor, salt),
                    Is.EqualTo(EnemyAgent.EliteIsExplosive(2024, floor, salt)),
                    $"Der Elite auf Etage {floor} wechselt seine Eigenschaft zwischen zwei Abfragen.");

            // Und beide Eigenschaften kommen vor: sonst haette der vampirische Elite nie einen Auftritt.
            var explosive = 0;
            for (var seed = 0; seed < 400; seed++)
                if (EnemyAgent.EliteIsExplosive(seed, 7, salt)) explosive++;
            Assert.That(explosive, Is.InRange(140, 260),
                $"{explosive} von 400 Eliten explosiv - eine der beiden Eigenschaften faellt aus.");
        }

        [Test]
        public void GegnerEinesLagersZiehenVerschiedeneEigenschaften()
        {
            // Acht Nachbarn im selben Lager, ein Seed: die Antworten duerfen nicht alle gleich sein.
            var explosive = 0;
            for (var slot = 0; slot < 8; slot++)
                if (EnemyAgent.EliteIsExplosive(31337, 4, EnemySpawner.EnemySalt(1, 2, slot))) explosive++;
            Assert.That(explosive, Is.InRange(1, 7),
                "Ein ganzes Lager mit derselben Eigenschaft - die Salze der Plaetze wirken nicht.");
        }

        [Test]
        public void BeuteFaelltReproduzierbar()
        {
            var salt = EnemySpawner.EnemySalt(1, 3, 5);
            for (var floor = 1; floor <= 20; floor++)
                Assert.That(HealthOrb.Drops(EnemyKind.Brute, 8080, floor, salt),
                    Is.EqualTo(HealthOrb.Drops(EnemyKind.Brute, 8080, floor, salt)),
                    $"Die Kugel auf Etage {floor} faellt mal und mal nicht.");

            for (var seed = 0; seed < 50; seed++)
            {
                Assert.That(HealthOrb.Drops(EnemyKind.Elite, seed, 9, salt), Is.True,
                    "Elite und Warden lassen immer etwas fallen - darauf ist der Kampf gerechnet.");
                Assert.That(HealthOrb.Drops(EnemyKind.IronWarden, seed, 9, salt), Is.True);
            }
        }

        [Test]
        public void BeuteHaeltSichAnDieAngegebeneHaeufigkeit()
        {
            // 16 Prozent beim Crawler, 42 beim Brute: die Zahlen sind das Versprechen an den Spieler,
            // gegen das Schwerere anzutreten. Ein Wurf, der daneben liegt, nimmt dem Tisch den Sinn.
            var crawler = 0;
            var brute = 0;
            for (var slot = 0; slot < 1000; slot++)
            {
                var salt = EnemySpawner.EnemySalt(1, slot / 20, slot % 20);
                if (HealthOrb.Drops(EnemyKind.Crawler, 555, 3, salt)) crawler++;
                if (HealthOrb.Drops(EnemyKind.Brute, 555, 3, salt)) brute++;
            }
            Assert.That(crawler, Is.InRange(120, 200), $"Crawler: {crawler} von 1000 statt rund 160.");
            Assert.That(brute, Is.InRange(370, 470), $"Brute: {brute} von 1000 statt rund 420.");
        }
    }
}
