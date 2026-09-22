using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Eine Lebenskugel, die aus einem gefallenen Gegner springt und sich einsammeln laesst.
    ///
    /// Zweck ist nicht die Heilung - die gibt es am Etagenende ohnehin. Es geht um die Schleife
    /// danach: ein Abschuss hinterlaesst etwas, man geht kurz hin, und genau dieses Hingehen zieht
    /// einen in die Gruppe hinein statt aus ihr heraus. Deshalb zieht die Kugel erst aus der Naehe
    /// an und fliegt nicht von selbst quer durch den Raum.
    /// </summary>
    public sealed class HealthOrb : MonoBehaviour
    {
        private const float LifeSeconds = 14f;
        /// <summary>Ab hier zieht die Kugel zum Helden.</summary>
        private const float AttractRadius = 3.2f;
        /// <summary>So nah gilt sie als eingesammelt.</summary>
        private const float PickupRadius = 0.8f;

        private static readonly System.Collections.Generic.List<HealthOrb> ActiveOrbs = new();

        /// <summary>Alle liegenden Kugeln. Wie <see cref="Health.Active"/>: melden sich selbst an und ab.</summary>
        public static System.Collections.Generic.IReadOnlyList<HealthOrb> Active => ActiveOrbs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveOrbs.Clear();

        private void OnEnable()
        {
            if (!ActiveOrbs.Contains(this)) ActiveOrbs.Add(this);
        }

        private void OnDisable() => ActiveOrbs.Remove(this);

        /// <summary>Wem sie gehoert - nur dieser Held kann sie einsammeln.</summary>
        public Transform Owner => target;

        /// <summary>Wie viel sie heilt.</summary>
        public float Heal => amount;

        private Transform target;
        private float amount;
        private float expiresAt;
        private float bobPhase;
        private Vector3 settle;
        private Vector3 velocity;

        /// <summary>
        /// Wahrscheinlichkeit und Menge je Gegnerart. Starke Gegner lassen oefter und mehr fallen -
        /// das belohnt, sich an das Schwierige zu halten, statt Crawler zu farmen.
        /// </summary>
        private static (float Chance, float Heal) Reward(EnemyKind kind) => kind switch
        {
            EnemyKind.Crawler => (0.16f, 6f),
            EnemyKind.Shooter or EnemyKind.Marksman => (0.2f, 7f),
            EnemyKind.Shieldbearer => (0.3f, 10f),
            EnemyKind.Brute => (0.42f, 14f),
            EnemyKind.Elite => (1f, 26f),
            EnemyKind.IronWarden => (1f, 60f),
            EnemyKind.RiftTwin => (1f, 55f),
            EnemyKind.ChoirWarden => (1f, 65f),
            _ => (0.16f, 6f)
        };

        /// <summary>
        /// Salz der Beutefrage. Steht neben den vier Fragen, die der Gegner selbst stellt
        /// (siehe <see cref="EnemyAgent"/>), damit keine zwei davon dieselbe Zahl ziehen.
        /// </summary>
        private const int SaltDrop = 5000000;

        /// <summary>
        /// Faellt fuer diesen Gegner eine Kugel? Aus (Seed, Etage, Salz) gezogen und nicht aus
        /// <see cref="UnityEngine.Random"/>: Beute ist im Co-op gemeinsame Sache, und eine Kugel,
        /// die nur bei einem Spieler liegt, laesst zwei andere ins Leere laufen.
        /// </summary>
        public static bool Drops(EnemyKind kind, int runSeed, int floor, int salt)
            => RunRandom.Chance(runSeed, floor, salt + SaltDrop, Reward(kind).Chance);

        public static void TryDrop(EnemyKind kind, Vector3 position, Transform hero, int runSeed, int floor, int salt)
        {
            if (!hero) return;
            if (!Drops(kind, runSeed, floor, salt)) return;
            Spawn(position, Reward(kind).Heal, hero);
        }

        public static HealthOrb Spawn(Vector3 position, float heal, Transform hero)
        {
            var go = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Health Orb",
                position + Vector3.up * 0.6f, Vector3.one * 0.34f, new Color(0.2f, 1f, 0.45f), true);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            var orb = go.AddComponent<HealthOrb>();
            orb.target = hero;
            orb.amount = heal;
            orb.expiresAt = Time.time + LifeSeconds;
            // Wackeln und Sprung bleiben gewuerfelt: sie entscheiden nichts, sie verhindern nur,
            // dass zwei Kugeln nebeneinander im Gleichtakt huepfen.
            orb.bobPhase = Random.value * 6.28f;
            // Kleiner Sprung beim Erscheinen, damit sie nicht im Gegner klebt.
            orb.velocity = new Vector3(Random.Range(-1.2f, 1.2f), 3.4f, Random.Range(-1.2f, 1.2f));
            orb.settle = position + Vector3.up * 0.55f;
            return orb;
        }

        private void Update()
        {
            if (!target)
            {
                Destroy(gameObject);
                return;
            }
            var toHero = target.position + Vector3.up * 0.8f - transform.position;
            var distance = toHero.magnitude;

            if (distance <= PickupRadius)
            {
                Collect();
                return;
            }

            if (distance <= AttractRadius)
            {
                // Anziehen, mit der Naehe schneller: das Einsammeln soll sich saugend anfuehlen.
                var pull = Mathf.Lerp(14f, 3f, distance / AttractRadius);
                transform.position += toHero / Mathf.Max(0.0001f, distance) * (pull * Time.deltaTime);
                return;
            }

            // Freier Fall in die Ruhelage, danach leichtes Schweben.
            if (velocity.sqrMagnitude > 0.01f || transform.position.y > settle.y + 0.01f)
            {
                velocity += Vector3.down * (11f * Time.deltaTime);
                transform.position += velocity * Time.deltaTime;
                if (transform.position.y > settle.y) return;
                transform.position = new Vector3(transform.position.x, settle.y, transform.position.z);
                settle = transform.position;
                velocity = Vector3.zero;
                return;
            }
            bobPhase += Time.deltaTime * 2.6f;
            transform.position = settle + Vector3.up * (Mathf.Sin(bobPhase) * 0.12f);

            // Am Ende der Lebenszeit vergeht sie sichtbar, statt einfach zu verschwinden.
            var remaining = expiresAt - Time.time;
            if (remaining > 0f)
            {
                if (remaining < 2f) transform.localScale = Vector3.one * (0.34f * Mathf.Clamp01(remaining / 2f));
                return;
            }
            GameEvents.RaiseOrbEnded(amount, false);
            Destroy(gameObject);
        }

        private void Collect()
        {
            var health = target.GetComponent<Health>();
            if (health && health.IsAlive) health.Heal(amount);
            GameEvents.RaiseOrbEnded(amount, true);
            Sfx.Play(Sound.Pickup, transform.position, 0.7f);
            PrototypeVfx.SpawnHit(transform.position, Vector3.up, DamageType.Poison, false);
            Destroy(gameObject);
        }
    }
}
