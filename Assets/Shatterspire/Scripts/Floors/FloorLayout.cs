using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public enum RoomRole { Start, Combat, Core, Lift, Boss }

    /// <summary>
    /// Achsparalleles Rechteck auf dem Boden (XZ-Ebene). Reine Rechnung ohne Unity-Laufzeitaufrufe,
    /// damit Etagen-Layouts und Navigation ohne Editor testbar sind.
    /// </summary>
    public readonly struct Area
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float MinZ;
        public readonly float MaxZ;

        public Area(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MaxX = Math.Max(minX, maxX);
            MinZ = Math.Min(minZ, maxZ);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public static Area Around(float centerX, float centerZ, float width, float depth)
            => new(centerX - width * 0.5f, centerX + width * 0.5f, centerZ - depth * 0.5f, centerZ + depth * 0.5f);

        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public Vector3 Center => new((MinX + MaxX) * 0.5f, 0f, (MinZ + MaxZ) * 0.5f);

        public bool Contains(Vector3 point, float margin = 0f)
            => point.x >= MinX + margin && point.x <= MaxX - margin &&
               point.z >= MinZ + margin && point.z <= MaxZ - margin;

        public bool Overlaps(Area other)
            => MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;

        public Area Union(Area other)
            => new(Math.Min(MinX, other.MinX), Math.Max(MaxX, other.MaxX),
                Math.Min(MinZ, other.MinZ), Math.Max(MaxZ, other.MaxZ));

        public Vector3 Clamp(Vector3 point, float margin)
        {
            point.x = ClampAxis(point.x, MinX + margin, MaxX - margin);
            point.z = ClampAxis(point.z, MinZ + margin, MaxZ - margin);
            return point;
        }

        internal static float ClampAxis(float value, float min, float max)
            => min > max ? (min + max) * 0.5f : value < min ? min : value > max ? max : value;
    }

    public sealed class LayoutRoom
    {
        public int Index { get; internal set; }
        public int CellX { get; internal set; }
        public int CellZ { get; internal set; }
        public Area Bounds { get; internal set; }
        public RoomRole Role { get; internal set; }

        /// <summary>Groesse des Gegnerlagers in diesem Raum. 0 heisst: kein Lager.</summary>
        public int CampSize { get; internal set; }
        public Vector3 CampCenter { get; internal set; }

        /// <summary>Position des Power Core. Nur in Raeumen mit <see cref="RoomRole.Core"/> gueltig.</summary>
        public Vector3 CorePosition { get; internal set; }

        /// <summary>Indizes in <see cref="FloorLayout.Doors"/>.</summary>
        public List<int> Doors { get; } = new();
    }

    /// <summary>
    /// Verbindung zweier benachbarter Raeume: ein gerader Gang, der ein Stueck in beide Raeume
    /// hineinreicht, damit die begehbare Flaeche lueckenlos ist.
    /// </summary>
    public sealed class LayoutDoor
    {
        public int RoomA { get; internal set; }
        public int RoomB { get; internal set; }
        public Area Corridor { get; internal set; }
        public Vector3 PortalA { get; internal set; }
        public Vector3 PortalB { get; internal set; }

        /// <summary>true: der Gang verlaeuft entlang der X-Achse.</summary>
        public bool AlongX { get; internal set; }

        public int Other(int room) => room == RoomA ? RoomB : RoomA;
        public Vector3 PortalOf(int room) => room == RoomA ? PortalA : PortalB;
        public bool Connects(int room) => room == RoomA || room == RoomB;
    }

    /// <summary>
    /// Eine Etage als Folge verbundener Raeume, nach dem Vorbild von R.I.S.E.: Power Cores in
    /// verschiedenen Raeumen finden, dann den Aufzug im entferntesten Raum nehmen.
    /// </summary>
    public sealed class FloorLayout
    {
        public const float CorridorWidth = 5f;

        public int Seed { get; internal set; }
        public int Floor { get; internal set; }
        public RoomKind Kind { get; internal set; }
        public bool IsBossFloor { get; internal set; }
        public List<LayoutRoom> Rooms { get; } = new();
        public List<LayoutDoor> Doors { get; } = new();
        public int StartRoom { get; internal set; }

        /// <summary>Aufzugsraum, auf Boss-Etagen der Bossraum.</summary>
        public int ExitRoom { get; internal set; }

        /// <summary>Umschliessendes Rechteck aller Raeume und Gaenge.</summary>
        public Area Bounds { get; internal set; }

        public Vector3 SpawnPoint => Rooms[StartRoom].Bounds.Center;
        public Vector3 ExitPoint => Rooms[ExitRoom].Bounds.Center;

        public IEnumerable<LayoutRoom> CoreRooms
        {
            get
            {
                foreach (var room in Rooms)
                    if (room.Role == RoomRole.Core) yield return room;
            }
        }

        /// <summary>Anzahl Raumwechsel vom angegebenen Raum zu jedem anderen, int.MaxValue wenn unerreichbar.</summary>
        public int[] HopsFrom(int room)
        {
            var distance = new int[Rooms.Count];
            for (var i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;
            if (room < 0 || room >= Rooms.Count) return distance;
            var queue = new Queue<int>();
            distance[room] = 0;
            queue.Enqueue(room);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var doorIndex in Rooms[current].Doors)
                {
                    var next = Doors[doorIndex].Other(current);
                    if (distance[next] != int.MaxValue) continue;
                    distance[next] = distance[current] + 1;
                    queue.Enqueue(next);
                }
            }
            return distance;
        }
    }
}
