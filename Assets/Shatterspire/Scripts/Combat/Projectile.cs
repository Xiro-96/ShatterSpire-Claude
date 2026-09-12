using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public enum ProjectileVisualKind { EnemyOrb, Arrow, Orb }

    [RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
    public sealed class Projectile : MonoBehaviour
    {
        private const int SweepCapacity = 24;
        private static readonly RaycastHit[] SweepHits = new RaycastHit[SweepCapacity];
        private static readonly Collider[] OverlapHits = new Collider[SweepCapacity];

        /// <summary>
        /// Alle fliegenden Geschosse. Gebraucht wird das vom Zeitriss, der Geschosse aus der Luft
        /// nimmt - dafuer muss man sie finden koennen. Gleiches Muster wie Health.Active und
        /// EnemyAgent: Liste der Instanzen, beim Start des Spiels geleert.
        /// </summary>
        private static readonly List<Projectile> Flying = new();
        public static IReadOnlyList<Projectile> Active => Flying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Flying.Clear();

        /// <summary>Wessen Geschoss das ist - der Zeitriss nimmt nur die der Gegner.</summary>
        public TeamId TargetTeam => payload.TargetTeam;

        private void OnEnable()
        {
            if (!Flying.Contains(this)) Flying.Add(this);
        }

        private void OnDisable() => Flying.Remove(this);

        public struct Payload
        {
            public GameObject Owner;
            public TeamId TargetTeam;
            public float Damage;
            public bool Critical;
            public PlayerBuild Build;
            public int RemainingPierces;
            public int RemainingRicochets;
            public DamageType Type;
            public bool ChargeHeavyOnHit;
            public bool Homing;
            public float ImpactRadius;
            public float ImpactDamage;
            public float VisualScale;
            public ProjectileVisualKind VisualKind;
        }

        private static readonly Stack<Projectile> Pool = new();
        private readonly HashSet<Health> hitTargets = new();
        private Payload payload;
        private Vector3 direction;
        private float speed;
        private float despawnAt;
        private float collisionRadius;
        private TrailRenderer trail;
        private Renderer coreRenderer;
        private Transform arrowRoot;
        private Renderer[] arrowRenderers;
        private Transform orbRoot;
        private Renderer orbRenderer;

        public static Projectile Spawn(Vector3 position, Vector3 direction, Payload payload, float speed = 22f)
        {
            Projectile projectile = null;
            while (Pool.Count > 0 && !projectile) projectile = Pool.Pop();
            if (!projectile) projectile = Create();
            projectile.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            projectile.payload = payload;
            projectile.direction = direction.normalized;
            projectile.speed = speed;
            projectile.despawnAt = Time.time + 2.4f;
            projectile.hitTargets.Clear();
            var visualScale = payload.VisualScale > 0f ? payload.VisualScale : 1f;
            // Intentionally a little wider than the visible tracer. Mobile action
            // combat should reward a well-aimed shot instead of demanding pixel precision.
            projectile.collisionRadius = Mathf.Clamp(0.14f * visualScale, 0.14f, 0.42f);
            projectile.transform.localScale = Vector3.one;
            projectile.gameObject.SetActive(true);
            projectile.trail.Clear();
            var color = PrototypeVfx.ElementColor(payload.Type);
            var arrow = payload.VisualKind == ProjectileVisualKind.Arrow;
            projectile.arrowRoot.gameObject.SetActive(arrow);
            projectile.orbRoot.gameObject.SetActive(!arrow);
            projectile.arrowRoot.localScale = Vector3.one * visualScale;
            projectile.orbRoot.localScale = Vector3.one * (payload.VisualKind == ProjectileVisualKind.EnemyOrb ? 0.3f : 0.36f) * visualScale;
            if (arrow)
            {
                foreach (var renderer in projectile.arrowRenderers)
                    ApplyProjectileColor(renderer, renderer.name.Contains("Shaft")
                        ? new Color(0.24f, 0.12f, 0.055f) : Color.Lerp(new Color(1f, 0.72f, 0.16f), color, 0.28f));
            }
            else ApplyProjectileColor(projectile.orbRenderer, color);
            ApplyProjectileColor(projectile.coreRenderer, Color.Lerp(Color.white, color, 0.34f));
            projectile.trail.startColor = color;
            projectile.trail.endColor = new Color(color.r, color.g, color.b, 0f);
            projectile.trail.time = arrow ? 0.07f : 0.12f;
            projectile.trail.startWidth = arrow ? 0.045f * visualScale : 0.1f * visualScale;
            return projectile;
        }

        private static Projectile Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one;
            go.GetComponent<Renderer>().enabled = false;
            var collider = go.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.1f;
            trail.startWidth = 0.08f;
            trail.endWidth = 0f;
            trail.material = PrototypeFactory.CreateMaterial(Color.white, true);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Spell Orb";
            orb.transform.SetParent(go.transform, false);
            orb.transform.localScale = Vector3.one * 0.32f;
            orb.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(Color.white, true, 0.55f);
            Object.Destroy(orb.GetComponent<Collider>());

            var arrow = new GameObject("Chunky Rift Arrow").transform;
            arrow.SetParent(go.transform, false);
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Arrow Shaft";
            shaft.transform.SetParent(arrow, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, -0.08f);
            shaft.transform.localScale = new Vector3(0.075f, 0.075f, 0.92f);
            shaft.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.24f, 0.12f, 0.055f), false, 0.16f);
            Object.Destroy(shaft.GetComponent<Collider>());
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Arrow Gold Head";
            head.transform.SetParent(arrow, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            head.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            head.transform.localScale = new Vector3(0.18f, 0.18f, 0.24f);
            head.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(new Color(1f, 0.72f, 0.16f), true, 0.35f);
            Object.Destroy(head.GetComponent<Collider>());
            for (var i = 0; i < 2; i++)
            {
                var feather = GameObject.CreatePrimitive(PrimitiveType.Cube);
                feather.name = "Arrow Fletching";
                feather.transform.SetParent(arrow, false);
                feather.transform.localPosition = new Vector3(0f, 0f, -0.48f);
                feather.transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f + 45f);
                feather.transform.localScale = new Vector3(0.2f, 0.035f, 0.24f);
                feather.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.08f, 0.82f, 0.88f), true, 0.28f);
                Object.Destroy(feather.GetComponent<Collider>());
            }

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Projectile Core";
            core.transform.SetParent(orb.transform, false);
            core.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            core.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(Color.white, true, 0.65f);
            Object.Destroy(core.GetComponent<Collider>());
            var projectile = go.AddComponent<Projectile>();
            projectile.trail = trail;
            projectile.coreRenderer = core.GetComponent<Renderer>();
            projectile.arrowRoot = arrow;
            projectile.arrowRenderers = arrow.GetComponentsInChildren<Renderer>();
            projectile.orbRoot = orb.transform;
            projectile.orbRenderer = orb.GetComponent<Renderer>();
            return projectile;
        }

        private static void ApplyProjectileColor(Renderer renderer, Color color)
        {
            if (!renderer) return;
            var material = renderer.material;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.65f);
            }
        }

        private void Update()
        {
            if (Time.time >= despawnAt) { Despawn(); return; }
            if (payload.Homing || (payload.Build && payload.Build.Has(PerkId.HomingShot)))
            {
                var target = Targeting.FindClosest(transform.position, 5f, payload.TargetTeam);
                if (target)
                {
                    var desired = (target.transform.position + Vector3.up * 0.6f - transform.position).normalized;
                    direction = Vector3.Slerp(direction, desired, 7f * Time.deltaTime).normalized;
                }
            }
            var start = transform.position;
            var travel = speed * Time.deltaTime;
            var next = start + direction * travel;

            // A trigger alone can miss a whole enemy when a fast tracer moves farther
            // than its own diameter in one frame. Sweep the complete travelled segment
            // so damage remains deterministic at low and high frame rates alike.
            // Feste Etagengeometrie zuerst: ein Ziel hinter einer Wand oder Deckung wird nicht
            // getroffen. Ohne das flog jeder Schuss durch den halben Turm, und Deckung waere
            // gegen Fernkampf wirkungslos.
            var obstacleDistance = float.PositiveInfinity;
            var obstaclePoint = next;
            var blocked = travel > 0f && TryObstacleHit(start, travel, out obstacleDistance, out obstaclePoint);
            if (travel > 0f && TrySweepHit(start, next, travel, out var health, out var hitPoint) &&
                (!blocked || (hitPoint - start).magnitude <= obstacleDistance))
            {
                ResolveHit(health, hitPoint);
                if (!gameObject.activeSelf) return;
                transform.position = hitPoint + direction * (collisionRadius + 0.035f);
            }
            else if (blocked)
            {
                transform.position = obstaclePoint;
                PrototypeVfx.SpawnHit(obstaclePoint, -direction, payload.Type, false);
                Despawn();
                return;
            }
            else
            {
                transform.position = next;
            }
            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<Health>();
            if (!IsValidTarget(health)) return;
            ResolveHit(health, other.ClosestPoint(transform.position));
        }

        /// <summary>Naechster Einschlag in Wand oder Deckung auf der Strecke dieses Frames.</summary>
        private bool TryObstacleHit(Vector3 start, float distance, out float hitDistance, out Vector3 hitPoint)
        {
            hitDistance = float.PositiveInfinity;
            hitPoint = start;
            var count = Physics.SphereCastNonAlloc(start, collisionRadius, direction, SweepHits, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var hit = SweepHits[i];
                if (!hit.collider || !hit.collider.GetComponentInParent<LevelObstacle>()) continue;
                if (hit.distance >= hitDistance) continue;
                hitDistance = hit.distance;
                hitPoint = hit.point == Vector3.zero ? start + direction * hit.distance : hit.point;
            }
            return hitDistance < float.PositiveInfinity;
        }

        private bool TrySweepHit(Vector3 start, Vector3 next, float distance, out Health target, out Vector3 hitPoint)
        {
            target = null;
            hitPoint = next;
            var closestDistance = float.PositiveInfinity;
            var count = Physics.SphereCastNonAlloc(start, collisionRadius, direction, SweepHits, distance,
                Physics.AllLayers, QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var candidate = SweepHits[i].collider
                    ? SweepHits[i].collider.GetComponentInParent<Health>()
                    : null;
                if (!IsValidTarget(candidate) || SweepHits[i].distance >= closestDistance) continue;
                target = candidate;
                closestDistance = SweepHits[i].distance;
                hitPoint = SweepHits[i].point == Vector3.zero
                    ? start + direction * SweepHits[i].distance
                    : SweepHits[i].point;
            }

            if (target) return true;

            // SphereCast does not report every collider when the segment starts inside
            // it. The end overlap closes that edge case for point-blank shots.
            count = Physics.OverlapSphereNonAlloc(next, collisionRadius, OverlapHits,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            var closestSqr = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var candidate = OverlapHits[i]
                    ? OverlapHits[i].GetComponentInParent<Health>()
                    : null;
                if (!IsValidTarget(candidate)) continue;
                var point = OverlapHits[i].ClosestPoint(next);
                var sqr = (point - start).sqrMagnitude;
                if (sqr >= closestSqr) continue;
                target = candidate;
                closestSqr = sqr;
                hitPoint = point;
            }
            if (target) return true;

            // Moving kinematic actors can briefly have a physics pose that trails
            // behind their visual Transform. Use the authoritative Health registry
            // as a deterministic final check so a visibly intersecting shot can
            // never pass through a valid combat target without dealing damage.
            return TryRegistryHit(start, next, out target, out hitPoint);
        }

        private bool TryRegistryHit(Vector3 start, Vector3 next, out Health target, out Vector3 hitPoint)
        {
            target = null;
            hitPoint = next;
            var segment = next - start;
            var segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.000001f) return false;

            var closestProgress = float.PositiveInfinity;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!IsValidTarget(candidate)) continue;

                var center = candidate.transform.position + Vector3.up * 0.65f;
                var targetRadius = 0.52f;
                var targetCollider = candidate.GetComponent<Collider>();
                if (targetCollider && targetCollider.enabled)
                {
                    var bounds = targetCollider.bounds;
                    // Combat projectiles are intentionally forgiving in the vertical
                    // axis for an isometric mobile camera. The horizontal centre comes
                    // from the current Transform, not a possibly one-frame-old physics pose.
                    center = candidate.transform.position;
                    center.y = start.y;
                    targetRadius = Mathf.Max(0.42f, Mathf.Max(bounds.extents.x, bounds.extents.z));
                }

                var progress = Mathf.Clamp01(Vector3.Dot(center - start, segment) / segmentLengthSqr);
                var closestPoint = start + segment * progress;
                var effectiveRadius = targetRadius + collisionRadius + 0.08f;
                if ((center - closestPoint).sqrMagnitude > effectiveRadius * effectiveRadius || progress >= closestProgress)
                    continue;

                target = candidate;
                hitPoint = closestPoint;
                closestProgress = progress;
            }
            return target;
        }

        private bool IsValidTarget(Health health)
            => health && health.IsAlive && health.Team == payload.TargetTeam && !hitTargets.Contains(health) &&
               (!payload.Owner || health.gameObject != payload.Owner);

        private void ResolveHit(Health health, Vector3 hitPoint)
        {
            if (!IsValidTarget(health)) return;
            hitTargets.Add(health);
            var execution = payload.Build && payload.Build.Has(PerkId.Execution) && health.Normalized <= 0.2f ? 2f : 1f;
            var amount = payload.Damage * execution;
            health.TakeDamage(new DamageInfo(amount, payload.Type, payload.Owner, hitPoint, direction * 3f, payload.Critical));
            if (payload.Owner && payload.Owner.TryGetComponent<WeaponSystem>(out var shooter)) shooter.NotifyDamageDealt(amount);
            if (payload.ChargeHeavyOnHit && payload.Owner)
                payload.Owner.GetComponent<WeaponSystem>()?.NotifyLightHit();
            ApplyElement(health, amount);
            if (payload.Build && payload.Build.Has(PerkId.Vampirism)) payload.Owner.GetComponent<Health>()?.Heal(amount * 0.04f);
            if (payload.Build && payload.Build.HasSiphonStone) payload.Owner.GetComponent<Health>()?.Heal(amount * 0.03f);

            if (payload.Build && payload.Build.Has(PerkId.ExplosiveShot))
                CombatUtility.Explode(hitPoint, payload.Build.IsInferno ? 3.2f : 2.2f, amount * 0.55f, payload.TargetTeam,
                    payload.Build.IsInferno ? DamageType.Fire : DamageType.Physical, payload.Owner);

            if (payload.ImpactRadius > 0f && payload.ImpactDamage > 0f)
                CombatUtility.Explode(hitPoint, payload.ImpactRadius, payload.ImpactDamage, payload.TargetTeam, payload.Type, payload.Owner);

            if (payload.Build && payload.Build.IsShatter && payload.Critical)
                CombatUtility.Explode(health.transform.position, 3f, amount * 0.8f, payload.TargetTeam, DamageType.Ice, payload.Owner);

            if (payload.RemainingRicochets > 0)
            {
                var next = Targeting.FindClosest(hitPoint, 8f, payload.TargetTeam, health);
                if (next)
                {
                    payload.RemainingRicochets--;
                    if (payload.Build && payload.Build.IsChainStorm)
                        CombatUtility.Explode(next.transform.position, 2f, amount * 0.45f, payload.TargetTeam, DamageType.Lightning, payload.Owner);
                    direction = (next.transform.position + Vector3.up * 0.5f - hitPoint).normalized;
                    return;
                }
            }
            if (payload.RemainingPierces > 0) { payload.RemainingPierces--; return; }
            Despawn();
        }

        private void ApplyElement(Health health, float amount)
        {
            var status = health.GetComponent<StatusReceiver>();
            if (!status) return;
            switch (payload.Type)
            {
                case DamageType.Fire: status.ApplyBurn(amount * 0.22f, 3f, payload.Owner); break;
                case DamageType.Ice: status.ApplySlow(0.55f, 2.2f); break;
                case DamageType.Poison: status.ApplyPoison(amount * 0.3f, 4f, payload.Owner); break;
                case DamageType.Lightning when Random.value < 0.22f:
                    CombatUtility.Explode(health.transform.position, 2f, amount * 0.35f, payload.TargetTeam, DamageType.Lightning, payload.Owner); break;
            }
        }

        private void Despawn()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
