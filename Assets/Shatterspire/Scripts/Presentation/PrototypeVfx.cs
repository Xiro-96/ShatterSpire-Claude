using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public static class PrototypeFactory
    {
        private static readonly Dictionary<int, Material> Materials = new();

        public static Material CreateMaterial(Color color, bool emissive = false, float smoothness = 0.3f, float metallic = 0f)
        {
            var key = color.GetHashCode();
            key = key * 397 ^ (emissive ? 1 : 0);
            key = key * 397 ^ Mathf.RoundToInt(smoothness * 100f);
            key = key * 397 ^ Mathf.RoundToInt(metallic * 100f);
            if (Materials.TryGetValue(key, out var cached) && cached) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color, name = "SS Material" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.35f);
            }
            Materials[key] = material;
            return material;
        }

        /// <summary>
        /// Unbeleuchtet und additiv - eine Klingenspur soll leuchten und nicht von der Raumbeleuchtung
        /// abhaengen, und sie soll sich dort, wo sie sich selbst ueberlappt, nicht abdunkeln.
        /// </summary>
        public static Material CreateTrailMaterial(Color color)
        {
            var key = color.GetHashCode() * 397 ^ 0x7241;
            if (Materials.TryGetValue(key, out var cached) && cached) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "SS Blade Trail" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            material.color = color;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);   // SrcAlpha
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 1f);   // One
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            Materials[key] = material;
            return material;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Color color, bool emissive = false, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color, emissive);
            if (!keepCollider)
            {
                RemoveCollider(go.GetComponent<Collider>());
            }
            return go;
        }

        public static void RemoveCollider(Collider collider)
        {
            if (!collider) return;
            collider.enabled = false;
            Object.Destroy(collider);
        }

        /// <summary>Creates a tiny procedural soft disc or ring for ground decals.</summary>
        public static Material CreateRadialDecal(Color color, float innerRadius = 0f)
        {
            var key = color.GetHashCode();
            key = key * 397 ^ Mathf.RoundToInt(innerRadius * 1000f);
            key = key * 397 ^ 9187;
            if (Materials.TryGetValue(key, out var cached) && cached) return cached;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = innerRadius > 0f ? "SS Soft Ring" : "SS Soft Shadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var px = (x + 0.5f) / size * 2f - 1f;
                var py = (y + 0.5f) / size * 2f - 1f;
                var radius = Mathf.Sqrt(px * px + py * py);
                var alpha = innerRadius > 0f
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius - 0.08f, innerRadius + 0.03f, radius)) *
                      (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f, 1f, radius)))
                    : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 1f, radius));
                var pixel = color;
                pixel.a *= Mathf.Clamp01(alpha);
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = texture.name };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            Materials[key] = material;
            return material;
        }
    }

    public static class PrototypeVfx
    {
        public static Color ElementColor(DamageType type) => type switch
        {
            DamageType.Fire => new Color(1f, 0.27f, 0.05f),
            DamageType.Ice => new Color(0.25f, 0.85f, 1f),
            DamageType.Lightning => new Color(1f, 0.9f, 0.18f),
            DamageType.Poison => new Color(0.38f, 1f, 0.2f),
            DamageType.Void => new Color(0.68f, 0.22f, 1f),
            DamageType.Holy => new Color(1f, 0.95f, 0.6f),
            _ => new Color(0.25f, 0.95f, 1f)
        };

        // Ton haengt an denselben Aufrufen wie das Bild: was blitzt, klingt auch. So gibt es keine
        // Stelle im Spiel, die einen Effekt zeigt und dabei stumm bleibt.
        public static void SpawnMuzzle(Vector3 position, Vector3 direction)
        {
            Sfx.Play(Sound.Shot, position);
            var go = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Muzzle Flash", position + direction * 0.2f,
                new Vector3(0.28f, 0.28f, 0.46f), new Color(1f, 0.8f, 0.2f), true);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.rotation = Quaternion.LookRotation(direction);
            go.AddComponent<VfxPulse>().Configure(0.11f, 2.2f, true);
        }

        public static void SpawnHit(Vector3 position, Vector3 force, DamageType type, bool critical)
        {
            var color = ElementColor(type);
            var direction = force;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            direction.Normalize();

            var core = PrototypeFactory.Primitive(PrimitiveType.Sphere, critical ? "Critical Impact" : "Impact Flash",
                position + Vector3.up * 0.08f, Vector3.one * (critical ? 0.24f : 0.16f),
                Color.Lerp(Color.white, color, 0.32f), true);
            core.AddComponent<VfxPulse>().Configure(critical ? 0.16f : 0.11f, critical ? 3.2f : 2.35f, true);

            var streakCount = critical ? 7 : 4;
            for (var i = 0; i < streakCount; i++)
            {
                var angle = (i - (streakCount - 1) * 0.5f) * (critical ? 18f : 22f);
                var streakDirection = Quaternion.Euler(0f, angle, 0f) * -direction;
                var streak = PrototypeFactory.Primitive(PrimitiveType.Cube, "Impact Streak",
                    position + Vector3.up * 0.08f + streakDirection * 0.16f,
                    new Vector3(0.035f, 0.035f, critical ? 0.72f : 0.46f), color, true);
                streak.transform.rotation = Quaternion.LookRotation(streakDirection);
                streak.AddComponent<VfxPulse>().Configure(critical ? 0.2f : 0.14f, 0f, false);
            }

            Sfx.Play(critical ? Sound.HitCritical : Sound.HitLight, position, critical ? 1f : 0.85f);
            CameraController.Impulse(critical ? 0.085f : 0.025f);
        }

        public static void SpawnEnemyArrival(Vector3 position, bool elite)
        {
            Sfx.Play(Sound.EnemyArrival, position, elite ? 1f : 0.7f);
            var color = elite ? new Color(1f, 0.18f, 0.58f) : new Color(0.62f, 0.24f, 1f);
            color.a = elite ? 0.9f : 0.76f;
            var socket = GameObject.CreatePrimitive(PrimitiveType.Quad);
            socket.name = "Enemy Arrival Rune";
            socket.transform.position = position + Vector3.up * 0.035f;
            socket.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            socket.transform.localScale = Vector3.one * 0.42f;
            socket.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(color, 0.64f);
            PrototypeFactory.RemoveCollider(socket.GetComponent<Collider>());
            socket.AddComponent<VfxPulse>().Configure(0.5f, elite ? 6.2f : 4.9f, true);
            for (var i = 0; i < (elite ? 6 : 3); i++)
            {
                var angle = i * (360f / (elite ? 6 : 3));
                var shard = PrototypeFactory.Primitive(PrimitiveType.Cube, "Arrival Shard",
                    position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.45f + Vector3.up * 0.18f,
                    new Vector3(0.08f, 0.42f, 0.08f), color, true);
                shard.transform.rotation = Quaternion.Euler(18f, angle, 38f);
                shard.AddComponent<VfxPulse>().Configure(0.42f, 0f, false);
            }
        }

        public static void SpawnHeavyReady(Vector3 position)
        {
            Sfx.Play(Sound.HeavyReady, position, 0.8f);
            var color = new Color(1f, 0.76f, 0.1f);
            var ring = PrototypeFactory.Primitive(PrimitiveType.Cylinder, "Heavy Ready Ring",
                position + Vector3.up * 0.035f, new Vector3(0.4f, 0.018f, 0.4f), color, true);
            ring.AddComponent<VfxPulse>().Configure(0.42f, 5.6f, true);
            var flare = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Heavy Ready Flare",
                position + Vector3.up * 1.05f, Vector3.one * 0.18f, Color.white, true);
            flare.AddComponent<VfxPulse>().Configure(0.28f, 3.2f, true);
        }

        /// <summary>
        /// Der Treffer einer Waffe: ein kurzer Aufblitz genau dort, wo die Klinge ankommt, und
        /// Funken, die in Schlagrichtung wegspritzen.
        ///
        /// Vorher lag bei jedem normalen Hieb eine Schockwelle auf dem Boden - drei Ringe, die einen
        /// Meter vor der Figur aus dem Fussboden wuchsen, auch wenn der Schlag danebenging. Das las
        /// sich als Zauberimpuls, nicht als Waffentreffer. Hier haengt der Effekt am getroffenen
        /// Gegner und entsteht gar nicht erst, wenn nichts getroffen wurde.
        /// </summary>
        public static void SpawnWeaponImpact(Vector3 point, Vector3 direction, Color color, bool heavy)
        {
            Sfx.Play(heavy ? Sound.HitHeavy : Sound.HitLight, point, heavy ? 0.9f : 0.7f);
            var core = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Weapon Impact", point,
                Vector3.one * (heavy ? 0.34f : 0.24f), Color.Lerp(Color.white, color, 0.35f), true);
            Object.Destroy(core.GetComponent<Collider>());
            core.AddComponent<VfxPulse>().Configure(heavy ? 0.16f : 0.12f, heavy ? 2.6f : 2.1f, true);

            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            var sparks = heavy ? 6 : 4;
            for (var i = 0; i < sparks; i++)
            {
                var spark = PrototypeFactory.Primitive(PrimitiveType.Cube, "Impact Spark", point,
                    new Vector3(0.06f, 0.06f, 0.3f), color, true);
                Object.Destroy(spark.GetComponent<Collider>());
                // In Schlagrichtung weg, faecherfoermig - nicht ringsum: der Funke soll die
                // Richtung des Hiebs zeigen.
                var spread = Quaternion.Euler(Random.Range(-26f, 26f), Random.Range(-44f, 44f), 0f);
                spark.AddComponent<VfxShard>().Configure(
                    (spread * flat + Vector3.up * 0.45f) * Random.Range(3.4f, 5.6f));
            }
        }

        /// <summary>
        /// Ein Urteil, das herabfaehrt: eine Saeule aus Licht, die in sich zusammenfaellt, mit einem
        /// Ring am Fuss. Anders als eine Explosion kommt sie von oben - das ist der ganze Punkt.
        /// </summary>
        public static void SpawnJudgement(Vector3 point, float radius, Color color)
        {
            Sfx.Play(Sound.Smash, point, 0.95f);
            CameraController.Impulse(Mathf.Min(0.18f, radius * 0.05f));
            var column = PrototypeFactory.Primitive(PrimitiveType.Cylinder, "Verdict Column",
                point + Vector3.up * 5.5f, new Vector3(radius * 0.9f, 5.5f, radius * 0.9f),
                Color.Lerp(color, Color.white, 0.4f), true);
            Object.Destroy(column.GetComponent<Collider>());
            column.AddComponent<VfxPulse>().Configure(0.3f, 0f, false);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "Verdict Ring";
            ring.transform.position = point + Vector3.up * 0.05f;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = Vector3.one * radius;
            var tint = color;
            tint.a = 0.8f;
            ring.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(tint, 0.72f);
            PrototypeFactory.RemoveCollider(ring.GetComponent<Collider>());
            ring.AddComponent<VfxPulse>().Configure(0.34f, 2.4f, true);

            for (var i = 0; i < 6; i++)
            {
                var mote = PrototypeFactory.Primitive(PrimitiveType.Cube, "Verdict Mote",
                    point + Vector3.up * 0.3f, new Vector3(0.08f, 0.24f, 0.08f), color, true);
                Object.Destroy(mote.GetComponent<Collider>());
                mote.AddComponent<VfxShard>().Configure(
                    (Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward + Vector3.up * 1.4f) * Random.Range(2.2f, 3.6f));
            }
        }

        public static void SpawnExplosion(Vector3 position, float radius, Color color)
        {
            // Eine kleine Detonation soll nicht so laut sein wie eine grosse.
            Sfx.Play(radius >= 2.4f ? Sound.Explosion : Sound.HitHeavy, position,
                Mathf.Clamp(0.45f + radius * 0.12f, 0.45f, 1f));
            CameraController.Impulse(Mathf.Min(0.16f, radius * 0.025f));
            var ring = PrototypeFactory.Primitive(PrimitiveType.Cylinder, "Impact Ring", position + Vector3.up * 0.06f,
                new Vector3(0.24f, 0.025f, 0.24f), color, true);
            Object.Destroy(ring.GetComponent<Collider>());
            ring.AddComponent<VfxPulse>().Configure(0.24f, Mathf.Max(8f, radius * 8f), true);

            var core = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Impact Core", position + Vector3.up * 0.55f,
                Vector3.one * 0.2f, Color.Lerp(Color.white, color, 0.45f), true);
            Object.Destroy(core.GetComponent<Collider>());
            core.AddComponent<VfxPulse>().Configure(0.16f, Mathf.Max(4f, radius * 2.5f), true);

            for (var i = 0; i < 5; i++)
            {
                var spark = PrototypeFactory.Primitive(PrimitiveType.Cube, "Impact Spark", position + Vector3.up * 0.35f,
                    new Vector3(0.1f, 0.28f, 0.1f), color, true);
                Object.Destroy(spark.GetComponent<Collider>());
                spark.AddComponent<VfxShard>().Configure((Quaternion.Euler(0f, i * 72f, 0f) * Vector3.forward + Vector3.up * 0.7f) * Random.Range(2.5f, 4.2f));
            }
        }

        public static void SpawnShockwave(Vector3 position, float radius, Color color)
        {
            Sfx.Play(Sound.Shockwave, position, Mathf.Clamp(0.55f + radius * 0.1f, 0.55f, 1f));
            for (var i = 0; i < 3; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
                ring.name = "Layered Combat Shockwave";
                ring.transform.position = position + Vector3.up * (0.045f + i * 0.012f);
                ring.transform.rotation = Quaternion.Euler(90f, i * 17f, 0f);
                ring.transform.localScale = Vector3.one * (0.34f + i * 0.08f);
                var tint = Color.Lerp(color, Color.white, i * 0.18f);
                tint.a = 0.82f - i * 0.16f;
                ring.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(tint, 0.68f);
                PrototypeFactory.RemoveCollider(ring.GetComponent<Collider>());
                var targetDiameter = radius * 2f * (0.82f + i * 0.1f);
                ring.AddComponent<VfxPulse>().Configure(0.3f + i * 0.06f,
                    targetDiameter / Mathf.Max(0.01f, ring.transform.localScale.x), true);
            }
        }

        public static void SpawnDeath(Vector3 position, Color color)
        {
            Sfx.Play(Sound.Death, position);
            for (var i = 0; i < 11; i++)
            {
                var shard = PrototypeFactory.Primitive(PrimitiveType.Cube, "Death Shard", position + Vector3.up,
                    new Vector3(0.12f, Random.Range(0.2f, 0.38f), 0.12f), i % 3 == 0 ? Color.white : color, true);
                Object.Destroy(shard.GetComponent<Collider>());
                var pulse = shard.AddComponent<VfxShard>();
                pulse.Configure(Random.onUnitSphere * Random.Range(2.5f, 5f));
            }
        }

        public static void SpawnTrail(Vector3 position, Color color)
        {
            var go = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Dash Trail", position + Vector3.up * 0.62f,
                new Vector3(1.05f, 0.16f, 1.05f), color, true);
            Object.Destroy(go.GetComponent<Collider>());
            go.AddComponent<VfxPulse>().Configure(0.35f, 1.8f, false);
        }

        /// <summary>
        /// Dauerhafte Flaeche am Boden fuer Ultimate-Zonen - Krater, Zeitriss. Anders als eine
        /// Vorwarnung bleibt sie stehen und pulsiert nur leicht, damit sie als Gelaende liest und
        /// nicht als Gefahr, auf die man reagieren muss.
        /// </summary>
        public static GameObject SpawnZone(Vector3 position, float radius, Color color, float seconds)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Ultimate Zone";
            go.transform.position = position + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * radius * 2f;
            // Fuellung sehr zurueckhaltend: die Zone soll den Boden faerben, nicht ihn zumalen.
            var fill = color;
            fill.a = 0.2f;
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(fill);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            go.AddComponent<VfxZonePulse>().Configure(seconds);

            // Die Kante als echter Ring. Vorher stand hier ein Zylinder - der ist massiv und
            // erschien im Bild als undurchsichtiger Teller ueber dem halben Raum.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rim.name = "Zone Rim";
            rim.transform.SetParent(go.transform, false);
            rim.transform.localPosition = Vector3.up * 0.01f;
            rim.transform.localRotation = Quaternion.identity;
            rim.transform.localScale = Vector3.one * 1.02f;
            var edge = color;
            edge.a = 0.75f;
            rim.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(edge, 0.88f);
            PrototypeFactory.RemoveCollider(rim.GetComponent<Collider>());
            return go;
        }

        public static GameObject SpawnTelegraph(Vector3 position, float radius, bool line)
        {
            Sfx.Play(Sound.Telegraph, position, 0.7f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Attack Area Telegraph";
            go.transform.position = position + Vector3.up * 0.035f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * radius * 2f;
            var danger = new Color(1f, 0.06f, 0.025f, 0.58f);
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(danger, 0.68f);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            go.AddComponent<DangerTelegraphMotion>();
            return go;
        }

        public static GameObject SpawnTelegraphLine(Vector3 origin, Vector3 direction, float length, float width)
        {
            Sfx.Play(Sound.Telegraph, origin, 0.7f);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            direction.Normalize();
            var midpoint = origin + direction * (length * 0.5f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Attack Line Telegraph";
            go.transform.position = midpoint + Vector3.up * 0.038f;
            go.transform.rotation = Quaternion.Euler(90f, Quaternion.LookRotation(direction).eulerAngles.y, 0f);
            go.transform.localScale = new Vector3(width, length, 1f);
            var danger = new Color(1f, 0.045f, 0.02f, 0.66f);
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(danger);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            go.AddComponent<DangerTelegraphMotion>().Configure(false);
            return go;
        }
    }

    /// <summary>Leises Atmen einer Ultimate-Zone. Nur Optik, keine Wirkung.</summary>
    public sealed class VfxZonePulse : MonoBehaviour
    {
        private Vector3 baseScale;
        private float endsAt;

        public void Configure(float seconds) => endsAt = Time.time + seconds;

        private void Awake() => baseScale = transform.localScale;

        private void Update()
        {
            var pulse = 1f + Mathf.Sin(Time.time * 3.4f) * 0.02f;
            // Am Ende schrumpft die Flaeche, damit das Auslaufen sichtbar ist.
            var remaining = endsAt - Time.time;
            if (remaining < 0.6f) pulse *= Mathf.Clamp01(remaining / 0.6f);
            transform.localScale = baseScale * pulse;
        }
    }

    public sealed class DangerTelegraphMotion : MonoBehaviour
    {
        private Vector3 baseScale;
        private bool radial = true;
        private float phase;

        public void Configure(bool isRadial) => radial = isRadial;

        private void Awake()
        {
            baseScale = transform.localScale;
            phase = Random.value * 2f;
        }

        private void Update()
        {
            var pulse = 0.965f + Mathf.Sin(Time.time * 10f + phase) * 0.035f;
            transform.localScale = radial
                ? baseScale * pulse
                : new Vector3(baseScale.x * pulse, baseScale.y, baseScale.z);
        }
    }

    public sealed class VfxPulse : MonoBehaviour
    {
        private float duration;
        private float targetScale;
        private bool grow;
        private float age;
        private Vector3 initial;
        public void Configure(float seconds, float scale, bool shouldGrow) { duration = seconds; targetScale = scale; grow = shouldGrow; initial = transform.localScale; }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(age / duration);
            transform.localScale = grow ? Vector3.Lerp(initial, initial * targetScale, t) : Vector3.Lerp(initial, Vector3.zero, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    public sealed class VfxShard : MonoBehaviour
    {
        private Vector3 velocity;
        private Vector3 initialScale;
        private float age;
        public void Configure(Vector3 value) { velocity = value; initialScale = transform.localScale; }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            velocity += Vector3.down * (7f * Time.unscaledDeltaTime);
            transform.position += velocity * Time.unscaledDeltaTime;
            transform.Rotate(220f * Time.unscaledDeltaTime, 170f * Time.unscaledDeltaTime, 90f * Time.unscaledDeltaTime);
            transform.localScale = initialScale * Mathf.Lerp(1f, 0f, age / 0.65f);
            if (age > 0.65f) Destroy(gameObject);
        }
    }

    public sealed class DamageNumber : MonoBehaviour
    {
        private TextMesh text;
        private float age;
        public static void Spawn(Vector3 point, float amount, bool critical, DamageType type)
        {
            var go = new GameObject("Damage Number");
            go.transform.position = point + Vector3.up * 1.2f + Random.insideUnitSphere * 0.18f;
            var number = go.AddComponent<DamageNumber>();
            number.text = go.AddComponent<TextMesh>();
            number.text.text = Mathf.RoundToInt(amount).ToString();
            number.text.fontSize = critical ? 48 : 38;
            number.text.characterSize = critical ? 0.075f : 0.06f;
            number.text.anchor = TextAnchor.MiddleCenter;
            number.text.alignment = TextAlignment.Center;
            number.text.color = critical ? new Color(1f, 0.88f, 0.1f) : ElementColor(type);
        }
        private void Update()
        {
            age += Time.unscaledDeltaTime;
            transform.position += Vector3.up * (1.4f * Time.unscaledDeltaTime);
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
            var color = text.color; color.a = 1f - age / 0.65f; text.color = color;
            if (age >= 0.65f) Destroy(gameObject);
        }
        private static Color ElementColor(DamageType type) => PrototypeVfx.ElementColor(type);
    }
}
