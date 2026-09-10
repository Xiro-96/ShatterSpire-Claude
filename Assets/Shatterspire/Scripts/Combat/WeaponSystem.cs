using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    [RequireComponent(typeof(PlayerInputRouter), typeof(PlayerBuild), typeof(Health))]
    public sealed class WeaponSystem : MonoBehaviour
    {
        private const float HeavyMeterMaximum = 100f;
        private const float HeavyChargeSeconds = 1.2f;
        private const float PerfectStart = 0.5f;
        private const float PerfectEnd = 0.76f;
        private PlayerInputRouter input;
        private PlayerBuild build;
        private Health health;
        private StylizedCharacterMotion motion;
        private Transform muzzle;
        private HeroClassId heroClass;
        private float nextShot;
        private float skillReadyAt;
        private float heavyMeter;
        private float ultimateMeter;
        private float heavyCharge;
        private bool chargingHeavy;
        private bool ultimateActive;
        private int lightComboStep;
        private float comboExpiresAt;
        private Health lockedTarget;
        private TargetLockIndicator targetIndicator;

        private float BaseDamage => HeroCatalog.BaseDamage(heroClass);
        private float SkillCooldown => HeroCatalog.SkillCooldown(heroClass);
        public float SkillNormalized => Mathf.Clamp01(1f - (skillReadyAt - Time.time) / SkillCooldown);
        public float HeavyMeterNormalized => heavyMeter / HeavyMeterMaximum;
        public float HeavyChargeNormalized => Mathf.Clamp01(heavyCharge / HeavyChargeSeconds);
        public bool HeavyReady => heavyMeter >= HeavyMeterMaximum;
        public bool ChargingHeavy => chargingHeavy;
        public bool HeavyPerfect => chargingHeavy && HeavyChargeNormalized >= PerfectStart && HeavyChargeNormalized <= PerfectEnd;
        public HeroClassId HeroClass => heroClass;
        public string LightName => HeroCatalog.LightAttackName(heroClass);
        public string HeavyName => HeroCatalog.HeavyAttackName(heroClass);
        public string SkillName => HeroCatalog.SkillName(heroClass);
        public float UltimateNormalized => ultimateMeter / 100f;
        public bool OverdriveActive => ultimateActive;

        public void ConfigureClass(HeroClassId value) => heroClass = value;

        public void SetMuzzle(Transform value)
        {
            muzzle = value;
            motion = GetComponent<StylizedCharacterMotion>();
        }

        private void Awake()
        {
            input = GetComponent<PlayerInputRouter>();
            build = GetComponent<PlayerBuild>();
            health = GetComponent<Health>();
            motion = GetComponent<StylizedCharacterMotion>();
            targetIndicator = gameObject.AddComponent<TargetLockIndicator>();
            PublishHeavyState();
        }

        private void Update()
        {
            if (!health.IsAlive || Time.timeScale <= 0f) return;
            UpdateHeavyAttack();
            UpdateTargetLock();
            if (!chargingHeavy && input.AttackHeld) TryLightAttack();
            if (!chargingHeavy && input.SkillPressed && Time.time >= skillReadyAt)
                StartCoroutine(ClassSkill());
            if (!chargingHeavy && input.UltimatePressed && ultimateMeter >= 100f && !ultimateActive)
                StartCoroutine(ClassUltimate());
        }

        public void NotifyLightHit()
        {
            if (chargingHeavy || HeavyReady) return;
            var gain = build.Has(PerkId.ExtraDash) ? 22f : 17f;
            gain *= build.HeavyChargeMultiplier;
            var wasReady = HeavyReady;
            heavyMeter = Mathf.Min(HeavyMeterMaximum, heavyMeter + gain);
            ultimateMeter = Mathf.Min(100f, ultimateMeter + 5.5f * build.UltimateChargeMultiplier);
            if (!wasReady && HeavyReady)
            {
                PrototypeVfx.SpawnHeavyReady(transform.position);
                CameraController.Impulse(0.07f);
            }
            PublishHeavyState();
        }

        private void UpdateHeavyAttack()
        {
            if (input.HeavyPressed && HeavyReady && !chargingHeavy)
            {
                chargingHeavy = true;
                heavyCharge = 0f;
                PublishHeavyState();
            }
            if (!chargingHeavy) return;
            heavyCharge = Mathf.Min(HeavyChargeSeconds, heavyCharge + Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(FlatAimDirection()), 28f * Time.deltaTime);
            PublishHeavyState();
            if (input.HeavyReleased || heavyCharge >= HeavyChargeSeconds) ReleaseHeavyAttack();
        }

        private void TryLightAttack()
        {
            if (Time.time < nextShot) return;
            if (Time.time > comboExpiresAt) lightComboStep = 0;
            lightComboStep = lightComboStep % 3 + 1;
            comboExpiresAt = Time.time + 0.95f;
            var finisher = lightComboStep == 3;
            var direction = AcquireAttackDirection();

            if (heroClass == HeroClassId.Guardian)
            {
                nextShot = Time.time + (finisher ? 0.62f : 0.38f) / build.AttackSpeedMultiplier;
                var point = transform.position + direction * (finisher ? 1.55f : 1.2f);
                var meleeRadius = (finisher ? 1.75f : 1.15f) + build.Pierces * 0.28f;
                var meleeDamage = BaseDamage * build.DamageMultiplier * (finisher ? 1.55f : 1f);
                CombatUtility.Explode(point, meleeRadius, meleeDamage, TeamId.Enemy, ResolveDamageType(), gameObject);
                if ((finisher && build.Ricochets > 0) || build.ProjectileCount > 1)
                    CombatUtility.Explode(point + direction * 1.25f, meleeRadius * 0.82f, meleeDamage * 0.62f,
                        TeamId.Enemy, ResolveDamageType(), gameObject);
                NotifyLightHit();
                // Nur noch auf dem Abschluss. Vorher schob jeder einzelne Schlag
                // nach vorn, was bei gehaltenem Angriff zu stetigem Kriechen ohne
                // Eingabe fuehrte - die Figur lief scheinbar von allein.
                if (finisher) GetComponent<PlayerController>()?.CombatStep(direction, 0.42f);
                motion?.PulseAttack(finisher ? 1.35f : 0.92f);
                if (finisher) CameraController.Impulse(0.085f);
                return;
            }

            var interval = heroClass == HeroClassId.Arcanist ? 0.34f : finisher ? 0.42f : 0.25f;
            nextShot = Time.time + interval / build.AttackSpeedMultiplier;
            var damage = BaseDamage * (finisher ? 1.38f : lightComboStep == 2 ? 1.1f : 1f);
            FireProjectile(direction, damage, true, build.Pierces, build.Ricochets,
                finisher ? 1.25f : 1f, finisher ? 0.75f : 0f,
                finisher ? BaseDamage * build.DamageMultiplier * 0.36f : 0f);
            if (finisher) GetComponent<PlayerController>()?.CombatStep(direction, 0.17f);
            PulseShot();
            if (finisher) CameraController.Impulse(0.055f);
        }

        private void ReleaseHeavyAttack()
        {
            var normalized = HeavyChargeNormalized;
            var perfect = normalized >= PerfectStart && normalized <= PerfectEnd;
            var multiplier = (perfect ? 4.5f : Mathf.Lerp(2f, 3.4f, normalized)) * build.HeavyDamageMultiplier;
            var direction = AcquireAttackDirection();

            if (heroClass == HeroClassId.Guardian)
            {
                var point = transform.position + direction * 1.4f;
                CombatUtility.Explode(point, perfect ? 4.25f : 3.15f,
                    BaseDamage * multiplier * build.DamageMultiplier, TeamId.Enemy, ResolveDamageType(), gameObject);
                PrototypeVfx.SpawnShockwave(point, perfect ? 4.8f : 3.6f, new Color(1f, 0.55f, 0.08f));
            }
            else if (heroClass == HeroClassId.Arcanist)
            {
                var target = transform.position + direction * (perfect ? 6.5f : 5f);
                CombatUtility.Explode(target, perfect ? 4.2f : 3f,
                    BaseDamage * multiplier * build.DamageMultiplier, TeamId.Enemy, DamageType.Void, gameObject);
                PrototypeVfx.SpawnShockwave(target, perfect ? 4.5f : 3.3f, HeroCatalog.Accent(heroClass));
            }
            else
            {
                var shots = perfect ? 3 : 1;
                for (var i = 0; i < shots; i++)
                {
                    var angle = shots == 1 ? 0f : Mathf.Lerp(-5f, 5f, i / (float)(shots - 1));
                    FireProjectile(Quaternion.Euler(0f, angle, 0f) * direction, BaseDamage * multiplier, false,
                        build.Pierces + 2, build.Ricochets, perfect ? 1.8f : 1.5f,
                        perfect || build.HasEmberLens ? 2.8f : 1.8f,
                        BaseDamage * build.DamageMultiplier * (perfect ? 1.6f : 0.9f));
                }
            }

            PrototypeVfx.SpawnExplosion(transform.position + direction * 1.1f + Vector3.up * 0.3f,
                perfect ? 2f : 1.2f, perfect ? new Color(1f, 0.78f, 0.12f) : HeroCatalog.Accent(heroClass));
            CameraController.Impulse(perfect ? 0.2f : 0.1f);
            // Das Perfect-Fenster ist die praeziseste Eingabe im ganzen Spiel und
            // hatte bisher kein eigenes Feedback ausser dem Schaden.
            Hitstop.Freeze(perfect ? 0.095f : 0.05f, perfect ? 0.04f : 0.09f);
            motion?.PulseAttack(perfect ? 1.5f : 1.05f);
            heavyMeter = 0f;
            heavyCharge = 0f;
            chargingHeavy = false;
            nextShot = Time.time + 0.25f;
            PublishHeavyState();
        }

        private IEnumerator ClassSkill()
        {
            skillReadyAt = Time.time + SkillCooldown;
            var direction = AcquireAttackDirection();
            motion?.PulseAttack(1.2f);
            if (heroClass == HeroClassId.Guardian)
            {
                health.SetInvulnerable(0.55f);
                for (var i = 0; i < 8; i++)
                {
                    transform.position += direction * 0.52f;
                    CombatUtility.Explode(transform.position + direction, 1.55f,
                        BaseDamage * 0.8f * build.DamageMultiplier, TeamId.Enemy, DamageType.Physical, gameObject);
                    yield return new WaitForSeconds(0.045f);
                }
                PrototypeVfx.SpawnShockwave(transform.position, 3.4f, HeroCatalog.Accent(heroClass));
                yield break;
            }
            if (heroClass == HeroClassId.Arcanist)
            {
                var center = transform.position + direction * 5.5f;
                for (var pulse = 0; pulse < 4; pulse++)
                {
                    CombatUtility.Explode(center, 4.2f, BaseDamage * 1.15f * build.DamageMultiplier,
                        TeamId.Enemy, DamageType.Void, gameObject);
                    PrototypeVfx.SpawnShockwave(center, 4.5f, HeroCatalog.Accent(heroClass));
                    yield return new WaitForSeconds(0.22f);
                }
                yield break;
            }
            for (var volley = 0; volley < 3; volley++)
            {
                for (var i = 0; i < 5; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, Mathf.Lerp(-22f, 22f, i / 4f), 0f) * direction;
                    FireProjectile(shotDirection, BaseDamage * 1.15f, false, build.Pierces,
                        Mathf.Max(1, build.Ricochets), 1.05f, 1.2f, BaseDamage * build.DamageMultiplier * 0.35f);
                }
                PulseShot();
                yield return new WaitForSeconds(0.1f);
            }
        }

        private IEnumerator ClassUltimate()
        {
            ultimateMeter = 0f;
            ultimateActive = true;
            health.SetInvulnerable(heroClass == HeroClassId.Guardian ? 2.2f : 1.1f);
            var direction = AcquireAttackDirection();
            CameraController.Impulse(0.24f);
            PrototypeVfx.SpawnHeavyReady(transform.position);

            if (heroClass == HeroClassId.Guardian)
            {
                for (var pulse = 0; pulse < 6; pulse++)
                {
                    var radius = 2.6f + pulse * 0.5f;
                    CombatUtility.Explode(transform.position, radius, BaseDamage * 1.4f * build.DamageMultiplier,
                        TeamId.Enemy, ResolveDamageType(), gameObject);
                    PrototypeVfx.SpawnShockwave(transform.position, radius + 0.4f, HeroCatalog.Accent(heroClass));
                    motion?.PulseAttack(1.5f);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            else if (heroClass == HeroClassId.Arcanist)
            {
                var center = transform.position + direction * 5.8f;
                for (var pulse = 0; pulse < 8; pulse++)
                {
                    CombatUtility.Explode(center, 4.8f, BaseDamage * 1.25f * build.DamageMultiplier,
                        TeamId.Enemy, DamageType.Void, gameObject);
                    PrototypeVfx.SpawnShockwave(center, 5.1f, Color.Lerp(HeroCatalog.Accent(heroClass), Color.black, 0.28f));
                    yield return new WaitForSeconds(0.16f);
                }
            }
            else
            {
                for (var volley = 0; volley < 5; volley++)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        var shotDirection = Quaternion.Euler(0f, Mathf.Lerp(-42f, 42f, i / 8f), 0f) * direction;
                        FireProjectile(shotDirection, BaseDamage * 1.7f, false, build.Pierces + 1,
                            build.Ricochets + 1, 1.18f, 1.35f, BaseDamage * build.DamageMultiplier * 0.45f);
                    }
                    yield return new WaitForSeconds(0.12f);
                }
            }
            ultimateActive = false;
        }

        private void FireProjectile(Vector3 direction, float baseAmount, bool chargeOnHit, int pierces,
            int ricochets, float visualScale, float impactRadius, float impactDamage)
        {
            var critical = Random.value < build.CritChance;
            var amount = baseAmount * build.DamageMultiplier * (critical ? build.CritMultiplier : 1f);
            var count = build.ProjectileCount;
            for (var i = 0; i < count; i++)
            {
                var spread = count == 1 ? 0f : Mathf.Lerp(-4f, 4f, i / (float)(count - 1));
                Projectile.Spawn(MuzzlePosition(), Quaternion.Euler(0f, spread, 0f) * direction, new Projectile.Payload
                {
                    Owner = gameObject,
                    TargetTeam = TeamId.Enemy,
                    Damage = amount,
                    Critical = critical,
                    Build = build,
                    RemainingPierces = pierces,
                    RemainingRicochets = ricochets,
                    Type = heroClass == HeroClassId.Arcanist ? DamageType.Void : ResolveDamageType(),
                    ChargeHeavyOnHit = chargeOnHit,
                    ImpactRadius = impactRadius,
                    ImpactDamage = impactDamage,
                    VisualScale = visualScale,
                    VisualKind = heroClass == HeroClassId.Arcanist ? ProjectileVisualKind.Orb : ProjectileVisualKind.Arrow
                }, visualScale > 1.45f ? 18f : heroClass == HeroClassId.Arcanist ? 18f : 22f);
            }
        }

        private void PulseShot()
        {
            PrototypeVfx.SpawnMuzzle(MuzzlePosition(), transform.forward);
            motion?.PulseAttack(0.72f);
        }

        private Vector3 MuzzlePosition()
            => muzzle ? muzzle.position : transform.position + Vector3.up * 1.05f + transform.forward * 0.8f;

        private Vector3 FlatAimDirection()
        {
            var direction = input.AimPoint - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.05f ? direction.normalized : transform.forward;
        }

        private Vector3 AcquireAttackDirection()
        {
            UpdateTargetLock(true);
            if (lockedTarget)
            {
                var direction = lockedTarget.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.05f) return direction.normalized;
            }
            return FlatAimDirection();
        }

        private void UpdateTargetLock(bool force = false)
        {
            var desired = FlatAimDirection();
            if (force || !lockedTarget || !lockedTarget.IsAlive ||
                (lockedTarget.transform.position - transform.position).sqrMagnitude > 15f * 15f)
                lockedTarget = Targeting.FindActionTarget(transform.position, desired, 15f, TeamId.Enemy, lockedTarget);
            targetIndicator?.SetTarget(lockedTarget);
        }

        private DamageType ResolveDamageType()
        {
            if (build.Has(PerkId.FireBullet)) return DamageType.Fire;
            if (build.Has(PerkId.IceBullet)) return DamageType.Ice;
            if (build.Has(PerkId.LightningBullet)) return DamageType.Lightning;
            if (build.Has(PerkId.PoisonBullet)) return DamageType.Poison;
            return DamageType.Physical;
        }

        private void PublishHeavyState()
            => GameEvents.RaiseHeavyAttackChanged(HeavyMeterNormalized, HeavyChargeNormalized, chargingHeavy, HeavyPerfect);
    }

    public sealed class TargetLockIndicator : MonoBehaviour
    {
        private Transform ring;
        private Health target;

        private void Awake()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Action Target Marker";
            PrototypeFactory.RemoveCollider(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(new Color(1f, 0.72f, 0.08f, 0.9f), 0.68f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ring = marker.transform;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.localScale = new Vector3(1.05f, 1.05f, 1f);
            ring.gameObject.SetActive(false);
        }

        public void SetTarget(Health value)
        {
            target = value && value.IsAlive ? value : null;
            if (ring) ring.gameObject.SetActive(target);
        }

        private void LateUpdate()
        {
            if (!ring || !target || !target.IsAlive)
            {
                if (ring) ring.gameObject.SetActive(false);
                return;
            }
            ring.gameObject.SetActive(true);
            ring.position = target.transform.position + Vector3.up * 0.055f;
            var pulse = 0.82f + Mathf.Sin(Time.unscaledTime * 8f) * 0.08f;
            ring.localScale = new Vector3(pulse * 1.25f, pulse * 1.25f, 1f);
            ring.Rotate(0f, 105f * Time.unscaledDeltaTime, 0f, Space.World);
        }

        private void OnDestroy()
        {
            if (ring) Destroy(ring.gameObject);
        }
    }
}
