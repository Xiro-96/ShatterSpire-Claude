using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
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
