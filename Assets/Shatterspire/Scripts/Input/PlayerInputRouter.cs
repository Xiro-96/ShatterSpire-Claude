using UnityEngine;

namespace Shatterspire
{
    /// <summary>Drei Aktionen und Dash wie in R.I.S.E., dazu die Ultimate auf R.</summary>
    public interface IPlayerInputSource
    {
        Vector2 Move { get; }
        Vector3 AimPoint { get; }
        bool AttackHeld { get; }
        bool HeavyHeld { get; }
        bool HeavyPressed { get; }
        bool HeavyReleased { get; }
        bool SkillPressed { get; }
        bool DashPressed { get; }
        bool UltimatePressed { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInputRouter : MonoBehaviour, IPlayerInputSource
    {
        private Camera worldCamera;
        private Plane aimPlane;
        public Vector2 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool HeavyHeld { get; private set; }
        public bool HeavyPressed { get; private set; }
        public bool HeavyReleased { get; private set; }
        public bool SkillPressed { get; private set; }
        public bool DashPressed { get; private set; }
        public bool UltimatePressed { get; private set; }
        /// <summary>Nur fuer automatische Vorfuehrungen (CaptureDemo): haelt den Angriff gedrueckt.</summary>
        public bool ScriptedAttack { get; set; }
        /// <summary>Nur fuer automatische Vorfuehrungen: feste Blickrichtung statt Maus.</summary>
        public Vector3? ScriptedAim { get; set; }
        /// <summary>Nur fuer automatische Vorfuehrungen: feste Laufrichtung statt Tastatur.</summary>
        public Vector2? ScriptedMove { get; set; }

        /// <summary>
        /// Zielhilfe: zieht die selbst gewaehlte Richtung auf ein Ziel, aber nur wenn es dicht daneben
        /// liegt. Volles Selbstzielen hat sich im Handytest falsch angefuehlt - die Figur schoss auf
        /// Gegner, die gerade erschienen, statt dorthin, wohin gezielt wurde.
        /// </summary>
        public static bool AimAssist = true;
        /// <summary>Bis zu diesem Winkel darf die Zielhilfe die Richtung verschieben.</summary>
        private const float AimAssistDegrees = 16f;
        private Vector3 aimDirection = Vector3.forward;

        private void Start()
        {
            worldCamera = Camera.main;
            aimPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Update()
        {
            var keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Move = ScriptedMove ?? Vector2.ClampMagnitude(keyboard + MobileInput.Move, 1f);
            // Auf dem Telefon meldet Unity jede Beruehrung zusaetzlich als linke Maustaste. Wer den Stick
            // hielt, griff dadurch dauernd an. Mit Touch zaehlen nur noch die Aktionsknoepfe.
            var mouse = !Application.isMobilePlatform && Input.touchCount == 0;
            AttackHeld = (mouse && Input.GetMouseButton(0)) || MobileInput.Attack || MobileInput.AimFire || ScriptedAttack;
            HeavyPressed = (mouse && Input.GetMouseButtonDown(1)) || MobileInput.ConsumeHeavyPressed();
            HeavyReleased = (mouse && Input.GetMouseButtonUp(1)) || MobileInput.ConsumeHeavyReleased();
            HeavyHeld = (mouse && Input.GetMouseButton(1)) || MobileInput.Heavy;
            SkillPressed = Input.GetKeyDown(KeyCode.Q) || MobileInput.ConsumeSkill();
            DashPressed = Input.GetKeyDown(KeyCode.Space) || MobileInput.ConsumeDash();
            UltimatePressed = Input.GetKeyDown(KeyCode.R) || MobileInput.ConsumeUltimate();

            if (worldCamera && !Application.isMobilePlatform && Input.touchCount == 0)
            {
                var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
                AimPoint = aimPlane.Raycast(ray, out var distance)
                    ? ray.GetPoint(distance)
                    : transform.position + transform.forward * 10f;
            }
            else
            {
                AimPoint = transform.position + ResolveTouchAim() * 10f;
            }
            if (ScriptedAim.HasValue) AimPoint = transform.position + ScriptedAim.Value;
        }

        /// <summary>
        /// Zielrichtung auf dem Telefon. Der Zielstick hat Vorrang; ohne ihn zeigt der Held dorthin,
        /// wohin er laeuft, und im Stand behaelt er seine letzte Richtung. So ist jederzeit manuell
        /// steuerbar, und die Zielhilfe korrigiert nur noch dicht daneben.
        /// </summary>
        private Vector3 ResolveTouchAim()
        {
            var stick = MobileInput.Aim;
            if (stick.sqrMagnitude > 0.0004f) aimDirection = new Vector3(stick.x, 0f, stick.y).normalized;
            else if (Move.sqrMagnitude > 0.02f) aimDirection = new Vector3(Move.x, 0f, Move.y).normalized;
            if (!AimAssist) return aimDirection;

            var target = Targeting.FindBestAutoAim(transform.position, aimDirection, 18f, TeamId.Enemy);
            if (!target) return aimDirection;
            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.04f) return aimDirection;
            toTarget.Normalize();
            return Vector3.Angle(aimDirection, toTarget) <= AimAssistDegrees ? toTarget : aimDirection;
        }
    }

    public static class MobileInput
    {
        public static Vector2 Move;
        /// <summary>Richtung des Zielsticks, Null wenn niemand zielt.</summary>
        public static Vector2 Aim;
        /// <summary>Der Zielstick ist ausgelenkt und feuert damit mit.</summary>
        public static bool AimFire;
        public static bool Attack;
        public static bool Heavy;
        private static bool skill;
        private static bool dash;
        private static bool ultimate;
        private static bool heavyPressed;
        private static bool heavyReleased;
        public static void SetHeavy(bool value)
        {
            if (value && !Heavy) heavyPressed = true;
            if (!value && Heavy) heavyReleased = true;
            Heavy = value;
        }
        public static void PressSkill() => skill = true;
        public static void PressDash() => dash = true;
        public static void PressUltimate() => ultimate = true;
        public static bool ConsumeSkill() { var value = skill; skill = false; return value; }
        public static bool ConsumeDash() { var value = dash; dash = false; return value; }
        public static bool ConsumeUltimate() { var value = ultimate; ultimate = false; return value; }
        public static bool ConsumeHeavyPressed() { var value = heavyPressed; heavyPressed = false; return value; }
        public static bool ConsumeHeavyReleased() { var value = heavyReleased; heavyReleased = false; return value; }
        public static void Reset()
        {
            Move = Vector2.zero;
            Aim = Vector2.zero;
            AimFire = false;
            Attack = Heavy = false;
            skill = dash = ultimate = heavyPressed = heavyReleased = false;
        }
    }
}
