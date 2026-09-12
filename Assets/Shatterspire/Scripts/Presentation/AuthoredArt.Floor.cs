using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public static partial class AuthoredArt
    {
        private const float FloorTileStep = 4.22f;
        private const float WallThickness = 0.8f;
        private const float WallColliderHeight = 2.6f;
        private const float WallModelSpacing = 3.8f;
        /// <summary>Hoehe der Deckungs-Kollider. Hoch genug, dass nichts darueber hinwegfliegt.</summary>
        private const float CoverHeight = 2.05f;
        /// <summary>Oberkante einer Bodenkachel je Einheit Kachelgroesse, einmal gemessen. -1 = noch nicht.</summary>
        private static float tileTopPerSize = -1f;

        /// <summary>
        /// Baut die Geometrie einer Etage aus ihrem Layout: Boden, Waende mit Durchgaengen, Gaenge,
        /// Deko und Kollision fuer den Spieler. Gegner und Bots bewegen sich ueber
        /// <see cref="FloorNavigation"/> und brauchen keine Physik.
        /// </summary>
        public static GameObject BuildFloor(FloorLayout layout, Transform parent)
        {
            var root = new GameObject($"Floor {layout.Floor} · Seed {layout.Seed}").transform;
            if (parent) root.SetParent(parent, false);
            var themeTint = FloorThemeTint(FloorCatalog.ThemeFor(layout.Floor));
            BuildAbyss(root, layout);
            foreach (var room in layout.Rooms) BuildRoom(root, layout, room, themeTint);
            foreach (var door in layout.Doors) BuildCorridor(root, layout, door, themeTint);
            LogCover(layout);
            return root.gameObject;
        }

        /// <summary>
        /// Schreibt je Raum, wie viel Deckung gesetzt wurde. Auf einem Bild laesst sich Deko nicht von
        /// Deckung unterscheiden - diese Zeile schon.
        /// </summary>
        private static void LogCover(FloorLayout layout)
        {
            var text = new System.Text.StringBuilder("SHATTERSPIRE Deckung Etage ").Append(layout.Floor).Append(':');
            foreach (var room in layout.Rooms)
            {
                text.Append(" [").Append(room.Index).Append(' ').Append(room.Role).Append(' ')
                    .Append(room.Cover.Count).Append('x');
                foreach (var cover in room.Cover)
                    text.Append(" (").Append(cover.Center.x.ToString("0.0")).Append('/')
                        .Append(cover.Center.z.ToString("0.0")).Append(' ')
                        .Append(cover.Width.ToString("0.0")).Append('×')
                        .Append(cover.Depth.ToString("0.0")).Append(')');
                text.Append(']');
            }
            Debug.Log(text.ToString());
        }

        private static Color FloorThemeTint(FloorTheme theme) => theme switch
        {
            FloorTheme.EmberFoundry => new Color(1f, 0.84f, 0.72f),
            FloorTheme.AstralArchive => new Color(0.9f, 0.86f, 1f),
            _ => new Color(1f, 0.98f, 0.94f)
        };

        private static void BuildAbyss(Transform root, FloorLayout layout)
        {
            var bounds = layout.Bounds;
            var center = bounds.Center;
            ArenaPart(root, PrimitiveType.Cube, "Tower Abyss", new Vector3(center.x, -1.4f, center.z),
                new Vector3(bounds.Width + 60f, 0.4f, bounds.Depth + 60f), new Color(0.05f, 0.045f, 0.06f),
                false, 0.02f, false);
        }

        private static void BuildRoom(Transform root, FloorLayout layout, LayoutRoom room, Color themeTint)
        {
            var bounds = room.Bounds;
            var center = bounds.Center;
            var roomRoot = new GameObject($"Room {room.Index} · {room.Role}").transform;
            roomRoot.SetParent(root, false);

            // Fundament unter dem Boden: der Raum liest sich als Plattform ueber dem Abgrund.
            // Oberkante knapp unter y = 0: dort liegt jetzt die Kacheloberflaeche, gleiche Hoehe wuerde flackern.
            ArenaPart(roomRoot, PrimitiveType.Cube, "Room Foundation", new Vector3(center.x, -0.47f, center.z),
                new Vector3(bounds.Width + 1.6f, 0.9f, bounds.Depth + 1.6f), new Color(0.22f, 0.17f, 0.14f),
                false, 0.04f, false);

            TileArea(roomRoot, bounds, themeTint, room.Role is RoomRole.Start or RoomRole.Lift);
            BuildRoomWalls(roomRoot, layout, room);
            BuildCover(roomRoot, room);
            DecorateRoom(roomRoot, room);
            CreateAccentLight(roomRoot, center + Vector3.up * 3.2f, RoleLight(room.Role),
                Mathf.Max(bounds.Width, bounds.Depth) * 0.75f, 0.55f);
        }

        private static Color RoleLight(RoomRole role) => role switch
        {
            RoomRole.Core => new Color(0.55f, 0.35f, 1f),
            RoomRole.Lift => new Color(0.3f, 1f, 0.6f),
            RoomRole.Boss => new Color(1f, 0.45f, 0.15f),
            _ => new Color(1f, 0.82f, 0.58f)
        };

        private static void TileArea(Transform parent, Area area, Color themeTint, bool highlight)
        {
            var countX = Mathf.Max(1, Mathf.RoundToInt(area.Width / FloorTileStep));
            var countZ = Mathf.Max(1, Mathf.RoundToInt(area.Depth / FloorTileStep));
            var stepX = area.Width / countX;
            var stepZ = area.Depth / countZ;
            // Leicht ueberlappend statt mit Fugen, falls Breite und Tiefe nicht aufgehen.
            var size = Mathf.Max(stepX, stepZ) + 0.12f;
            var edgeTint = new Color(0.72f, 0.68f, 0.63f) * themeTint;
            var innerTint = (highlight ? new Color(0.96f, 0.93f, 0.87f) : new Color(0.86f, 0.82f, 0.76f)) * themeTint;
            edgeTint.a = innerTint.a = 1f;
            for (var ix = 0; ix < countX; ix++)
            for (var iz = 0; iz < countZ; iz++)
            {
                var position = new Vector3(area.MinX + stepX * (ix + 0.5f), 0f, area.MinZ + stepZ * (iz + 0.5f));
                var edge = ix == 0 || iz == 0 || ix == countX - 1 || iz == countZ - 1;
                var model = edge && (ix + iz) % 3 == 0 ? "floor_tile_large_rocks" : "floor_tile_large";
                var tint = edge ? edgeTint : innerTint;
                var tile = SpawnDungeonModel(parent, model, position, ((ix + iz) & 1) * 90f, size,
                    FitAxis.Horizontal, tint);
                // Nachbarkacheln ueberlappen leicht. Zwei Flaechen auf exakt derselben Hoehe flackern
                // gegeneinander (Z-Fighting) - im Spielbild vom 11.09. als Schraffur an den Kachelkanten.
                // Jede Kachel liegt deshalb wenige Millimeter anders als ihre Nachbarn, auch diagonal.
                var stagger = ((ix + iz) & 1) * 0.006f + (ix & 1) * 0.003f;
                if (tile) tile.transform.position += Vector3.up * (stagger - TileTop(parent, size));
                if (!tile)
                    ArenaPart(parent, PrimitiveType.Cube, "Floor Slab", position + Vector3.down * 0.05f,
                        new Vector3(stepX + 0.05f, 0.1f, stepZ + 0.05f), tint, false, 0.08f, false);
            }
        }

        /// <summary>
        /// Hoehe der Kacheloberflaeche bei gegebener Groesse. Kacheln werden so tief gesetzt, dass ihre
        /// Oberflaeche bei y = 0 liegt - dort stehen die Figuren, knapp darueber liegen Schattenfleck,
        /// Auswahlring und Zielmarkierung. Vorher lag die Oberflaeche hoeher, diese flachen Quads steckten
        /// halb im Boden und flackerten, sobald sich jemand bewegte.
        /// </summary>
        private static float TileTop(Transform parent, float size)
        {
            if (tileTopPerSize < 0f)
            {
                tileTopPerSize = 0f;
                var probe = SpawnDungeonModel(parent, "floor_tile_large", Vector3.zero, 0f, 4f, FitAxis.Horizontal, Color.white);
                if (probe)
                {
                    if (TryGetBounds(probe, out var bounds)) tileTopPerSize = Mathf.Max(0f, bounds.max.y) / 4f;
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }
            return tileTopPerSize * size;
        }

        private static void BuildRoomWalls(Transform parent, FloorLayout layout, LayoutRoom room)
        {
            var bounds = room.Bounds;
            var south = new List<float>();
            var north = new List<float>();
            var west = new List<float>();
            var east = new List<float>();
            foreach (var doorIndex in room.Doors)
            {
                var portal = layout.Doors[doorIndex].PortalOf(room.Index);
                if (Mathf.Abs(portal.z - bounds.MinZ) < 0.01f) south.Add(portal.x);
                else if (Mathf.Abs(portal.z - bounds.MaxZ) < 0.01f) north.Add(portal.x);
                else if (Mathf.Abs(portal.x - bounds.MinX) < 0.01f) west.Add(portal.z);
                else if (Mathf.Abs(portal.x - bounds.MaxX) < 0.01f) east.Add(portal.z);
            }
            // Nord- und Suedwand reichen ueber die Ecken hinaus, damit dort keine Luecke bleibt.
            WallLine(parent, true, bounds.MinZ - WallThickness * 0.5f, bounds.MinX - WallThickness,
                bounds.MaxX + WallThickness, south);
            WallLine(parent, true, bounds.MaxZ + WallThickness * 0.5f, bounds.MinX - WallThickness,
                bounds.MaxX + WallThickness, north);
            WallLine(parent, false, bounds.MinX - WallThickness * 0.5f, bounds.MinZ, bounds.MaxZ, west);
            WallLine(parent, false, bounds.MaxX + WallThickness * 0.5f, bounds.MinZ, bounds.MaxZ, east);
        }

        private static void WallLine(Transform parent, bool alongX, float line, float from, float to, List<float> openings)
        {
            openings.Sort();
            var half = FloorLayout.CorridorWidth * 0.5f;
            var cursor = from;
            foreach (var opening in openings)
            {
                WallSegment(parent, alongX, line, cursor, opening - half);
                cursor = opening + half;
            }
            WallSegment(parent, alongX, line, cursor, to);
        }

        private static void WallSegment(Transform parent, bool alongX, float line, float from, float to)
        {
            var length = to - from;
            if (length < 0.2f) return;
            var middle = (from + to) * 0.5f;
            var center = alongX ? new Vector3(middle, 0f, line) : new Vector3(line, 0f, middle);

            var blocker = new GameObject("Wall Collider");
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = center + Vector3.up * (WallColliderHeight * 0.5f);
            blocker.AddComponent<BoxCollider>().size = alongX
                ? new Vector3(length, WallColliderHeight, WallThickness)
                : new Vector3(WallThickness, WallColliderHeight, length);
            // Auch Waende halten Geschosse auf - vorher flog jeder Schuss durch die halbe Etage.
            blocker.AddComponent<LevelObstacle>();

            var count = Mathf.Max(1, Mathf.RoundToInt(length / WallModelSpacing));
            var step = length / count;
            for (var i = 0; i < count; i++)
            {
                var along = from + step * (i + 0.5f);
                var position = alongX ? new Vector3(along, 0f, line) : new Vector3(line, 0f, along);
                var modelName = (i + Mathf.Abs(Mathf.RoundToInt(line))) % 5 == 0 ? "wall_broken" : "barrier";
                // Wie im Hof: bei Gierung 0 verlaeuft das Wandmodell entlang X.
                var model = SpawnDungeonModel(parent, modelName, position, alongX ? 0f : 90f, step + 0.15f,
                    FitAxis.Horizontal, new Color(0.94f, 0.9f, 0.84f));
                if (!model)
                    ArenaPart(parent, PrimitiveType.Cube, "Wall Block", position + Vector3.up * 0.9f,
                        alongX ? new Vector3(step, 1.8f, WallThickness) : new Vector3(WallThickness, 1.8f, step),
                        new Color(0.62f, 0.56f, 0.5f), false, 0.06f, false);
            }
        }

        private static void BuildCorridor(Transform root, FloorLayout layout, LayoutDoor door, Color themeTint)
        {
            var corridorRoot = new GameObject($"Corridor {door.RoomA}-{door.RoomB}").transform;
            corridorRoot.SetParent(root, false);
            var a = layout.Rooms[door.RoomA].Bounds;
            var b = layout.Rooms[door.RoomB].Bounds;
            var corridor = door.Corridor;
            // Nur der Abschnitt zwischen den Raumwaenden. Die Ueberlappung in die Raeume ist dort schon Boden.
            var span = door.AlongX
                ? new Area(Mathf.Min(a.MaxX, b.MaxX), Mathf.Max(a.MinX, b.MinX), corridor.MinZ, corridor.MaxZ)
                : new Area(corridor.MinX, corridor.MaxX, Mathf.Min(a.MaxZ, b.MaxZ), Mathf.Max(a.MinZ, b.MinZ));
            var middle = span.Center;
            ArenaPart(corridorRoot, PrimitiveType.Cube, "Corridor Foundation", new Vector3(middle.x, -0.47f, middle.z),
                door.AlongX
                    ? new Vector3(span.Width, 0.9f, span.Depth + 1.6f)
                    : new Vector3(span.Width + 1.6f, 0.9f, span.Depth),
                new Color(0.22f, 0.17f, 0.14f), false, 0.04f, false);
            TileArea(corridorRoot, span, themeTint * new Color(0.92f, 0.9f, 0.88f), false);

            if (door.AlongX)
            {
                WallSegment(corridorRoot, true, corridor.MinZ - WallThickness * 0.5f, span.MinX, span.MaxX);
                WallSegment(corridorRoot, true, corridor.MaxZ + WallThickness * 0.5f, span.MinX, span.MaxX);
            }
            else
            {
                WallSegment(corridorRoot, false, corridor.MinX - WallThickness * 0.5f, span.MinZ, span.MaxZ);
                WallSegment(corridorRoot, false, corridor.MaxX + WallThickness * 0.5f, span.MinZ, span.MaxZ);
            }
        }

        /// <summary>
        /// Baut die Deckung eines Raums: sichtbare Props auf der Flaeche aus dem Layout und ein
        /// Kollider, der genau dieses Rechteck fuellt. Figuren laufen ueber <see cref="FloorNavigation"/>
        /// darum herum, der Spieler stoesst physisch dagegen, Geschosse schlagen ein.
        /// </summary>
        private static void BuildCover(Transform parent, LayoutRoom room)
        {
            for (var i = 0; i < room.Cover.Count; i++)
            {
                var area = room.Cover[i];
                var centre = area.Center;
                var longSide = Mathf.Max(area.Width, area.Depth);
                var alongX = area.Width >= area.Depth;
                var barricade = longSide > Mathf.Min(area.Width, area.Depth) * 1.6f;
                var seed = room.Index * 7 + i;

                if (barricade)
                {
                    // Laengliche Deckung aus mehreren Teilen, damit sie nicht wie ein gedehnter Klotz wirkt.
                    var pieces = Mathf.Max(2, Mathf.RoundToInt(longSide / 1.8f));
                    var step = longSide / pieces;
                    for (var piece = 0; piece < pieces; piece++)
                    {
                        var offset = -longSide * 0.5f + step * (piece + 0.5f);
                        var position = alongX
                            ? new Vector3(centre.x + offset, 0.03f, centre.z)
                            : new Vector3(centre.x, 0.03f, centre.z + offset);
                        // Kisten stapeln sich hoch genug, um wirklich Deckung zu geben; die Bruchwand
                        // lockert die Reihe auf, bleibt aber auf derselben Hoehe.
                        var model = (seed + piece) % 3 == 0 ? "wall_broken" : "crates_stacked";
                        SpawnDungeonModel(parent, model, position, alongX ? 0f : 90f,
                            model == "wall_broken" ? step + 0.35f : 1.95f,
                            model == "wall_broken" ? FitAxis.Horizontal : FitAxis.Height,
                            new Color(0.93f, 0.89f, 0.83f));
                    }
                }
                else
                {
                    // Bewusst hohe Props: ein flacher Schutthaufen sieht aus, als koennte man darueber
                    // springen und schiessen - er haelt aber Figuren und Geschosse auf. Was blockt,
                    // muss auch so aussehen.
                    var model = seed % 3 == 0 ? "pillar_decorated"
                        : seed % 3 == 1 ? "crates_stacked" : "barrel_large_decorated";
                    var height = model == "pillar_decorated" ? 2.9f : 2.05f;
                    SpawnDungeonModel(parent, model, new Vector3(centre.x, 0.03f, centre.z),
                        (seed * 53) % 360, height, FitAxis.Height, new Color(0.95f, 0.91f, 0.85f));
                }

                var blocker = new GameObject("Cover Collider");
                blocker.transform.SetParent(parent, false);
                blocker.transform.localPosition = new Vector3(centre.x, CoverHeight * 0.5f, centre.z);
                blocker.AddComponent<BoxCollider>().size = new Vector3(area.Width, CoverHeight, area.Depth);
                blocker.AddComponent<LevelObstacle>();
            }
        }

        private static void DecorateRoom(Transform parent, LayoutRoom room)
        {
            var bounds = room.Bounds;
            var center = bounds.Center;
            const float inset = 2.2f;
            var corners = new[]
            {
                new Vector3(bounds.MinX + inset, 0.03f, bounds.MinZ + inset),
                new Vector3(bounds.MaxX - inset, 0.03f, bounds.MinZ + inset),
                new Vector3(bounds.MinX + inset, 0.03f, bounds.MaxZ - inset),
                new Vector3(bounds.MaxX - inset, 0.03f, bounds.MaxZ - inset)
            };

            switch (room.Role)
            {
                case RoomRole.Boss:
                    foreach (var corner in corners) PillarWithTorch(parent, corner, center);
                    break;
                case RoomRole.Start:
                    PillarWithTorch(parent, corners[2], center);
                    PillarWithTorch(parent, corners[3], center);
                    break;
                default:
                    var props = new[] { "crates_stacked", "barrel_large_decorated", "rubble_large", "pillar_decorated" };
                    for (var i = 0; i < corners.Length; i++)
                    {
                        // Nicht jede Ecke fuellen, sonst sieht jeder Raum gleich aus. Nur Ecken: dort
                        // verlaeuft kein Weg zwischen Tueren, und Gegner laufen selten hindurch.
                        if ((room.Index + i) % 3 == 1) continue;
                        var prop = props[(room.Index * 3 + i) % props.Length];
                        var tall = prop == "pillar_decorated";
                        SpawnDungeonModel(parent, prop, corners[i], (room.Index * 37 + i * 90) % 360, tall ? 2.8f : 2f,
                            tall ? FitAxis.Height : FitAxis.Horizontal, Color.white);
                    }
                    break;
            }
        }

        private static void PillarWithTorch(Transform parent, Vector3 corner, Vector3 roomCenter)
        {
            SpawnDungeonModel(parent, "pillar_decorated", corner, 0f, 3f, FitAxis.Height, new Color(0.94f, 0.9f, 0.84f));
            var toCenter = roomCenter - corner;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 0.01f) return;
            toCenter.Normalize();
            var yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            SpawnDungeonModel(parent, "torch_lit", corner + toCenter * 0.72f + Vector3.up * 1.38f, yaw + 180f, 1.15f,
                FitAxis.Height, new Color(1f, 0.78f, 0.52f));
        }
    }
}
