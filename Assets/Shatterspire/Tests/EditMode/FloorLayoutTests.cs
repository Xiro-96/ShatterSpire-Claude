using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Etagen-Layout, Navigation und Pfade sind reine Rechnung. Diese Tests laufen ohne Unity und
    /// sichern ab, was man beim Spielen sonst nur zufaellig entdeckt: eine Etage ohne Weg zum Aufzug,
    /// ein Core im Startraum, ein Gegner, der durch eine Wand laeuft.
    /// </summary>
    public sealed class FloorLayoutTests
    {
        private static readonly RoomKind[] Routes = { RoomKind.Combat, RoomKind.Elite, RoomKind.Treasure, RoomKind.Mystery };

        private static IEnumerable<FloorLayout> SampleFloors()
        {
            for (var seed = 1; seed <= 40; seed++)
            for (var floor = 1; floor <= 12; floor++)
            {
                if (PathCatalog.IsBossFloor(floor)) continue;
                yield return FloorLayoutGenerator.Generate(seed * 7919 + floor, floor, Routes[(seed + floor) % Routes.Length]);
            }
        }

        private static float Flat(Vector3 delta) => Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);

        // ── Layout ───────────────────────────────────────────────

        [Test]
        public void GleicherSeedErgibtDieselbeEtage()
        {
            var a = FloorLayoutGenerator.Generate(4242, 7, RoomKind.Elite);
            var b = FloorLayoutGenerator.Generate(4242, 7, RoomKind.Elite);
            Assert.That(b.Rooms.Count, Is.EqualTo(a.Rooms.Count));
            for (var i = 0; i < a.Rooms.Count; i++)
            {
                Assert.That(b.Rooms[i].CellX, Is.EqualTo(a.Rooms[i].CellX));
                Assert.That(b.Rooms[i].CellZ, Is.EqualTo(a.Rooms[i].CellZ));
                Assert.That(b.Rooms[i].Role, Is.EqualTo(a.Rooms[i].Role));
                Assert.That(b.Rooms[i].Bounds.Width, Is.EqualTo(a.Rooms[i].Bounds.Width));
                Assert.That(b.Rooms[i].CampSize, Is.EqualTo(a.Rooms[i].CampSize));
            }
            Assert.That(b.Doors.Count, Is.EqualTo(a.Doors.Count));
        }

        [Test]
        public void JederRaumIstVomStartErreichbar()
        {
            foreach (var layout in SampleFloors())
            {
                var hops = layout.HopsFrom(layout.StartRoom);
                Assert.That(hops.All(h => h != int.MaxValue), Is.True,
                    $"Seed {layout.Seed}: nicht jeder Raum ist erreichbar.");
            }
        }

        [Test]
        public void RaumanzahlPasstZurEtage()
        {
            foreach (var layout in SampleFloors())
                Assert.That(layout.Rooms.Count, Is.EqualTo(FloorLayoutGenerator.RoomCountFor(layout.Floor, layout.Kind)),
                    $"Seed {layout.Seed}, Etage {layout.Floor}");
        }

        [Test]
        public void ZweiCoresInVerschiedenenRaeumenNieImStartOderAmAufzug()
        {
            foreach (var layout in SampleFloors())
            {
                var cores = layout.CoreRooms.ToList();
                Assert.That(cores, Has.Count.EqualTo(FloorLayoutGenerator.CoresPerFloor), $"Seed {layout.Seed}");
                Assert.That(cores.Select(r => r.Index).Distinct().Count(), Is.EqualTo(cores.Count));
                Assert.That(cores.Any(r => r.Index == layout.StartRoom || r.Index == layout.ExitRoom), Is.False,
                    $"Seed {layout.Seed}: Core im Start- oder Aufzugsraum.");
                foreach (var core in cores)
                    Assert.That(core.Bounds.Contains(core.CorePosition, 2f), Is.True,
                        $"Seed {layout.Seed}: Core zu nah an der Wand.");
            }
        }

        [Test]
        public void AufzugLiegtImEntferntestenRaum()
        {
            foreach (var layout in SampleFloors())
            {
                var hops = layout.HopsFrom(layout.StartRoom);
                Assert.That(hops[layout.ExitRoom], Is.EqualTo(hops.Max()), $"Seed {layout.Seed}");
                Assert.That(layout.Rooms[layout.ExitRoom].Role, Is.EqualTo(RoomRole.Lift));
            }
        }

        [Test]
        public void RaeumeUeberlappenSichNicht()
        {
            foreach (var layout in SampleFloors())
                for (var i = 0; i < layout.Rooms.Count; i++)
                for (var j = i + 1; j < layout.Rooms.Count; j++)
                    Assert.That(layout.Rooms[i].Bounds.Overlaps(layout.Rooms[j].Bounds), Is.False,
                        $"Seed {layout.Seed}: Raum {i} und {j} ueberlappen.");
        }

        [Test]
        public void JederGangReichtInBeideRaeume()
        {
            foreach (var layout in SampleFloors())
            foreach (var door in layout.Doors)
            {
                Assert.That(door.Corridor.Overlaps(layout.Rooms[door.RoomA].Bounds), Is.True, $"Seed {layout.Seed}");
                Assert.That(door.Corridor.Overlaps(layout.Rooms[door.RoomB].Bounds), Is.True, $"Seed {layout.Seed}");
            }
        }

        [Test]
        public void StartraumIstSicherAndereRaeumeHabenLager()
        {
            foreach (var layout in SampleFloors())
            {
                Assert.That(layout.Rooms[layout.StartRoom].CampSize, Is.Zero, $"Seed {layout.Seed}: Lager im Startraum.");
                foreach (var room in layout.Rooms.Where(r => r.Role != RoomRole.Start))
                    Assert.That(room.CampSize, Is.GreaterThan(0), $"Seed {layout.Seed}: Raum {room.Index} ohne Lager.");
            }
        }

        [Test]
        public void BossEtageBestehtAusStartUndBossraum()
        {
            var layout = FloorLayoutGenerator.Generate(99, 5, RoomKind.Boss);
            Assert.That(layout.Rooms, Has.Count.EqualTo(2));
            Assert.That(layout.Rooms[layout.ExitRoom].Role, Is.EqualTo(RoomRole.Boss));
            Assert.That(layout.CoreRooms, Is.Empty);
            Assert.That(layout.Doors, Has.Count.EqualTo(1));
        }

        // ── Navigation ───────────────────────────────────────────

        private static bool Walk(FloorNavigation navigation, Vector3 from, Vector3 to, out string problem)
        {
            const float radius = 0.45f;
            const float step = 0.35f;
            var position = navigation.ClampToWalkable(from, radius);
            for (var i = 0; i < 5000; i++)
            {
                if (Flat(position - to) < 0.6f)
                {
                    problem = null;
                    return true;
                }
                var waypoint = navigation.NextWaypoint(position, to);
                var delta = waypoint - position;
                delta.y = 0f;
                var length = Flat(delta);
                if (length < 0.0001f)
                {
                    problem = $"Stillstand bei {position}";
                    return false;
                }
                position += delta / length * Mathf.Min(step, length);
                position = navigation.ClampToWalkable(position, radius);
                if (!navigation.IsWalkable(position, radius - 0.01f))
                {
                    problem = $"Nicht begehbar bei {position}";
                    return false;
                }
            }
            problem = $"Ziel nicht erreicht, zuletzt bei {position}";
            return false;
        }

        [Test]
        public void NavigationFuehrtVomStartZuJedemCoreUndZumAufzug()
        {
            foreach (var layout in SampleFloors().Take(120))
            {
                var navigation = new FloorNavigation(layout);
                var targets = layout.CoreRooms.Select(r => r.CorePosition).Append(layout.ExitPoint);
                foreach (var target in targets)
                    Assert.That(Walk(navigation, layout.SpawnPoint, target, out var problem), Is.True,
                        $"Seed {layout.Seed}, Etage {layout.Floor}: {problem}");
            }
        }

        [Test]
        public void NavigationFindetAuchDenRueckwegVomAufzugZumStart()
        {
            foreach (var layout in SampleFloors().Take(60))
            {
                var navigation = new FloorNavigation(layout);
                Assert.That(Walk(navigation, layout.ExitPoint, layout.SpawnPoint, out var problem), Is.True,
                    $"Seed {layout.Seed}: {problem}");
            }
        }

        [Test]
        public void KlemmenLandetImmerAufBegehbaremBoden()
        {
            var random = new System.Random(7);
            foreach (var layout in SampleFloors().Take(40))
            {
                var navigation = new FloorNavigation(layout);
                var bounds = layout.Bounds;
                for (var i = 0; i < 200; i++)
                {
                    var point = new Vector3(
                        bounds.MinX - 6f + (float)random.NextDouble() * (bounds.Width + 12f), 0f,
                        bounds.MinZ - 6f + (float)random.NextDouble() * (bounds.Depth + 12f));
                    var clamped = navigation.ClampToWalkable(point, 0.5f);
                    Assert.That(navigation.IsWalkable(clamped, 0.49f), Is.True,
                        $"Seed {layout.Seed}: {point} wurde auf {clamped} geklemmt, das ist nicht begehbar.");
                }
            }
        }

        [Test]
        public void AbgrundZwischenDenRaeumenIstNichtBegehbar()
        {
            var layout = FloorLayoutGenerator.Generate(1234, 2, RoomKind.Combat);
            var navigation = new FloorNavigation(layout);
            var farOutside = new Vector3(layout.Bounds.MaxX + 20f, 0f, layout.Bounds.MaxZ + 20f);
            Assert.That(navigation.IsWalkable(farOutside, 0.4f), Is.False);
            Assert.That(navigation.IsWalkable(layout.SpawnPoint, 0.4f), Is.True);
        }

        // ── Deckung ──────────────────────────────────────────────

        [Test]
        public void DeckungHaeltAbstandZuWaendenZielenUndZueinander()
        {
            foreach (var layout in SampleFloors())
            foreach (var room in layout.Rooms)
            {
                for (var i = 0; i < room.Cover.Count; i++)
                {
                    var cover = room.Cover[i];
                    // Vollstaendig im Raum, mit genug Rand fuer einen Rundweg aussen herum.
                    Assert.That(room.Bounds.Contains(new Vector3(cover.MinX, 0f, cover.MinZ), 3f), Is.True,
                        $"Seed {layout.Seed}, Raum {room.Index}: Deckung zu nah an der Wand.");
                    Assert.That(room.Bounds.Contains(new Vector3(cover.MaxX, 0f, cover.MaxZ), 3f), Is.True,
                        $"Seed {layout.Seed}, Raum {room.Index}: Deckung zu nah an der Wand.");
                    Assert.That(cover.Contains(room.CampCenter), Is.False,
                        $"Seed {layout.Seed}, Raum {room.Index}: Lagermitte steckt in einer Deckung.");
                    if (room.Role == RoomRole.Core)
                        Assert.That(cover.Contains(room.CorePosition), Is.False,
                            $"Seed {layout.Seed}, Raum {room.Index}: Core steckt in einer Deckung.");
                    if (room.Role is RoomRole.Lift or RoomRole.Boss)
                        Assert.That(cover.Contains(room.Bounds.Center), Is.False,
                            $"Seed {layout.Seed}, Raum {room.Index}: Raummitte ist verstellt.");
                    for (var j = i + 1; j < room.Cover.Count; j++)
                        Assert.That(cover.Overlaps(room.Cover[j]), Is.False,
                            $"Seed {layout.Seed}, Raum {room.Index}: zwei Deckungen ueberlappen.");
                }
            }
        }

        [Test]
        public void KampfraeumeHabenDeckungDerStartraumNicht()
        {
            foreach (var layout in SampleFloors())
            {
                Assert.That(layout.Rooms[layout.StartRoom].Cover, Is.Empty,
                    $"Seed {layout.Seed}: Deckung im Startraum.");
                foreach (var room in layout.Rooms.Where(r => r.Role != RoomRole.Start))
                    Assert.That(room.Cover, Is.Not.Empty,
                        $"Seed {layout.Seed}: Raum {room.Index} ({room.Role}) ohne Deckung.");
            }
        }

        [Test]
        public void DeckungIstNichtBegehbarUndSchiebtHeraus()
        {
            foreach (var layout in SampleFloors().Take(40))
            {
                var navigation = new FloorNavigation(layout);
                foreach (var room in layout.Rooms)
                foreach (var cover in room.Cover)
                {
                    var centre = cover.Center;
                    Assert.That(navigation.IsWalkable(centre, 0.45f), Is.False,
                        $"Seed {layout.Seed}: man kann in eine Deckung laufen.");
                    var pushed = navigation.ClampToWalkable(centre, 0.45f);
                    Assert.That(navigation.IsWalkable(pushed, 0.44f), Is.True,
                        $"Seed {layout.Seed}: Herausschieben landet nicht auf begehbarem Boden.");
                }
            }
        }

        [Test]
        public void DeckungUnterbrichtDieSichtlinie()
        {
            foreach (var layout in SampleFloors().Take(40))
            {
                var navigation = new FloorNavigation(layout);
                foreach (var room in layout.Rooms)
                foreach (var cover in room.Cover)
                {
                    var centre = cover.Center;
                    var acrossX = cover.Width * 0.5f + 2f;
                    var acrossZ = cover.Depth * 0.5f + 2f;
                    Assert.That(navigation.HasLineOfSight(centre + Vector3.left * acrossX, centre + Vector3.right * acrossX),
                        Is.False, $"Seed {layout.Seed}: Sicht geht quer durch eine Deckung.");
                    Assert.That(navigation.HasLineOfSight(centre + Vector3.back * acrossZ, centre + Vector3.forward * acrossZ),
                        Is.False, $"Seed {layout.Seed}: Sicht geht laengs durch eine Deckung.");
                }
            }
        }

        [Test]
        public void SprungLandetAnDerKanteUndNichtWiederAmStart()
        {
            foreach (var layout in SampleFloors().Take(40))
            {
                var navigation = new FloorNavigation(layout);
                var start = layout.SpawnPoint;
                foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    // Weit ueber die Etage hinaus zielen: das Ergebnis muss begehbar sein und darf
                    // nicht hinter dem Start liegen - genau das war der Fehler, der einen Sprung von
                    // 7,5 Metern zu 2,1 Metern gemacht hat.
                    var wanted = start + direction * 40f;
                    var landing = navigation.FurthestWalkableAlong(start, wanted, 0.6f);
                    Assert.That(navigation.IsWalkable(landing, 0.59f), Is.True,
                        $"Seed {layout.Seed}: Landung {landing} ist nicht begehbar.");
                    var travelled = Vector3.Dot(landing - start, direction);
                    Assert.That(travelled, Is.GreaterThanOrEqualTo(-0.01f),
                        $"Seed {layout.Seed}: Sprung nach {direction} ging rueckwaerts.");
                }
            }
        }

        [Test]
        public void SprungBleibtVorEinerDeckungStehen()
        {
            var layout = FloorLayoutGenerator.Generate(1234, 3, RoomKind.Combat);
            var navigation = new FloorNavigation(layout);
            foreach (var room in layout.Rooms)
            foreach (var cover in room.Cover)
            {
                // Von einer Seite genau durch die Deckung zielen: die Landung darf nie darin liegen.
                var centre = cover.Center;
                var from = centre + Vector3.left * (cover.Width * 0.5f + 2.5f);
                if (!navigation.IsWalkable(from, 0.6f)) continue;
                var landing = navigation.FurthestWalkableAlong(from, centre + Vector3.right * 3f, 0.6f);
                Assert.That(cover.Contains(landing), Is.False,
                    $"Landung {landing} steckt in einer Deckung.");
                Assert.That(navigation.IsWalkable(landing, 0.59f), Is.True);
            }
        }

        [Test]
        public void SichtlinieBleibtOhneDeckungDazwischenFrei()
        {
            var layout = FloorLayoutGenerator.Generate(1234, 2, RoomKind.Combat);
            var navigation = new FloorNavigation(layout);
            var start = layout.SpawnPoint;
            Assert.That(navigation.HasLineOfSight(start, start + Vector3.right * 2f), Is.True);
        }

        // ── Pfade ────────────────────────────────────────────────

        [Test]
        public void PfadeHabenDieLaengenAusRise()
        {
            Assert.That(PathCatalog.FloorCount(RunMode.Brave), Is.EqualTo(5));
            Assert.That(PathCatalog.FloorCount(RunMode.Heroic), Is.EqualTo(15));
            Assert.That(PathCatalog.IsEndless(RunMode.Legendary), Is.True);
        }

        [Test]
        public void LetzteEtageUndBossEtagenStimmen()
        {
            Assert.That(PathCatalog.IsFinalFloor(RunMode.Brave, 5), Is.True);
            Assert.That(PathCatalog.IsFinalFloor(RunMode.Brave, 4), Is.False);
            Assert.That(PathCatalog.IsFinalFloor(RunMode.Heroic, 10), Is.False);
            Assert.That(PathCatalog.IsFinalFloor(RunMode.Heroic, 15), Is.True);
            Assert.That(PathCatalog.IsFinalFloor(RunMode.Legendary, 500), Is.False);
            Assert.That(new[] { 5, 10, 15 }.All(PathCatalog.IsBossFloor), Is.True);
            Assert.That(PathCatalog.IsBossFloor(4), Is.False);
            // Jeder Pfad mit fester Laenge endet auf einer Boss-Etage.
            Assert.That(PathCatalog.IsBossFloor(PathCatalog.FloorCount(RunMode.Brave)), Is.True);
            Assert.That(PathCatalog.IsBossFloor(PathCatalog.FloorCount(RunMode.Heroic)), Is.True);
        }
    }
}
