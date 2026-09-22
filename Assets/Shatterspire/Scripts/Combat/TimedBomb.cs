using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Eine Ladung mit Zuendschnur. Sie fliegt im Bogen an ihren Platz, blinkt schneller, je naeher
    /// die Zuendung kommt, und reisst dann eine Flaeche auf.
    ///
    /// Das ist das eigene Verb des Bombers: waehrend die anderen drei Helden im Moment des
    /// Tastendrucks Schaden machen, legt er vor und spielt dem Gegner voraus. Deshalb liegt die
    /// Zuendschnur offen im Bild - sie ist die Information, aus der die Entscheidung entsteht.
    /// </summary>
    /// <summary>Wohin eine Ladung fliegt. Reine Rechnung, damit sie sich ohne Spiel pruefen laesst.</summary>
    public static class BombThrow
    {
        /// <summary>So weit wird hoechstens vorgehalten - wie bei Geschossen (Targeting.PredictIntercept).</summary>
        public const float MaxLeadSeconds = 0.85f;

        /// <summary>
        /// Die Landestelle einer Ladung.
        ///
        /// Vorher immer der Zielpunkt der Eingabe, begrenzt auf die Wurfweite. Auf dem Telefon liegt
        /// dieser Punkt aber stets zehn Einheiten in Blickrichtung - die Ladung flog damit auf volle
        /// Weite, egal wie nah der Gegner stand, und ein Gegner in drei Einheiten wurde nie
        /// getroffen. Der Selbsttest mass KORR auf Etage 1 bei 18 Schaden je Kampfsekunde, die
        /// anderen bei 32 bis 44. Und selbst auf der richtigen Stelle ist ein laufender Gegner nach
        /// Flug und Zuendschnur (gut eine Sekunde) nicht mehr da.
        ///
        /// Jetzt: gibt es ein Ziel, und zeigt die Eingabe nicht auf eine Stelle in Wurfweite (die
        /// Maus), fliegt die Ladung dorthin, wo das Ziel bei der Zuendung sein wird.
        /// </summary>
        public static Vector3 Landing(Vector3 hero, Vector3 aimPoint, bool autoAim, Vector3 direction,
            Vector3? target, Vector3 targetVelocity, float maximum, float secondsToBlast)
        {
            hero.y = 0f;
            aimPoint.y = 0f;
            var offset = aimPoint - hero;
            var pointsAtASpot = !autoAim && offset.sqrMagnitude >= 1f && offset.magnitude <= maximum;
            if (target.HasValue && !pointsAtASpot)
            {
                var velocity = targetVelocity;
                velocity.y = 0f;
                var at = target.Value + velocity * Mathf.Clamp(secondsToBlast, 0f, MaxLeadSeconds);
                at.y = 0f;
                var toTarget = at - hero;
                return toTarget.magnitude > maximum ? hero + toTarget.normalized * maximum : hero + toTarget;
            }
            if (pointsAtASpot) return hero + offset;
            direction.y = 0f;
            return hero + (direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward) * maximum;
        }
    }

    public sealed class TimedBomb : MonoBehaviour
    {
        private static readonly List<TimedBomb> Live = new();

        /// <summary>Alle scharfen Ladungen. Der Kettenzuender braucht sie.</summary>
        public static IReadOnlyList<TimedBomb> Active => Live;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Live.Clear();

        private GameObject owner;
        private DamageType type;
        private float damage;
        private float radius;
        private float fuseEnds;
        private float fuseSeconds;
        private Vector3 start;
        private Vector3 landing;
        private float flightSeconds;
        private float flightElapsed;
        private bool landed;
        private bool spent;
        private Renderer shell;
        private GameObject marker;

        /// <summary>
        /// Laedt ein Treffer dieser Ladung den schweren Balken?
        ///
        /// Nur die leichten Wuerfe tun das - genau wie bei den anderen drei Helden, wo der Balken
        /// aus leichten Treffern kommt und nicht aus der Faehigkeit. Ohne diese Meldung blieb KORRs
        /// Balken auf null, und die rechte Maustaste war ohne jede Wirkung.
        /// </summary>
        private bool chargesHeavy;

        /// <summary>
        /// Legt eine Ladung. <paramref name="flight"/> ist die Wurfzeit bis zur Landung,
        /// <paramref name="fuse"/> die Zeit danach bis zur Zuendung.
        /// </summary>
        public static TimedBomb Throw(Vector3 from, Vector3 to, float flight, float fuse, float blastRadius,
            float blastDamage, DamageType damageType, GameObject source, Color accent,
            bool chargesHeavyMeter = false)
        {
            var go = PrototypeFactory.Primitive(PrimitiveType.Sphere, "Timed Bomb",
                from, Vector3.one * 0.42f, accent, true);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            var bomb = go.AddComponent<TimedBomb>();
            bomb.owner = source;
            bomb.type = damageType;
            bomb.damage = blastDamage;
            bomb.radius = blastRadius;
            bomb.fuseSeconds = Mathf.Max(0.05f, fuse);
            bomb.start = from;
            bomb.landing = to;
            bomb.flightSeconds = Mathf.Max(0.01f, flight);
            bomb.shell = go.GetComponent<Renderer>();
            bomb.chargesHeavy = chargesHeavyMeter;
            // Die Flaeche liegt von Anfang an am Boden: man muss vor der Zuendung wissen, wo es knallt.
            bomb.marker = PrototypeVfx.SpawnZone(to, blastRadius, accent, flight + bomb.fuseSeconds);
            return bomb;
        }

        /// <summary>Zuendet alle scharfen Ladungen. Gibt zurueck, wie viele es waren.</summary>
        public static int DetonateAll(float radiusFactor, float damageFactor)
        {
            var count = 0;
            // Rueckwaerts, weil das Zuenden die Liste veraendert.
            for (var i = Live.Count - 1; i >= 0; i--)
            {
                var bomb = Live[i];
                if (!bomb || bomb.spent) continue;
                bomb.radius *= radiusFactor;
                bomb.damage *= damageFactor;
                bomb.Detonate();
                count++;
            }
            return count;
        }

        private void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        private void OnDisable() => Live.Remove(this);

        private void Update()
        {
            if (spent) return;
            if (!landed)
            {
                flightElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(flightElapsed / flightSeconds);
                var flat = Vector3.Lerp(start, landing, t);
                transform.position = flat + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 2.6f + 0.3f);
                transform.Rotate(420f * Time.deltaTime, 260f * Time.deltaTime, 0f, Space.Self);
                if (t < 1f) return;
                landed = true;
                fuseEnds = Time.time + fuseSeconds;
                Sfx.Play(Sound.BombLand, transform.position, 0.5f);
                return;
            }

            var remaining = fuseEnds - Time.time;
            if (remaining <= 0f)
            {
                Detonate();
                return;
            }
            // Blinken wird schneller, je knapper die Zeit. Das ist die Uhr, die der Spieler liest.
            var rate = Mathf.Lerp(16f, 3f, Mathf.Clamp01(remaining / fuseSeconds));
            var on = Mathf.Sin(Time.time * rate) > 0f;
            if (shell) shell.enabled = on;
            transform.localScale = Vector3.one * (0.42f + (on ? 0.06f : 0f));
        }

        private void Detonate()
        {
            if (spent) return;
            spent = true;
            if (marker) Destroy(marker);
            var hits = CombatUtility.Explode(transform.position, radius, damage, TeamId.Enemy, type, owner);
            GameEvents.RaiseBombDetonated(owner, hits);
            if (hits > 0 && chargesHeavy && owner)
                owner.GetComponent<WeaponSystem>()?.NotifyLightHit();
            var mine = !PartyMember.IsOtherHero(owner);
            PrototypeVfx.SpawnExplosion(transform.position, radius, PrototypeVfx.ElementColor(type), mine);
            PrototypeVfx.SpawnShockwave(transform.position, radius + 0.4f, PrototypeVfx.ElementColor(type));
            Sfx.Play(Sound.Explosion, transform.position);
            if (mine) CameraController.Impulse(Mathf.Min(0.14f, radius * 0.03f));
            Destroy(gameObject);
        }
    }
}
