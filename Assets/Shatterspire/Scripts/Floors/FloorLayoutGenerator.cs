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
        public const float CellSize = 30f;
        public const int CoresPerFloor = 2;
        private const int GridLimit = 3;
        private const float CorridorOverlap = 0.6f;

        private static readonly int[] StepX = { 1, -1, 0, 0 };
        private static readonly int[] StepZ = { 0, 0, 1, -1 };

        public static int RoomCountFor(int floor, RoomKind kind)
        {
            if (kind == RoomKind.Boss) return 2;
            var count = 4 + (floor >= 3 ? 1 : 0) + (kind == RoomKind.Elite ? 1 : 0);
            return Math.Min(6, count);
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
