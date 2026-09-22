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
    /// <remarks>
    /// Die Eingabe kommt ueber <see cref="IPlayerInputSource"/>, nicht von der Tastatur: an dieser
    /// Stelle steht beim Spieler der Router, bei einem Mitglied der Gruppe der Bot - und spaeter
    /// ein Mitspieler aus dem Netz. Deshalb steht hier keine RequireComponent-Zeile fuer die
    /// Eingabe; eine Schnittstelle laesst sich so nicht fordern.
    /// </remarks>
    [RequireComponent(typeof(PlayerBuild), typeof(Health))]
    public sealed class WeaponSystem : MonoBehaviour
    {
        private const float HeavyMeterMaximum = 100f;
        private const float HeavyChargeSeconds = ActionBalance.HeavyChargeSeconds;
        // Zielhilfe nur knapp neben der Ziellinie. Alles ausserhalb trifft nur, wer dorthin zielt.
        /// <summary>
        /// Kegel, in dem die Waffe ein Ziel erfasst. Stand auf 12 Grad und war damit enger als die
        /// Zielhilfe der Eingabe - die Eingabe zog auf einen Gegner, die Waffe erkannte ihn nicht
        /// mehr als erfasst, und der Schritt ins Ziel blieb aus.
        /// </summary>
        private const float AimAssistAngle = 32f;
        private const float AimAssistRange = 15f;
        /// <summary>
        /// Schaden bis zur vollen Ultimate, in Grundschaden des Helden. Bei Dauerfeuer rund 20 bis 25
        /// Sekunden; zusammen mit erlittenem Schaden und Kills etwa eine Ultimate je grossem Kampf.
        /// </summary>
        /// <summary>
        /// Wie viel eigener Schaden - in Vielfachen des Grundschadens - eine volle Ultimate-Ladung
        /// kostet. Stand vorher auf 70 und war damit in gut zehn Sekunden Dauerkampf wieder voll.
        /// </summary>
        private const float UltimateDamageUnits = 150f;

        /// <summary>
        /// Sperre nach einer Ultimate. Deckt die Wirkdauer der laengsten Ultimate (9 s) mit ab, denn
        /// keine darf sich waehrend ihrer eigenen Wirkung nachladen.
        /// </summary>
        private const float UltimateLockoutSeconds = 14f;

        private float ultimateLockedUntil;

        /// <summary>Ob der Ton fuer das offene Fenster in dieser Ladung schon gelaufen ist.</summary>
        private bool windowAnnounced;

        private float chargeStartedAt;

        /// <summary>
        /// Wann zuletzt eine Ladung abgebrochen wurde, weil zu frueh losgelassen wurde. Das HUD
        /// erklaert dann kurz den Griff - ein Knopf, der nichts tut, ist sonst nur verwirrend.
        /// </summary>
        public float LastHeavyCancel { get; private set; } = -99f;

        /// <summary>Der Ring am Boden, der die Ladung zeigt. Nur beim Helden an diesem Geraet.</summary>
        private HeavyChargeRing chargeRing;
        private readonly List<Health> strikeTargets = new();
        private IPlayerInputSource input;
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
        private KillStreak killStreak;
        /// <summary>Bis hierhin laesst jeder Abschuss eine neue Ladung fallen (Kettenzuender).</summary>
        private float chainUntil;

        /// <summary>Wie weit XIROs Richturteil reicht.</summary>
        private const float VerdictRange = 13f;
        /// <summary>Rex' Jaegerblick laeuft bis zu diesem Zeitpunkt. Jeder Abschuss verlaengert ihn.</summary>
        private float focusUntil;
        private const float FocusBaseSeconds = 5f;
        private const float FocusPerKill = 0.6f;
        private const float FocusMaximum = 9f;
        /// <summary>Der Jaegerblick laeuft. Das HUD zeigt es am Ultimate-Knopf.</summary>
        public bool HunterFocusActive => Time.time < focusUntil;
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
        /// <summary>Wie gut der Moment gerade ist, solange geladen wird. Sonst <see cref="HeavyTiming.Loose"/>.</summary>
        public HeavyTiming HeavyTimingNow
            => chargingHeavy ? ActionBalance.Judge(HeavyChargeNormalized) : HeavyTiming.Loose;

        public bool HeavyPerfect => HeavyTimingNow == HeavyTiming.Perfect;
        public float UltimateNormalized => ultimateCharge;
        public bool UltimateReady => UltimateUnlocked && ultimateCharge >= 1f;

        /// <summary>Die Etage, auf der der Held gerade kaempft. Setzt der RunDirector.</summary>
        private int currentFloor = 1;

        /// <summary>Nur fuer die automatische Vorfuehrung: Ultimate unabhaengig von der Etage.</summary>
        private bool captureUltimate;

        /// <summary>Ist die Ultimate auf dieser Etage schon verdient? Siehe UltimateProgression.</summary>
        public bool UltimateUnlocked => captureUltimate || UltimateProgression.IsUnlocked(currentFloor, UltimateUnlockFloor);

        /// <summary>Ab welcher Etage die Ultimate da ist - ein Veteran bekommt sie eine Etage frueher.</summary>
        public int UltimateUnlockFloor => HeroPrestige.UltimateUnlockFloor(build ? build.PrestigeStep : 0);

        /// <summary>Wie stark die Ultimate gerade wirkt, 0 bis 1.</summary>
        public float UltimatePower => captureUltimate
            ? 1f
            : UltimateProgression.Power(currentFloor, UltimateUnlockFloor,
                HeroPrestige.UltimateStartPower(build ? build.PrestigeStep : 0));

        /// <summary>Wird bei jedem Etagenwechsel gerufen.</summary>
        public void SetFloor(int floor)
        {
            currentFloor = Mathf.Max(1, floor);
            build?.SetClimbFloor(currentFloor);
        }
        public bool UltimateActive => ultimateActive;
        public HeroClassId HeroClass => heroClass;
        public string LightName => HeroCatalog.LightAttackName(heroClass);
        public string HeavyName => HeroCatalog.HeavyAttackName(heroClass);
        public string SkillName => HeroCatalog.SkillName(heroClass);
        public string UltimateName => HeroCatalog.UltimateName(heroClass);

        /// <summary>
        /// Steuert dieses Geraet diesen Helden?
        ///
        /// Davon haengt mehr ab als die Zielanzeige. Alles, was der Spieler an seinem eigenen
        /// Helden spueren soll, gehoert nur dem Helden an diesem Geraet: die Linie und der Ring am
        /// Boden, die Trefferstarre, der Kamerastoss, die Ansagen ohne Ort, und der Zustand des
        /// schweren Angriffs im HUD.
        ///
        /// Seit die zwei Begleiter echte Helden sind, lief all das auch fuer sie. Die
        /// Trefferstarre setzt Time.timeScale fuer die ganze Welt herunter - gemessen lief das Spiel
        /// mit zwei Begleitern 13 bis 17 % der Zeit in Zeitlupe statt 4 bis 12 %, auch die eigene
        /// Figur unter dem Daumen. Und das HUD zeigte ihre Ladungen auf dem eigenen Knopf an: der
        /// schwere Angriff schien zu laden und auszuloesen, ohne dass jemand drueckte.
        ///
        /// Muss vor <see cref="ConfigureClass"/> gesetzt werden - dort entstehen die Anzeigen.
        /// </summary>
        public void SetLocal(bool value) => isLocal = value;

        private bool isLocal = true;

        /// <summary>Steuert dieses Geraet diesen Helden? Siehe <see cref="SetLocal"/>.</summary>
        public bool IsLocal => isLocal;

        // ── Was dieser Held ausloest ────────────────────────────────────────
        // Ereignisse an der Instanz, keine globalen: jeder Held meldet nur sich selbst. Der
        // Selbsttest zaehlt damit mit - und vergleicht die schweren Angriffe mit den Loslassern, die
        // sein Autopilot gedrueckt hat. Ein Unterschied waere der Fehler "loest von selbst aus".

        public event System.Action<HeavyTiming> HeavyFired;
        public event System.Action SkillFired;
        public event System.Action UltimateFired;

        // ── Rueckmeldung, die nur dem eigenen Helden gehoert ────────────────
        // Jeder Aufruf in dieser Klasse geht ueber diese fuenf. Ein Test haelt fest, dass keiner
        // daran vorbei direkt Hitstop, Kamera oder Ansage anspricht.

        private void LocalHitstop(float seconds, float scale = 0.06f)
        {
            if (isLocal) Hitstop.Freeze(seconds, scale);
        }

        private void LocalShake(float amount)
        {
            if (isLocal) CameraController.Impulse(amount);
        }

        private void LocalStinger(Sound sound, float gain = 1f)
        {
            if (isLocal) Sfx.Play2D(sound, gain);
        }

        /// <summary>Die Explosion sehen alle; den Stoss spuert nur, wem sie gehoert.</summary>
        private void Explosion(Vector3 position, float radius, Color color)
            => PrototypeVfx.SpawnExplosion(position, radius, color, isLocal);

        private void Judgement(Vector3 point, float radius, Color color)
            => PrototypeVfx.SpawnJudgement(point, radius, color, isLocal);

        public void ConfigureClass(HeroClassId value)
        {
            heroClass = value;
            if (!isLocal) return;
            // Erst hier steht die Klasse fest, und damit die Farbe der Anzeige.
            if (!aimIndicator) aimIndicator = AimIndicator.Attach(transform, HeroCatalog.Accent(heroClass));
            if (!targetIndicator) targetIndicator = gameObject.AddComponent<TargetLockIndicator>();
            if (!chargeRing) chargeRing = HeavyChargeRing.Attach(transform);
        }

        private AimIndicator aimIndicator;

        public void SetMuzzle(Transform value)
        {
            muzzle = value;
            motion = GetComponent<StylizedCharacterMotion>();
        }

        private void Awake()
        {
            input = GetComponent<IPlayerInputSource>();
            if (input == null)
                Debug.LogError($"SHATTERSPIRE: {name} hat keine Eingabe. Ein Held braucht einen "
                               + "PlayerInputRouter oder ein BotInput, und zwar vor der Waffe.");
            build = GetComponent<PlayerBuild>();
            health = GetComponent<Health>();
            killStreak = GetComponent<KillStreak>();
            controller = GetComponent<PlayerController>();
            motion = GetComponent<StylizedCharacterMotion>();
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
            ResolveAim();
            UpdateHeavyAttack();
            // Angriffswunsch kurz merken: ein schneller Tipp auf dem Telefon fiel bisher unter den
            // Tisch, wenn er in die Abklingzeit fiel. Jetzt loest er aus, sobald sie endet.
            if (input.AttackHeld) attackRequestedAt = Time.time;
            if (!chargingHeavy && !ultimateActive && Time.time - attackRequestedAt <= AttackBufferSeconds)
                TryLightAttack();
            // Ausloesen beim Loslassen, nicht beim Druck: ein kurzer Tipp ist beides in einem, wer
            // haelt kann vorher richten. Das ist der ganze Unterschied zwischen "sofort" und
            // "genau", und er kostet keine zweite Taste.
            if (input.SkillReleased) RequestSkill();
            if (skillQueuedUntil >= Time.time && SkillCanStart)
            {
                skillQueuedUntil = -1f;
                StartCoroutine(ClassSkill(skillQueuedAuto, skillQueuedDirection));
            }
            if (!chargingHeavy && input.UltimateReleased && UltimateReady && !ultimateActive)
                StartCoroutine(Ultimate());
            UpdateAimIndicator();
        }

        /// <summary>
        /// Fuellt die Ultimate sofort. Nur fuer die automatische Vorfuehrung (CaptureDemo): ohne das
        /// liesse sich der Schmiedesturz nicht ohne Kampf pruefen.
        /// </summary>
        public void FillUltimateForCapture()
        {
            captureUltimate = true;
            ultimateCharge = 1f;
            PublishHeavyState();
        }

        public void NotifyLightHit()
        {
            if (chargingHeavy || HeavyReady) return;
            heavyMeter = Mathf.Min(HeavyMeterMaximum,
                heavyMeter + ActionBalance.HeavyFillPerHit(heroClass) * build.HeavyChargeMultiplier);
            if (HeavyReady)
            {
                PrototypeVfx.SpawnHeavyReady(transform.position);
                LocalShake(0.07f);
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
            // Wer einsteckt, laedt mit: 40 % der Lebenspunkte verloren ergibt 10 % Ladung.
            if (health.Maximum > 0f) AddUltimateCharge(damage.Amount / (health.Maximum * 4f));
        }

        private void OnEntityDied(Health value)
        {
            if (!value || value.Team != TeamId.Enemy) return;
            if (build.HasSplinterBurst) QueueSplinterBurst(value.transform.position);
            if (build.HasAdrenaline)
            {
                var offset = value.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= RelicRules.AdrenalineRange * RelicRules.AdrenalineRange)
                    build.TriggerAdrenaline();
            }
            AddUltimateCharge(0.01f);
            // Die Serie zaehlt jeden gefallenen Gegner des Aufstiegs, nicht nur die eigenen Treffer:
            // im Co-op kaempft die Gruppe zusammen, und eine Serie, die ein Mitspieler kaputtmacht,
            // waere eine Strafe fuer Zusammenspiel.
            if (killStreak) killStreak.Register();
            // Kettenzuender: jeder gefallene Gegner hinterlaesst selbst eine scharfe Ladung.
            if (heroClass == HeroClassId.Bomber && Time.time < chainUntil && value)
                TimedBomb.Throw(value.transform.position + Vector3.up, value.transform.position, 0.12f, 0.5f,
                    2.6f, BaseDamage * 1.8f * build.DamageMultiplier,
                    ResolveDamageType(DamageType.Fire), gameObject, HeroCatalog.Accent(heroClass));
            // Jaegerblick lebt von Abschuessen: wer trifft, bleibt laenger im Zustand. Das macht die
            // Ultimate zu einer Kette statt zu einem einzelnen Knall.
            if (!HunterFocusActive) return;
            // Die Obergrenze waechst mit der Staerke: sonst holten ein paar Abschuesse die volle
            // Dauer zurueck, und eine frisch verdiente Ultimate waere nur am Anfang schwaecher.
            focusUntil = Mathf.Min(Time.time + FocusMaximum * UltimatePower, focusUntil + FocusPerKill);
            LocalStinger(Sound.FocusExtend, 0.6f);
        }

        private void AddUltimateCharge(float amount)
        {
            if (ultimateActive || UltimateReady || amount <= 0f) return;
            // Noch nicht verdient: nichts ansammeln. Sonst stuende sie auf Etage 3 sofort voll da,
            // und die Freischaltung waere nur eine Verzoegerung statt eines Anfangs.
            if (!UltimateUnlocked) return;
            // Eine Ultimate darf sich nicht selbst nachladen. Genau das passierte: XIROs Vergeltung
            // gibt 25 % mehr Schaden und 25 % mehr Angriffstempo, und jeder dieser Treffer lud die
            // naechste Vergeltung. Wer sie einmal hatte, hatte sie gleich wieder. Dasselbe gilt fuer
            // Rex' Jaegerblick, der sich mit Abschuessen selbst verlaengert.
            if (Time.time < ultimateLockedUntil || HunterFocusActive || build.Retribution) return;
            ultimateCharge = Mathf.Min(1f, ultimateCharge + amount * build.UltimateChargeMultiplier);
            if (!UltimateReady) return;
            PrototypeVfx.SpawnHeavyReady(transform.position);
            LocalShake(0.06f);
        }

        private void UpdateHeavyAttack()
        {
            if (input.HeavyPressed && HeavyReady && !chargingHeavy && !ultimateActive)
            {
                chargingHeavy = true;
                heavyCharge = 0f;
                windowAnnounced = false;
                chargeStartedAt = Time.time;
                PublishHeavyState();
            }
            if (!chargingHeavy) return;
            heavyCharge = Mathf.Min(HeavyChargeSeconds, heavyCharge + Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimDirection), 28f * Time.deltaTime);
            // Das Fenster sagt an, dass es offen ist. Auf dem Telefon liegt der Daumen auf dem Knopf
            // und verdeckt jeden Ring darunter - ein Ton ist der einzige Weg, der immer ankommt.
            var open = ActionBalance.Judge(HeavyChargeNormalized) == HeavyTiming.Perfect;
            if (open && !windowAnnounced)
            {
                windowAnnounced = true;
                LocalStinger(Sound.HeavyWindow, 0.9f);
            }
            chargeRing?.Show(HeavyChargeNormalized, open);
            PublishHeavyState();

            // Der schwere Angriff loest NIE von selbst aus. Nur das Loslassen loest ihn aus.
            //
            // Zweimal falsch repariert, beide Male am selben Punkt vorbei. Erst loeste ein Tipp ihn
            // sofort bei Ladung 0 aus - Druck und Loslassen sind auf dem Telefon ein Bild. Dann lud
            // ein Tipp durch und loeste am Ende der Ladung aus, also 1,2 s spaeter, ohne dass jemand
            // etwas tat. Beides liest sich von aussen gleich: "wird automatisch eingesetzt".
            //
            // Gemeinsame Ursache war, dass volle Ladung selbst ausloeste. Wer den Knopf einmal
            // beruehrt hatte, konnte den Schlag nicht mehr aufhalten. Jetzt wartet die volle Ladung,
            // und ein zu frueh losgelassener Knopf bricht ab und gibt den Balken zurueck: ein
            // versehentlicher Tipp kostet nichts und loest nichts aus.
            if (input.HeavyReleased)
            {
                if (ActionBalance.CanRelease(HeavyChargeNormalized)) ReleaseHeavyAttack();
                else CancelHeavyCharge();
                return;
            }
            // Notbremse fuer ein verlorenes Loslassen - etwa wenn der Finger die Flaeche verlaesst.
            // Sie bricht ab, sie schlaegt nicht zu: nichts darf ohne Loslassen ausloesen.
            if (Time.time - chargeStartedAt > HeavyChargeSeconds * 2.5f) CancelHeavyCharge();
        }

        // ── LIGHT ───────────────────────────────────────────────────────────

        private void TryLightAttack()
        {
            if (Time.time < nextShot) return;
            if (Time.time > comboExpiresAt) lightComboStep = 0;
            lightComboStep = lightComboStep % 3 + 1;
            var finisher = lightComboStep == 3;
            var direction = AcquireAttackDirection();
            // Die Figur steht im Moment des Schusses genau in Schussrichtung - sonst sitzt die
            // Muendung noch am alten Winkel, und Pfeil und Linie laufen nebeneinander her.
            FaceNow(direction);
            if (build.HasStormBell && ++stormBellCount >= RelicRules.StormBellEvery)
            {
                stormBellCount = 0;
                StartCoroutine(StormBell(direction));
            }

            if (heroClass == HeroClassId.Guardian)
            {
                HammerCombo(direction);
                return;
            }

            if (heroClass == HeroClassId.Bomber)
            {
                ThrowCharge(direction, finisher);
                return;
            }

            if (heroClass == HeroClassId.Paladin)
            {
                OathbladeCombo(direction, finisher);
                return;
            }

            comboExpiresAt = Time.time + 0.95f;
            var interval = heroClass == HeroClassId.Arcanist ? 0.34f : finisher ? 0.42f : 0.25f;
            // Im Jaegerblick schiesst Rex fast ohne Pause, und jeder Pfeil sucht sich sein Ziel.
            if (HunterFocusActive && heroClass == HeroClassId.Ranger) interval = 0.075f;
            nextShot = Time.time + interval / build.AttackSpeedMultiplier;
            var damage = BaseDamage * (finisher ? 1.38f : lightComboStep == 2 ? 1.1f : 1f);
            var impactRadius = finisher ? 0.75f : 0f;
            var impactDamage = finisher ? BaseDamage * build.DamageMultiplier * 0.36f : 0f;
            if (heroClass == HeroClassId.Arcanist && build.Has(PerkId.ArcanistVoidBurst))
            {
                impactRadius = Mathf.Max(impactRadius, 1.4f);
                impactDamage += BaseDamage * build.DamageMultiplier * 0.4f;
            }
            var focus = HunterFocusActive && heroClass == HeroClassId.Ranger;
            var arrows = finisher && heroClass == HeroClassId.Ranger && build.Has(PerkId.RangerSplitFinisher) ? 3 : 1;
            if (focus) arrows = Mathf.Max(arrows, 2);
            for (var i = 0; i < arrows; i++)
            {
                var angle = arrows == 1 ? 0f : Mathf.Lerp(-11f, 11f, i / (float)(arrows - 1));
                FireProjectile(Quaternion.Euler(0f, angle, 0f) * direction, damage, i == arrows / 2,
                    focus ? build.Pierces + 2 : build.Pierces,
                    build.Ricochets, finisher ? 1.25f : 1f, impactRadius, impactDamage, focus);
            }
            if (finisher) controller?.CombatStep(direction, 0.17f);
            PulseShot(finisher ? 1f : 0.62f);
            if (finisher) LocalShake(0.055f);
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
            comboExpiresAt = Time.time + 1.1f;

            if (lightComboStep == 1)
            {
                var gap = 0.38f / build.AttackSpeedMultiplier;
                nextShot = Time.time + gap;
                motion?.PlayMotion(AttackMotion.Swing, 0.9f);
                CommitMelee(AttackMotion.Swing, direction, gap);
                StartCoroutine(MeleeImpact(StrikeDelay(AttackMotion.Swing, 0.15f), () =>
                {
                    var point = transform.position + direction * 1.25f;
                    Strike(point, 1.35f + reach, hit, type, flash: false, weaponImpact: true);
                    if (build.ProjectileCount > 1)
                        Strike(point + direction * 1.2f, 1.1f, hit * 0.6f, type, flash: false, weaponImpact: true);
                }));
                return;
            }

            if (lightComboStep == 2)
            {
                var gap = 0.46f / build.AttackSpeedMultiplier;
                nextShot = Time.time + gap;
                motion?.PlayMotion(AttackMotion.Smash, 1.1f);
                CommitMelee(AttackMotion.Smash, direction, gap);
                StartCoroutine(MeleeImpact(StrikeDelay(AttackMotion.Smash, 0.2f), () =>
                {
                    const float offset = 1.55f;
                    var point = transform.position + direction * offset;
                    Strike(point, ActionBalance.MeleeReach(heroClass) - offset + reach, hit * 1.4f, type, 7f,
                        flash: false, weaponImpact: true, heavyImpact: true);
                    if (build.ProjectileCount > 1)
                        Strike(point + direction * 1.25f, 1.3f, hit * 0.8f, type, flash: false, weaponImpact: true);
                    controller?.CombatStep(direction, 0.25f);
                    LocalShake(0.06f);
                }));
                return;
            }

            var spinGap = 0.64f / build.AttackSpeedMultiplier;
            nextShot = Time.time + spinGap;
            motion?.PlayMotion(AttackMotion.Spin, 1.3f);
            CommitMelee(AttackMotion.Spin, direction, spinGap);
            StartCoroutine(MeleeImpact(StrikeDelay(AttackMotion.Spin, 0.17f), () =>
            {
                var radius = 2.6f + reach;
                Strike(transform.position, radius, hit * 1.75f, type, 8f, flash: false,
                    weaponImpact: true, heavyImpact: true);
                LocalShake(0.09f);
                if (build.Ricochets > 0) StartCoroutine(DelayedBlast(transform.position, radius * 0.85f, hit * 0.6f, type, 0.22f));
                if (build.Has(PerkId.GuardianCleaveWave))
                    StartCoroutine(Eruptions(transform.position, direction, 3, 3.4f, 2.4f, 1.2f, hit * 1.2f, 0.07f));
            }));
        }

        /// <summary>
        /// KORRs Licht-Angriff: eine Ladung im Bogen auf die Zielstelle, die nach kurzer Zuendschnur
        /// aufreisst. Jeder dritte Wurf ist eine Doppelladung.
        ///
        /// Der Unterschied zu den anderen drei Helden liegt nicht in der Zahl, sondern im Zeitpunkt:
        /// der Schaden faellt nicht beim Tastendruck, sondern dort, wo der Gegner gleich sein wird.
        /// </summary>
        private void ThrowCharge(Vector3 direction, bool finisher)
        {
            comboExpiresAt = Time.time + 1.05f;
            nextShot = Time.time + (finisher ? 0.5f : 0.34f) / build.AttackSpeedMultiplier;
            var type = ResolveDamageType(DamageType.Fire);
            var accent = HeroCatalog.Accent(heroClass);
            var reach = 6.5f + build.Pierces * 0.8f;
            var baseFuse = (finisher ? 0.75f : 0.9f) * (build.Has(PerkId.BomberShortFuse) ? 0.5f : 1f);
            var landing = BombTarget(direction, reach, 0.34f + baseFuse);
            var charges = finisher ? 2 : 1;
            if (build.ProjectileCount > 1) charges++;
            for (var i = 0; i < charges; i++)
            {
                var spread = charges == 1 ? Vector3.zero
                    : Quaternion.Euler(0f, Mathf.Lerp(-16f, 16f, i / (float)(charges - 1)), 0f) * direction * 1.4f;
                var fuse = baseFuse;
                // Nur der leichte Wurf laedt den schweren Balken - wie bei den anderen drei Helden
                // die leichten Treffer. Ohne das blieb KORRs Balken leer und RMB ohne Wirkung.
                TimedBomb.Throw(MuzzlePosition(), landing + spread, 0.34f, fuse,
                    2.1f + build.Pierces * 0.25f,
                    BaseDamage * (finisher ? 1.5f : 1.1f) * build.DamageMultiplier, type, gameObject, accent,
                    chargesHeavyMeter: true);
                if (!build.Has(PerkId.BomberClusterCharge)) continue;
                // Splitterladung: zwei kleinere kurz danach, leicht versetzt.
                for (var shard = -1; shard <= 1; shard += 2)
                    TimedBomb.Throw(MuzzlePosition(), landing + spread + Vector3.Cross(Vector3.up, direction) * (shard * 1.6f),
                        0.4f, fuse + 0.22f, 1.5f, BaseDamage * 0.7f * build.DamageMultiplier, type, gameObject, accent);
            }
            motion?.PlayMotion(AttackMotion.Swing, finisher ? 1f : 0.7f);
            Sfx.Play(Sound.BombThrow, transform.position);
            if (finisher) LocalShake(0.03f);
        }

        /// <summary>
        /// Wohin eine Ladung fliegt. Auf dem Boden vor dem Helden, aber nie weiter als seine
        /// Wurfweite - sonst legt man Ladungen in Gegenden, die man nicht sieht.
        /// </summary>
        private Vector3 ThrowTarget(Vector3 direction, float maximum)
        {
            var aim = input.AimPoint;
            aim.y = 0f;
            var offset = aim - transform.position;
            offset.y = 0f;
            var target = offset.magnitude > maximum || offset.sqrMagnitude < 1f
                ? transform.position + direction * maximum
                : transform.position + offset;
            var navigation = controller ? controller.Navigation : null;
            return navigation != null ? navigation.FurthestWalkableAlong(transform.position, target, 0.4f) : target;
        }

        /// <summary>
        /// Wohin eine Ladung von KORR fliegt: dorthin, wo das Ziel bei der Zuendung sein wird (siehe
        /// <see cref="BombThrow.Landing"/>). Nur fuer Ladungen mit Zuendschnur - Faehigkeiten, die
        /// eine Stelle oder eine Linie von Stellen treffen, bleiben bei <see cref="ThrowTarget"/>.
        /// </summary>
        private Vector3 BombTarget(Vector3 direction, float maximum, float secondsToBlast)
        {
            Vector3? target = null;
            var velocity = Vector3.zero;
            if (lockedTarget && lockedTarget.IsAlive)
            {
                target = lockedTarget.transform.position;
                var agent = lockedTarget.GetComponent<EnemyAgent>();
                if (agent) velocity = agent.Velocity;
            }
            var landing = BombThrow.Landing(transform.position, input.AimPoint, input.AutoAim, direction, target,
                velocity, maximum, secondsToBlast);
            var navigation = controller ? controller.Navigation : null;
            return navigation != null ? navigation.FurthestWalkableAlong(transform.position, landing, 0.4f) : landing;
        }

        /// <summary>
        /// KORRs schwerer Angriff: eine Haftmine. Gehalten wird sie groesser; im goldenen Fenster
        /// losgelassen zuendet sie sofort statt nach Zuendschnur - der perfekte Moment ist hier also
        /// nicht mehr Schaden, sondern kein Warten.
        /// </summary>
        private void ReleaseStickyMine(bool perfect, float normalized)
        {
            var type = ResolveDamageType(DamageType.Fire);
            var accent = HeroCatalog.Accent(heroClass);
            var direction = AcquireAttackDirection();
            var landing = BombTarget(direction, 7.5f, 0.3f + (perfect ? 0.05f : 0.85f));
            var radius = Mathf.Lerp(2.6f, 4.4f, normalized) * (perfect ? 1.25f : 1f);
            var damage = BaseDamage * Mathf.Lerp(2.2f, 3.6f, normalized) * build.HeavyDamageMultiplier
                         * build.DamageMultiplier * (build.Has(PerkId.BomberStickyCluster) ? 1.4f : 1f);
            TimedBomb.Throw(MuzzlePosition(), landing, 0.3f, perfect ? 0.05f : 0.85f, radius, damage,
                type, gameObject, accent);
            if (build.Has(PerkId.PerfectEcho) && perfect)
                TimedBomb.Throw(MuzzlePosition(), landing + direction * 2.4f, 0.34f, 0.2f, radius * 0.8f,
                    damage * 0.6f, type, gameObject, accent);
            motion?.PlayMotion(AttackMotion.Smash, perfect ? 1.4f : 1f);
            Sfx.Play(Sound.BombThrow, transform.position);
        }

        /// <summary>
        /// KORRs Faehigkeit: eine Sprengschnur. Drei Ladungen in einer Linie, die von vorn nach
        /// hinten zuenden - eine Wand, hinter die man sich zurueckzieht.
        /// </summary>
        private IEnumerator BlastCord(Vector3 direction)
        {
            var type = ResolveDamageType(DamageType.Fire);
            var accent = HeroCatalog.Accent(heroClass);
            var charges = build.Has(PerkId.BomberLongCord) ? 5 : 3;
            motion?.PlayMotion(AttackMotion.Cast, 1.2f);
            for (var i = 0; i < charges; i++)
            {
                var spot = ThrowTarget(direction, 2.6f + i * 2.4f);
                TimedBomb.Throw(MuzzlePosition(), spot, 0.28f, 0.55f + i * 0.12f, 2.4f,
                    BaseDamage * 1.6f * build.DamageMultiplier, type, gameObject, accent);
                Sfx.Play(Sound.BombThrow, transform.position, 0.7f);
                yield return new WaitForSeconds(0.09f);
            }
        }

        /// <summary>
        /// KORRs Ultimate: KETTENZUENDER. Alles, was gerade scharf ist, geht gleichzeitig hoch - mit
        /// doppeltem Radius. Danach laesst jeder gefallene Gegner sechs Sekunden lang selbst eine
        /// Ladung fallen.
        ///
        /// Sie macht aus sich heraus wenig: sie ist die Belohnung dafuer, vorher das Feld vorbereitet
        /// zu haben. Damit ist es die einzige Ultimate im Spiel, deren Wirkung davon abhaengt, was
        /// man in den Sekunden davor getan hat.
        /// </summary>
        private IEnumerator ChainDetonator()
        {
            var accent = HeroCatalog.Accent(heroClass);
            LocalStinger(Sound.ChainDetonate);
            Explosion(transform.position, 3f, accent);
            LocalShake(0.26f);
            // Der Zuschlag waechst mit der Staerke, nicht die ganze Ladung: auch eine frische
            // Ultimate zuendet mindestens mit vollem Wurfschaden.
            var power = UltimatePower;
            var chained = TimedBomb.DetonateAll(1f + 1f * power, 1f + 0.4f * power);
            Debug.Log($"SHATTERSPIRE Kettenzuender: {chained} Ladung(en) gleichzeitig gezuendet.");
            // Ohne vorbereitetes Feld wenigstens ein Fundament, damit die Ultimate nie ins Leere geht.
            if (chained == 0)
            {
                var direction = AcquireAttackDirection();
                for (var i = 0; i < 3; i++)
                    TimedBomb.Throw(MuzzlePosition(), ThrowTarget(direction, 3f + i * 2.2f), 0.3f, 0.35f + i * 0.1f,
                        3.4f, BaseDamage * 2.4f * power * build.DamageMultiplier,
                        ResolveDamageType(DamageType.Fire), gameObject, accent);
            }
            chainUntil = Time.time + (build.Has(PerkId.BomberChainFeed) ? 10f : 6f) * power;
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// XIROs Licht-Angriff: drei Schwerthiebe, und der dritte schickt eine Weihewelle nach
        /// vorn, die Gegner trifft und die Gruppe heilt.
        ///
        /// Das ist ihre Handschrift schon im einfachsten Angriff: jeder Abschluss gibt der Gruppe
        /// etwas zurueck. Die drei anderen Helden machen im Moment des Tastendrucks Schaden - sie
        /// macht Schaden und Boden gut.
        /// </summary>
        /// <summary>
        /// XIROs Licht-Angriff: zwei Hiebe und ein Sweep, dann laeuft eine Front nach vorn.
        ///
        /// Die Reichweite war der Grund, warum er sich schlecht angefuehlt hat. Sie stand auf 1,25
        /// plus 1,35 Radius, also 2,6 Einheiten - die kuerzeste im Spiel, kuerzer als der
        /// Kriegshammer mit 3,25. Ein Zweihaender, der kuerzer reicht als ein Hammer, ist einfach
        /// falsch; zusammen mit dem langsamsten Lauftempo des Spiels hiess das, dass jeder Crawler
        /// nach drei Hieben aus seiner Reichweite gelaufen war.
        ///
        /// Der dritte Hieb ist jetzt ein Sweep um ihn herum statt eines Punktes vor ihm. Der Schaden
        /// bleibt derselbe - die Klinge geht nur dorthin, wo man sie sieht.
        /// </summary>
        private void OathbladeCombo(Vector3 direction, bool finisher)
        {
            var type = ResolveDamageType(DamageType.Physical);
            var accent = HeroCatalog.Accent(heroClass);
            var hit = BaseDamage * build.DamageMultiplier * (finisher ? 1.4f : lightComboStep == 2 ? 1.12f : 1f);
            comboExpiresAt = Time.time + 1.05f;
            var gap = (finisher ? 0.42f : 0.3f) / build.AttackSpeedMultiplier;
            nextShot = Time.time + gap;
            var kind = finisher ? AttackMotion.Smash : AttackMotion.Swing;
            motion?.PlayMotion(kind, finisher ? 1.15f : 0.85f);
            CommitMelee(kind, direction, gap);
            StartCoroutine(MeleeImpact(StrikeDelay(kind, finisher ? 0.18f : 0.13f), () =>
            {
                // Der Sweep liegt naeher am Koerper und deckt dafuer auch die Seiten ab; die zwei
                // Hiebe davor reichen weit nach vorn. Beide enden bei 3,4 Einheiten.
                var offset = finisher ? 0.9f : 1.9f;
                var radius = ActionBalance.MeleeReach(heroClass) - offset + build.Pierces * 0.3f;
                var point = transform.position + direction * offset;
                Strike(point, radius, hit, type, flash: false, weaponImpact: true, heavyImpact: finisher);
                if (!finisher) return;
                PrototypeVfx.SpawnShockwave(point, radius + 0.4f, accent);
                ConsecrationWave(direction, accent, type);
            }));
            if (finisher) controller?.CombatStep(direction, 0.18f);
        }

        /// <summary>
        /// Die Weihewelle des dritten Hiebs: drei Stoesse nach vorn, die Gegner treffen und jedem
        /// Verbuendeten in der Naehe etwas Leben geben.
        /// </summary>
        private void ConsecrationWave(Vector3 direction, Color accent, DamageType type)
        {
            var damage = BaseDamage * build.DamageMultiplier * 0.55f;
            for (var step = 1; step <= 3; step++)
            {
                var point = transform.position + direction * (1.6f + step * 1.25f);
                Strike(point, 1.5f, damage, type, flash: false);
                PrototypeVfx.SpawnShockwave(point, 1.7f, accent);
            }
            if (build.Has(PerkId.PaladinTwinWave))
            {
                // Die zweite Welle laeuft weiter hinaus als die erste.
                for (var step = 4; step <= 6; step++)
                {
                    var far = transform.position + direction * (1.6f + step * 1.25f);
                    Strike(far, 1.5f, damage * 0.8f, type, flash: false);
                    PrototypeVfx.SpawnShockwave(far, 1.7f, accent);
                }
            }
            HealParty(BaseDamage * (build.Has(PerkId.PaladinTwinWave) ? 0.6f : 0.3f));
            Sfx.Play(Sound.CoreActivated, transform.position, 0.45f);
            LocalShake(0.05f);
        }

        /// <summary>
        /// Heilt XIRO und jeden Verbuendeten im Umkreis. Laeuft ueber die Lebensregister und nicht
        /// ueber eine feste Liste: im Co-op sind die Mitspieler keine Bots dieser Instanz.
        /// </summary>
        private void HealParty(float amount)
        {
            if (amount <= 0f) return;
            var active = Health.Active;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var health = active[i];
                if (!health || !health.IsAlive || health.Team != TeamId.Player) continue;
                var offset = health.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > 64f) continue;
                health.Heal(amount);
            }
        }

        /// <summary>
        /// XIROs schwerer Angriff: RICHTURTEIL. Halten richtet die Klinge auf, Loslassen ruft ein
        /// Urteil auf die anvisierte Stelle herab - eine Saeule aus Licht, die auch aus der Ferne
        /// trifft.
        ///
        /// Vorher stand hier die Klingenwehr: halten, Schlaege auffangen, zurueckgeben. Als Mechanik
        /// in Ordnung, aber es war die einzige schwere Aktion im Spiel, die nicht zuschlaegt - und
        /// sie belohnte nicht das Timing, sondern das In-Gefahr-Geraten. Wer nicht getroffen wurde,
        /// gab nichts zurueck.
        ///
        /// Das Richturteil ist die Gegenrichtung: XIRO braucht keinen Nahkontakt mehr, um Druck zu
        /// machen. Der perfekte Moment gibt weiterhin Kontrolle statt einer groesseren Zahl - das
        /// bleibt seine Handschrift.
        /// </summary>
        private void ReleaseVerdict(bool perfect, float normalized)
        {
            var type = ResolveDamageType(DamageType.Holy);
            var accent = HeroCatalog.Accent(heroClass);
            var direction = AcquireAttackDirection();
            var point = lockedTarget && lockedTarget.IsAlive
                ? new Vector3(lockedTarget.transform.position.x, transform.position.y, lockedTarget.transform.position.z)
                : ThrowTarget(direction, VerdictRange);
            var radius = perfect ? 3.4f : 2.6f;
            // Dieselbe Stufenrechnung wie bei den anderen vier. Vorher lief hier eine eigene Kurve,
            // und ein perfekter Moment brachte XIRO nur einen groesseren Kreis, keinen Aufschlag.
            var damage = BaseDamage * ActionBalance.Multiplier(normalized)
                         * build.HeavyDamageMultiplier * build.DamageMultiplier;

            // Die Klinge geht hoch und faellt - das Urteil kommt mit ihr herunter.
            motion?.PlayMotion(AttackMotion.Smash, perfect ? 1.4f : 1.15f);
            Sfx.Play(Sound.Draw, transform.position, 0.8f);
            var fall = StrikeDelay(AttackMotion.Smash, 0.22f);
            StartCoroutine(Verdict(point, radius, damage, type, accent, perfect, fall));
            if (build.Has(PerkId.PaladinTwinVerdict))
                StartCoroutine(Verdict(point, radius * 0.85f, damage * 0.55f, type, accent, false, fall + 0.34f));
            Debug.Log($"SHATTERSPIRE Richturteil: {damage:0} Schaden, Radius {radius:0.0}, "
                      + $"{Vector3.Distance(transform.position, point):0.0} Einheiten entfernt, perfekt {perfect}.");
        }

        private IEnumerator Verdict(Vector3 point, float radius, float damage, DamageType type,
            Color accent, bool perfect, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!health.IsAlive) yield break;
            Judgement(point, radius, accent);
            Strike(point, radius, damage, type, 6f, flash: false);
            if (!perfect) yield break;
            // Kein hoeherer Schaden, sondern Zeit: was unter der Saeule steht, steht still.
            foreach (var enemy in EnemyAgent.Active)
            {
                if (!enemy) continue;
                var offset = enemy.transform.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radius * radius) enemy.Stun(1.4f);
            }
            Explosion(point, radius, accent);
        }

        /// <summary>
        /// XIROs Faehigkeit: ASCHEWELLE. Ein maechtiger Schlag mit dem Zweihaender nach vorn, aus
        /// dem eine Front aus gluehender Asche ueber den Boden laeuft.
        ///
        /// Sie ersetzt den geweihten Boden. Der war ein Kreis, in den man sich stellte - eine
        /// Faehigkeit, die nichts tut, sondern etwas hinlegt. Hier ist der Schlag die Faehigkeit:
        /// Ausholen, treffen, und die Welle nimmt alles mit, was in der Bahn steht.
        /// </summary>
        private IEnumerator AshStrike(Vector3 direction)
        {
            var accent = HeroCatalog.Accent(heroClass);
            var type = ResolveDamageType(DamageType.Fire);
            // Der schwere Zweihand-Hieb, nicht die Zauberbewegung: die Faehigkeit ist ein Schlag.
            motion?.PlayMotion(AttackMotion.Smash, 1.45f);
            Sfx.Play(Sound.Draw, transform.position, 0.7f);
            // Ausholen bis zu dem Bild, in dem die Klinge im Clip tatsaechlich durchzieht.
            yield return new WaitForSeconds(StrikeDelay(AttackMotion.Smash, 0.28f));
            if (!health.IsAlive) yield break;

            // Die Welle kommt aus der Klinge - also wird die Richtung in dem Bild festgelegt, in dem
            // der Hieb durchzieht, und der Koerper dreht sich dazu.
            //
            // Vorher stand sie beim Tastendruck fest, 0,22 s vorher. In der Zwischenzeit drehte sich
            // die Figur weiter: am PC der Maus nach, auf dem Telefon einem neu gewaehlten Ziel. Man
            // sah XIRO in eine Richtung schlagen und die Welle in eine andere laufen - "Aschewelle
            // fliegt sonst wo hin". Jetzt koennen die zwei nicht mehr auseinanderfallen, weil es nur
            // noch eine Richtung gibt.
            direction = AcquireAttackDirection();
            FaceNow(direction);

            var reach = build.Has(PerkId.PaladinWideGround) ? 16f : 11f;
            var waveDamage = BaseDamage * 2.6f * build.DamageMultiplier;
            // Der Hieb selbst trifft, was direkt vor ihm steht - die Welle den Rest der Bahn.
            Strike(transform.position + direction * 1.6f, 2.2f, waveDamage * 0.8f, type, flash: true);
            AshWave.Launch(transform.position + direction * 1.2f + Vector3.up * 0.1f, direction,
                reach, waveDamage, type, gameObject, accent);
            controller?.CombatStep(direction, 0.45f);
            LocalShake(0.16f);
            Sfx.Play(Sound.Shockwave, transform.position, 0.9f);
            Debug.Log($"SHATTERSPIRE Aschewelle: {reach:0} Einheiten Reichweite, {waveDamage:0} Schaden.");
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// XIROs Ultimate: AEGIS. Eine Kuppel, die den Schaden der Gruppe schluckt und ihn am Ende
        /// zurueckgibt.
        /// </summary>
        private IEnumerator WrathfulRetribution()
        {
            var accent = HeroCatalog.Accent(heroClass);
            LocalStinger(Sound.UltimateRise);
            Explosion(transform.position + Vector3.up * 0.8f, 3.4f, accent);
            PrototypeVfx.SpawnShockwave(transform.position, 4.2f, accent);
            LocalShake(0.22f);
            var seconds = (build.Has(PerkId.PaladinLongVigil) ? 9f : 7f) * UltimatePower;
            build.BeginRetribution(seconds);
            motion?.ShowWrathWings(accent, seconds);
            Debug.Log($"SHATTERSPIRE Zornige Vergeltung: {seconds:0} s, "
                      + $"{(PlayerBuild.RetributionDamage - 1f) * 100f:0} % Schaden, "
                      + $"{(PlayerBuild.RetributionAttackSpeed - 1f) * 100f:0} % Angriffstempo, "
                      + $"{(PlayerBuild.RetributionSpeed - 1f) * 100f:0} % Lauftempo.");
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// Bindung und Schritt: der Held bleibt waehrend des Schlags weitgehend stehen und wird
        /// dabei auf sein Ziel zugetragen.
        ///
        /// Die beiden gehoeren zusammen. Bindung allein macht den Kampf zaeh - man steht fest und
        /// der Gegner ist trotzdem einen halben Meter zu weit weg. Erst der Schritt macht daraus
        /// Wucht statt Bremse.
        /// </summary>
        /// <summary>
        /// Bindet den Helden fuer die Dauer des Hiebs und laesst ihn ins Ziel nachsetzen.
        /// <paramref name="gap"/> ist die Zeit bis zum naechsten Hieb - daran haengt, wie lange die
        /// Bindung halten darf.
        /// </summary>
        private void CommitMelee(AttackMotion kind, Vector3 direction, float gap)
        {
            var windup = StrikeDelay(kind, 0.15f);
            controller?.BindDuringAttack(MeleeApproach.BoundSeconds(windup, gap), MeleeApproach.BoundSpeed);
            if (!lockedTarget || !lockedTarget.IsAlive) return;
            var offset = lockedTarget.transform.position - transform.position;
            offset.y = 0f;
            var run = HeroCatalog.BaseSpeed(heroClass) * build.MoveSpeedMultiplier;
            var step = MeleeApproach.StepDistance(offset.magnitude, windup, heroClass, run);
            if (step > 0f) controller?.Lunge(direction, step, windup);
        }

        /// <summary>
        /// Wann der Schaden faellt: in dem Moment, in dem die Waffe im Clip durchzieht. Vorher stand
        /// hier je Schlag eine Zahl, die neben der Animation herlief - beim Wirbel fiel der Schaden
        /// 0,31 s bevor sich die Waffe ueberhaupt bewegte. Ohne echten Kampfclip bleibt der alte Wert.
        /// </summary>
        private float StrikeDelay(AttackMotion kind, float fallback)
        {
            var measured = motion ? motion.StrikeSeconds(kind) : 0f;
            return measured > 0f ? measured : fallback;
        }

        // ── Relikte ─────────────────────────────────────────────────────────

        private int stormBellCount;
        private float splinterWindowStart = -10f;
        private int splinterBurstsInWindow;

        /// <summary>Splitterbersten: der Gegner berstet einen Moment nach seinem Fall.</summary>
        private void QueueSplinterBurst(Vector3 point)
        {
            if (Time.time - splinterWindowStart >= 1f)
            {
                splinterWindowStart = Time.time;
                splinterBurstsInWindow = 0;
            }
            if (splinterBurstsInWindow >= RelicRules.SplinterBurstsPerSecond) return;
            splinterBurstsInWindow++;
            StartCoroutine(SplinterBurst(point));
        }

        private IEnumerator SplinterBurst(Vector3 point)
        {
            // Kurz verzoegert: sonst toetet ein Bersten im selben Aufruf den naechsten Gegner, der
            // wieder berstet - eine Rekursion statt einer sichtbaren Kette.
            yield return new WaitForSeconds(0.12f);
            CombatUtility.Explode(point, RelicRules.SplinterBurstRadius,
                BaseDamage * RelicRules.SplinterBurstDamage * build.DamageMultiplier,
                TeamId.Enemy, ResolveDamageType(DamageType.Physical), gameObject);
        }

        /// <summary>Sturmglocke: ein Blitz faellt auf das Ziel des Angriffs.</summary>
        private IEnumerator StormBell(Vector3 direction)
        {
            var point = lockedTarget && lockedTarget.IsAlive
                ? lockedTarget.transform.position
                : ThrowTarget(direction, 8f);
            point.y = transform.position.y;
            yield return new WaitForSeconds(0.18f);
            if (!health.IsAlive) yield break;
            var color = PrototypeVfx.ElementColor(DamageType.Lightning);
            Judgement(point, RelicRules.StormBellRadius, color);
            Strike(point, RelicRules.StormBellRadius,
                BaseDamage * RelicRules.StormBellDamage * build.DamageMultiplier, DamageType.Lightning, 4f, flash: false);
        }

        /// <summary>Phantomklinge: nach dem Dash schneidet es entlang der gerade gelaufenen Strecke.</summary>
        private IEnumerator PhantomEdge(Vector3 origin)
        {
            yield return new WaitForSeconds(0.24f);
            if (!health.IsAlive) yield break;
            var end = transform.position;
            var type = ResolveDamageType(DamageType.Physical);
            var damage = BaseDamage * RelicRules.PhantomEdgeDamage * build.DamageMultiplier;
            for (var cut = 1; cut <= RelicRules.PhantomEdgeCuts; cut++)
                Strike(Vector3.Lerp(origin, end, cut / (float)RelicRules.PhantomEdgeCuts), RelicRules.PhantomEdgeRadius,
                    damage, type, 3f, flash: false, weaponImpact: true);
        }

        /// <summary>
        /// Laesst den Hieb im Bild des Treffers fallen. Der schwere Balken fuellt sich nur, wenn der
        /// Hieb auch etwas getroffen hat.
        ///
        /// Vorher lud jeder Nahkampfschwung, auch ins Leere. Fernkaempfer luden nur bei Treffern
        /// (Geschoss und Bombe melden erst beim Einschlag) - Nahkaempfer bekamen ihren schweren
        /// Angriff also geschenkt, und ein Begleiter, der in die Luft schlug, hatte ihn alle zwei
        /// Sekunden. Die Norm in ActionBalance sagt "zwei Sekunden, wenn er dabei trifft".
        /// </summary>
        private IEnumerator MeleeImpact(float delay, System.Action impact)
        {
            var swing = swingGeneration;
            yield return new WaitForSeconds(delay);
            if (!health.IsAlive || swing != swingGeneration) yield break;
            var before = landedHits;
            impact();
            if (landedHits > before) NotifyLightHit();
        }

        /// <summary>Wie viele Gegner die Schlaege dieses Helden insgesamt getroffen haben.</summary>
        private int landedHits;

        // ── Zielanzeige ─────────────────────────────────────────────────────

        /// <summary>
        /// Zeigt, was die gerade gerichtete Aktion trifft. Die Reihenfolge ist die Reihenfolge der
        /// Absicht: wer die Ultimate haelt, will die Ultimate sehen, nicht seinen Schuss.
        /// </summary>
        private void UpdateAimIndicator()
        {
            if (!aimIndicator) return;
            if (ultimateActive || !health.IsAlive)
            {
                aimIndicator.Hide();
                return;
            }
            var slot = input.UltimateHeld && UltimateReady ? ActionSlot.Ultimate
                : input.SkillHeld && Time.time >= skillReadyAt ? ActionSlot.Skill
                : chargingHeavy ? ActionSlot.Heavy
                : input.AttackHeld ? ActionSlot.Light
                : ActionSlot.Passive;
            if (slot == ActionSlot.Passive)
            {
                aimIndicator.Hide();
                return;
            }
            var aim = DescribeAim(slot);
            aimIndicator.Show(aim, AcquireAttackDirection(), AimTarget(aim));
        }

        /// <summary>
        /// Welche Form die Aktion dieses Helden auf dem Boden hat. Die Zahlen stehen in AimCatalog,
        /// damit sie sich ohne laufendes Spiel nachrechnen lassen.
        /// </summary>
        public AimDescription DescribeAim(ActionSlot slot)
            => AimCatalog.Describe(heroClass, slot, build.Pierces, build.Has(PerkId.PaladinWideGround),
                AimAssistRange, VerdictRange);

        /// <summary>
        /// Wohin ein Kreis dieser Aktion faellt. Dieselbe Rechnung wie die Aktion selbst: erfasstes
        /// Ziel zuerst, sonst die Zielrichtung auf Reichweite - und nie durch eine Wand.
        /// </summary>
        public Vector3 AimTarget(AimDescription aim)
        {
            if (aim.Shape == AimShape.Around) return transform.position;
            var direction = AcquireAttackDirection();
            if (lockedTarget && lockedTarget.IsAlive)
            {
                var offset = lockedTarget.transform.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= aim.Range)
                    return new Vector3(lockedTarget.transform.position.x, transform.position.y,
                        lockedTarget.transform.position.z);
            }
            return ThrowTarget(direction, aim.Range);
        }

        // ── HEAVY ───────────────────────────────────────────────────────────

        /// <summary>
        /// Bricht die Ladung ab und laesst den Balken stehen. Ein Tipp daneben darf nichts kosten -
        /// und er darf vor allem nichts ausloesen.
        /// </summary>
        private void CancelHeavyCharge(bool explain = true)
        {
            chargingHeavy = false;
            heavyCharge = 0f;
            // Nur ein zu frueh losgelassener Knopf braucht die Erklaerung des Griffs. Wer mit dem
            // Dash abbricht, hat es so gewollt.
            if (explain) LastHeavyCancel = Time.time;
            chargeRing?.Hide();
            PublishHeavyState();
        }

        private void ReleaseHeavyAttack()
        {
            var normalized = HeavyChargeNormalized;
            var timing = ActionBalance.Judge(normalized);
            HeavyFired?.Invoke(timing);
            var perfect = timing == HeavyTiming.Perfect;
            chargeRing?.Hide();
            // Der Klang sagt, welche Stufe es war - noch bevor die Zahl am Gegner steht.
            if (timing != HeavyTiming.Loose)
                LocalStinger(perfect ? Sound.HeavyPerfect : Sound.HeavyGood, perfect ? 1f : 0.7f);
            if (perfect && build.HasSteadyHeart) health.Heal(health.Maximum * RelicRules.SteadyHeartHeal);
            if (heroClass == HeroClassId.Paladin)
            {
                ReleaseVerdict(perfect, normalized);
                heavyMeter = 0f;
                chargingHeavy = false;
                heavyCharge = 0f;
                nextShot = Time.time + 0.32f;
                PublishHeavyState();
                LocalShake(perfect ? 0.2f : 0.1f);
                return;
            }
            if (heroClass == HeroClassId.Bomber)
            {
                ReleaseStickyMine(perfect, normalized);
                heavyMeter = 0f;
                chargingHeavy = false;
                heavyCharge = 0f;
                nextShot = Time.time + 0.3f;
                PublishHeavyState();
                LocalShake(perfect ? 0.16f : 0.08f);
                return;
            }
            var multiplier = ActionBalance.Multiplier(normalized) * build.HeavyDamageMultiplier;
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

            Explosion(transform.position + direction * 1.1f + Vector3.up * 0.3f,
                perfect ? 2f : 1.2f, perfect ? new Color(1f, 0.78f, 0.12f) : HeroCatalog.Accent(heroClass));
            LocalShake(perfect ? 0.2f : 0.1f);
            // Das Perfect-Fenster ist die praeziseste Eingabe im ganzen Spiel und
            // hatte bisher kein eigenes Feedback ausser dem Schaden.
            LocalHitstop(perfect ? 0.095f : 0.05f, perfect ? 0.04f : 0.09f);
            heavyMeter = 0f;
            heavyCharge = 0f;
            chargingHeavy = false;
            nextShot = Time.time + 0.25f;
            PublishHeavyState();
        }

        // ── SKILL ───────────────────────────────────────────────────────────

        /// <summary>
        /// Rahmen um die Faehigkeit: haelt ihre Zielabsicht fest, solange sie laeuft, und gibt sie
        /// danach frei - egal, an welcher der vielen Stellen die Faehigkeit endet.
        /// </summary>
        /// <summary>
        /// So lange vor dem Ende der Abklingzeit zaehlt ein Loslassen noch: die Faehigkeit loest aus,
        /// sobald sie bereit ist.
        ///
        /// Vorher ging ein Loslassen kurz vor dem Ende einfach verloren - kein Schlag, kein Hinweis.
        /// Der normale Angriff hatte seit Langem einen Puffer, die Faehigkeit nicht. Wer im Kampf auf
        /// die Zahl im Knopf schaut und bei "0,2" loslaesst, hat richtig gespielt.
        /// </summary>
        public const float SkillBufferSeconds = 0.4f;

        private float skillQueuedUntil = -1f;
        private bool skillQueuedAuto;
        private Vector3 skillQueuedDirection;

        /// <summary>
        /// Wann zuletzt ein Loslassen abgewiesen wurde, weil die Faehigkeit noch lange nicht bereit
        /// war. Das HUD zeigt es am Knopf - ein Druck ohne jede Antwort liest sich wie ein Defekt.
        /// </summary>
        public float LastSkillRefused { get; private set; } = -99f;

        private bool SkillCanStart => !chargingHeavy && !ultimateActive && Time.time >= skillReadyAt;

        /// <summary>
        /// Das Loslassen der Faehigkeit: sofort ausloesen, wenn sie bereit ist; knapp davor merken,
        /// samt der Richtung, die in diesem Moment gemeint war; sonst sichtbar abweisen.
        /// </summary>
        private void RequestSkill()
        {
            if (SkillCanStart)
            {
                StartCoroutine(ClassSkill());
                return;
            }
            var wait = skillReadyAt - Time.time;
            if (!chargingHeavy && !ultimateActive && wait <= SkillBufferSeconds)
            {
                skillQueuedUntil = Time.time + Mathf.Max(0f, wait) + 0.1f;
                skillQueuedAuto = input.AutoAim;
                skillQueuedDirection = aimDirection;
                return;
            }
            LastSkillRefused = Time.time;
        }

        private IEnumerator ClassSkill(bool? auto = null, Vector3? direction = null)
        {
            // Im Bild des Ausloesens, nicht erst in der verschachtelten Faehigkeit: sonst koennte ein
            // zweites Loslassen im selben Moment eine zweite Faehigkeit starten.
            skillReadyAt = Time.time + SkillCooldown;
            BeginAbilityAim(auto, direction);
            SkillFired?.Invoke();
            if (build.HasOverflow)
            {
                heavyMeter = Mathf.Min(HeavyMeterMaximum, heavyMeter + HeavyMeterMaximum * RelicRules.OverflowHeavyFill);
                PublishHeavyState();
            }
            yield return ClassSkillBody();
            abilityAimUntil = 0f;
        }

        private IEnumerator ClassSkillBody()
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
                LocalShake(0.12f);
                yield break;
            }

            if (heroClass == HeroClassId.Bomber)
            {
                yield return BlastCord(direction);
                yield break;
            }

            if (heroClass == HeroClassId.Paladin)
            {
                yield return AshStrike(direction);
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
                        center = Vector3.MoveTowards(center, transform.position + aimDirection * 5.5f, 1.8f);
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
            UltimateFired?.Invoke();
            ultimateActive = true;
            ultimateCharge = 0f;
            ultimateLockedUntil = Time.time + UltimateLockoutSeconds;
            BeginAbilityAim();
            var direction = AcquireAttackDirection();
            health.SetInvulnerable(heroClass == HeroClassId.Guardian ? 2.2f : 1.3f);
            if (build.Has(PerkId.UltimateAfterglow)) health.Heal(health.Maximum * 0.3f);
            LocalShake(0.24f);
            LocalHitstop(0.07f, 0.06f);
            // Ohne Ort und damit ohne Abstandsdaempfung: die eigene Ultimate soll im Vordergrund stehen.
            LocalStinger(Sound.UltimateRise);
            PrototypeVfx.SpawnHeavyReady(transform.position);

            if (heroClass == HeroClassId.Guardian) yield return ForgePlunge(direction);
            else if (heroClass == HeroClassId.Arcanist) yield return OpenTimeRift(direction);
            else if (heroClass == HeroClassId.Bomber) yield return ChainDetonator();
            else if (heroClass == HeroClassId.Paladin) yield return WrathfulRetribution();
            else yield return HuntersFocus();

            abilityAimUntil = 0f;
            ultimateActive = false;
        }

        /// <summary>
        /// Brax: SCHMIEDESTURZ. Er springt in die Hoehe und kommt dort herunter, wohin gezielt wurde.
        /// Der Aufschlag betaeubt alles im Umkreis und laesst einen Krater zurueck, der Gegner
        /// festhaelt und Brax' Schlaege verstaerkt, solange er darin steht.
        ///
        /// Bewusst kein groesseres Beben: sein Skill ist ein Vorwaertssturm, seine Ultimate ist ein
        /// Ortswechsel mit Kontrolle. Man springt damit auf einen Armbruster im Rueckraum oder in
        /// die Mitte einer Gruppe - und besitzt danach ein Stueck Boden.
        /// </summary>
        private IEnumerator ForgePlunge(Vector3 direction)
        {
            const float airSeconds = 0.62f;
            const float impactRadius = 5.2f;
            var accent = HeroCatalog.Accent(heroClass);
            var start = transform.position;
            var landing = start + direction * 7.5f;
            var navigation = controller ? controller.Navigation : null;
            // Entlang der Strecke tasten statt den Endpunkt zu klemmen: wer auf eine Wand zielt,
            // landet an der Wand und nicht wieder bei sich selbst.
            if (navigation != null) landing = navigation.FurthestWalkableAlong(start, landing, 0.6f);

            // Steht er mit dem Gesicht zur Wand, bringt der Flug nichts. Dann schlaegt er auf der
            // Stelle ein: Betaeubung und Krater wirken trotzdem, die Ultimate ist nicht verschenkt.
            var reach = new Vector2(landing.x - start.x, landing.z - start.z).magnitude;
            var leaps = reach >= 1.5f;

            // Wohin es geht, muss vorher sichtbar sein - auch fuer die zwei Mitspieler.
            var marker = PrototypeVfx.SpawnTelegraph(landing, impactRadius, false);
            motion?.PlayMotion(AttackMotion.Leap, 1.6f);
            health.SetInvulnerable(airSeconds + 0.35f);
            Sfx.Play(Sound.PlungeRise, start);

            var flightSeconds = leaps ? airSeconds : airSeconds * 0.45f;
            var elapsed = 0f;
            while (elapsed < flightSeconds)
            {
                var t = elapsed / flightSeconds;
                // Wurfparabel: waagerecht gleichmaessig, senkrecht als Bogen.
                var flat = Vector3.Lerp(start, landing, t);
                var height = Mathf.Sin(t * Mathf.PI) * (leaps ? 4.2f : 2.2f);
                controller?.Airborne(flat + Vector3.up * height);
                elapsed += Time.deltaTime;
                yield return null;
            }
            controller?.Land(landing);
            if (marker) Destroy(marker);
            // Die Strecke ins Log: auf einem Bild ist ein Ortswechsel von wenigen Metern nicht
            // sicher zu erkennen, und der Ortswechsel ist der Sinn dieser Ultimate.
            var travelled = new Vector2(landing.x - start.x, landing.z - start.z).magnitude;
            Debug.Log($"SHATTERSPIRE Schmiedesturz: {travelled:0.0} m versetzt, Landung {landing}.");

            motion?.PlayMotion(AttackMotion.Smash, 1.8f);
            Sfx.Play(Sound.PlungeImpact, landing);
            LocalShake(0.3f);
            LocalHitstop(0.08f, 0.08f);
            var type = build.Has(PerkId.GuardianMoltenQuake) ? DamageType.Fire : ResolveDamageType(DamageType.Physical);
            // Frisch verdient wirkt die Ultimate schwaecher; der Radius bleibt, damit man sie lesen kann.
            var power = UltimatePower;
            Strike(landing, impactRadius, BaseDamage * 4.5f * power * build.DamageMultiplier, type, 6f);
            PrototypeVfx.SpawnShockwave(landing, impactRadius + 0.6f, accent);
            Explosion(landing, impactRadius * 0.7f, accent);

            // Betaeubung: das Fenster, in dem die Gruppe nachsetzen kann.
            foreach (var agent in EnemyAgent.Active)
            {
                if (!agent) continue;
                var offset = agent.transform.position - landing;
                offset.y = 0f;
                if (offset.sqrMagnitude <= impactRadius * impactRadius) agent.Stun(2f * power);
            }
            ForgeCrater.Spawn(landing, impactRadius, 8f * power, transform, build,
                type == DamageType.Fire ? new Color(1f, 0.42f, 0.08f) : accent);
        }

        /// <summary>
        /// Orion: ZEITRISS. Eine Flaeche, in der Gegner kriechen und in der jedes feindliche Geschoss
        /// aufgeloest wird. Der Schaden ist gering - das ist der Punkt.
        ///
        /// Sein Skill ist der Schwarze Stern, also Schaden auf einer Flaeche. Die Ultimate macht das
        /// Gegenteil: sie nimmt dem Gegner die Mittel. Gegen Armbruester und Schuetzen ist sie die
        /// Antwort, die es vorher nicht gab, und im Co-op ist sie der Rueckzugsraum fuer das Team.
        /// </summary>
        private IEnumerator OpenTimeRift(Vector3 direction)
        {
            motion?.PlayMotion(AttackMotion.Summon, 1.6f);
            var centre = transform.position + direction * 5.5f;
            var navigation = controller ? controller.Navigation : null;
            if (navigation != null) centre = navigation.ClampToWalkable(centre, 0.6f);
            var accent = Color.Lerp(HeroCatalog.Accent(heroClass), new Color(0.1f, 0.9f, 1f), 0.45f);
            Sfx.Play(Sound.RiftOpen, centre);
            Explosion(centre, 3.2f, accent);
            var power = UltimatePower;
            var seconds = (build.Has(PerkId.ArcanistEventHorizon) ? 8.5f : 6.5f) * power;
            TimeRift.Spawn(centre, 6.2f, seconds, BaseDamage * 0.18f * power * build.DamageMultiplier, gameObject, accent);
            // Der Arkanist ist sofort wieder handlungsfaehig: die Zone arbeitet allein.
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>
        /// Rex: JAEGERBLICK. Kein Pfeilhagel, sondern ein Zustand: fuenf Sekunden schiesst er fast
        /// ohne Pause, jeder Pfeil sucht sein Ziel und durchbohrt mehr, und jeder Abschuss verlaengert
        /// den Zustand um 0,6 Sekunden bis maximal neun.
        ///
        /// Damit ist die Ultimate nicht mehr die groessere Version seines Skills, sondern eine Kette,
        /// die man selbst am Leben haelt - wer trifft, bleibt drin.
        /// </summary>
        private IEnumerator HuntersFocus()
        {
            focusUntil = Time.time + (build.Has(PerkId.RangerHomingBarrage) ? FocusBaseSeconds + 2f : FocusBaseSeconds)
                * UltimatePower;
            motion?.PlayMotion(AttackMotion.Draw, 1.4f);
            LocalStinger(Sound.FocusEnter);
            PrototypeVfx.SpawnHeavyReady(transform.position);
            // Schneller unterwegs: der Zustand ist zum Kiten gedacht, nicht zum Stehenbleiben.
            build.SetFocusSpeed(1.25f);
            // Sofort wieder handlungsfaehig - der Zustand lebt davon, dass man schiesst.
            yield return null;
            ultimateActive = false;
            while (HunterFocusActive) yield return null;
            build.SetFocusSpeed(1f);
            LocalStinger(Sound.FocusEnd, 0.7f);
        }

        // ── DASH ────────────────────────────────────────────────────────────

        /// <summary>Vom PlayerController zu Beginn eines Dash gerufen. Hier haengen die Dash-Upgrades der Helden.</summary>
        /// <summary>
        /// Zaehler der Hiebe, die ein Dash abgebrochen hat. Ein Hieb merkt sich beim Ausholen den
        /// Stand und faellt nicht, wenn sich der Stand bis zum Treffer geaendert hat.
        /// </summary>
        private int swingGeneration;

        public void OnDashStarted(Vector3 origin, Vector3 direction)
        {
            // Der Dash ist der Ausweg aus jeder Aktion. Vorher landete ein Hieb, der schon ausgeholt
            // hatte, auch nach dem Dash noch - an der neuen Stelle, weil der Treffer erst beim
            // Einschlag berechnet wird. Wer aus einem Schlag herausrollte, schlug trotzdem zu, nur
            // woanders. Eine Ladung des schweren Angriffs bricht er ebenso ab und gibt den Balken
            // zurueck.
            swingGeneration++;
            if (chargingHeavy) CancelHeavyCharge(explain: false);
            if (build.HasSparkWard) build.ArmSparkWard();
            if (build.HasPhantomEdge) StartCoroutine(PhantomEdge(origin));
            if (heroClass == HeroClassId.Ranger && build.Has(PerkId.RangerPartingShot))
            {
                var aim = AcquireAttackDirection();
                for (var i = 0; i < 5; i++)
                    FireProjectile(Quaternion.Euler(0f, Mathf.Lerp(-24f, 24f, i / 4f), 0f) * aim, BaseDamage * 0.8f,
                        false, build.Pierces, build.Ricochets, 0.95f, 0f, 0f);
                PrototypeVfx.SpawnMuzzle(ShotOrigin(aim), aim);
            }
            if (heroClass == HeroClassId.Bomber && build.Has(PerkId.BomberSmokeStep))
                TimedBomb.Throw(origin + Vector3.up * 0.5f, origin, 0.1f, 0.7f, 2.4f,
                    BaseDamage * 1.5f * build.DamageMultiplier, ResolveDamageType(DamageType.Fire),
                    gameObject, HeroCatalog.Accent(heroClass));
            if (heroClass == HeroClassId.Arcanist && build.Has(PerkId.ArcanistPhaseRift))
                StartCoroutine(DelayedBlast(origin, 3f, BaseDamage * 1.8f * build.DamageMultiplier,
                    ResolveDamageType(DamageType.Void), 0.45f));
        }

        public void OnDashEnded(Vector3 origin, Vector3 end, Vector3 direction)
        {
            if (heroClass == HeroClassId.Paladin && build.Has(PerkId.PaladinAshStep))
            {
                // Aschespur: der Dash zieht eine kleine Welle hinter sich her. Aus dem Ausweichen
                // wird damit ein Angriff, ohne dass es eine zweite Taste braucht.
                var back = origin - end;
                back.y = 0f;
                if (back.sqrMagnitude > 0.04f)
                    AshWave.Launch(end, -back.normalized, 5.5f,
                        BaseDamage * 0.9f * build.DamageMultiplier,
                        ResolveDamageType(DamageType.Fire), gameObject, HeroCatalog.Accent(heroClass));
                return;
            }
            if (heroClass != HeroClassId.Guardian || !build.Has(PerkId.GuardianShoulderCharge)) return;
            // Ein Treffer ueber die ganze Strecke: Mittelpunkt des Wegs, Radius bis zu beiden Enden.
            var middle = Vector3.Lerp(origin, end, 0.5f);
            var radius = Mathf.Max(1.6f, Vector3.Distance(origin, end) * 0.5f + 0.9f);
            Strike(middle, radius, BaseDamage * 1.2f * build.DamageMultiplier, ResolveDamageType(DamageType.Physical));
            PrototypeVfx.SpawnShockwave(end, 2.2f, HeroCatalog.Accent(heroClass));
            LocalShake(0.06f);
        }

        // ── Treffer ─────────────────────────────────────────────────────────

        /// <summary>
        /// Flaechentreffer des Spielers. Anders als CombatUtility.Explode wirken hier Krit, Finisher,
        /// Lebensraub und Elemente, und der Schaden laedt die Ultimate. pull zieht Gegner zur Mitte.
        /// </summary>
        /// <summary>
        /// Ein Schlag in einem Umkreis. Mit <paramref name="weaponImpact"/> entsteht an jedem
        /// getroffenen Gegner ein Aufblitzen mit Funken in Schlagrichtung - und nur dann. Vorher lag
        /// bei jedem Hieb eine Schockwelle auf dem Boden, auch beim Danebenhauen.
        /// </summary>
        private void Strike(Vector3 point, float radius, float damage, DamageType type, float knockback = 4f, bool pull = false,
            bool flash = true, bool weaponImpact = false, bool heavyImpact = false)
        {
            // Krit und Kettenblitz bleiben gewuerfelt, anders als alles am Raum und am Gegner:
            // sie haengen am einzelnen Schlag, und den gibt es nur bei dem Spieler, der ihn fuehrt.
            // Ein Salz aus (Seed, Etage) gaebe es dafuer nicht - die Zahl der Schlaege ist bei
            // jedem Spieler eine andere. Im Netzwerk wird spaeter das Ergebnis verschickt.
            var critical = Random.value < build.CritChance;
            var amount = damage * (critical ? build.CritMultiplier : 1f);
            if (flash) Explosion(point, radius, PrototypeVfx.ElementColor(type));
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
                landedHits++;
                ApplyStatus(target, type, dealt);
                if (build.Has(PerkId.Vampirism)) health.Heal(dealt * 0.04f);
                if (build.HasSiphonStone) health.Heal(dealt * 0.03f);
                if (weaponImpact) health.Heal(dealt * ActionBalance.MeleeLifesteal(heroClass));
                if (weaponImpact)
                {
                    // Auf Brusthoehe zwischen Klinge und Gegner, nicht in seinem Mittelpunkt: dort
                    // beruehrt die Waffe ihn.
                    var contact = Vector3.Lerp(point, target.transform.position, 0.7f) + Vector3.up * 1.05f;
                    var along = target.transform.position - transform.position;
                    PrototypeVfx.SpawnWeaponImpact(contact, along, PrototypeVfx.ElementColor(type), heavyImpact);
                }
                NotifyDamageDealt(dealt);
            }
            // Ein schwerer Treffer bleibt kurz haengen, sonst laeuft der Schlag durch den Gegner
            // hindurch. Einmal je Schlag, nicht je getroffenem Gegner.
            //
            // Nur schwere Hiebe - der dritte im Kombo, Schmettern, Wirbel. Die Starre haelt die ganze
            // Welt an, auch die eigene Figur unter dem Daumen, und bei XIRO kam sie mit jedem der
            // drei Hiebe: allein 12 % der Zeit Zeitlupe, 2,8 Stopps je Sekunde. Leichte Hiebe haben
            // Klingenspur, Einschlag und Klang; das Gewicht gehoert dem Schlag, der das Kombo
            // beendet. Nachgerechnet sinkt XIRO damit auf 8 % und 1,3 Stopps, BRAX von 10 auf 9 %.
            // Und weil die Sperre nach einem Stopp nicht mehr von leichten Hieben belegt ist,
            // bekommen Finisher und Krits ihre Starre jetzt zuverlaessig.
            if (weaponImpact && heavyImpact && strikeTargets.Count > 0)
                LocalHitstop(0.05f, 0.07f);
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
            LocalShake(0.05f);
        }

        private IEnumerator DelayedBlast(Vector3 point, float radius, float damage, DamageType type, float delay)
        {
            // Erst die Vorwarnung am Boden, dann der Einschlag.
            PrototypeVfx.SpawnShockwave(point, radius * 0.55f, HeroCatalog.Accent(heroClass));
            yield return new WaitForSeconds(delay);
            Strike(point, radius, damage, type);
            PrototypeVfx.SpawnShockwave(point, radius + 0.4f, HeroCatalog.Accent(heroClass));
            LocalShake(0.08f);
        }

        private void Advance(Vector3 direction, float distance)
        {
            // Ueber den CharacterController, damit die Waende der Raeume den Anlauf stoppen. Ein
            // Sturmangriff ist ausdruecklich schneller als Laufen - er laeuft deshalb nicht ueber
            // den gedeckelten Schritt, sondern ueber einen eigenen Weg, der sich als Sturm meldet.
            if (controller) controller.Charge(direction, distance);
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
                Projectile.Spawn(ShotOrigin(direction), Quaternion.Euler(0f, spread, 0f) * direction, new Projectile.Payload
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
            PrototypeVfx.SpawnMuzzle(ShotOrigin(aimDirection), aimDirection);
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

        /// <summary>Geschwindigkeit der Geschosse. Muss zu Projectile.Spawn passen.</summary>
        private const float ProjectileSpeed = 22f;

        /// <summary>Die in diesem Bild entschiedene Richtung. Siehe ResolveAim.</summary>
        private Vector3 AcquireAttackDirection() => aimDirection;

        // ── Zielrichtung ────────────────────────────────────────────────────

        /// <summary>Wie weit das Selbstzielen sucht.</summary>
        private const float AutoAimRange = 18f;

        /// <summary>
        /// Gewicht der Blickrichtung beim Selbstzielen. Klein, damit Rueckwaertslaufen den Gegner
        /// hinter einem trifft; 180 Grad Abweichung kosten so nur gut zwei Einheiten.
        /// </summary>
        private const float AutoAimFacingWeight = 0.012f;

        /// <summary>Um so viel muss ein neues Ziel beim Selbstzielen besser sein, damit gewechselt wird.</summary>
        private const float AutoSwitchMargin = 2f;

        /// <summary>Um so viele Grad naeher an der Stickrichtung muss ein neues Ziel beim Richten liegen.</summary>
        private const float ManualSwitchDegrees = 6f;

        /// <summary>So lange nach dem letzten Angriff schaut die Figur noch in Schussrichtung.</summary>
        private const float EngageHoldSeconds = 0.4f;

        private Vector3 aimDirection = Vector3.forward;
        private float engagedUntil;

        /// <summary>
        /// So lange haelt eine Faehigkeit die Absicht fest, mit der sie ausgeloest wurde. Deckt die
        /// laengste mehrstufige Faehigkeit ab; endet sie frueher, wird die Sperre sofort geloest.
        /// </summary>
        private const float AbilityAimSeconds = 1.6f;
        private float abilityAimUntil;
        private bool abilityAimAuto;
        private Vector3 abilityAimDirection = Vector3.forward;

        /// <summary>
        /// Die eine Richtung, in die in diesem Bild geschossen wird. Koerper, Zielanzeige und Schuss
        /// lesen alle diesen Wert.
        ///
        /// Vorher gab es drei: der Koerper folgte der Eingabe, die mit eigenen Regeln ein Ziel
        /// suchte, ohne Vorhalten; die Linie folgte der Waffe, die danach noch einmal suchte, mit
        /// anderen Regeln und mit Vorhalten; und der Pfeil startete an einer Muendung, die am
        /// nachhaengenden Koerper sass. Drei Stellen, drei Richtungen - "Linie und Schuss sind nicht
        /// synchron".
        /// </summary>
        public Vector3 AimDirection => aimDirection;

        /// <summary>Soll die Figur gerade zur Schussrichtung schauen statt in Laufrichtung?</summary>
        public bool AimEngaged => Time.time < engagedUntil;

        /// <summary>
        /// Entscheidet Ziel und Richtung fuer dieses Bild. Genau einmal je Bild, am Anfang von Update.
        /// </summary>
        private void ResolveAim()
        {
            var manual = FlatAimDirection();
            var auto = input.AutoAim;
            // Waehrend eine Faehigkeit laeuft, gilt die Absicht vom Ausloesen - es sei denn, der
            // Spieler richtet gerade neu. Rex' Pfeilhagel fragt die Richtung bei jeder Salve ab; ohne
            // das gingen die Salven nach dem Loslassen in Laufrichtung.
            if (Time.time < abilityAimUntil && !input.ManualAim)
            {
                auto = abilityAimAuto;
                if (!auto) manual = abilityAimDirection;
            }
            lockedTarget = ChooseTarget(manual, auto);
            targetIndicator?.SetTarget(lockedTarget);
            aimDirection = lockedTarget ? DirectionTo(lockedTarget) : manual;

            if (input.AttackHeld || input.SkillHeld || input.UltimateHeld || chargingHeavy)
                engagedUntil = Time.time + EngageHoldSeconds;
            // Die Eingabe merkt sich, wohin wirklich geschossen wird. Verliert das Selbstzielen sein
            // Ziel, geht es von dort weiter und nicht von einer alten Stickrichtung.
            if (auto) input.Follow(aimDirection);
        }

        /// <summary>
        /// Waehlt das Ziel - und behaelt das bisherige, solange kein deutlich besseres da ist. Ohne
        /// dieses Gedaechtnis sprang das Ziel zwischen zwei fast gleich guten Gegnern bei jedem Bild
        /// hin und her, und mit ihm die Linie.
        /// </summary>
        private Health ChooseTarget(Vector3 manual, bool auto)
        {
            var best = auto
                ? Targeting.FindBestAutoAim(transform.position, aimDirection, AutoAimRange, TeamId.Enemy,
                    AutoAimFacingWeight)
                : Targeting.FindAimAssistTarget(transform.position, manual, AimAssistRange, AimAssistAngle,
                    TeamId.Enemy);
            var current = lockedTarget;
            if (!current || !StillLockable(current, manual, auto)) return best;
            if (!best || best == current) return current;
            if (auto)
            {
                var currentScore = Targeting.AutoAimScore(transform.position, aimDirection, current, AutoAimFacingWeight);
                var bestScore = Targeting.AutoAimScore(transform.position, aimDirection, best, AutoAimFacingWeight);
                return bestScore + AutoSwitchMargin < currentScore ? best : current;
            }
            return AngleTo(best, manual) + ManualSwitchDegrees < AngleTo(current, manual) ? best : current;
        }

        private bool StillLockable(Health target, Vector3 manual, bool auto)
        {
            if (!Targeting.IsTargetable(target, TeamId.Enemy)) return false;
            var distance = FlatOffset(target).magnitude;
            if (auto) return distance <= AutoAimRange + 1.5f;
            return distance <= AimAssistRange + 1f && AngleTo(target, manual) <= AimAssistAngle + ManualSwitchDegrees;
        }

        private Vector3 FlatOffset(Health target)
        {
            var offset = target.transform.position - transform.position;
            offset.y = 0f;
            return offset;
        }

        private float AngleTo(Health target, Vector3 direction) => Vector3.Angle(direction, FlatOffset(target));

        /// <summary>Richtung auf ein Ziel - fuer Geschosse mit Vorhalten, fuer Hiebe auf die heutige Stelle.</summary>
        private Vector3 DirectionTo(Health target)
        {
            var aimAt = target.transform.position;
            if (heroClass is HeroClassId.Ranger or HeroClassId.Arcanist)
            {
                var agent = target.GetComponent<EnemyAgent>();
                if (agent) aimAt = Targeting.PredictIntercept(ShotOrigin(FlatOffset(target)), aimAt,
                    agent.Velocity, ProjectileSpeed);
            }
            var direction = aimAt - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.05f ? direction.normalized : aimDirection;
        }

        /// <summary>Haelt die Absicht einer gerade ausgeloesten Faehigkeit fest und dreht die Figur dorthin.</summary>
        /// <summary>
        /// Haelt die Absicht fest, mit der eine Faehigkeit ausgeloest wurde. Eine gepufferte
        /// Faehigkeit bringt ihre eigene mit - die aus dem Moment des Loslassens, nicht die von
        /// jetzt. Sonst ginge eine im Rueckzug losgelassene Welle beim Ausloesen nach hinten.
        /// </summary>
        private void BeginAbilityAim(bool? auto = null, Vector3? direction = null)
        {
            abilityAimUntil = Time.time + AbilityAimSeconds;
            abilityAimAuto = auto ?? input.AutoAim;
            abilityAimDirection = direction ?? aimDirection;
            // Nur ein Ziel, das das Selbstzielen gerade erfasst hat, geht der Richtung vom Loslassen
            // vor - dafuer ist es da. Ohne Ziel gilt, was beim Loslassen gemeint war.
            if (!(abilityAimAuto && lockedTarget)) aimDirection = abilityAimDirection;
            FaceNow(aimDirection);
        }

        /// <summary>Dreht die Figur ohne Verzoegerung in Schussrichtung - im Moment des Schusses.</summary>
        private void FaceNow(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            engagedUntil = Time.time + EngageHoldSeconds;
        }

        /// <summary>
        /// Wo ein Geschoss startet: in Hoehe und Abstand der Muendung, aber auf der Schussrichtung
        /// statt auf der Blickrichtung. So beginnt der Pfeil auf der angezeigten Linie, auch wenn die
        /// Muendung seitlich an der Figur sitzt.
        /// </summary>
        private Vector3 ShotOrigin(Vector3 direction)
        {
            var height = 1.05f;
            var reach = 0.8f;
            if (muzzle)
            {
                var local = muzzle.position - transform.position;
                height = local.y;
                reach = new Vector3(local.x, 0f, local.z).magnitude;
            }
            direction.y = 0f;
            var flat = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            return transform.position + Vector3.up * height + flat * reach;
        }

        private DamageType ResolveDamageType(DamageType fallback)
        {
            if (build.Has(PerkId.FireBullet)) return DamageType.Fire;
            if (build.Has(PerkId.IceBullet)) return DamageType.Ice;
            if (build.Has(PerkId.LightningBullet)) return DamageType.Lightning;
            if (build.Has(PerkId.PoisonBullet)) return DamageType.Poison;
            return fallback;
        }

        /// <summary>
        /// Meldet den Zustand des schweren Angriffs ans HUD - aber nur fuer den eigenen Helden.
        /// Das Ereignis ist global und traegt keinen Absender. Bis hierher meldeten auch die
        /// Begleiter, und der eigene Knopf lud und loeste im Takt ihrer Schlaege aus.
        /// </summary>
        private void PublishHeavyState()
        {
            if (!isLocal) return;
            GameEvents.RaiseHeavyAttackChanged(HeavyMeterNormalized, HeavyChargeNormalized, chargingHeavy,
                HeavyTimingNow);
        }
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

    /// <summary>
    /// Wie weit ein Nahkampfschlag den Helden auf sein Ziel zutraegt.
    ///
    /// Der Schritt schliesst nur die Luecke bis auf Schlagweite - nie weiter. Zoege er den Helden
    /// bis auf den Gegner, stuenden beide ineinander; liefe er unbegrenzt, waere er ein Sprung
    /// quer durch den Raum. Beide Grenzen stehen hier und werden in SwingTimingTests nachgerechnet.
    /// </summary>
    public static class MeleeApproach
    {
        /// <summary>
        /// Abstand, auf den der Schritt heranfuehrt, wenn nichts ueber den Helden bekannt ist.
        /// </summary>
        public const float IdealGap = 1.5f;

        /// <summary>
        /// Anteil der eigenen Reichweite, auf den herangetreten wird.
        ///
        /// Knapp innerhalb, nicht an der Grenze: wer genau am Rand seiner Reichweite stehen bleibt,
        /// verfehlt beim ersten Schritt des Gegners.
        /// </summary>
        public const float EngageShare = 0.7f;

        /// <summary>
        /// Wie nah dieser Held herangeht, bevor er nicht mehr nachsetzt.
        ///
        /// Stand fuer alle auf 1,5. Solange XIRO 2,60 weit reichte, hiess das "bis knapp in die
        /// Reichweite". Mit 3,40 Reichweite hiess dieselbe Zahl "renne bis tief unter deine eigene
        /// Klinge" - er trat bei jedem Hieb heran, obwohl er laengst traf. Genau das sah aus wie ein
        /// Dash auf den Gegner zu.
        /// </summary>
        public static float IdealGapFor(HeroClassId hero)
        {
            var reach = ActionBalance.MeleeReach(hero);
            return reach <= 0f ? IdealGap : Mathf.Max(1.2f, reach * EngageShare);
        }

        /// <summary>Obergrenze eines einzelnen Schritts.</summary>
        public const float MaxStep = 1.7f;

        /// <summary>
        /// Weiter entfernte Ziele werden nicht angegangen. Die Zielhilfe erfasst bis 15 Einheiten
        /// weit; ein Schritt von 1,7 bringt dort nichts, sieht aber aus wie ein Sprung nach vorn.
        /// Genau das war zu sehen: bei manchen normalen Hieben schoss der Held auf sein Ziel zu.
        /// </summary>
        public const float MaxEngage = 4.2f;

        /// <summary>
        /// Hoechstes Tempo des Schritts, wenn das Lauftempo des Helden nicht bekannt ist.
        ///
        /// Stand als einzige Zahl fuer alle auf 6,5 - und war damit fuer XIRO (4,9 Lauftempo) um ein
        /// Drittel schneller als Laufen. Ein Schritt, der schneller ist als Rennen, ist ein Dash,
        /// egal wie kurz er ist. Der Schritt ist eine Gewichtsverlagerung und darf das Lauftempo des
        /// Helden nie ueberschreiten.
        /// </summary>
        public const float MaxStepSpeed = 6.5f;

        /// <summary>Anteil des vollen Lauftempos, waehrend ein Schlag laeuft.</summary>
        public const float BoundSpeed = 0.3f;

        /// <summary>Wie lange die Bindung ueber den Treffer hinaus haelt.</summary>
        public const float HoldAfterStrike = 0.08f;

        /// <summary>Obergrenze der Bindung, damit zwischen zwei Hieben Platz bleibt.</summary>
        public const float MaxBoundSeconds = 0.3f;

        /// <summary>
        /// Hoechster Anteil der Zeit bis zum naechsten Hieb, den die Bindung belegen darf.
        ///
        /// Vorher war die Bindung eine absolute Zahl: Ausholzeit plus 0,08 s, gedeckelt auf 0,3 s.
        /// Damit war ein Held mit kurzen Pausen zwischen den Hieben anteilig laenger festgenagelt
        /// als einer mit langen. Gemessen: BRAX 51 % der Zeit gebunden, XIRO 70 % - und XIRO ist
        /// ausserdem der langsamste Held im Spiel. Er kam auf 2,51 Einheiten je Sekunde, ein Crawler
        /// laeuft 3,50. Wer angreift, konnte seinem Ziel nicht folgen.
        ///
        /// Als Anteil gerechnet trifft die Bindung alle gleich: wer schneller schlaegt, ist kuerzer
        /// gebunden.
        /// </summary>
        public const float MaxBoundShare = 0.55f;

        /// <summary>
        /// Wie lange ein Hieb den Helden bindet. <paramref name="gapToNextSwing"/> ist die Zeit bis
        /// zum naechsten moeglichen Hieb.
        /// </summary>
        public static float BoundSeconds(float windup, float gapToNextSwing)
            => Mathf.Min(Mathf.Min(windup + HoldAfterStrike, MaxBoundSeconds),
                Mathf.Max(0.05f, gapToNextSwing) * MaxBoundShare);

        public static float StepDistance(float distanceToTarget)
            => distanceToTarget > MaxEngage ? 0f : Mathf.Clamp(distanceToTarget - IdealGap, 0f, MaxStep);

        /// <summary>
        /// Derselbe Schritt, zusaetzlich vom Tempo begrenzt: was in <paramref name="seconds"/> nicht
        /// als Verlagerung durchgeht, wird gekuerzt.
        /// </summary>
        public static float StepDistance(float distanceToTarget, float seconds)
            => Mathf.Min(StepDistance(distanceToTarget), MaxStepSpeed * Mathf.Max(0.01f, seconds));

        /// <summary>
        /// Der Schritt, wie ihn das Spiel wirklich nimmt: er haelt an der Reichweite dieses Helden an
        /// und ist nie schneller als sein Laufen.
        /// </summary>
        /// <summary>
        /// Wie weit ein erzwungener Schritt in diesem Bild gehen darf.
        ///
        /// Der Selbsttest hat es gemessen: XIRO kam beim Angreifen auf bis zu 8,0 Einheiten je
        /// Sekunde, bei 4,9 Lauftempo. Keiner der Schritte war fuer sich zu schnell - der Schritt ins
        /// Ziel lief im Mittel mit Lauftempo, die Rucke am Kombo-Ende waren kurz. Aber der Schritt ins
        /// Ziel wird weich beschleunigt und ist in der Mitte anderthalbmal so schnell wie im Mittel,
        /// er kam zum Laufen noch dazu, und die Rucke fielen auf einen Schlag in ein Bild. Das Auge
        /// sieht die Summe.
        ///
        /// Also rechnet der Schritt mit dem, was das Laufen in diesem Bild schon verbraucht.
        /// </summary>
        public static float StepBudget(float wanted, float runSpeed, float walkingSpeed, float deltaTime)
            => Mathf.Clamp(wanted, 0f, Mathf.Max(0f, runSpeed - walkingSpeed) * Mathf.Max(0f, deltaTime));

        public static float StepDistance(float distanceToTarget, float seconds, HeroClassId hero, float runSpeed)
        {
            if (distanceToTarget > MaxEngage) return 0f;
            var wanted = Mathf.Clamp(distanceToTarget - IdealGapFor(hero), 0f, MaxStep);
            return Mathf.Min(wanted, Mathf.Max(0.5f, runSpeed) * Mathf.Max(0.01f, seconds));
        }
    }
}
