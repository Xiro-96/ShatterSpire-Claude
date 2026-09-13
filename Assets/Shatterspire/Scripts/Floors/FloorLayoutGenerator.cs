using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Erzeugt eine Etage aus verbundenen Raeumen auf einem Raster. Deterministisch fuer denselben
    /// Seed und ohne Unity-Laufzeitaufrufe: jede Etage ist dadurch im Test pruefbar und spaeter
    /// zwischen Co-op-Clients reproduzierbar, die nur den Seed austauschen.
    ///
    /// Ersetzt die eine kreisfoermige Arena, in der jede Etage bisher stattfand.
    /// </summary>
    public static class FloorLayoutGenerator
    {
        /// <summary>
        /// Abstand der Raummitten. Raeume sind bis zu 22 breit, es bleiben also rund drei bis
        /// sieben Einheiten Gang dazwischen.
        ///
        /// Vorher 30. Gemessen an 480 erzeugten Etagen waren das im Mittel 120 Einheiten Weg je
        /// Etage - beim langsamsten Helden 29 Sekunden reines Laufen, ohne einen einzigen Kampf,
        /// und auf dem Heroic-Pfad ueber sieben Minuten Weg je Aufstieg. Der Kampf findet im Raum
        /// statt; der Gang dazwischen ist nur Weg und gehoert kurz.
        /// </summary>
        public const float CellSize = 25f;
        public const int CoresPerFloor = 2;
        private const int GridLimit = 3;
        private const float CorridorOverlap = 0.6f;
        /// <summary>
        /// Abstand jeder Deckung zu allen Waenden. Weil Tueren in Waenden liegen, haelt derselbe Wert
        /// auch die Durchgaenge frei, und aussen herum bleibt in jedem Raum ein Rundweg.
        /// </summary>
        private const float CoverWallMargin = 3.4f;
        /// <summary>Freiraum um Core, Lagermitte und Aufzug.</summary>
        private const float CoverSpotMargin = 2.6f;
        /// <summary>Luecke zwischen zwei Deckungen, damit man dazwischen durchlaufen kann.</summary>
        private const float CoverSpacing = 2.4f;

        private static readonly int[] StepX = { 1, -1, 0, 0 };
        private static readonly int[] StepZ = { 0, 0, 1, -1 };

        public static int RoomCountFor(int floor, RoomKind kind)
        {
            if (kind == RoomKind.Boss) return 2;
            // Vier ist die Untergrenze und nicht Geschmackssache: Start, zwei Core-Raeume und der
            // Aufzugsraum. Mit dreien bliebe fuer den zweiten Core kein Platz.
            //
            // Vorher wuchs die Zahl mit der Tiefe auf fuenf und sechs. Tiefe Etagen sind jetzt ueber
            // die Gegner haerter, nicht ueber die Laenge - ein laengerer Weg macht eine Etage nicht
            // schwerer, nur zaeher.
            return kind == RoomKind.Elite ? 5 : 4;
        }

        public static FloorLayout Generate(int seed, int floor, RoomKind kind)
        {
            var rng = new System.Random(seed);
            var layout = new FloorLayout
            {
                Seed = seed,
                Floor = Math.Max(1, floor),
                Kind = kind,
                IsBossFloor = kind == RoomKind.Boss
            };
            var occupied = new Dictionary<long, int>();
            var start = AddRoom(layout, occupied, 0, 0, RoomRole.Start, rng);

            if (layout.IsBossFloor)
            {
                // Boss-Etage: der Bossraum liegt geradeaus vor dem Start. Kein Suchen, nur der Kampf.
                var arena = AddRoom(layout, occupied, 0, 1, RoomRole.Boss, rng);
                Connect(layout, start, arena);
            }
            else
            {
                GrowTree(layout, occupied, RoomCountFor(layout.Floor, kind), rng);
                AddLoop(layout, occupied, rng);
            }

            AssignRoles(layout, rng);
            AssignCamps(layout, kind);
            AssignCover(layout, rng);
            layout.Bounds = ComputeBounds(layout);
            return layout;
        }

        private static void GrowTree(FloorLayout layout, Dictionary<long, int> occupied, int target, System.Random rng)
        {
            var attempts = 0;
            while (layout.Rooms.Count < target && attempts++ < 1000)
            {
                // Meist am zuletzt gebauten Raum anhaengen: das ergibt eher einen Weg durch die
                // Etage als einen kompakten Klumpen um den Start.
                var anchor = rng.NextDouble() < 0.7
                    ? layout.Rooms[layout.Rooms.Count - 1]
                    : layout.Rooms[rng.Next(layout.Rooms.Count)];
                var step = rng.Next(4);
                var cellX = anchor.CellX + StepX[step];
                var cellZ = anchor.CellZ + StepZ[step];
                if (Math.Abs(cellX) > GridLimit || Math.Abs(cellZ) > GridLimit) continue;
                if (occupied.ContainsKey(Key(cellX, cellZ))) continue;
                var room = AddRoom(layout, occupied, cellX, cellZ, RoomRole.Combat, rng);
                Connect(layout, anchor, room);
            }
        }

        /// <summary>Gelegentlich ein zusaetzlicher Durchgang, damit nicht jeder Rueckweg eine Sackgasse ist.</summary>
        private static void AddLoop(FloorLayout layout, Dictionary<long, int> occupied, System.Random rng)
        {
            if (rng.NextDouble() >= 0.35) return;
            var candidates = new List<(LayoutRoom A, LayoutRoom B)>();
            foreach (var room in layout.Rooms)
            {
                // Nur +X und +Z pruefen, damit jedes Nachbarpaar genau einmal vorkommt.
                foreach (var step in new[] { 0, 2 })
                {
                    if (!occupied.TryGetValue(Key(room.CellX + StepX[step], room.CellZ + StepZ[step]), out var index))
                        continue;
                    var neighbour = layout.Rooms[index];
                    if (!AreConnected(layout, room, neighbour)) candidates.Add((room, neighbour));
                }
            }
            if (candidates.Count == 0) return;
            var pick = candidates[rng.Next(candidates.Count)];
            Connect(layout, pick.A, pick.B);
        }

        private static LayoutRoom AddRoom(FloorLayout layout, Dictionary<long, int> occupied, int cellX, int cellZ,
            RoomRole role, System.Random rng)
        {
            float width;
            float depth;
            switch (role)
            {
                case RoomRole.Start:
                    width = 16f;
                    depth = 14f;
                    break;
                case RoomRole.Boss:
                    width = 26f;
                    depth = 22f;
                    break;
                default:
                    width = RoundHalf(18f + (float)rng.NextDouble() * 4f);
                    depth = RoundHalf(15f + (float)rng.NextDouble() * 3f);
                    break;
            }
            var room = new LayoutRoom
            {
                Index = layout.Rooms.Count,
                CellX = cellX,
                CellZ = cellZ,
                Role = role,
                Bounds = Area.Around(cellX * CellSize, cellZ * CellSize, width, depth)
            };
            layout.Rooms.Add(room);
            occupied[Key(cellX, cellZ)] = room.Index;
            return room;
        }

        private static void Connect(FloorLayout layout, LayoutRoom a, LayoutRoom b)
        {
            if (AreConnected(layout, a, b)) return;
            var door = new LayoutDoor { RoomA = a.Index, RoomB = b.Index };
            var half = FloorLayout.CorridorWidth * 0.5f;
            if (a.CellZ == b.CellZ)
            {
                var west = a.CellX < b.CellX ? a : b;
                var east = west == a ? b : a;
                var z = west.Bounds.Center.z;
                door.AlongX = true;
                door.Corridor = new Area(west.Bounds.MaxX - CorridorOverlap, east.Bounds.MinX + CorridorOverlap,
                    z - half, z + half);
                var westPortal = new Vector3(west.Bounds.MaxX, 0f, z);
                var eastPortal = new Vector3(east.Bounds.MinX, 0f, z);
                door.PortalA = a == west ? westPortal : eastPortal;
                door.PortalB = a == west ? eastPortal : westPortal;
            }
            else
            {
                var south = a.CellZ < b.CellZ ? a : b;
                var north = south == a ? b : a;
                var x = south.Bounds.Center.x;
                door.AlongX = false;
                door.Corridor = new Area(x - half, x + half,
                    south.Bounds.MaxZ - CorridorOverlap, north.Bounds.MinZ + CorridorOverlap);
                var southPortal = new Vector3(x, 0f, south.Bounds.MaxZ);
                var northPortal = new Vector3(x, 0f, north.Bounds.MinZ);
                door.PortalA = a == south ? southPortal : northPortal;
                door.PortalB = a == south ? northPortal : southPortal;
            }
            var index = layout.Doors.Count;
            layout.Doors.Add(door);
            a.Doors.Add(index);
            b.Doors.Add(index);
        }

        private static void AssignRoles(FloorLayout layout, System.Random rng)
        {
            layout.StartRoom = 0;
            var fromStart = layout.HopsFrom(0);
            var exit = layout.Rooms.Count > 1 ? 1 : 0;
            for (var i = 1; i < layout.Rooms.Count; i++)
                if (fromStart[i] > fromStart[exit] || (fromStart[i] == fromStart[exit] && i > exit))
                    exit = i;
            layout.ExitRoom = exit;

            if (layout.IsBossFloor)
            {
                layout.Rooms[exit].Role = RoomRole.Boss;
                return;
            }
            layout.Rooms[exit].Role = RoomRole.Lift;

            var candidates = new List<int>();
            for (var i = 1; i < layout.Rooms.Count; i++)
                if (i != exit) candidates.Add(i);
            if (candidates.Count == 0) return;

            // Erster Core am weitesten vom Start, zweiter am weitesten vom ersten. So liegen die
            // beiden nicht nebeneinander, und die Etage wird tatsaechlich erkundet.
            var first = candidates[0];
            foreach (var candidate in candidates)
                if (fromStart[candidate] > fromStart[first]) first = candidate;
            MakeCore(layout.Rooms[first], rng);
            if (candidates.Count < CoresPerFloor) return;

            var fromFirst = layout.HopsFrom(first);
            var second = -1;
            foreach (var candidate in candidates)
            {
                if (candidate == first) continue;
                if (second < 0 || fromFirst[candidate] > fromFirst[second] ||
                    (fromFirst[candidate] == fromFirst[second] && fromStart[candidate] > fromStart[second]))
                    second = candidate;
            }
            if (second >= 0) MakeCore(layout.Rooms[second], rng);
        }

        private static void MakeCore(LayoutRoom room, System.Random rng)
        {
            room.Role = RoomRole.Core;
            var bounds = room.Bounds;
            var offsetX = ((float)rng.NextDouble() - 0.5f) * bounds.Width * 0.3f;
            var offsetZ = ((float)rng.NextDouble() - 0.5f) * bounds.Depth * 0.3f;
            room.CorePosition = bounds.Center + new Vector3(offsetX, 0f, offsetZ);
        }

        private static void AssignCamps(FloorLayout layout, RoomKind kind)
        {
            var depthBonus = Math.Min(3, (layout.Floor - 1) / 3);
            var kindBonus = kind == RoomKind.Elite ? 1 : kind == RoomKind.Treasure ? -1 : 0;
            foreach (var room in layout.Rooms)
            {
                var baseSize = room.Role switch
                {
                    RoomRole.Combat => 4,
                    RoomRole.Core => 3,
                    RoomRole.Lift => 2,
                    _ => 0
                };
                room.CampSize = baseSize == 0 ? 0 : Math.Max(1, baseSize + depthBonus + kindBonus);
                var center = room.Bounds.Center;
                room.CampCenter = room.Role switch
                {
                    RoomRole.Core => room.CorePosition,
                    // Im Aufzugsraum nicht direkt auf dem Aufzug lagern.
                    RoomRole.Lift => center + new Vector3(room.Bounds.Width * 0.25f, 0f, 0f),
                    _ => center
                };
            }
        }

        /// <summary>
        /// Verteilt Deckung in den Kampfraeumen. In R.I.S.E. wird in grossen Arealen gekaempft, in denen
        /// Stellung zaehlt - ohne etwas, hinter das man treten kann, ist jede Position gleich gut.
        /// Der Startraum bleibt leer: dort soll niemand gleich um eine Ecke suchen muessen.
        /// </summary>
        private static void AssignCover(FloorLayout layout, System.Random rng)
        {
            foreach (var room in layout.Rooms)
            {
                if (room.Role == RoomRole.Start) continue;
                var wanted = room.Role == RoomRole.Boss ? 4 : 3;
                foreach (var candidate in CoverCandidates(room, rng))
                {
                    if (room.Cover.Count >= wanted) break;
                    if (!CoverFits(room, candidate)) continue;
                    room.Cover.Add(candidate);
                }
            }
        }

        /// <summary>
        /// Moegliche Deckungen eines Raums, in zufaelliger aber seedfester Reihenfolge. Die Mittelpunkte
        /// sind Bruchteile des Spielraums, der nach Abzug von Wandabstand und halber Groesse bleibt -
        /// damit liegt jede Deckung von vornherein vollstaendig im Raum.
        /// </summary>
        private static List<Area> CoverCandidates(LayoutRoom room, System.Random rng)
        {
            var bounds = room.Bounds;
            var result = new List<Area>();
            var shapes = new[] { new Vector2(3.6f, 1.4f), new Vector2(1.4f, 3.6f), new Vector2(1.9f, 1.9f) };
            var fractions = new[] { -0.84f, 0f, 0.84f };
            for (var shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
            {
                var size = shapes[shapeIndex];
                var spanX = bounds.Width * 0.5f - CoverWallMargin - size.x * 0.5f;
                var spanZ = bounds.Depth * 0.5f - CoverWallMargin - size.y * 0.5f;
                if (spanX < 0f || spanZ < 0f) continue;
                foreach (var fx in fractions)
                foreach (var fz in fractions)
                {
                    if (fx == 0f && fz == 0f) continue;
                    result.Add(Area.Around(bounds.Center.x + spanX * fx, bounds.Center.z + spanZ * fz,
                        size.x, size.y));
                }
            }
            // Fisher-Yates mit dem Etagen-Zufall: gleicher Seed, gleiche Raeume.
            for (var i = result.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        private static bool CoverFits(LayoutRoom room, Area candidate)
        {
            foreach (var placed in room.Cover)
                if (Expand(placed, CoverSpacing).Overlaps(candidate)) return false;
            if (DistanceToArea(room.CampCenter, candidate) < CoverSpotMargin) return false;
            if (room.Role == RoomRole.Core && DistanceToArea(room.CorePosition, candidate) < CoverSpotMargin)
                return false;
            // Im Aufzugs- und im Bossraum steht in der Mitte etwas, das frei bleiben muss.
            if (room.Role is RoomRole.Lift or RoomRole.Boss &&
                DistanceToArea(room.Bounds.Center, candidate) < CoverSpotMargin) return false;
            return true;
        }

        private static Area Expand(Area area, float margin)
            => new(area.MinX - margin, area.MaxX + margin, area.MinZ - margin, area.MaxZ + margin);

        private static float DistanceToArea(Vector3 point, Area area)
        {
            var dx = Math.Max(Math.Max(area.MinX - point.x, 0f), point.x - area.MaxX);
            var dz = Math.Max(Math.Max(area.MinZ - point.z, 0f), point.z - area.MaxZ);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static Area ComputeBounds(FloorLayout layout)
        {
            var bounds = layout.Rooms[0].Bounds;
            foreach (var room in layout.Rooms) bounds = bounds.Union(room.Bounds);
            foreach (var door in layout.Doors) bounds = bounds.Union(door.Corridor);
            return bounds;
        }

        private static bool AreConnected(FloorLayout layout, LayoutRoom a, LayoutRoom b)
        {
            foreach (var index in a.Doors)
                if (layout.Doors[index].Connects(b.Index)) return true;
            return false;
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        private static float RoundHalf(float value) => (float)Math.Round(value * 2f) / 2f;
    }
}
