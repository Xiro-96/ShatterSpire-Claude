using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Aktionen eines Helden wie in R.I.S.E.: Light, Heavy und Skill, dazu die Dash-Effekte.
    /// Eine Ultimate gibt es nicht mehr - ihre Wirkung ist jetzt das Skill-Upgrade OVERDRIVE.
    /// Upgrades veraendern gezielt eine dieser Aktionen; die Abfragen stehen jeweils dort, wo die
    /// Aktion ausgefuehrt wird.
    /// </summary>
    [RequireComponent(typeof(PlayerInputRouter), typeof(PlayerBuild), typeof(Health))]
    public sealed class WeaponSystem : MonoBehaviour
    {
        private const float HeavyMeterMaximum = 100f;
        private const float HeavyChargeSeconds = 1.2f;
        private const float PerfectStart = 0.5f;
        private const float PerfectEnd = 0.76f;
        // Zielhilfe nur knapp neben der Ziellinie. Alles ausserhalb trifft nur, wer dorthin zielt.
        private const float AimAssistAngle = 12f;
        private const float AimAssistRange = 15f;
        private readonly List<Health> strikeTargets = new();
        private PlayerInputRouter input;
        private PlayerBuild build;
        private Health health;
        private PlayerController controller;
        private StylizedCharacterMotion motion;
        private Transform muzzle;
        private HeroClassId heroClass;
        private float nextShot;
        private float skillReadyAt;
        private float heavyMeter;
        private float heavyCharge;
        private bool chargingHeavy;
        private int lightComboStep;
        private float comboExpiresAt;
        private Health lockedTarget;
        private TargetLockIndicator targetIndicator;

        private float BaseDamage => HeroCatalog.BaseDamage(heroClass);
        private float SkillCooldown => HeroCatalog.SkillCooldown(heroClass) * build.SkillCooldownMultiplier *
                                       (build.Has(PerkId.SkillOverdrive) ? 1.4f : 1f);
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
            controller = GetComponent<PlayerController>();
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
        }

        public void NotifyLightHit()
        {
            if (chargingHeavy || HeavyReady) return;
            heavyMeter = Mathf.Min(HeavyMeterMaximum, heavyMeter + 17f * build.HeavyChargeMultiplier);
            if (HeavyReady)
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

        // ── LIGHT ───────────────────────────────────────────────────────────

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
                var type = ResolveDamageType(DamageType.Physical);
                Strike(point, meleeRadius, meleeDamage, type);
                if ((finisher && build.Ricochets > 0) || build.ProjectileCount > 1)
                    Strike(point + direction * 1.25f, meleeRadius * 0.82f, meleeDamage * 0.62f, type);
                if (finisher && build.Has(PerkId.GuardianCleaveWave))
                    StartCoroutine(Eruptions(transform.position, direction, 3, 3.4f, 2.4f, 1.2f, meleeDamage * 0.7f, 0.07f));
                NotifyLightHit();
                // Nur noch auf dem Abschluss. Vorher schob jeder einzelne Schlag
                // nach vorn, was bei gehaltenem Angriff zu stetigem Kriechen ohne
                // Eingabe fuehrte - die Figur lief scheinbar von allein.
                if (finisher) controller?.CombatStep(direction, 0.42f);
                motion?.PulseAttack(finisher ? 1.35f : 0.92f);
                if (finisher) CameraController.Impulse(0.085f);
                return;
            }

            var interval = heroClass == HeroClassId.Arcanist ? 0.34f : finisher ? 0.42f : 0.25f;
            nextShot = Time.time + interval / build.AttackSpeedMultiplier;
            var damage = BaseDamage * (finisher ? 1.38f : lightComboStep == 2 ? 1.1f : 1f);
            var impactRadius = finisher ? 0.75f : 0f;
            var impactDamage = finisher ? BaseDamage * build.DamageMultiplier * 0.36f : 0f;
            if (heroClass == HeroClassId.Arcanist && build.Has(PerkId.ArcanistVoidBurst))
            {
                impactRadius = Mathf.Max(impactRadius, 1.4f);
                impactDamage += BaseDamage * build.DamageMultiplier * 0.4f;
            }
            var arrows = finisher && heroClass == HeroClassId.Ranger && build.Has(PerkId.RangerSplitFinisher) ? 3 : 1;
            for (var i = 0; i < arrows; i++)
            {
                var angle = arrows == 1 ? 0f : Mathf.Lerp(-11f, 11f, i / (float)(arrows - 1));
                FireProjectile(Quaternion.Euler(0f, angle, 0f) * direction, damage, i == arrows / 2, build.Pierces,
                    build.Ricochets, finisher ? 1.25f : 1f, impactRadius, impactDamage);
            }
            if (finisher) controller?.CombatStep(direction, 0.17f);
            PulseShot();
            if (finisher) CameraController.Impulse(0.055f);
        }

        // ── HEAVY ───────────────────────────────────────────────────────────

        private void ReleaseHeavyAttack()
        {
            var normalized = HeavyChargeNormalized;
            var perfect = normalized >= PerfectStart && normalized <= PerfectEnd;
            var multiplier = (perfect ? 4.5f : Mathf.Lerp(2f, 3.4f, normalized)) * build.HeavyDamageMultiplier;
            var direction = AcquireAttackDirection();
            var echo = perfect && build.Has(PerkId.PerfectEcho);

            if (heroClass == HeroClassId.Guardian)
            {
                var point = transform.position + direction * 1.4f;
                var damage = BaseDamage * multiplier * build.DamageMultiplier;
                var type = ResolveDamageType(DamageType.Physical);
                Strike(point, perfect ? 4.25f : 3.15f, damage, type);
                PrototypeVfx.SpawnShockwave(point, perfect ? 4.8f : 3.6f, new Color(1f, 0.55f, 0.08f));
                if (build.Has(PerkId.GuardianEarthsplitter))
                    StartCoroutine(Eruptions(transform.position, direction, 4, 5.8f, 2.4f, 1.3f, damage * 0.45f, 0.08f));
                if (echo) StartCoroutine(DelayedBlast(point, 4.6f, damage * 0.6f, type, 0.35f));
            }
            else if (heroClass == HeroClassId.Arcanist)
            {
                var target = transform.position + direction * (perfect ? 6.5f : 5f);
                var damage = BaseDamage * multiplier * build.DamageMultiplier;
                var type = ResolveDamageType(DamageType.Void);
                Strike(target, perfect ? 4.2f : 3f, damage, type);
                PrototypeVfx.SpawnShockwave(target, perfect ? 4.5f : 3.3f, HeroCatalog.Accent(heroClass));
                if (build.Has(PerkId.ArcanistCollapse))
                    StartCoroutine(DelayedBlast(target + direction * 3.4f, 3f, damage * 0.6f, type, 0.3f));
                if (echo) StartCoroutine(DelayedBlast(target, 4.6f, damage * 0.6f, type, 0.4f));
            }
            else
            {
                var rail = build.Has(PerkId.RangerRailShot);
                var shots = (perfect || rail ? 3 : 1) + (echo ? 2 : 0);
                for (var i = 0; i < shots; i++)
                {
                    var halfSpread = 2.5f * (shots - 1);
                    var angle = shots == 1 ? 0f : Mathf.Lerp(-halfSpread, halfSpread, i / (float)(shots - 1));
                    FireProjectile(Quaternion.Euler(0f, angle, 0f) * direction, BaseDamage * multiplier, false,
                        build.Pierces + (rail ? 8 : 2), build.Ricochets, perfect ? 1.8f : 1.5f,
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

        // ── SKILL ───────────────────────────────────────────────────────────

        private IEnumerator ClassSkill()
        {
            skillReadyAt = Time.time + SkillCooldown;
            if (build.Has(PerkId.SkillOverdrive))
            {
                yield return Overdrive();
                yield break;
            }

            var direction = AcquireAttackDirection();
            motion?.PulseAttack(1.2f);
            if (heroClass == HeroClassId.Guardian)
            {
                health.SetInvulnerable(0.55f);
                var type = ResolveDamageType(DamageType.Physical);
                for (var i = 0; i < 8; i++)
                {
                    Advance(direction, 0.52f);
                    Strike(transform.position + direction, 1.55f, BaseDamage * 0.8f * build.DamageMultiplier, type);
                    yield return new WaitForSeconds(0.045f);
                }
                PrototypeVfx.SpawnShockwave(transform.position, 3.4f, HeroCatalog.Accent(heroClass));
                if (!build.Has(PerkId.GuardianBulwark)) yield break;
                Strike(transform.position, 3.6f, BaseDamage * 1.6f * build.DamageMultiplier, type);
                PrototypeVfx.SpawnShockwave(transform.position, 4.2f, new Color(1f, 0.55f, 0.08f));
                health.SetInvulnerable(1.5f);
                CameraController.Impulse(0.12f);
                yield break;
            }
            if (heroClass == HeroClassId.Arcanist)
            {
                var lingering = build.Has(PerkId.ArcanistLingeringStar);
                var pulses = lingering ? 7 : 4;
                var center = transform.position + direction * 5.5f;
                var type = ResolveDamageType(DamageType.Void);
                for (var pulse = 0; pulse < pulses; pulse++)
                {
                    if (lingering)
                        center = Vector3.MoveTowards(center, transform.position + FlatAimDirection() * 5.5f, 1.8f);
                    Strike(center, 4.2f, BaseDamage * 1.15f * build.DamageMultiplier, type);
                    PrototypeVfx.SpawnShockwave(center, 4.5f, HeroCatalog.Accent(heroClass));
                    yield return new WaitForSeconds(0.22f);
                }
                yield break;
            }

            var rain = build.Has(PerkId.RangerArrowRain);
            var volleys = rain ? 5 : 3;
            var spread = rain ? 30f : 22f;
            for (var volley = 0; volley < volleys; volley++)
            {
                var aim = rain ? AcquireAttackDirection() : direction;
                for (var i = 0; i < 5; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, Mathf.Lerp(-spread, spread, i / 4f), 0f) * aim;
                    FireProjectile(shotDirection, BaseDamage * 1.15f, false, build.Pierces,
                        Mathf.Max(1, build.Ricochets), 1.05f, 1.2f, BaseDamage * build.DamageMultiplier * 0.35f);
                }
                PulseShot();
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>Die fruehere Ultimate, jetzt als Skill-Upgrade OVERDRIVE.</summary>
        private IEnumerator Overdrive()
        {
            health.SetInvulnerable(heroClass == HeroClassId.Guardian ? 2.2f : 1.1f);
            var direction = AcquireAttackDirection();
            CameraController.Impulse(0.24f);
            PrototypeVfx.SpawnHeavyReady(transform.position);
            motion?.PulseUltimate();

            if (heroClass == HeroClassId.Guardian)
            {
                var type = ResolveDamageType(DamageType.Physical);
                for (var pulse = 0; pulse < 6; pulse++)
                {
                    var radius = 2.6f + pulse * 0.5f;
                    Strike(transform.position, radius, BaseDamage * 1.4f * build.DamageMultiplier, type);
                    PrototypeVfx.SpawnShockwave(transform.position, radius + 0.4f, HeroCatalog.Accent(heroClass));
                    motion?.PulseAttack(1.5f);
                    yield return new WaitForSeconds(0.2f);
                }
                yield break;
            }
            if (heroClass == HeroClassId.Arcanist)
            {
                var center = transform.position + direction * 5.8f;
                var type = ResolveDamageType(DamageType.Void);
                for (var pulse = 0; pulse < 8; pulse++)
                {
                    Strike(center, 4.8f, BaseDamage * 1.25f * build.DamageMultiplier, type);
                    PrototypeVfx.SpawnShockwave(center, 5.1f, Color.Lerp(HeroCatalog.Accent(heroClass), Color.black, 0.28f));
                    yield return new WaitForSeconds(0.16f);
                }
                yield break;
            }
            for (var volley = 0; volley < 5; volley++)
            {
                for (var i = 0; i < 9; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, Mathf.Lerp(-42f, 42f, i / 8f), 0f) * direction;
                    FireProjectile(shotDirection, BaseDamage * 1.7f, false, build.Pierces + 1,
                        build.Ricochets + 1, 1.18f, 1.35f, BaseDamage * build.DamageMultiplier * 0.45f);
                }
                PulseShot();
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ── DASH ────────────────────────────────────────────────────────────

        /// <summary>Vom PlayerController zu Beginn eines Dash gerufen. Hier haengen die Dash-Upgrades der Helden.</summary>
        public void OnDashStarted(Vector3 origin, Vector3 direction)
        {
            if (heroClass == HeroClassId.Ranger && build.Has(PerkId.RangerPartingShot))
            {
                var aim = AcquireAttackDirection();
                for (var i = 0; i < 5; i++)
                    FireProjectile(Quaternion.Euler(0f, Mathf.Lerp(-24f, 24f, i / 4f), 0f) * aim, BaseDamage * 0.8f,
                        false, build.Pierces, build.Ricochets, 0.95f, 0f, 0f);
                PrototypeVfx.SpawnMuzzle(MuzzlePosition(), aim);
            }
            if (heroClass == HeroClassId.Arcanist && build.Has(PerkId.ArcanistPhaseRift))
                StartCoroutine(DelayedBlast(origin, 3f, BaseDamage * 1.8f * build.DamageMultiplier,
                    ResolveDamageType(DamageType.Void), 0.45f));
        }

        public void OnDashEnded(Vector3 origin, Vector3 end, Vector3 direction)
        {
            if (heroClass != HeroClassId.Guardian || !build.Has(PerkId.GuardianShoulderCharge)) return;
            // Ein Treffer ueber die ganze Strecke: Mittelpunkt des Wegs, Radius bis zu beiden Enden.
            var middle = Vector3.Lerp(origin, end, 0.5f);
            var radius = Mathf.Max(1.6f, Vector3.Distance(origin, end) * 0.5f + 0.9f);
            Strike(middle, radius, BaseDamage * 1.2f * build.DamageMultiplier, ResolveDamageType(DamageType.Physical));
            PrototypeVfx.SpawnShockwave(end, 2.2f, HeroCatalog.Accent(heroClass));
            CameraController.Impulse(0.06f);
        }

        // ── Treffer ─────────────────────────────────────────────────────────

        /// <summary>
        /// Flaechentreffer des Spielers. Anders als CombatUtility.Explode wirken hier Krit, Finisher,
        /// Lebensraub und Elemente - vorher bekamen Guardian-Schlaege und die Flaechen des Arcanist
        /// von diesen Upgrades nichts.
        /// </summary>
        private void Strike(Vector3 point, float radius, float damage, DamageType type)
        {
            var critical = Random.value < build.CritChance;
            var amount = damage * (critical ? build.CritMultiplier : 1f);
            PrototypeVfx.SpawnExplosion(point, radius, PrototypeVfx.ElementColor(type));
            strikeTargets.Clear();
            foreach (var candidate in Health.Active)
            {
                if (!candidate || !candidate.IsAlive || candidate.Team != TeamId.Enemy) continue;
                var offset = candidate.transform.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) strikeTargets.Add(candidate);
            }
            // Erst sammeln, dann treffen: sterbende Gegner veraendern Health.Active.
            foreach (var target in strikeTargets)
            {
                if (!target || !target.IsAlive) continue;
                var offset = target.transform.position - point;
                offset.y = 0f;
                var dealt = amount * (build.Has(PerkId.Execution) && target.Normalized <= 0.2f ? 2f : 1f);
                var force = offset.sqrMagnitude > 0.001f ? offset.normalized * 4f : Vector3.zero;
                target.TakeDamage(new DamageInfo(dealt, type, gameObject, point, force, critical));
                ApplyStatus(target, type, dealt);
                if (build.Has(PerkId.Vampirism)) health.Heal(dealt * 0.04f);
            }
            if (critical && build.IsShatter && strikeTargets.Count > 0)
                CombatUtility.Explode(point, 3f, amount * 0.8f, TeamId.Enemy, DamageType.Ice, gameObject);
        }

        private void ApplyStatus(Health target, DamageType type, float amount)
        {
            var status = target.GetComponent<StatusReceiver>();
            if (!status) return;
            switch (type)
            {
                case DamageType.Fire: status.ApplyBurn(amount * 0.16f, 2.6f, gameObject); break;
                case DamageType.Ice: status.ApplySlow(0.62f, 1.8f); break;
                case DamageType.Poison: status.ApplyPoison(amount * 0.2f, 3.2f, gameObject); break;
                case DamageType.Lightning when Random.value < 0.22f:
                    CombatUtility.Explode(target.transform.position, 2f, amount * 0.35f, TeamId.Enemy, DamageType.Lightning, gameObject);
                    break;
            }
        }

        private IEnumerator Eruptions(Vector3 origin, Vector3 direction, int count, float start, float spacing,
            float radius, float damage, float interval)
        {
            var type = ResolveDamageType(DamageType.Physical);
            for (var i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(interval);
                var point = origin + direction * (start + i * spacing);
                Strike(point, radius, damage, type);
                PrototypeVfx.SpawnShockwave(point, radius + 0.35f, new Color(1f, 0.55f, 0.08f));
            }
            CameraController.Impulse(0.05f);
        }

        private IEnumerator DelayedBlast(Vector3 point, float radius, float damage, DamageType type, float delay)
        {
            // Erst die Vorwarnung am Boden, dann der Einschlag.
            PrototypeVfx.SpawnShockwave(point, radius * 0.55f, HeroCatalog.Accent(heroClass));
            yield return new WaitForSeconds(delay);
            Strike(point, radius, damage, type);
            PrototypeVfx.SpawnShockwave(point, radius + 0.4f, HeroCatalog.Accent(heroClass));
            CameraController.Impulse(0.08f);
        }

        private void Advance(Vector3 direction, float distance)
        {
            // Ueber den CharacterController, damit die Waende der Raeume den Anlauf stoppen.
            if (controller) controller.CombatStep(direction, distance);
            else transform.position += direction * distance;
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
                    // Elemente gelten jetzt auch fuer den Arcanist. Vorher war sein Schaden immer Void,
                    // EMBER, CRYO, STORM und TOXIN CORE wirkten bei ihm nicht.
                    Type = ResolveDamageType(heroClass == HeroClassId.Arcanist ? DamageType.Void : DamageType.Physical),
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
            UpdateTargetLock();
            if (lockedTarget)
            {
                var direction = lockedTarget.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.05f) return direction.normalized;
            }
            return FlatAimDirection();
        }

        /// <summary>
        /// Zielhilfe nur in einem schmalen Kegel um die Zielrichtung. Angriffe gehen dorthin, wohin
        /// gezielt wird; ein Gegner knapp neben der Linie wird noch getroffen, einer seitlich oder
        /// hinter dem Spieler nie. Siehe Targeting.FindAimAssistTarget.
        /// </summary>
        private void UpdateTargetLock()
        {
            lockedTarget = Targeting.FindAimAssistTarget(transform.position, FlatAimDirection(),
                AimAssistRange, AimAssistAngle, TeamId.Enemy);
            targetIndicator?.SetTarget(lockedTarget);
        }

        private DamageType ResolveDamageType(DamageType fallback)
        {
            if (build.Has(PerkId.FireBullet)) return DamageType.Fire;
            if (build.Has(PerkId.IceBullet)) return DamageType.Ice;
            if (build.Has(PerkId.LightningBullet)) return DamageType.Lightning;
            if (build.Has(PerkId.PoisonBullet)) return DamageType.Poison;
            return fallback;
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
