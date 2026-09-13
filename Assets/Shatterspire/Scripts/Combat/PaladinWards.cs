using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Geweihter Boden: ein Kreis, in dem die Gruppe weniger einsteckt und sich erholt, und in dem
    /// Gegner langsamer werden.
    ///
    /// LYRAs Verb ist ein anderes als das der drei anderen: sie macht Orte sicher, statt im Moment
    /// des Tastendrucks Schaden zu machen. Deshalb ist ihre Faehigkeit keine Salve, sondern ein
    /// Stueck Boden - und im Co-op fuer drei ist ein Stueck Boden etwas, das man teilt.
    /// </summary>
    public sealed class HallowedGround : MonoBehaviour
    {
        /// <summary>Wie viel Schaden im Kreis abgehalten wird.</summary>
        public const float DamageTaken = 0.62f;

        /// <summary>Anteil des Hoechstlebens, der je Sekunde zurueckkommt.</summary>
        public const float RegenerationPerSecond = 0.02f;

        /// <summary>Wie stark Gegner im Kreis gebremst werden.</summary>
        public const float EnemySlow = 0.62f;

        private float radius;
        private float endsAt;
        private GameObject decal;

        /// <summary>
        /// Wer gerade drinsteht und dafuer eine Abwehr angehaengt bekommen hat. Die Zuordnung
        /// gehoert an die Instanz: zwei Kreise duerfen sich nicht gegenseitig die Abwehr loeschen.
        /// </summary>
        private readonly Dictionary<Health, System.Func<DamageInfo, float, float>> warded = new();

        public static HallowedGround Spawn(Vector3 center, float groundRadius, float seconds, Color accent)
        {
            var go = new GameObject("Hallowed Ground");
            go.transform.position = center;
            var ward = go.AddComponent<HallowedGround>();
            ward.radius = groundRadius;
            ward.endsAt = Time.time + seconds;
            ward.decal = PrototypeVfx.SpawnZone(center, groundRadius, accent, seconds);
            return ward;
        }

        private void Update()
        {
            var expired = Time.time >= endsAt;
            var active = Health.Active;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var health = active[i];
                if (!health) continue;
                var inside = !expired && health.IsAlive && Inside(health.transform.position);
                if (health.Team == TeamId.Player) UpdateAlly(health, inside);
                else if (inside) health.GetComponent<StatusReceiver>()?.ApplySlow(EnemySlow, 0.4f);
            }
            // Abwehren einsammeln, deren Traeger verschwunden ist - sonst haelt der Kreis eine
            // Referenz auf einen zerstoerten Gegner fest.
            if (!expired) return;
            foreach (var pair in warded)
                if (pair.Key) pair.Key.RemoveDamageFilter(pair.Value);
            warded.Clear();
            if (decal) Destroy(decal);
            Destroy(gameObject);
        }

        private void UpdateAlly(Health health, bool inside)
        {
            if (inside)
            {
                if (!warded.ContainsKey(health))
                {
                    System.Func<DamageInfo, float, float> filter = (_, amount) => amount * DamageTaken;
                    warded[health] = filter;
                    health.AddDamageFilter(filter);
                }
                health.Heal(health.Maximum * RegenerationPerSecond * Time.deltaTime);
                return;
            }
            if (!warded.TryGetValue(health, out var existing)) return;
            health.RemoveDamageFilter(existing);
            warded.Remove(health);
        }

        private void OnDisable()
        {
            foreach (var pair in warded)
                if (pair.Key) pair.Key.RemoveDamageFilter(pair.Value);
            warded.Clear();
        }

        private bool Inside(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>
    /// AEGIS: eine Kuppel, die den Schaden der ganzen Gruppe schluckt - und ihn am Ende
    /// zurueckgibt.
    ///
    /// Sie ist deshalb keine groessere Faehigkeit, sondern eine andere Frage: nicht "wie viel
    /// Schaden mache ich", sondern "wie viel halte ich aus, um ihn zurueckzugeben". Wer die Kuppel
    /// setzt und sich dann versteckt, bekommt nichts; wer darin steht und einsteckt, raeumt den
    /// Raum ab. Das ist die einzige Ultimate im Spiel, deren Wirkung davon abhaengt, wie viel man
    /// waehrenddessen einsteckt.
    /// </summary>
    public sealed class AegisDome : MonoBehaviour
    {
        /// <summary>Anteil des geschluckten Schadens, der als Entladung zurueckgeht.</summary>
        public const float ReturnFactor = 2.4f;

        /// <summary>Grundschaden der Entladung, auch wenn nichts angekommen ist.</summary>
        public const float FloorDamage = 40f;

        private float radius;
        private float endsAt;
        private GameObject decal;
        private Color accent;
        private GameObject owner;
        private float absorbed;
        private readonly Dictionary<Health, System.Func<DamageInfo, float, float>> shielded = new();

        /// <summary>Wie viel die Kuppel bisher geschluckt hat. Fuer die Anzeige und fuer Tests.</summary>
        public float Absorbed => absorbed;

        public static AegisDome Spawn(Vector3 center, float domeRadius, float seconds, Color color, GameObject source)
        {
            var go = new GameObject("Aegis Dome");
            go.transform.position = center;
            var dome = go.AddComponent<AegisDome>();
            dome.radius = domeRadius;
            dome.endsAt = Time.time + seconds;
            dome.accent = color;
            dome.owner = source;
            dome.decal = PrototypeVfx.SpawnZone(center, domeRadius, color, seconds);
            return dome;
        }

        private void Update()
        {
            if (Time.time < endsAt)
            {
                var active = Health.Active;
                for (var i = active.Count - 1; i >= 0; i--)
                {
                    var health = active[i];
                    if (!health || health.Team != TeamId.Player) continue;
                    var inside = health.IsAlive && Inside(health.transform.position);
                    if (inside && !shielded.ContainsKey(health))
                    {
                        System.Func<DamageInfo, float, float> filter = (_, amount) =>
                        {
                            // Ein Viertel kommt durch: eine Kuppel, die alles schluckt, nimmt dem
                            // Kampf darin jede Spannung.
                            absorbed += amount * 0.75f;
                            return amount * 0.25f;
                        };
                        shielded[health] = filter;
                        health.AddDamageFilter(filter);
                    }
                    else if (!inside && shielded.TryGetValue(health, out var existing))
                    {
                        health.RemoveDamageFilter(existing);
                        shielded.Remove(health);
                    }
                }
                return;
            }
            Discharge();
        }

        private void Discharge()
        {
            Release();
            var damage = FloorDamage + absorbed * ReturnFactor;
            var hits = CombatUtility.Explode(transform.position, radius + 1.2f, damage, TeamId.Enemy,
                DamageType.Lightning, owner);
            PrototypeVfx.SpawnShockwave(transform.position, radius + 1.6f, accent);
            Sfx.Play2D(Sound.ChainDetonate);
            CameraController.Impulse(0.24f);
            Debug.Log($"SHATTERSPIRE Aegis: {absorbed:0} Schaden geschluckt, {damage:0} zurueckgegeben, "
                      + $"{hits} getroffen.");
            if (decal) Destroy(decal);
            Destroy(gameObject);
        }

        private void Release()
        {
            foreach (var pair in shielded)
                if (pair.Key) pair.Key.RemoveDamageFilter(pair.Value);
            shielded.Clear();
        }

        private void OnDisable() => Release();

        private bool Inside(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }
    }
}
