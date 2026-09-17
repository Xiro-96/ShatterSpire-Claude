using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputRouter), typeof(PlayerBuild))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float RollSpeed = 16f;
        private const float RollDuration = 0.22f;
        private const float ChargeCooldown = 2.4f;
        private const float ArenaRadius = 14.75f;
        // Antritt kuerzer als Auslauf: die Steuerung bleibt direkt, der Stopp
        // bekommt Gewicht. Vorher ging roher Input direkt in die Geschwindigkeit,
        // also Vollgas aus dem Stand und Vollstopp beim Loslassen.
        private const float AccelerationSeconds = 0.075f;
        private const float BrakingSeconds = 0.125f;
        private CharacterController motor;
        private PlayerInputRouter input;
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
            input = GetComponent<PlayerInputRouter>();
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
            if (velocity.sqrMagnitude > 0f) motor.Move(velocity * Time.deltaTime);
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
                if (wanted > moved) motor.Move(direction * (wanted - moved));
                moved = wanted;
                yield return null;
            }
            lunge = null;
        }

        public void CombatStep(Vector3 direction, float distance)
        {
            if (!motor || rolling || distance <= 0f) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            motor.Move(direction.normalized * distance);
        }

        private IEnumerator Roll()
        {
            rolling = true;
            dashCharges--;
            if (dashCharges == MaxCharges - 1) nextRecharge = Time.time + RechargeSeconds;
            rollDirection = new Vector3(input.Move.x, 0f, input.Move.y);
            if (rollDirection.sqrMagnitude < 0.1f) rollDirection = transform.forward;
            rollDirection.Normalize();
            GetComponent<StylizedCharacterMotion>()?.PulseDash(transform.InverseTransformDirection(rollDirection));
            var dashStart = transform.position;
            var weapon = GetComponent<WeaponSystem>();
            weapon?.OnDashStarted(dashStart, rollDirection);
            health.SetInvulnerable(RollDuration + 0.05f);
            PrototypeVfx.SpawnTrail(transform.position, new Color(0.2f, 0.9f, 1f));
            var elapsed = 0f;
            while (elapsed < RollDuration)
            {
                motor.Move(rollDirection * (RollSpeed * Time.deltaTime));
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
            var wasEnabled = motor.enabled;
            motor.enabled = false;
            transform.position = corrected;
            motor.enabled = wasEnabled;
        }
    }
}
