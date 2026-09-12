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
        /// <summary>Abstand, mit dem um eine Deckung herumgelaufen wird. Etwas mehr als ein Figurenradius.</summary>
        private const float DetourClearance = 0.75f;

        private readonly FloorLayout layout;
        private readonly int[][] hops;
        private readonly Area[] cover;

        public FloorNavigation(FloorLayout floorLayout)
        {
            layout = floorLayout;
            hops = new int[layout.Rooms.Count][];
            for (var i = 0; i < hops.Length; i++) hops[i] = layout.HopsFrom(i);
            var list = new System.Collections.Generic.List<Area>();
            foreach (var room in layout.Rooms) list.AddRange(room.Cover);
            cover = list.ToArray();
        }

        public FloorLayout Layout => layout;

        /// <summary>Alle Deckungen der Etage, in Raumreihenfolge.</summary>
        public System.Collections.Generic.IReadOnlyList<Area> Cover => cover;

        /// <summary>
        /// Freie Sicht zwischen zwei Punkten, also keine Deckung dazwischen. Der Armbruster spannt nur,
        /// wenn er sein Ziel sieht; wer hinter eine Deckung tritt, bricht den Schuss ab.
        /// </summary>
        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            for (var i = 0; i < cover.Length; i++)
                if (SegmentHitsArea(from, to, cover[i], 0f, false)) return false;
            return true;
        }

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
            => !InsideCover(point, radius) && OnFloor(point, radius);

        /// <summary>Auf Boden einer Etage, ohne Ruecksicht auf Deckung.</summary>
        private bool OnFloor(Vector3 point, float radius)
        {
            foreach (var room in layout.Rooms)
                if (room.Bounds.Contains(point, radius)) return true;
            foreach (var door in layout.Doors)
                if (InsideCorridor(door, point, radius)) return true;
            return false;
        }

        private bool InsideCover(Vector3 point, float radius)
        {
            for (var i = 0; i < cover.Length; i++)
                if (cover[i].Contains(point, -radius)) return true;
            return false;
        }

        /// <summary>Naechster begehbarer Punkt. Liegt der Punkt schon auf begehbarem Boden, bleibt er unveraendert.</summary>
        public Vector3 ClampToWalkable(Vector3 point, float radius)
        {
            if (IsWalkable(point, radius)) return point;
            var best = point;
            if (!OnFloor(point, radius))
            {
                var bestSqr = float.MaxValue;
                foreach (var room in layout.Rooms)
                    Consider(point, room.Bounds.Clamp(point, radius), ref best, ref bestSqr);
                foreach (var door in layout.Doors)
                    Consider(point, ClampCorridor(door, point, radius), ref best, ref bestSqr);
            }
            // Deckung steht immer mit Abstand zu den Waenden, deshalb bleibt der Punkt beim
            // Herausschieben sicher im Raum.
            return PushOutOfCover(best, radius);
        }

        /// <summary>Schiebt einen Punkt ueber die naechstgelegene Kante aus jeder Deckung heraus.</summary>
        private Vector3 PushOutOfCover(Vector3 point, float radius)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                var moved = false;
                for (var i = 0; i < cover.Length; i++)
                {
                    if (!cover[i].Contains(point, -radius)) continue;
                    var minX = cover[i].MinX - radius;
                    var maxX = cover[i].MaxX + radius;
                    var minZ = cover[i].MinZ - radius;
                    var maxZ = cover[i].MaxZ + radius;
                    var west = point.x - minX;
                    var east = maxX - point.x;
                    var south = point.z - minZ;
                    var north = maxZ - point.z;
                    var shortest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));
                    if (shortest <= 0f) continue;
                    if (shortest == west) point.x = minX - 0.002f;
                    else if (shortest == east) point.x = maxX + 0.002f;
                    else if (shortest == south) point.z = minZ - 0.002f;
                    else point.z = maxZ + 0.002f;
                    moved = true;
                }
                if (!moved) break;
            }
            return point;
        }

        /// <summary>
        /// Der weiteste begehbare Punkt auf der Strecke von <paramref name="from"/> nach
        /// <paramref name="to"/>. Anders als <see cref="ClampToWalkable"/> springt das Ergebnis nicht
        /// zurueck, wenn das Ziel im Abgrund oder hinter einer Wand liegt - es bleibt an der Kante
        /// stehen.
        ///
        /// Gebraucht wird das von Spruengen: wer auf eine Wand zielt, soll bis an die Wand kommen.
        /// Mit reinem Klemmen wurde ein Sprung von 7,5 Metern gemessen zu 2,1 Metern, weil der
        /// naechstgelegene begehbare Punkt hinter dem Springer lag.
        /// </summary>
        public Vector3 FurthestWalkableAlong(Vector3 from, Vector3 to, float radius, float step = 0.5f)
        {
            var delta = to - from;
            delta.y = 0f;
            var distance = delta.magnitude;
            if (distance < 0.001f) return ClampToWalkable(from, radius);
            var direction = delta / distance;
            var best = ClampToWalkable(from, radius);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.05f, step)));
            for (var i = 1; i <= steps; i++)
            {
                var candidate = from + direction * (distance * i / steps);
                if (!IsWalkable(candidate, radius)) break;
                best = candidate;
            }
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

            if (fromRoom == toRoom) return Detour(from, to);
            return StepTowards(from, fromRoom, toRoom, to);
        }

        /// <summary>
        /// Liegt eine Deckung auf der geraden Strecke, wird die guenstigste ihrer vier Ecken zum
        /// naechsten Zwischenpunkt. Steht man bereits an der Deckung, zaehlt sie nicht mehr als
        /// Hindernis - dann schiebt <see cref="ClampToWalkable"/> an ihrer Flanke entlang.
        /// </summary>
        private Vector3 Detour(Vector3 from, Vector3 to)
        {
            var index = FirstCoverOnSegment(from, to, DetourClearance);
            if (index < 0) return to;
            var blocker = cover[index];
            var best = to;
            var bestCost = float.MaxValue;
            for (var corner = 0; corner < 4; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? blocker.MinX - DetourClearance : blocker.MaxX + DetourClearance, 0f,
                    (corner & 2) == 0 ? blocker.MinZ - DetourClearance : blocker.MaxZ + DetourClearance);
                if (!OnFloor(point, 0.3f) || InsideCover(point, 0.2f)) continue;
                if (FirstCoverOnSegment(from, point, DetourClearance * 0.85f) >= 0) continue;
                var cost = Flat(from, point) + Flat(point, to);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = point;
            }
            return best;
        }

        private int FirstCoverOnSegment(Vector3 from, Vector3 to, float margin)
        {
            var nearest = -1;
            var nearestSqr = float.MaxValue;
            for (var i = 0; i < cover.Length; i++)
            {
                if (!SegmentHitsArea(from, to, cover[i], margin, true)) continue;
                var centre = cover[i].Center;
                var sqr = (centre.x - from.x) * (centre.x - from.x) + (centre.z - from.z) * (centre.z - from.z);
                if (sqr >= nearestSqr) continue;
                nearestSqr = sqr;
                nearest = i;
            }
            return nearest;
        }

        /// <summary>
        /// Schnitt einer Strecke mit einem achsparallelen Rechteck (Slab-Test auf der XZ-Ebene).
        /// Mit <paramref name="ignoreIfStartInside"/> zaehlt ein Rechteck nicht, in dem die Strecke
        /// bereits beginnt - sonst gaebe es fuer eine Figur direkt an der Deckung keinen Ausweg.
        /// </summary>
        private static bool SegmentHitsArea(Vector3 from, Vector3 to, Area area, float margin, bool ignoreIfStartInside)
        {
            var minX = area.MinX - margin;
            var maxX = area.MaxX + margin;
            var minZ = area.MinZ - margin;
            var maxZ = area.MaxZ + margin;
            if (ignoreIfStartInside && from.x >= minX && from.x <= maxX && from.z >= minZ && from.z <= maxZ)
                return false;
            var enter = 0f;
            var exit = 1f;
            return Slab(from.x, to.x - from.x, minX, maxX, ref enter, ref exit) &&
                   Slab(from.z, to.z - from.z, minZ, maxZ, ref enter, ref exit) &&
                   enter <= exit;
        }

        private static bool Slab(float origin, float delta, float min, float max, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) < 0.00001f) return origin >= min && origin <= max;
            var first = (min - origin) / delta;
            var second = (max - origin) / delta;
            if (first > second) (first, second) = (second, first);
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, second);
            return enter <= exit;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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
                return Near(from, portal) ? door.PortalOf(next) : Detour(from, portal);
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
