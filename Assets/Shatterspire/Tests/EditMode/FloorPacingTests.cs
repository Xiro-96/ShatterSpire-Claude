using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Das Tempo einer Etage in Zahlen: wie viele Raeume, wie weit der Weg, wie viel davon reines
    /// Laufen ohne Gegner ist.
    ///
    /// Der Anlass: "das Spiel muss fluessiger werden". Fluss laesst sich nicht am Bild beurteilen,
    /// aber Leerlauf laesst sich messen - und Leerlauf ist die haeufigste Ursache dafuer, dass sich
    /// ein Raum zaeh anfuehlt. Diese Tests halten fest, was heute gilt, damit eine Aenderung am
    /// Aufbau nicht unbemerkt den Turm auseinanderzieht.
    /// </summary>
    public sealed class FloorPacingTests
    {
        private static readonly RoomKind[] Routes =
            { RoomKind.Combat, RoomKind.Elite, RoomKind.Treasure, RoomKind.Mystery };

        private static IEnumerable<FloorLayout> SampleFloors()
        {
            for (var seed = 1; seed <= 40; seed++)
            for (var floor = 1; floor <= 12; floor++)
            {
                if (PathCatalog.IsBossFloor(floor)) continue;
                yield return FloorLayoutGenerator.Generate(seed * 7919 + floor, floor,
                    Routes[(seed + floor) % Routes.Length]);
            }
        }

        /// <summary>Weg vom Start ueber alle Cores zum Aufzug - der kuerzeste Durchgang der Etage.</summary>
        private static float RouteLength(FloorLayout layout)
        {
            var walk = 0f;
            var from = layout.SpawnPoint;
            foreach (var room in layout.CoreRooms)
            {
                walk += (room.CorePosition - from).magnitude;
                from = room.CorePosition;
            }
            return walk + (layout.ExitPoint - from).magnitude;
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        [Test]
        public void EineEtageBleibtInEinerSitzungDurchspielbar()
        {
            // Beim langsamsten Helden, mit Abschlag fuer Umwege. Reines Laufen, ohne einen einzigen
            // Kampf: was hier steht, ist die Untergrenze der Zeit je Etage.
            var pace = HeroCatalog.BaseSpeed(HeroClassId.Guardian) * 0.8f;
            var lengths = new List<float>();
            var worst = 0f;
            foreach (var layout in SampleFloors())
            {
                var length = RouteLength(layout);
                lengths.Add(length);
                worst = Mathf.Max(worst, length);
            }
            var median = Median(lengths);
            TestContext.WriteLine($"TEMPO Weg je Etage: median {median:0} Einheiten "
                                  + $"({median / pace:0} s reines Laufen), laengste {worst:0} "
                                  + $"({worst / pace:0} s)");
            // Fuenfzehn Etagen auf dem Heroic-Pfad: bei ueber 40 Sekunden reinem Laufen je Etage
            // waere allein der Weg zehn Minuten, bevor ein einziger Gegner gefallen ist.
            Assert.That(median / pace, Is.LessThan(40f),
                $"Der mittlere Weg dauert {median / pace:0} s, ohne jeden Kampf.");
        }

        [Test]
        public void JedeEtageHatGenugRaeumeUmSichZuUnterscheiden()
        {
            var counts = new List<float>();
            foreach (var layout in SampleFloors())
            {
                counts.Add(layout.Rooms.Count);
                Assert.That(layout.CoreRooms.Count(), Is.GreaterThan(0),
                    $"Etage {layout.Floor} hat keinen Power Core.");
            }
            TestContext.WriteLine($"TEMPO Raeume je Etage: median {Median(counts):0}, "
                                  + $"min {counts[0]:0}, max {counts[counts.Count - 1]:0}");
            Assert.That(Median(counts), Is.GreaterThanOrEqualTo(3f));
        }

        [Test]
        public void DerAnteilLeerlaufBleibtBeherrschbar()
        {
            // Leerlauf: Weg, auf dem kein Lager liegt. Lager sitzen in den Raeumen, also zaehlt der
            // Anteil der Strecke, der zwischen den Raeumen statt in ihnen verlaeuft.
            var shares = new List<float>();
            foreach (var layout in SampleFloors())
            {
                var total = RouteLength(layout);
                if (total <= 0f) continue;
                var inside = 0f;
                foreach (var room in layout.Rooms)
                    if (room.CampSize > 0)
                        inside += Mathf.Min(room.Bounds.Width, room.Bounds.Depth);
                shares.Add(Mathf.Clamp01(1f - inside / total));
            }
            var median = Median(shares);
            TestContext.WriteLine($"TEMPO Leerlaufanteil: median {median:P0}");
            Assert.That(median, Is.LessThan(0.85f),
                $"{median:P0} des Weges verlaeuft ausserhalb belebter Raeume.");
        }
    }
}
