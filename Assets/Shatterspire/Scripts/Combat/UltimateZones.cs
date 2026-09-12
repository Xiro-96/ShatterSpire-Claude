using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der Krater, den Brax' Schmiedesturz hinterlaesst. Er gehoert dem Spieler: solange er darin
    /// steht, schlaegt er harter zu; Gegner darin kommen nur schleppend voran.
    ///
    /// Das ist der Punkt, an dem sich die Ultimate vom Skill unterscheidet - sie erzeugt kein
    /// groesseres Beben, sondern ein Stueck Boden, um das gekaempft wird.
    /// </summary>
    public sealed class ForgeCrater : MonoBehaviour
    {
        private const float TickSeconds = 0.25f;

        private Transform owner;
        private PlayerBuild build;
        private float radius;
        private float endsAt;
        private float nextTick;
        private GameObject decal;

        public static ForgeCrater Spawn(Vector3 position, float craterRadius, float seconds,
            Transform hero, PlayerBuild heroBuild, Color color)
        {
            var root = new GameObject("Forge Crater");
            root.transform.position = position;
            var crater = root.AddComponent<ForgeCrater>();
            crater.owner = hero;
            crater.build = heroBuild;
            crater.radius = craterRadius;
            crater.endsAt = Time.time + seconds;
            crater.decal = PrototypeVfx.SpawnZone(position, craterRadius, color, seconds);
            return crater;
        }

        private void Update()
        {
            if (Time.time >= endsAt)
            {
                Release();
                Destroy(gameObject);
                return;
            }
            // Der Held zaehlt nur, solange er wirklich drinsteht - der Bonus muss man sich halten.
            if (build) build.InForgeCrater = owner && Flat(owner.position) <= radius;
            if (Time.time < nextTick) return;
            nextTick = Time.time + TickSeconds;
            foreach (var candidate in Health.Active)
            {
                if (!candidate || !candidate.IsAlive || candidate.Team != TeamId.Enemy) continue;
                if (Flat(candidate.transform.position) > radius) continue;
                candidate.GetComponent<StatusReceiver>()?.ApplySlow(0.45f, TickSeconds * 2f);
            }
        }

        private void OnDestroy()
        {
            Release();
            if (decal) Destroy(decal);
        }

        private void Release()
        {
            if (build) build.InForgeCrater = false;
        }

        private float Flat(Vector3 point)
        {
            var dx = point.x - transform.position.x;
            var dz = point.z - transform.position.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// Der Zeitriss des Arkanisten. Er macht kaum Schaden - er nimmt dem Gegner die Zeit: wer
    /// hineinlaeuft, kriecht, und jedes feindliche Geschoss, das die Flaeche beruehrt, wird
    /// aufgeloest.
    ///
    /// Damit ist die Ultimate ausdruecklich keine groessere Zauberflaeche, sondern die Antwort auf
    /// Armbruester und Schuetzen: ein Stueck Raum, in dem ihre Schuesse nichts wert sind.
    /// </summary>
    public sealed class TimeRift : MonoBehaviour
    {
        private const float TickSeconds = 0.2f;

        private float radius;
        private float endsAt;
        private float nextTick;
        private float damagePerTick;
        private GameObject owner;
        private GameObject decal;
        private int swallowed;

        public static TimeRift Spawn(Vector3 position, float riftRadius, float seconds, float tickDamage,
            GameObject source, Color color)
        {
            var root = new GameObject("Time Rift");
            root.transform.position = position;
            var rift = root.AddComponent<TimeRift>();
            rift.radius = riftRadius;
            rift.endsAt = Time.time + seconds;
            rift.damagePerTick = tickDamage;
            rift.owner = source;
            rift.decal = PrototypeVfx.SpawnZone(position, riftRadius, color, seconds);
            return rift;
        }

        private void Update()
        {
            SwallowProjectiles();
            if (Time.time >= endsAt)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + TickSeconds;
            foreach (var candidate in Health.Active)
            {
                if (!candidate || !candidate.IsAlive || candidate.Team != TeamId.Enemy) continue;
                if (Flat(candidate.transform.position) > radius) continue;
                candidate.GetComponent<StatusReceiver>()?.ApplySlow(0.28f, TickSeconds * 2f);
                candidate.TakeDamage(new DamageInfo(damagePerTick, DamageType.Void, owner,
                    candidate.transform.position + Vector3.up, Vector3.zero));
            }
        }

        /// <summary>
        /// Nimmt feindliche Geschosse aus der Luft. Rueckwaerts durch die Liste, weil das Aufloesen
        /// die Liste veraendert.
        /// </summary>
        private void SwallowProjectiles()
        {
            var flying = Projectile.Active;
            for (var i = flying.Count - 1; i >= 0; i--)
            {
                var shot = flying[i];
                if (!shot || shot.TargetTeam != TeamId.Player) continue;
                if (Flat(shot.transform.position) > radius) continue;
                PrototypeVfx.SpawnHit(shot.transform.position, Vector3.up, DamageType.Void, false);
                Destroy(shot.gameObject);
                swallowed++;
            }
        }

        private void OnDestroy()
        {
            if (decal) Destroy(decal);
            if (swallowed > 0) Debug.Log($"SHATTERSPIRE Zeitriss: {swallowed} Geschoss(e) aufgeloest.");
        }

        private float Flat(Vector3 point)
        {
            var dx = point.x - transform.position.x;
            var dz = point.z - transform.position.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
