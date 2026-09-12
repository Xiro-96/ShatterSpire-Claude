using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Creates original chunky class emblems at runtime, avoiding mismatched 2D portraits.</summary>
    public static class UiIconFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Hero(HeroClassId hero) => Build("hero:" + hero, hero, -1);
        public static Sprite Ability(HeroClassId hero, int slot) => Build("ability:" + hero + ":" + slot, hero, slot);

        /// <summary>Ring fuer Fortschritt um Aktionsknoepfe; thickness als Anteil des Radius.</summary>
        public static Sprite Ring(float thickness) => Shape("ring:" + thickness, thickness);

        public static Sprite Disc() => Shape("disc", 1f);

        /// <summary>
        /// Pfeilspitze, die nach oben zeigt. Fuer die Randmarkierungen: sie muss auch bei 40 Pixeln
        /// und im Gewuehl noch eine erkennbare Richtung haben, deshalb ein Dreieck und kein Kreis.
        /// </summary>
        public static Sprite Chevron()
        {
            const string key = "chevron";
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SHATTERSPIRE " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Normiert auf -1 bis 1, Spitze oben.
                var nx = (x + 0.5f) / size * 2f - 1f;
                var ny = (y + 0.5f) / size * 2f - 1f;
                // Dreieck: Breite nimmt nach oben ab. Dazu eine ausgesparte Innenkante, damit die
                // Marke als Spitze und nicht als Klotz liest.
                var width = Mathf.Lerp(0.88f, 0f, Mathf.InverseLerp(-0.75f, 0.9f, ny));
                var inside = ny > -0.8f && ny < 0.92f && Mathf.Abs(nx) <= width;
                var hollow = ny < -0.3f && Mathf.Abs(nx) <= width * 0.42f;
                var alpha = inside && !hollow ? 1f : 0f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite Shape(string key, float thickness)
        {
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SHATTERSPIRE " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var outer = size * 0.5f - 1f;
            var inner = thickness >= 1f ? -1f : outer * (1f - thickness);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - size * 0.5f;
                var dy = y + 0.5f - size * 0.5f;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Senkrechter Verlauf, unten deckend, oben transparent. Lesbarkeit ohne Vollflaeche.</summary>
        public static Sprite VerticalFade()
        {
            const string key = "fade";
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            var texture = new Texture2D(4, 64, TextureFormat.RGBA32, false)
            {
                name = "SHATTERSPIRE fade", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[4 * 64];
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 4; x++)
                pixels[y * 4 + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.SmoothStep(1f, 0f, y / 63f)));
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite Build(string key, HeroClassId hero, int slot)
        {
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SHATTERSPIRE " + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var accent = HeroCatalog.Accent(hero);
            var dark = new Color(0.018f, 0.035f, 0.075f, 1f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - size * 0.5f;
                var dy = y - size * 0.5f;
                var radius = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = radius < 45f
                    ? (Color32)Color.Lerp(dark, accent, Mathf.Clamp01((45f - radius) / 92f))
                    : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);

            var symbol = Color.Lerp(Color.white, accent, 0.18f);
            if (slot < 0) DrawHero(texture, hero, symbol, accent);
            else DrawAbility(texture, hero, slot, symbol, accent);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 96f);
            Cache[key] = sprite;
            return sprite;
        }

        private static void DrawHero(Texture2D texture, HeroClassId hero, Color color, Color accent)
        {
            if (hero == HeroClassId.Ranger)
            {
                DrawArc(texture, new Vector2(48, 48), 28, -70, 70, 5, color);
                DrawLine(texture, new Vector2(58, 23), new Vector2(58, 73), 4, accent);
                DrawLine(texture, new Vector2(25, 48), new Vector2(70, 48), 5, color);
                DrawTriangle(texture, new Vector2(76, 48), 9, color);
            }
            else if (hero == HeroClassId.Guardian)
            {
                DrawLine(texture, new Vector2(32, 24), new Vector2(60, 70), 8, color);
                FillRect(texture, 45, 56, 31, 18, accent);
                FillRect(texture, 52, 22, 9, 40, color);
            }
            else
            {
                DrawCircle(texture, 48, 48, 17, accent);
                DrawCircle(texture, 48, 48, 9, color);
                for (var i = 0; i < 6; i++)
                {
                    var direction = Quaternion.Euler(0f, 0f, i * 60f) * Vector3.up;
                    DrawLine(texture, new Vector2(48, 48) + new Vector2(direction.x, direction.y) * 18f,
                        new Vector2(48, 48) + new Vector2(direction.x, direction.y) * 32f, 4, color);
                }
            }
        }

        private static void DrawAbility(Texture2D texture, HeroClassId hero, int slot, Color color, Color accent)
        {
            if (slot == 2)
            {
                DrawLine(texture, new Vector2(25, 58), new Vector2(68, 35), 8, color);
                DrawTriangle(texture, new Vector2(73, 32), 10, accent);
                return;
            }
            if (hero == HeroClassId.Guardian)
            {
                if (slot == 0) DrawArc(texture, new Vector2(48, 42), 29, -110, 55, 8, color);
                else if (slot == 1) { DrawCircle(texture, 48, 45, 23, accent); DrawLine(texture, new Vector2(27, 28), new Vector2(69, 67), 7, color); }
                else { for (var i = 0; i < 3; i++) DrawLine(texture, new Vector2(22, 30 + i * 14), new Vector2(72, 30 + i * 14), 5, color); }
            }
            else if (hero == HeroClassId.Arcanist)
            {
                DrawCircle(texture, 48, 48, slot == 0 ? 14 : 23, accent);
                DrawCircle(texture, 48, 48, slot == 0 ? 6 : 9, color);
                if (slot > 0) DrawArc(texture, new Vector2(48, 48), 31, 0, 330, 4, color);
            }
            else
            {
                var arrows = slot == 1 ? 3 : 1;
                for (var i = 0; i < arrows; i++)
                {
                    var y = 48 + (i - (arrows - 1) * 0.5f) * 15;
                    DrawLine(texture, new Vector2(20, y), new Vector2(70, y), 5, color);
                    DrawTriangle(texture, new Vector2(76, y), 8, accent);
                }
            }
        }

        private static void DrawCircle(Texture2D t, int cx, int cy, int radius, Color color)
        {
            for (var y = -radius; y <= radius; y++)
            for (var x = -radius; x <= radius; x++)
                if (x * x + y * y <= radius * radius) Set(t, cx + x, cy + y, color);
        }

        private static void FillRect(Texture2D t, int x, int y, int width, int height, Color color)
        {
            for (var py = y; py < y + height; py++)
            for (var px = x; px < x + width; px++) Set(t, px, py, color);
        }

        private static void DrawLine(Texture2D t, Vector2 a, Vector2 b, int width, Color color)
        {
            var steps = Mathf.CeilToInt(Vector2.Distance(a, b));
            for (var i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, steps));
                DrawCircle(t, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), width / 2, color);
            }
        }

        private static void DrawTriangle(Texture2D t, Vector2 center, int radius, Color color)
        {
            for (var x = -radius; x <= radius; x++)
            {
                var halfHeight = Mathf.RoundToInt((radius - Mathf.Abs(x)) * 0.8f);
                for (var y = -halfHeight; y <= halfHeight; y++) Set(t, Mathf.RoundToInt(center.x) + x, Mathf.RoundToInt(center.y) + y, color);
            }
        }

        private static void DrawArc(Texture2D t, Vector2 center, float radius, float start, float end, int width, Color color)
        {
            var previous = center + Direction(start) * radius;
            for (var angle = start + 4f; angle <= end; angle += 4f)
            {
                var point = center + Direction(angle) * radius;
                DrawLine(t, previous, point, width, color);
                previous = point;
            }
        }

        private static Vector2 Direction(float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void Set(Texture2D texture, int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height) return;
            texture.SetPixel(x, y, color);
        }
    }
}
