using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Aschewelle: ein Schlag mit dem Zweihaender nach vorn, aus dem eine Front aus glühender
    /// Asche über den Boden läuft.
    ///
    /// Sie ersetzt den geweihten Boden. Der war ein Kreis, in den man sich stellte - eine Aktion,
    /// die nichts tut, sondern etwas hinlegt. XIROs Faehigkeit ist jetzt der Schlag selbst: sie
    /// beginnt an der Klinge, laeuft nach vorn und trifft alles auf ihrem Weg.
    ///
    /// Die Welle laeuft in Schritten und trifft jeden Gegner genau einmal - eine Front, die jeden
    /// Bildschritt neu Schaden macht, waere bei niedriger Bildrate schwaecher als bei hoher.
    /// </summary>
    public sealed class AshWave : MonoBehaviour
    {
        /// <summary>Wie schnell die Front laeuft, in Einheiten je Sekunde.</summary>
        public const float Speed = 17f;

        /// <summary>Halbe Breite der Front.</summary>
        public const float HalfWidth = 2.2f;

        /// <summary>Wie lange die Asche liegen bleibt und weiter brennt.</summary>
        public const float EmberSeconds = 3.5f;

        private Vector3 direction;
        private float travelled;
        private float range;
        private float damage;
        private DamageType type;
        private GameObject owner;
        private Color accent;
        private Vector3 origin;

        /// <summary>Wer schon getroffen wurde. Die Front trifft jeden genau einmal.</summary>
        private readonly HashSet<Health> struck = new();

        public static AshWave Launch(Vector3 from, Vector3 forward, float reach, float waveDamage,
            DamageType damageType, GameObject source, Color color)
        {
            var go = new GameObject("Ash Wave");
            go.transform.position = from;
            var wave = go.AddComponent<AshWave>();
            wave.origin = from;
            wave.direction = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            wave.range = Mathf.Max(1f, reach);
            wave.damage = waveDamage;
            wave.type = damageType;
            wave.owner = source;
            wave.accent = color;
            return wave;
        }

        private void Update()
        {
            var step = Speed * Time.deltaTime;
            var before = travelled;
            travelled = Mathf.Min(range, travelled + step);
            Sweep(before, travelled);
            if (travelled < range) return;
            Destroy(gameObject);
        }

        /// <summary>
        /// Traegt den Abschnitt zwischen zwei Bildern ab. Getroffen wird, wer in dem Streifen steht,
        /// den die Front in diesem Bild ueberstrichen hat - nicht, wer gerade zufaellig auf der
        /// Linie liegt.
        /// </summary>
        private void Sweep(float from, float to)
        {
            var mid = (from + to) * 0.5f;
            var point = origin + direction * mid;
            var half = Mathf.Max(0.6f, (to - from) * 0.5f + 0.9f);
            PrototypeVfx.SpawnShockwave(origin + direction * to, HalfWidth, accent);

            var active = Health.Active;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var health = active[i];
                if (!health || !health.IsAlive || health.Team != TeamId.Enemy) continue;
                if (struck.Contains(health)) continue;
                var offset = health.transform.position - point;
                offset.y = 0f;
                var along = Vector3.Dot(offset, direction);
                if (Mathf.Abs(along) > half) continue;
                var across = Vector3.Cross(Vector3.up, direction);
                if (Mathf.Abs(Vector3.Dot(offset, across)) > HalfWidth) continue;
                struck.Add(health);
                var force = direction * 5f;
                health.TakeDamage(new DamageInfo(damage, type, owner, health.transform.position, force));
                health.GetComponent<StatusReceiver>()?.ApplyBurn(damage * 0.2f, EmberSeconds, owner);
            }
        }
    }
}
