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
        private Vector3 rollDirection;
        private Vector3 velocity;
        private Vector3 acceleration;
        private FloorNavigation navigation;
        private HeroClassId heroClass;
        public int DashCharges => dashCharges;
        public float DashRechargeNormalized => dashCharges >= MaxCharges ? 1f : 1f - Mathf.Clamp01((nextRecharge - Time.time) / RechargeSeconds);
        public int MaxDashCharges => MaxCharges;
        private int MaxCharges => 2 + build.ExtraDashCharges;
        private float RechargeSeconds => ChargeCooldown / build.DashRechargeMultiplier;

        public void ConfigureClass(HeroClassId value) => heroClass = value;

        public void SetNavigation(FloorNavigation value) => navigation = value;

        public void Teleport(Vector3 position)
        {
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
            if (input.DashPressed && !rolling && dashCharges > 0) StartCoroutine(Roll());
            if (!rolling) MoveAndAim();
        }

        private void MoveAndAim()
        {
            var stick = Vector3.ClampMagnitude(new Vector3(input.Move.x, 0f, input.Move.y), 1f);
            var topSpeed = HeroCatalog.BaseSpeed(heroClass) * build.MoveSpeedMultiplier;
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
            var aim = input.AimPoint - transform.position;
            aim.y = 0f;
            // Schneller drehen als vorher: mit Zielstick soll die Figur der Eingabe folgen und nicht
            // hinterherschwenken. 22 pro Sekunde war bei schnellen Richtungswechseln sichtbar traege.
            if (aim.sqrMagnitude > 0.1f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim), 30f * Time.deltaTime);
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
