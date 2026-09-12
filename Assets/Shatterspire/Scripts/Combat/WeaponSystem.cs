using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Aktionen eines Helden: Light, Heavy und Skill wie in R.I.S.E., dazu Dash-Effekte und die
    /// Ultimate auf R, die sich im Kampf laedt. Upgrades veraendern gezielt eine dieser Aktionen; die
    /// Abfragen stehen jeweils dort, wo die Aktion ausgefuehrt wird.
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
        /// <summary>
        /// Schaden bis zur vollen Ultimate, in Grundschaden des Helden. Bei Dauerfeuer rund 20 bis 25
        /// Sekunden; zusammen mit erlittenem Schaden und Kills etwa eine Ultimate je grossem Kampf.
        /// </summary>
        private const float UltimateDamageUnits = 70f;
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
        /// <summary>So lange gilt ein Tipp als noch offen, wenn die Aktion gerade nicht bereit war.</summary>
        private const float AttackBufferSeconds = 0.15f;
        private float attackRequestedAt = -10f;
        private float ultimateCharge;
        private bool ultimateActive;
        private int lightComboStep;
        private float comboExpiresAt;
        private Health lockedTarget;
        private TargetLockIndicator targetIndicator;

        private float BaseDamage => HeroCatalog.BaseDamage(heroClass);
        private float SkillCooldown => HeroCatalog.SkillCooldown(heroClass) * build.SkillCooldownMultiplier;
        public float SkillNormalized => Mathf.Clamp01(1f - (skillReadyAt - Time.time) / SkillCooldown);
        public float SkillCooldownRemaining => Mathf.Max(0f, skillReadyAt - Time.time);
        public float HeavyMeterNormalized => heavyMeter / HeavyMeterMaximum;
        public float HeavyChargeNormalized => Mathf.Clamp01(heavyCharge / HeavyChargeSeconds);
        public bool HeavyReady => heavyMeter >= HeavyMeterMaximum;
        public bool ChargingHeavy => chargingHeavy;
        public bool HeavyPerfect => chargingHeavy && HeavyChargeNormalized >= PerfectStart && HeavyChargeNormalized <= PerfectEnd;
        public float UltimateNormalized => ultimateCharge;
        public bool UltimateReady => ultimateCharge >= 1f;
        public bool UltimateActive => ultimateActive;
        public HeroClassId HeroClass => heroClass;
        public string LightName => HeroCatalog.LightAttackName(heroClass);
        public string HeavyName => HeroCatalog.HeavyAttackName(heroClass);
        public string SkillName => HeroCatalog.SkillName(heroClass);
        public string UltimateName => HeroCatalog.UltimateName(heroClass);

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
            health.Damaged += OnDamaged;
            GameEvents.EntityDied += OnEntityDied;
            PublishHeavyState();
        }

        private void OnDestroy()
        {
            if (health) health.Damaged -= OnDamaged;
            GameEvents.EntityDied -= OnEntityDied;
        }

        private void Update()
        {
            if (!health.IsAlive || Time.timeScale <= 0f) return;
            UpdateHeavyAttack();
            UpdateTargetLock();
            // Angriffswunsch kurz merken: ein schneller Tipp auf dem Telefon fiel bisher unter den
            // Tisch, wenn er in die Abklingzeit fiel. Jetzt loest er aus, sobald sie endet.
            if (input.AttackHeld) attackRequestedAt = Time.time;
            if (!chargingHeavy && !ultimateActive && Time.time - attackRequestedAt <= AttackBufferSeconds)
                TryLightAttack();
            if (!chargingHeavy && input.SkillPressed && Time.time >= skillReadyAt && !ultimateActive)
                StartCoroutine(ClassSkill());
            if (!chargingHeavy && input.UltimatePressed && UltimateReady && !ultimateActive)
                StartCoroutine(Ultimate());
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

        // ── Ultimate-Ladung ─────────────────────────────────────────────────

        /// <summary>Schaden des Spielers laedt die Ultimate. Gerufen von Strike und von Projektiltreffern.</summary>
        public void NotifyDamageDealt(float amount)
        {
            if (amount <= 0f) return;
            AddUltimateCharge(amount / (BaseDamage * UltimateDamageUnits));
        }

        private void OnDamaged(DamageInfo damage)
        {
            // Wer einsteckt, laedt mit: 40 % der Lebenspunkte verloren ergibt 16 % Ladung.
            if (health.Maximum > 0f) AddUltimateCharge(damage.Amount / (health.Maximum * 2.5f));
        }

        private void OnEntityDied(Health value)
        {
            if (value && value.Team == TeamId.Enemy) AddUltimateCharge(0.02f);
        }

        private void AddUltimateCharge(float amount)
        {
            if (ultimateActive || UltimateReady || amount <= 0f) return;
            ultimateCharge = Mathf.Min(1f, ultimateCharge + amount * build.UltimateChargeMultiplier);
            if (!UltimateReady) return;
            PrototypeVfx.SpawnHeavyReady(transform.position);
            CameraController.Impulse(0.06f);
        }

        private void UpdateHeavyAttack()
        {
            if (input.HeavyPressed && HeavyReady && !chargingHeavy && !ultimateActive)
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
            var finisher = lightComboStep == 3;
            var direction = AcquireAttackDirection();

            if (heroClass == HeroClassId.Guardian)
            {
                HammerCombo(direction);
                return;
            }

            comboExpiresAt = Time.time + 0.95f;
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
            PulseShot(finisher ? 1f : 0.62f);
            if (finisher) CameraController.Impulse(0.055f);
        }

        /// <summary>
        /// Brax' Hammer-Kombo: erster Schlag quer, zweiter ueber den Kopf mit Wucht, dritter ein Wirbel
        /// um sich selbst. Der Schaden faellt im Moment des Treffers, nicht beim Tastendruck - vorher
        /// traf jeder Schlag, bevor der Hammer sich bewegt hatte, und es sah aus wie "nur so tun".
        /// </summary>
        private void HammerCombo(Vector3 direction)
        {
            var type = ResolveDamageType(DamageType.Physical);
            var reach = build.Pierces * 0.28f;
            var hit = BaseDamage * build.DamageMultiplier;
            var accent = HeroCatalog.Accent(heroClass);
            // Ring statt Explosionsscheibe: die tuerkise Scheibe wirkte beim Hammer wie eine Plattform aus dem Boden.
            var ring = type == DamageType.Physical ? accent : PrototypeVfx.ElementColor(type);
            comboExpiresAt = Time.time + 1.1f;

            if (lightComboStep == 1)
            {
                nextShot = Time.time + 0.38f / build.AttackSpeedMultiplier;
                motion?.PlayMotion(AttackMotion.Swing, 0.9f);
                StartCoroutine(MeleeImpact(0.15f, () =>
                {
                    var point = transform.position + direction * 1.25f;
                    Strike(point, 1.35f + reach, hit, type, flash: false);
                    PrototypeVfx.SpawnShockwave(point, 1.6f + reach, ring);
                    if (build.ProjectileCount > 1) Strike(point + direction * 1.2f, 1.1f, hit * 0.6f, type, flash: false);
                }));
                return;
            }

            if (lightComboStep == 2)
            {
                nextShot = Time.time + 0.46f / build.AttackSpeedMultiplier;
                motion?.PlayMotion(AttackMotion.Smash, 1.1f);
                StartCoroutine(MeleeImpact(0.2f, () =>
                {
                    var point = transform.position + direction * 1.55f;
                    Strike(point, 1.7f + reach, hit * 1.4f, type, 7f, flash: false);
                    PrototypeVfx.SpawnShockwave(point, 2.1f, ring);
                    if (build.ProjectileCount > 1) Strike(point + direction * 1.25f, 1.3f, hit * 0.8f, type, flash: false);
                    controller?.CombatStep(direction, 0.25f);
                    CameraController.Impulse(0.06f);
                }));
                return;
            }

            nextShot = Time.time + 0.64f / build.AttackSpeedMultiplier;
            motion?.PlayMotion(AttackMotion.Spin, 1.3f);
            StartCoroutine(MeleeImpact(0.17f, () =>
            {
                var radius = 2.6f + reach;
                Strike(transform.position, radius, hit * 1.75f, type, 8f, flash: false);
                PrototypeVfx.SpawnShockwave(transform.position, radius + 0.4f, ring);
                CameraController.Impulse(0.09f);
                Hitstop.Freeze(0.04f, 0.1f);
                if (build.Ricochets > 0) StartCoroutine(DelayedBlast(transform.position, radius * 0.85f, hit * 0.6f, type, 0.22f));
                if (build.Has(PerkId.GuardianCleaveWave))
                    StartCoroutine(Eruptions(transform.position, direction, 3, 3.4f, 2.4f, 1.2f, hit * 1.2f, 0.07f));
            }));
        }

        private IEnumerator MeleeImpact(float delay, System.Action impact)
        {
            yield return new WaitForSeconds(delay);
            if (!health.IsAlive) yield break;
            impact();
            NotifyLightHit();
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
                Strike(point, perfect ? 4.25f : 3.15f, damage, type, 9f);
                PrototypeVfx.SpawnShockwave(point, perfect ? 4.8f : 3.6f, new Color(1f, 0.55f, 0.08f));
                if (build.Has(PerkId.GuardianEarthsplitter))
                    StartCoroutine(Eruptions(transform.position, direction, 4, 5.8f, 2.4f, 1.3f, damage * 0.45f, 0.08f));
                if (echo) StartCoroutine(DelayedBlast(point, 4.6f, damage * 0.6f, type, 0.35f));
                motion?.PlayMotion(AttackMotion.Smash, perfect ? 1.5f : 1.05f);
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
                motion?.PlayMotion(AttackMotion.Cast, perfect ? 1.5f : 1.05f);
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
                motion?.PlayMotion(AttackMotion.Shot, perfect ? 1.5f : 1.05f);
            }

            PrototypeVfx.SpawnExplosion(transform.position + direction * 1.1f + Vector3.up * 0.3f,
                perfect ? 2f : 1.2f, perfect ? new Color(1f, 0.78f, 0.12f) : HeroCatalog.Accent(heroClass));
            CameraController.Impulse(perfect ? 0.2f : 0.1f);
            // Das Perfect-Fenster ist die praeziseste Eingabe im ganzen Spiel und
            // hatte bisher kein eigenes Feedback ausser dem Schaden.
            Hitstop.Freeze(perfect ? 0.095f : 0.05f, perfect ? 0.04f : 0.09f);
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
            if (build.Has(PerkId.SkillOvercharge)) AddUltimateCharge(0.1f);
            var direction = AcquireAttackDirection();

            if (heroClass == HeroClassId.Guardian)
            {
                motion?.PlayMotion(AttackMotion.Swing, 1.2f);
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
                motion?.PlayMotion(AttackMotion.Smash, 1.3f);
                Strike(transform.position, 3.6f, BaseDamage * 1.6f * build.DamageMultiplier, type, 9f);
                PrototypeVfx.SpawnShockwave(transform.position, 4.2f, new Color(1f, 0.55f, 0.08f));
                health.SetInvulnerable(1.5f);
                CameraController.Impulse(0.12f);
                yield break;
            }

            if (heroClass == HeroClassId.Arcanist)
            {
                motion?.PlayMotion(AttackMotion.Channel, 1.2f);
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
                PulseShot(0.9f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ── ULTIMATE ────────────────────────────────────────────────────────

        /// <summary>
        /// Rift Barrage, Forge Quake oder Singularity. Laedt sich durch Schaden, erlittenen Schaden und
        /// Kills; der Held ist waehrenddessen kurz unverwundbar.
        /// </summary>
        private IEnumerator Ultimate()
        {
            ultimateActive = true;
            ultimateCharge = 0f;
            var direction = AcquireAttackDirection();
            health.SetInvulnerable(heroClass == HeroClassId.Guardian ? 2.2f : 1.3f);
            if (build.Has(PerkId.UltimateAfterglow)) health.Heal(health.Maximum * 0.3f);
            CameraController.Impulse(0.24f);
            Hitstop.Freeze(0.07f, 0.06f);
            // Ohne Ort und damit ohne Abstandsdaempfung: die eigene Ultimate soll im Vordergrund stehen.
            Sfx.Play2D(Sound.UltimateRise);
            PrototypeVfx.SpawnHeavyReady(transform.position);

            if (heroClass == HeroClassId.Guardian)
            {
                // Forge Quake: sechs Beben, jedes weiter als das vorige.
                var type = build.Has(PerkId.GuardianMoltenQuake) ? DamageType.Fire : ResolveDamageType(DamageType.Physical);
                for (var pulse = 0; pulse < 6; pulse++)
                {
                    if (pulse % 2 == 0) motion?.PlayMotion(pulse == 0 ? AttackMotion.Leap : AttackMotion.Spin, 1.4f);
                    var radius = 2.6f + pulse * 0.55f;
                    Strike(transform.position, radius, BaseDamage * 1.5f * build.DamageMultiplier, type, 8f);
                    PrototypeVfx.SpawnShockwave(transform.position, radius + 0.4f,
                        type == DamageType.Fire ? new Color(1f, 0.4f, 0.08f) : HeroCatalog.Accent(heroClass));
                    CameraController.Impulse(0.08f);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            else if (heroClass == HeroClassId.Arcanist)
            {
                // Singularity: ein schwarzer Stern vor dem Arcanist, acht Pulse.
                motion?.PlayMotion(AttackMotion.Summon, 1.5f);
                var center = transform.position + direction * 5.8f;
                var pull = build.Has(PerkId.ArcanistEventHorizon);
                var type = ResolveDamageType(DamageType.Void);
                for (var pulse = 0; pulse < 8; pulse++)
                {
                    Strike(center, 4.8f, BaseDamage * 1.3f * build.DamageMultiplier, type, pull ? 6f : 4f, pull);
                    PrototypeVfx.SpawnShockwave(center, 5.1f, Color.Lerp(HeroCatalog.Accent(heroClass), Color.black, 0.28f));
                    yield return new WaitForSeconds(0.16f);
                }
            }
            else
            {
                // Rift Barrage: fuenf breite Salven in Zielrichtung.
                var homing = build.Has(PerkId.RangerHomingBarrage);
                for (var volley = 0; volley < 5; volley++)
                {
                    var aim = AcquireAttackDirection();
                    for (var i = 0; i < 9; i++)
                    {
                        var shotDirection = Quaternion.Euler(0f, Mathf.Lerp(-42f, 42f, i / 8f), 0f) * aim;
                        FireProjectile(shotDirection, BaseDamage * 1.7f, false, build.Pierces + 1,
                            build.Ricochets + 1, 1.18f, 1.35f, BaseDamage * build.DamageMultiplier * 0.45f, homing);
                    }
                    PulseShot(1.2f);
                    yield return new WaitForSeconds(0.12f);
                }
            }
            ultimateActive = false;
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
        /// Lebensraub und Elemente, und der Schaden laedt die Ultimate. pull zieht Gegner zur Mitte.
        /// </summary>
        private void Strike(Vector3 point, float radius, float damage, DamageType type, float knockback = 4f, bool pull = false,
            bool flash = true)
        {
            var critical = Random.value < build.CritChance;
            var amount = damage * (critical ? build.CritMultiplier : 1f);
            if (flash) PrototypeVfx.SpawnExplosion(point, radius, PrototypeVfx.ElementColor(type));
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
                var away = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector3.zero;
                target.TakeDamage(new DamageInfo(dealt, type, gameObject, point, away * (pull ? -knockback : knockback), critical));
                ApplyStatus(target, type, dealt);
                if (build.Has(PerkId.Vampirism)) health.Heal(dealt * 0.04f);
                NotifyDamageDealt(dealt);
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
            int ricochets, float visualScale, float impactRadius, float impactDamage, bool homing = false)
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
                    // Elemente gelten auch fuer den Arcanist; vorher war sein Schaden immer Void.
                    Type = ResolveDamageType(heroClass == HeroClassId.Arcanist ? DamageType.Void : DamageType.Physical),
                    ChargeHeavyOnHit = chargeOnHit,
                    Homing = homing,
                    ImpactRadius = impactRadius,
                    ImpactDamage = impactDamage,
                    VisualScale = visualScale,
                    VisualKind = heroClass == HeroClassId.Arcanist ? ProjectileVisualKind.Orb : ProjectileVisualKind.Arrow
                }, visualScale > 1.45f ? 18f : heroClass == HeroClassId.Arcanist ? 18f : 22f);
            }
        }

        /// <summary>Muendungsblitz plus Bewegung: Rex schiesst, Orion wirft einen Zauber.</summary>
        private void PulseShot(float strength)
        {
            PrototypeVfx.SpawnMuzzle(MuzzlePosition(), transform.forward);
            motion?.PlayMotion(heroClass == HeroClassId.Arcanist ? AttackMotion.Cast : AttackMotion.Shot, strength);
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
