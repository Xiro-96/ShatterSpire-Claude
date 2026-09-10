using UnityEngine;

namespace Shatterspire
{
    public enum CompanionRole { Guardian, Support, Ranger }

    /// <summary>
    /// Offline stand-in for a three-player party. The two roles deliberately behave
    /// differently so the prototype demonstrates team composition instead of three
    /// identical shooters. They use the same damage and targeting contracts as players.
    /// </summary>
    public sealed class CompanionBot : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<CompanionBot> ActiveCompanions = new();
        private Transform leader;
        private Transform muzzle;
        private StylizedCharacterMotion motion;
        private Health leaderHealth;
        private Vector3 formationOffset;
        private CompanionRole role;
        private Color accent;
        private float nextAttack;
        private float nextSupportPulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveCompanions.Clear();

        private void OnEnable()
        {
            if (!ActiveCompanions.Contains(this)) ActiveCompanions.Add(this);
        }

        private void OnDisable() => ActiveCompanions.Remove(this);

        public void Configure(Transform player, Vector3 offset, CompanionRole companionRole,
            Color color, string championName)
        {
            leader = player;
            leaderHealth = player.GetComponent<Health>();
            formationOffset = offset;
            role = companionRole;
            accent = color;
            if (!AuthoredArt.TryBuildCompanion(transform, role, color, out muzzle))
                muzzle = StylizedArt.BuildRex(transform);
            motion = GetComponent<StylizedCharacterMotion>();
        }

        private void Update()
        {
            if (!leader || Time.timeScale <= 0f) return;
            if (role == CompanionRole.Guardian) GuardianUpdate();
            else if (role == CompanionRole.Support) SupportUpdate();
            else RangerUpdate();
        }

        private void GuardianUpdate()
        {
            var target = Targeting.FindClosest(transform.position, 8f, TeamId.Enemy);
            if (!target)
            {
                FollowFormation(6.1f);
                return;
            }

            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 2.3f * 2.3f)
            {
                var approach = target.transform.position - delta.normalized * 1.75f;
                approach.y = 0f;
                transform.position = Vector3.MoveTowards(transform.position, approach, 6.5f * Time.deltaTime);
                ApplyPartySeparation();
            }
            Face(delta);
            if (Time.time < nextAttack || delta.sqrMagnitude > 2.75f * 2.75f) return;

            nextAttack = Time.time + 1.6f;
            motion?.PulseAttack(1.25f);
            var impact = transform.position + transform.forward * 1.35f;
            CombatUtility.Explode(impact, 1.8f, 8f, TeamId.Enemy, DamageType.Physical, gameObject);
            PrototypeVfx.SpawnExplosion(impact + Vector3.up * 0.25f, 1.65f, accent);
            CameraController.Impulse(0.045f);
        }

        private void SupportUpdate()
        {
            FollowFormation(5.7f);
            var target = Targeting.FindClosest(transform.position, 10f, TeamId.Enemy);
            if (target)
            {
                var delta = target.transform.position - transform.position;
                delta.y = 0f;
                Face(delta);
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + 1.25f;
                    var direction = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
                    var start = muzzle ? muzzle.position : transform.position + Vector3.up * 1.1f + direction * 0.65f;
                    Projectile.Spawn(start, direction, new Projectile.Payload
                    {
                        Owner = gameObject,
                        TargetTeam = TeamId.Enemy,
                        Damage = 3.2f,
                        Type = DamageType.Holy,
                        RemainingRicochets = 1,
                        VisualScale = 0.9f
                    }, 20f);
                    PrototypeVfx.SpawnMuzzle(start, direction);
                    motion?.PulseAttack(0.68f);
                }
            }

            if (!leaderHealth || !leaderHealth.IsAlive || leaderHealth.Normalized >= 0.72f || Time.time < nextSupportPulse) return;
            nextSupportPulse = Time.time + 8f;
            leaderHealth.Heal(10f);
            PrototypeVfx.SpawnExplosion(leader.position + Vector3.up * 0.6f, 1.8f, new Color(0.3f, 1f, 0.62f));
        }

        private void RangerUpdate()
        {
            FollowFormation(6.2f);
            var target = Targeting.FindClosest(transform.position, 12f, TeamId.Enemy);
            if (!target) return;
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            Face(delta);
            if (Time.time < nextAttack) return;
            nextAttack = Time.time + 0.58f;
            var direction = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
            var start = muzzle ? muzzle.position : transform.position + Vector3.up + direction * 0.7f;
            Projectile.Spawn(start, direction, new Projectile.Payload
            {
                Owner = gameObject,
                TargetTeam = TeamId.Enemy,
                Damage = 5.5f,
                Type = DamageType.Physical,
                RemainingPierces = 1,
                VisualScale = 0.92f,
                VisualKind = ProjectileVisualKind.Arrow
            }, 21f);
            PrototypeVfx.SpawnMuzzle(start, direction);
            motion?.PulseAttack(0.7f);
        }

        private void FollowFormation(float speed)
        {
            // Formation is world-aligned so aiming never spins both companions on
            // top of Rex. This keeps all three silhouettes readable on screen.
            var destination = leader.position + Vector3.right * formationOffset.x + Vector3.forward * formationOffset.z;
            destination.y = 0f;
            var distance = Vector3.Distance(transform.position, destination);
            transform.position = Vector3.MoveTowards(transform.position, destination,
                (distance > 6f ? 12f : speed) * Time.deltaTime);
            ApplyPartySeparation();
        }

        private void ApplyPartySeparation()
        {
            var correction = Vector3.zero;
            if (leader)
            {
                var fromLeader = transform.position - leader.position;
                fromLeader.y = 0f;
                if (fromLeader.sqrMagnitude < 1.45f * 1.45f)
                    correction += (fromLeader.sqrMagnitude > 0.01f ? fromLeader.normalized : Vector3.right) * 0.16f;
            }

            for (var i = 0; i < ActiveCompanions.Count; i++)
            {
                var other = ActiveCompanions[i];
                if (!other || other == this) continue;
                var away = transform.position - other.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f || away.sqrMagnitude >= 1.7f * 1.7f) continue;
                correction += away.normalized * 0.13f;
            }
            transform.position += correction;
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 15f * Time.deltaTime);
        }

    }

    public sealed class WorldFacingLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
        }
    }
}
