using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Wodurch sich ein Held bewegt hat. Der Selbsttest ordnet damit zu schnelle Schritte zu.</summary>
    public enum MoveSource { Walk, Step, Charge, Roll, Clamp, Push }

    [RequireComponent(typeof(CharacterController), typeof(PlayerBuild))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float RollSpeed = 16f;
        private const float RollDuration = 0.22f;

        /// <summary>So lange ist ein Held nach Beginn der Rolle unverwundbar.</summary>
        public const float RollInvulnerableSeconds = RollDuration + 0.05f;
        private const float ChargeCooldown = 2.4f;
        private const float ArenaRadius = 14.75f;
        // Antritt kuerzer als Auslauf: die Steuerung bleibt direkt, der Stopp
        // bekommt Gewicht. Vorher ging roher Input direkt in die Geschwindigkeit,
        // also Vollgas aus dem Stand und Vollstopp beim Loslassen.
        private const float AccelerationSeconds = 0.075f;
        private const float BrakingSeconds = 0.125f;
        private CharacterController motor;
        private IPlayerInputSource input;
        private PlayerBuild build;
        private Health health;
        private int dashCharges;
        private float nextRecharge;
        private bool rolling;
        private bool airborne;
        private Vector3 rollDirection;
        private Vector3 velocity;
        private Vector3 acceleration;
        private float boundUntil = -1f;
        private float boundFactor = 1f;
        private Coroutine lunge;
        private FloorNavigation navigation;
        private HeroClassId heroClass;
        private WeaponSystem weaponSystem;
        public int DashCharges => dashCharges;

        /// <summary>Rollt der Held gerade? Der Selbsttest nimmt Dash-Tempo von der Schritt-Pruefung aus.</summary>
        public bool Rolling => rolling;

        /// <summary>Fliegt er gerade - etwa im Sprung einer Ultimate?</summary>
        public bool IsAirborne => airborne;
        public float DashRechargeNormalized => dashCharges >= MaxCharges ? 1f : 1f - Mathf.Clamp01((nextRecharge - Time.time) / RechargeSeconds);
        public int MaxDashCharges => MaxCharges;
        private int MaxCharges => 2 + build.ExtraDashCharges;
        private float RechargeSeconds => ChargeCooldown / build.DashRechargeMultiplier;

        public void ConfigureClass(HeroClassId value) => heroClass = value;

        public void SetNavigation(FloorNavigation value) => navigation = value;

        /// <summary>Wegenetz der Etage. Der Schmiedesturz braucht es, um im begehbaren Bereich zu landen.</summary>
        public FloorNavigation Navigation => navigation;

        /// <summary>
        /// Setzt den Helden waehrend eines Sprungs frei im Raum. Der Motor ist dabei aus: eine
        /// CharacterController-Bewegung wuerde ihn sofort wieder auf den Boden ziehen.
        /// </summary>
        public void Airborne(Vector3 position)
        {
            airborne = true;
            motor.enabled = false;
            transform.position = position;
        }

        /// <summary>Beendet einen Sprung und setzt den Helden auf den Boden zurueck.</summary>
        public void Land(Vector3 position)
        {
            transform.position = position;
            motor.enabled = true;
            airborne = false;
            velocity = Vector3.zero;
            acceleration = Vector3.zero;
        }

        public void Teleport(Vector3 position)
        {
            // Eine Versetzung beendet immer auch einen Flug - sonst bliebe der Held gesperrt.
            airborne = false;
            var wasEnabled = motor.enabled;
            motor.enabled = false;
            transform.position = position;
            motor.enabled = wasEnabled;
            velocity = Vector3.zero;
            acceleration = Vector3.zero;
        }

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            input = GetComponent<IPlayerInputSource>();
            // Eine Schnittstelle laesst sich nicht mit RequireComponent fordern. Statt einer Flut von
            // Nullverweisen im Spiel soll hier eine Zeile stehen, die den Grund nennt.
            if (input == null)
                Debug.LogError($"SHATTERSPIRE: {name} hat keine Eingabe. Ein Held braucht einen "
                               + "PlayerInputRouter oder ein BotInput, und zwar vor der Steuerung.");
            weaponSystem = GetComponent<WeaponSystem>();
            build = GetComponent<PlayerBuild>();
            health = GetComponent<Health>();
            dashCharges = MaxCharges;
            build.Changed += ClampCharges;
        }

        private void OnDestroy() { if (build != null) build.Changed -= ClampCharges; }
        private void ClampCharges() => dashCharges = Mathf.Min(MaxCharges, dashCharges + 1);

        private void Update()
        {
            RechargeDash();
            if (!health.IsAlive || Time.timeScale <= 0f) return;
            // Im Sprung steuert die Ultimate die Position; Eingaben wuerden sie sonst verreissen.
            if (airborne) return;
            if (input.DashPressed && !rolling && dashCharges > 0) StartCoroutine(Roll());
            if (!rolling) MoveAndAim();
        }

        private void MoveAndAim()
        {
            var stick = Vector3.ClampMagnitude(new Vector3(input.Move.x, 0f, input.Move.y), 1f);
            var topSpeed = HeroCatalog.BaseSpeed(heroClass) * build.MoveSpeedMultiplier;
            // Waehrend eines Nahkampfschlags bleibt der Held weitgehend stehen. Ohne das kostet ein
            // Schlag nichts: man laeuft mit vollem Tempo weiter, waehrend der Oberkoerper schwingt,
            // und der Treffer wirkt folgenlos. Die Bindung endet kurz nach dem Treffer, damit
            // zwischen zwei Hieben noch Platz zum Nachsetzen bleibt.
            if (Time.time < boundUntil) topSpeed *= boundFactor;
            var desired = stick * topSpeed;
            var smoothing = desired.sqrMagnitude > velocity.sqrMagnitude ? AccelerationSeconds : BrakingSeconds;
            velocity = Vector3.SmoothDamp(velocity, desired, ref acceleration, smoothing);
            // SmoothDamp laeuft nur asymptotisch gegen null. Ohne diese Schwelle
            // bliebe ein Rest stehen und die Figur wuerde ewig weiterkriechen.
            if (desired.sqrMagnitude < 0.0001f && velocity.sqrMagnitude < 0.04f)
            {
                velocity = Vector3.zero;
                acceleration = Vector3.zero;
            }
            if (velocity.sqrMagnitude > 0f) MoveBody(velocity * Time.deltaTime, MoveSource.Walk);
            // Im Kampf schaut die Figur dorthin, wohin die Waffe schiesst - dieselbe Richtung, die
            // auch die Linie zeigt. Ausserhalb des Kampfes dorthin, wohin gezielt oder gelaufen wird.
            // Vorher folgte der Koerper immer der Eingabe, und die entschied das Ziel anders als die
            // Waffe: die Figur sah auf einen Gegner und schoss auf einen anderen.
            var engaged = weaponSystem && weaponSystem.AimEngaged;
            var aim = engaged ? weaponSystem.AimDirection : input.AimPoint - transform.position;
            aim.y = 0f;
            if (aim.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim),
                    (engaged ? 45f : 30f) * Time.deltaTime);
        }

        /// <summary>
        /// Bindet den Helden fuer die Dauer eines Schlags an seinen Platz. Der Faktor ist ein Anteil
        /// des vollen Tempos, kein absoluter Wert - ein Relikt auf Lauftempo wirkt weiter.
        /// </summary>
        public void BindDuringAttack(float seconds, float factor)
        {
            if (seconds <= 0f) return;
            boundUntil = Time.time + seconds;
            boundFactor = Mathf.Clamp01(factor);
        }

        /// <summary>
        /// Der Schritt in den Schlag: traegt den Helden waehrend des Ausholens auf sein Ziel zu, damit
        /// der Hieb ankommt statt knapp davor ins Leere zu gehen.
        ///
        /// Anders als CombatStep versetzt er nicht auf einen Schlag, sondern ueber die ganze
        /// Ausholzeit - ein Versatz von anderthalb Einheiten in einem Bild waere ein Blinzeln.
        /// </summary>
        public void Lunge(Vector3 direction, float distance, float seconds)
        {
            if (!motor || rolling || airborne || distance <= 0.02f || seconds <= 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            if (lunge != null) StopCoroutine(lunge);
            lunge = StartCoroutine(LungeRoutine(direction.normalized, distance, seconds));
        }

        private IEnumerator LungeRoutine(Vector3 direction, float distance, float seconds)
        {
            var elapsed = 0f;
            var moved = 0f;
            while (elapsed < seconds && !rolling && !airborne && motor.enabled)
            {
                elapsed += Time.deltaTime;
                // Weich hinein und weich hinaus: der Schritt soll sich als Gewichtsverlagerung
                // lesen, nicht als Ruck.
                var wanted = distance * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
                // Zusammen mit dem Laufen nie schneller als das Lauftempo. Was in diesem Bild keinen
                // Platz hat, holt der Schritt in den naechsten nach - oder laesst es.
                var walking = new Vector3(velocity.x, 0f, velocity.z).magnitude;
                var step = MeleeApproach.StepBudget(wanted - moved, TopSpeed, walking, Time.deltaTime);
                if (step > 0f) MoveBody(direction * step, MoveSource.Step);
                moved += step;
                yield return null;
            }
            lunge = null;
        }

        /// <summary>
        /// Ein kleiner Schritt nach vorn im Schlag. Vorher ein Versatz auf einen Schlag in einem
        /// einzigen Bild - jetzt gleitend und gedeckelt wie der Schritt ins Ziel.
        /// </summary>
        public void CombatStep(Vector3 direction, float distance)
        {
            if (!motor || rolling || distance <= 0f) return;
            Lunge(direction, distance, Mathf.Max(0.06f, distance / Mathf.Max(0.5f, TopSpeed)));
        }

        /// <summary>
        /// Ein Sturmangriff: schneller als Laufen, und das mit Absicht. Nur fuer Faehigkeiten, die ein
        /// Ansturm sind. Meldet sich ueber <see cref="Charging"/>, damit der Selbsttest ihn nicht
        /// fuer einen zu schnellen Schritt haelt.
        /// </summary>
        public void Charge(Vector3 direction, float distance)
        {
            if (!motor || rolling || distance <= 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            chargingUntil = Time.time + 0.12f;
            MoveBody(direction.normalized * distance, MoveSource.Charge);
        }

        private float chargingUntil = -1f;

        /// <summary>Laeuft gerade ein Sturmangriff?</summary>
        public bool Charging => Time.time < chargingUntil;

        /// <summary>Das Lauftempo dieses Helden, mit allem, was es gerade veraendert.</summary>
        private float TopSpeed => HeroCatalog.BaseSpeed(heroClass) * build.MoveSpeedMultiplier;

        private readonly float[] travelled = new float[6];

        /// <summary>Wie weit sich der Held insgesamt auf diesem Weg bewegt hat, in Einheiten.</summary>
        public float Travelled(MoveSource source) => travelled[(int)source];

        /// <summary>
        /// Die einzige Stelle, an der der Held bewegt wird.
        ///
        /// Ein gefallener Held hat seine Bewegung abgeschaltet (FallenHero). Eine Rolle oder ein
        /// Ansturm, die in dem Moment noch liefen, riefen Move trotzdem weiter auf -
        /// "CharacterController.Move called on inactive controller", vom Selbsttest zwoelfmal in
        /// einem Aufstieg gefunden, jedes Mal kurz nachdem ein Begleiter gefallen war.
        /// </summary>
        private void MoveBody(Vector3 delta, MoveSource source)
        {
            if (!motor || !motor.enabled) return;
            var before = transform.position;
            motor.Move(delta);
            var moved = transform.position - before;
            moved.y = 0f;
            // Was die Kollision ueber den angeforderten Weg hinaus verschiebt, ist keine Bewegung des
            // Helden, sondern Verdraengung - etwa wenn ein Gegnerkoerper in ihn hineinlaeuft. Der
            // Selbsttest fand Stoesse von 1,5 Einheiten in 0,13 s, die sonst als Laufen zaehlten.
            var requested = new Vector3(delta.x, 0f, delta.z).magnitude;
            travelled[(int)source] += Mathf.Min(moved.magnitude, requested);
            if (moved.magnitude > requested + 0.001f)
                travelled[(int)MoveSource.Push] += moved.magnitude - requested;
        }

        private IEnumerator Roll()
        {
            rolling = true;
            // Die Bindung eines Hiebs endet mit dem Dash - sonst schleicht der Held nach der Rolle
            // mit einem Drittel seines Tempos weiter, bis der abgebrochene Schlag ausgelaufen waere.
            boundUntil = -1f;
            dashCharges--;
            if (dashCharges == MaxCharges - 1) nextRecharge = Time.time + RechargeSeconds;
            rollDirection = new Vector3(input.Move.x, 0f, input.Move.y);
            if (rollDirection.sqrMagnitude < 0.1f) rollDirection = transform.forward;
            rollDirection.Normalize();
            GetComponent<StylizedCharacterMotion>()?.PulseDash(transform.InverseTransformDirection(rollDirection));
            var dashStart = transform.position;
            var weapon = GetComponent<WeaponSystem>();
            weapon?.OnDashStarted(dashStart, rollDirection);
            health.SetInvulnerable(RollInvulnerableSeconds);
            PrototypeVfx.SpawnTrail(transform.position, new Color(0.2f, 0.9f, 1f));
            var elapsed = 0f;
            while (elapsed < RollDuration)
            {
                MoveBody(rollDirection * (RollSpeed * Time.deltaTime), MoveSource.Roll);
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (build.Has(PerkId.DashExplosion))
                CombatUtility.Explode(transform.position, 3f, 24f * build.DamageMultiplier, TeamId.Enemy, DamageType.Lightning, gameObject);
            weapon?.OnDashEnded(dashStart, transform.position, rollDirection);
            // Mit der Dash-Richtung als Startgeschwindigkeit weiterlaufen, sonst
            // greift SmoothDamp nach dem Roll noch den alten Wert von davor auf
            // und der Uebergang ruckt.
            velocity = rollDirection * (HeroCatalog.BaseSpeed(heroClass) * build.MoveSpeedMultiplier);
            acceleration = Vector3.zero;
            rolling = false;
        }

        private void RechargeDash()
        {
            if (dashCharges >= MaxCharges) return;
            if (Time.time < nextRecharge) return;
            dashCharges++;
            if (dashCharges < MaxCharges) nextRecharge = Time.time + RechargeSeconds;
        }

        private void LateUpdate()
        {
            if (airborne) return;
            // Sicherung fuer Faelle, in denen die Wandkollision nicht greift - etwa beim Bull Rush,
            // der die Position direkt versetzt. Die Etage kennt ihre begehbare Flaeche.
            var position = transform.position;
            Vector3 corrected;
            if (navigation != null)
            {
                corrected = navigation.ClampToWalkable(position, motor.radius * 0.9f);
                corrected.y = position.y;
            }
            else
            {
                var flat = new Vector2(position.x, position.z);
                if (flat.sqrMagnitude <= ArenaRadius * ArenaRadius) return;
                flat = flat.normalized * ArenaRadius;
                corrected = new Vector3(flat.x, position.y, flat.y);
            }
            if ((corrected - position).sqrMagnitude < 0.000001f) return;
            var pushed = corrected - position;
            pushed.y = 0f;
            travelled[(int)MoveSource.Clamp] += pushed.magnitude;
            var wasEnabled = motor.enabled;
            motor.enabled = false;
            transform.position = corrected;
            motor.enabled = wasEnabled;
        }
    }
}
