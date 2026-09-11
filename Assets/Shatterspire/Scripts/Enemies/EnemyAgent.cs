using System;
using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    [RequireComponent(typeof(Health), typeof(StatusReceiver))]
    public sealed class EnemyAgent : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<EnemyAgent> ActiveAgents = new();
        private enum State { Chase, Telegraph, Attack, Dead }
        private EnemyKind kind;
        private EnemyStats stats;
        private const float ArrivalGraceSeconds = 0.75f;
        private float spawnedAt;
        private Health health;
        private StatusReceiver status;
        private StylizedCharacterMotion motion;
        private Transform target;
        private State state;
        private float speed;
        private float attackRange;
        private float attackDamage;
        private float attackReadyAt;
        private bool eliteExplosive;
        private bool eliteVampiric;
        private int bossAttackIndex;
        private int announcedBossPhase;
        private float hitStaggerUntil;
        private float strafeDirection;
        private Vector3 knockbackVelocity;
        public event Action<EnemyAgent> Defeated;
        public EnemyKind Kind => kind;
        /// <summary>Kurz nach dem Erscheinen: fuer die Zielhilfe noch kein gueltiges Ziel.</summary>
        public bool IsArriving => Time.time - spawnedAt < ArrivalGraceSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveAgents.Clear();

        private void OnEnable()
        {
            if (!ActiveAgents.Contains(this)) ActiveAgents.Add(this);
        }

        private void OnDisable() => ActiveAgents.Remove(this);

        public void Configure(EnemyKind value, Transform player, int floor)
        {
            kind = value;
            target = player;
            health = GetComponent<Health>();
            status = GetComponent<StatusReceiver>();
            motion = GetComponent<StylizedCharacterMotion>();
            stats = EnemyBalance.For(kind);
            speed = stats.Speed;
            attackRange = stats.AttackRange;
            attackDamage = stats.AttackDamage;
            health.Configure(TeamId.Enemy, stats.Health);
            if (kind == EnemyKind.Elite)
            {
                eliteExplosive = UnityEngine.Random.value < 0.5f;
                eliteVampiric = !eliteExplosive;
            }

            var depth = Mathf.Max(0, floor - 1);
            var healthScale = 1f + depth * EnemyBalance.HealthPerFloor;
            var damageScale = 1f + depth * EnemyBalance.DamagePerFloor;
            health.IncreaseMaximum(health.Maximum * (healthScale - 1f), true);
            attackDamage *= damageScale;
            speed *= 1f + Mathf.Min(EnemyBalance.MaximumSpeedBonus, depth * EnemyBalance.SpeedPerFloor);
            health.Died += Die;
            health.Damaged += OnDamaged;
            strafeDirection = GetInstanceID() % 2 == 0 ? 1f : -1f;
            spawnedAt = Time.time;
            attackReadyAt = Time.time + UnityEngine.Random.Range(0.35f, 0.85f);
            state = State.Chase;
        }

        private void OnDestroy()
        {
            if (!health) return;
            health.Died -= Die;
            health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            if (state == State.Dead || !target) return;
            ApplyKnockback();
            if (Time.time < hitStaggerUntil) return;
            var targetHealth = target.GetComponent<Health>();
            if (!targetHealth || !targetHealth.IsAlive) return;
            var offset = target.position - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (state == State.Chase)
            {
                var direction = offset.sqrMagnitude > 0.01f ? offset.normalized : transform.forward;
                var movement = Vector3.zero;
                if (kind == EnemyKind.Shooter)
                {
                    if (distance > attackRange * 0.86f) movement = direction;
                    else if (distance < attackRange * 0.52f) movement = -direction;
                    else movement = Vector3.Cross(Vector3.up, direction) * strafeDirection * 0.56f;
                }
                else if (distance > attackRange * 0.86f)
                {
                    movement = direction;
                }

                if (movement.sqrMagnitude > 0.01f)
                {
                    movement = (movement + SeparationForce() * 1.35f).normalized;
                    transform.position += movement * (speed * status.SpeedMultiplier * Time.deltaTime);
                    ClampToArena();
                }
                if (offset.sqrMagnitude > 0.05f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), 10f * Time.deltaTime);
                if (distance <= attackRange && Time.time >= attackReadyAt) StartCoroutine(AttackRoutine());
            }
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (state == State.Dead) return;
            var force = damage.Force;
            force.y = 0f;
            knockbackVelocity += force * stats.KnockbackResistance;
            hitStaggerUntil = Mathf.Max(hitStaggerUntil, Time.time + stats.StaggerSeconds);
        }

        private void ApplyKnockback()
        {
            if (knockbackVelocity.sqrMagnitude < 0.0025f) return;
            transform.position += knockbackVelocity * Time.deltaTime;
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 13f * Time.deltaTime);
            ClampToArena();
        }

        private Vector3 SeparationForce()
        {
            var force = Vector3.zero;
            var desiredSpacing = stats.SeparationSpacing;
            for (var i = 0; i < ActiveAgents.Count; i++)
            {
                var other = ActiveAgents[i];
                if (!other || other == this || other.state == State.Dead) continue;
                var away = transform.position - other.transform.position;
                away.y = 0f;
                var distanceSqr = away.sqrMagnitude;
                if (distanceSqr < 0.001f || distanceSqr >= desiredSpacing * desiredSpacing) continue;
                force += away.normalized * (1f - Mathf.Sqrt(distanceSqr) / desiredSpacing);
            }
            return force;
        }

        private void ClampToArena()
        {
            const float radius = 14.55f;
            var flat = new Vector2(transform.position.x, transform.position.z);
            if (flat.sqrMagnitude <= radius * radius) return;
            flat = flat.normalized * radius;
            transform.position = new Vector3(flat.x, transform.position.y, flat.y);
        }

        private IEnumerator AttackRoutine()
        {
            if (kind == EnemyKind.IronWarden)
            {
                yield return BossAttackRoutine();
                yield break;
            }

            switch (kind)
            {
                case EnemyKind.Crawler:
                    yield return CrawlerLunge();
                    break;
                case EnemyKind.Shooter:
                    yield return ShooterBurst();
                    break;
                case EnemyKind.Brute:
                    yield return BruteSlam();
                    break;
                case EnemyKind.Elite:
                    yield return EliteAttack();
                    break;
            }
        }

        private IEnumerator CrawlerLunge()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 2.8f, 0.72f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PulseAttack(0.9f);
            var elapsed = 0f;
            while (elapsed < 0.13f && state != State.Dead)
            {
                transform.position += direction * (12.5f * Time.deltaTime);
                ClampToArena();
                elapsed += Time.deltaTime;
                yield return null;
            }
            CombatUtility.Explode(transform.position + direction * 0.45f, 1.15f,
                attackDamage, TeamId.Player, DamageType.Physical, gameObject);
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator ShooterBurst()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 9.5f, 0.5f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PulseAttack(0.8f);
            for (var i = 0; i < 3 && state != State.Dead; i++)
            {
                var spread = (i - 1) * 6.5f;
                Shoot(Quaternion.Euler(0f, spread, 0f) * direction);
                yield return new WaitForSeconds(0.075f);
            }
            transform.position += Vector3.Cross(Vector3.up, direction) * strafeDirection * 0.75f;
            ClampToArena();
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator BruteSlam()
        {
            state = State.Telegraph;
            var telegraph = PrototypeVfx.SpawnTelegraph(transform.position, 2.45f, false);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PulseAttack(1.25f);
            CombatUtility.Explode(transform.position, 2.45f, attackDamage,
                TeamId.Player, DamageType.Physical, gameObject);
            PrototypeVfx.SpawnShockwave(transform.position, 2.7f, new Color(1f, 0.42f, 0.08f));
            CameraController.Impulse(0.11f);
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator EliteAttack()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = eliteExplosive
                ? PrototypeVfx.SpawnTelegraph(transform.position, 3.25f, false)
                : PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 5.6f, 1.15f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PulseAttack(1.45f);
            if (eliteExplosive)
            {
                CombatUtility.Explode(transform.position, 3.25f, attackDamage,
                    TeamId.Player, DamageType.Fire, gameObject);
                PrototypeVfx.SpawnShockwave(transform.position, 3.8f, new Color(1f, 0.12f, 0.52f));
                for (var i = 0; i < 8; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    Shoot(shotDirection, DamageType.Void, attackDamage * 0.42f);
                }
            }
            else
            {
                transform.position += direction * 3.4f;
                ClampToArena();
                CombatUtility.Explode(transform.position, 2.15f, attackDamage,
                    TeamId.Player, DamageType.Void, gameObject);
                health.Heal(16f);
            }
            CameraController.Impulse(0.14f);
            FinishAttack(stats.AttackCooldown);
        }

        private bool BeginAttack(GameObject telegraph)
        {
            if (telegraph) Destroy(telegraph);
            if (state == State.Dead) return false;
            state = State.Attack;
            return true;
        }

        private void FinishAttack(float cooldown)
        {
            if (state == State.Dead) return;
            attackReadyAt = Time.time + cooldown;
            state = State.Chase;
        }

        private Vector3 FlatDirectionToTarget()
        {
            var direction = target ? target.position - transform.position : transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
        }

        private IEnumerator BossAttackRoutine()
        {
            state = State.Telegraph;
            var phase = health.Normalized > 0.66f ? 1 : health.Normalized > 0.33f ? 2 : 3;
            if (phase != announcedBossPhase)
            {
                announcedBossPhase = phase;
                GameEvents.RaiseObjectiveChanged(0, 1, "IRON WARDEN  ·  PHASE " + phase);
                PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.7f,
                    2.2f + phase * 0.35f, phase == 3 ? new Color(1f, 0.08f, 0.03f) : new Color(1f, 0.48f, 0.08f));
                CameraController.Impulse(phase == 3 ? 0.24f : 0.12f);
            }

            var patternCount = phase == 1 ? 2 : 3;
            var pattern = bossAttackIndex++ % patternCount;
            var targetPoint = target.position;
            targetPoint.y = 0f;
            var direction = targetPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();

            GameObject telegraph;
            if (pattern == 0)
                telegraph = PrototypeVfx.SpawnTelegraph(targetPoint, phase == 3 ? 3.2f : 2.65f, false);
            else if (pattern == 1)
                telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction,
                    phase == 3 ? 13f : 10f, phase == 3 ? 1.6f : 1.15f);
            else
                telegraph = PrototypeVfx.SpawnTelegraph(transform.position, phase == 3 ? 5.8f : 4.8f, false);

            // Phase 1 kommt aus dem Statblock, die Verkürzung in Phase 2 und 3 ist
            // Verhalten und bleibt bewusst hier.
            var warning = phase == 3 ? 0.58f : phase == 2 ? 0.72f : stats.TelegraphSeconds;
            yield return new WaitForSeconds(warning);
            if (state == State.Dead)
            {
                if (telegraph) Destroy(telegraph);
                yield break;
            }
            if (telegraph) Destroy(telegraph);

            state = State.Attack;
            motion?.PulseAttack(1.35f + phase * 0.08f);
            if (pattern == 0)
            {
                CombatUtility.Explode(targetPoint, phase == 3 ? 3.2f : 2.65f,
                    attackDamage + phase * 2f, TeamId.Player, DamageType.Physical, gameObject);
            }
            else if (pattern == 1)
            {
                var dashDistance = phase == 3 ? 7.2f : 5.4f;
                transform.position += direction * dashDistance;
                CombatUtility.Explode(transform.position, phase == 3 ? 2.7f : 2.15f,
                    attackDamage, TeamId.Player, DamageType.Fire, gameObject);
            }
            else
            {
                var shots = phase == 3 ? 16 : 12;
                for (var i = 0; i < shots; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, i * (360f / shots) + bossAttackIndex * 11f, 0f) * Vector3.forward;
                    Projectile.Spawn(transform.position + Vector3.up + shotDirection, shotDirection,
                        new Projectile.Payload
                        {
                            Owner = gameObject,
                            TargetTeam = TeamId.Player,
                            Damage = phase == 3 ? 14f : 11f,
                            Type = DamageType.Lightning,
                            VisualScale = phase == 3 ? 1.25f : 1f
                        }, phase == 3 ? 10f : 8f);
                }
            }

            attackReadyAt = Time.time + BossCooldown();
            state = State.Chase;
        }

        private void MeleeAttack()
        {
            motion?.PulseAttack(kind == EnemyKind.IronWarden ? 1.4f : 0.85f);
            PrototypeVfx.SpawnExplosion(transform.position + transform.forward, attackRange, new Color(1f, 0.18f, 0.08f));
            if (Vector3.Distance(transform.position, target.position) <= attackRange + 0.7f)
            {
                var targetHealth = target.GetComponent<Health>();
                targetHealth.TakeDamage(new DamageInfo(attackDamage, DamageType.Physical, gameObject, target.position, transform.forward * 5f));
                if (eliteVampiric) health.Heal(attackDamage * 0.5f);
            }
        }

        private void Shoot(Vector3 direction, DamageType type = DamageType.Fire, float damage = -1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();
            Projectile.Spawn(transform.position + Vector3.up * 0.8f + direction, direction, new Projectile.Payload
            {
                Owner = gameObject, TargetTeam = TeamId.Player,
                Damage = damage < 0f ? attackDamage : damage, Type = type,
                VisualScale = kind == EnemyKind.Elite ? 1.2f : 0.9f
            }, 10f);
        }

        private float BossCooldown() => health.Normalized > 0.33f ? stats.AttackCooldown : 1.15f;

        private void Die()
        {
            if (state == State.Dead) return;
            state = State.Dead;
            StopAllCoroutines();
            if (eliteExplosive) CombatUtility.Explode(transform.position, 3.5f, 16f, TeamId.Player, DamageType.Fire, gameObject);
            var bodyRenderer = GetComponentInChildren<Renderer>();
            PrototypeVfx.SpawnDeath(transform.position, bodyRenderer ? bodyRenderer.material.color : Color.magenta);
            Defeated?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
