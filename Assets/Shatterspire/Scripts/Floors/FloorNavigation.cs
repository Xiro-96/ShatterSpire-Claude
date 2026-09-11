using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Wegfindung auf einer Etage ohne NavMesh. Raeume und Gaenge sind konvexe Rechtecke, deshalb
    /// ist jede gerade Strecke innerhalb eines Raums begehbar. Zwischen Raeumen fuehrt der Weg
    /// ueber die Portale an den Gangenden, der Raumgraph liefert den kuerzesten Wechsel.
    ///
    /// Ersetzt das bisherige Festklemmen auf einen Kreis mit Radius 14,55. Reine Rechnung, damit
    /// Gegner, Bots und Spieler dieselbe, im Test nachweisbare Regel teilen. Wird pro Etage erzeugt
    /// und herumgereicht, nicht global abgelegt.
    /// </summary>
    public sealed class FloorNavigation
    {
        private const float PortalReach = 1.2f;
        private const float RoomEntryDepth = 1.5f;

        private readonly FloorLayout layout;
        private readonly int[][] hops;

        public FloorNavigation(FloorLayout floorLayout)
        {
            layout = floorLayout;
            hops = new int[layout.Rooms.Count][];
            for (var i = 0; i < hops.Length; i++) hops[i] = layout.HopsFrom(i);
        }

        public FloorLayout Layout => layout;

        public int RoomAt(Vector3 point)
        {
            for (var i = 0; i < layout.Rooms.Count; i++)
                if (layout.Rooms[i].Bounds.Contains(point)) return i;
            return -1;
        }

        public int CorridorAt(Vector3 point)
        {
            for (var i = 0; i < layout.Doors.Count; i++)
                if (layout.Doors[i].Corridor.Contains(point)) return i;
            return -1;
        }

        public bool IsWalkable(Vector3 point, float radius)
        {
            foreach (var room in layout.Rooms)
                if (room.Bounds.Contains(point, radius)) return true;
            foreach (var door in layout.Doors)
                if (InsideCorridor(door, point, radius)) return true;
            return false;
        }

        /// <summary>Naechster begehbarer Punkt. Liegt der Punkt schon auf begehbarem Boden, bleibt er unveraendert.</summary>
        public Vector3 ClampToWalkable(Vector3 point, float radius)
        {
            if (IsWalkable(point, radius)) return point;
            var best = point;
            var bestSqr = float.MaxValue;
            foreach (var room in layout.Rooms)
                Consider(point, room.Bounds.Clamp(point, radius), ref best, ref bestSqr);
            foreach (var door in layout.Doors)
                Consider(point, ClampCorridor(door, point, radius), ref best, ref bestSqr);
            return best;
        }

        /// <summary>
        /// Naechster Zwischenpunkt auf dem Weg von <paramref name="from"/> nach <paramref name="to"/>.
        /// Die gerade Strecke dorthin ist immer begehbar.
        /// </summary>
        public Vector3 NextWaypoint(Vector3 from, Vector3 to)
        {
            var fromRoom = RoomAt(from);
            var toRoom = RoomAt(to);

            if (toRoom < 0)
            {
                var targetCorridor = CorridorAt(to);
                if (targetCorridor < 0) return ClampToWalkable(to, 0f);
                var targetDoor = layout.Doors[targetCorridor];
                if (fromRoom < 0) return to;
                if (targetDoor.Connects(fromRoom))
                {
                    var portal = targetDoor.PortalOf(fromRoom);
                    return Near(from, portal) ? to : portal;
                }
                toRoom = HopsTo(targetDoor.RoomA, fromRoom) <= HopsTo(targetDoor.RoomB, fromRoom)
                    ? targetDoor.RoomA
                    : targetDoor.RoomB;
            }

            if (fromRoom < 0)
            {
                var corridor = CorridorAt(from);
                if (corridor < 0) return ClampToWalkable(from, 0.3f);
                var door = layout.Doors[corridor];
                var exit = door.Connects(toRoom) ? toRoom
                    : HopsTo(door.RoomA, toRoom) <= HopsTo(door.RoomB, toRoom) ? door.RoomA : door.RoomB;
                return IntoRoom(door, exit);
            }

            if (fromRoom == toRoom) return to;
            return StepTowards(from, fromRoom, toRoom, to);
        }

        private Vector3 StepTowards(Vector3 from, int fromRoom, int toRoom, Vector3 fallback)
        {
            var remaining = HopsTo(fromRoom, toRoom);
            if (remaining == int.MaxValue) return fallback;
            foreach (var doorIndex in layout.Rooms[fromRoom].Doors)
            {
                var door = layout.Doors[doorIndex];
                var next = door.Other(fromRoom);
                if (HopsTo(next, toRoom) != remaining - 1) continue;
                var portal = door.PortalOf(fromRoom);
                // Am eigenen Portal angekommen: quer durch den Gang zum gegenueberliegenden.
                return Near(from, portal) ? door.PortalOf(next) : portal;
            }
            return fallback;
        }

        private Vector3 IntoRoom(LayoutDoor door, int room)
        {
            // Portal plus ein Stueck in den Raum hinein, damit der Gang sicher verlassen wird.
            var portal = door.PortalOf(room);
            var inward = layout.Rooms[room].Bounds.Center - portal;
            inward.y = 0f;
            var length = inward.magnitude;
            return length < 0.01f ? portal : portal + inward / length * RoomEntryDepth;
        }

        private int HopsTo(int from, int to)
            => from < 0 || to < 0 || from >= hops.Length || to >= hops.Length ? int.MaxValue : hops[from][to];

        private static bool InsideCorridor(LayoutDoor door, Vector3 point, float radius)
        {
            // Quer zum Gang zaehlt der Radius, laengs nicht: dort geht der Gang in den Raum ueber.
            var corridor = door.Corridor;
            return door.AlongX
                ? point.x >= corridor.MinX && point.x <= corridor.MaxX &&
                  point.z >= corridor.MinZ + radius && point.z <= corridor.MaxZ - radius
                : point.z >= corridor.MinZ && point.z <= corridor.MaxZ &&
                  point.x >= corridor.MinX + radius && point.x <= corridor.MaxX - radius;
        }

        private static Vector3 ClampCorridor(LayoutDoor door, Vector3 point, float radius)
        {
            var corridor = door.Corridor;
            if (door.AlongX)
            {
                point.x = Area.ClampAxis(point.x, corridor.MinX, corridor.MaxX);
                point.z = Area.ClampAxis(point.z, corridor.MinZ + radius, corridor.MaxZ - radius);
            }
            else
            {
                point.z = Area.ClampAxis(point.z, corridor.MinZ, corridor.MaxZ);
                point.x = Area.ClampAxis(point.x, corridor.MinX + radius, corridor.MaxX - radius);
            }
            return point;
        }

        private static void Consider(Vector3 origin, Vector3 candidate, ref Vector3 best, ref float bestSqr)
        {
            var dx = candidate.x - origin.x;
            var dz = candidate.z - origin.z;
            var sqr = dx * dx + dz * dz;
            if (sqr >= bestSqr) return;
            bestSqr = sqr;
            best = candidate;
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz < PortalReach * PortalReach;
        }
    }
}
