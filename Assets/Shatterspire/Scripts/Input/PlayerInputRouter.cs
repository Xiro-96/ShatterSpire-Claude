using UnityEngine;

namespace Shatterspire
{
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

        private void Start()
        {
            worldCamera = Camera.main;
            aimPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private void Update()
        {
            var keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Move = Vector2.ClampMagnitude(keyboard + MobileInput.Move, 1f);
            AttackHeld = Input.GetMouseButton(0) || MobileInput.Attack;
            HeavyPressed = Input.GetMouseButtonDown(1) || MobileInput.ConsumeHeavyPressed();
            HeavyReleased = Input.GetMouseButtonUp(1) || MobileInput.ConsumeHeavyReleased();
            HeavyHeld = Input.GetMouseButton(1) || MobileInput.Heavy;
            SkillPressed = Input.GetKeyDown(KeyCode.Q) || MobileInput.ConsumeSkill();
            DashPressed = Input.GetKeyDown(KeyCode.Space) || MobileInput.ConsumeDash();
            UltimatePressed = Input.GetKeyDown(KeyCode.E) || MobileInput.ConsumeUltimate();

            if (worldCamera && !Application.isMobilePlatform)
            {
                var ray = worldCamera.ScreenPointToRay(Input.mousePosition);
                AimPoint = aimPlane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : transform.position + transform.forward * 10f;
            }
            else
            {
                var target = Targeting.FindBestAutoAim(transform.position, transform.forward, 18f, TeamId.Enemy);
                AimPoint = target ? target.transform.position : transform.position + transform.forward * 10f;
            }
        }
    }

    public static class MobileInput
    {
        public static Vector2 Move;
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
            Attack = Heavy = false;
            skill = dash = ultimate = heavyPressed = heavyReleased = false;
        }
    }
}
