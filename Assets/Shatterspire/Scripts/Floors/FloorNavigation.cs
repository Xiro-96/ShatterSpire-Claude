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

        /// <summary>Abstand, mit dem eine Strecke zwischen zwei Umweg-Ecken an Deckungen vorbeifuehrt.</summary>
        private const float CornerClearance = DetourClearance * 0.85f;

        /// <summary>
        /// Abstand, ab dem eine Strecke eine Deckung noch trifft, auch fuer wen schon an ihr steht.
        /// Etwas weniger als ein Figurenradius: wer die Ecke nur streift, gleitet an ihr entlang.
        /// </summary>
        private const float BodyClearance = 0.3f;

        private readonly FloorLayout layout;
        private readonly int[][] hops;
        private readonly Area[] cover;
        private readonly Area[][] roomCover;

        // Arbeitsspeicher der Umweg-Suche. Gehoert zu dieser Etage, nicht zum Spiel - jede Etage hat
        // ihre eigene Navigation, und die Suche laeuft im Hauptfaden, eine nach der anderen.
        private readonly Vector3[] nodes;
        private readonly float[] reached;
        private readonly int[] previous;
        private readonly bool[] settled;
        private readonly int[] path;

        public FloorNavigation(FloorLayout floorLayout)
        {
            layout = floorLayout;
            hops = new int[layout.Rooms.Count][];
            for (var i = 0; i < hops.Length; i++) hops[i] = layout.HopsFrom(i);
            var list = new System.Collections.Generic.List<Area>();
            foreach (var room in layout.Rooms) list.AddRange(room.Cover);
            cover = list.ToArray();
            roomCover = new Area[layout.Rooms.Count][];
            for (var i = 0; i < roomCover.Length; i++) roomCover[i] = layout.Rooms[i].Cover.ToArray();
            var size = cover.Length * 4 + 2;
            nodes = new Vector3[size];
            reached = new float[size];
            previous = new int[size];
            settled = new bool[size];
            path = new int[size];
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

            if (fromRoom == toRoom) return Detour(from, to, fromRoom);
            return StepTowards(from, fromRoom, toRoom, to);
        }

        /// <summary>
        /// Der naechste Zwischenpunkt in einem Raum, um seine Deckungen herum. Ist die gerade Strecke
        /// frei, ist es das Ziel selbst - der haeufige Fall kostet nur diese eine Pruefung. Sonst der
        /// erste Punkt auf dem kuerzesten Weg ueber die Ecken der Deckungen, mit Umweg-Abstand: eine
        /// kleine Suche ueber eine Handvoll Punkte, denn ein Raum hat nur wenige Deckungen.
        ///
        /// Vorher wurde gierig die guenstigste Ecke der ersten Deckung auf der Strecke gewaehlt, und
        /// wer schon an einer Deckung stand, fuer den zaehlte sie gar nicht mehr. Lag das Ziel genau
        /// dahinter, lief die Figur stur gegen die Kiste: ein CharacterController gleitet an einer
        /// Flaeche nur entlang, wenn er schraeg auf sie trifft. Der Selbsttest fand den Helden so
        /// sechs Sekunden an derselben Ecke, die Begleiter auch. Eine gierige Wahl mit Koerperabstand
        /// liess die Figur stattdessen zwischen zwei Ecken pendeln - einen Schritt nach der einen war
        /// die andere wieder die guenstigere. Ein kuerzester Weg pendelt nicht: von jedem Punkt auf
        /// ihm fuehrt derselbe Rest weiter.
        /// </summary>
        private Vector3 Detour(Vector3 from, Vector3 to, int room)
        {
            var covers = room >= 0 && room < roomCover.Length ? roomCover[room] : cover;
            if (covers.Length == 0 || Clear(from, to, covers, DetourClearance)) return to;

            // Knoten 0 ist der Start, 1 das Ziel, danach die begehbaren Ecken.
            var count = 2;
            nodes[0] = from;
            nodes[1] = to;
            for (var i = 0; i < covers.Length && count + 4 <= nodes.Length; i++)
            {
                var area = covers[i];
                for (var corner = 0; corner < 4; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? area.MinX - DetourClearance : area.MaxX + DetourClearance, 0f,
                        (corner & 2) == 0 ? area.MinZ - DetourClearance : area.MaxZ + DetourClearance);
                    if (!OnFloor(point, 0.3f) || InsideCover(point, 0.2f)) continue;
                    nodes[count++] = point;
                }
            }

            for (var i = 0; i < count; i++)
            {
                reached[i] = float.MaxValue;
                previous[i] = -1;
                settled[i] = false;
            }
            reached[0] = 0f;
            for (var round = 0; round < count; round++)
            {
                var current = -1;
                for (var i = 0; i < count; i++)
                    if (!settled[i] && reached[i] < float.MaxValue && (current < 0 || reached[i] < reached[current]))
                        current = i;
                if (current < 0 || current == 1) break;
                settled[current] = true;
                for (var next = 1; next < count; next++)
                {
                    if (settled[next]) continue;
                    var through = reached[current] + Flat(nodes[current], nodes[next]);
                    // Erst die Laenge, dann die teure Pruefung.
                    if (through >= reached[next] || !Clear(nodes[current], nodes[next], covers, CornerClearance)) continue;
                    reached[next] = through;
                    previous[next] = current;
                }
            }
            // Kein Weg: geradeaus wie frueher, am Rand gleitet die Figur.
            if (previous[1] < 0) return to;

            var length = 0;
            for (var node = 1; node > 0 && length < path.Length; node = previous[node]) path[length++] = node;
            // path[length - 1] ist der erste Punkt nach dem Start. Steht man schon an ihm, gilt der naechste -
            // sonst waere er immer der guenstigste, und die Figur bliebe an der Ecke stehen.
            var first = path[length - 1];
            if (length > 1 && Flat(from, nodes[first]) < 0.3f) first = path[length - 2];
            return nodes[first];
        }

        /// <summary>
        /// Keine Deckung auf der Strecke. Jede zaehlt mit <paramref name="margin"/> Abstand - ausser
        /// fuer ein Ende, das schon in diesem Abstand liegt; fuer das zaehlt sie mit dem Koerperabstand.
        /// </summary>
        private static bool Clear(Vector3 a, Vector3 b, Area[] covers, float margin)
        {
            for (var i = 0; i < covers.Length; i++)
                if (Blocks(a, b, covers[i], margin) || Blocks(a, b, covers[i], BodyClearance)) return false;
            return true;
        }

        /// <summary>Die Strecke trifft das Rechteck, und keines ihrer Enden liegt schon darin.</summary>
        private static bool Blocks(Vector3 a, Vector3 b, Area area, float margin)
            => SegmentHitsArea(a, b, area, margin, true) && SegmentHitsArea(b, a, area, margin, true);

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
                return Near(from, portal) ? door.PortalOf(next) : Detour(from, portal, fromRoom);
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
