using UnityEngine;

namespace Shatterspire
{
    public sealed class CameraController : MonoBehaviour
    {
        private static CameraController instance;
        private Transform target;
        private Vector3 velocity;
        private Vector3 lookVelocity;
        private Vector3 previousTargetPosition;
        private Vector3 lookAhead;
        private Vector3 offset = new(0f, 11.4f, -10.2f);
        private float shake;
        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }
        public static void Impulse(float strength) { if (instance) instance.shake = Mathf.Max(instance.shake, strength); }
        public void Configure(Transform value)
        {
            target = value;
            if (target) previousTargetPosition = target.position;
        }
        private void LateUpdate()
        {
            if (!target) return;
            var targetVelocity = Time.deltaTime > 0f
                ? (target.position - previousTargetPosition) / Time.deltaTime : Vector3.zero;
            previousTargetPosition = target.position;
            targetVelocity.y = 0f;
            var desiredLookAhead = Vector3.ClampMagnitude(targetVelocity * 0.13f, 1.05f);
            lookAhead = Vector3.SmoothDamp(lookAhead, desiredLookAhead, ref lookVelocity, 0.2f);
            var jitter = Random.insideUnitSphere * shake;
            jitter.y *= 0.25f;
            transform.position = Vector3.SmoothDamp(transform.position,
                target.position + offset + lookAhead, ref velocity, 0.12f) + jitter;
            transform.rotation = Quaternion.Euler(51.5f, 0f, 0f);
            shake = Mathf.MoveTowards(shake, 0f, 2.8f * Time.unscaledDeltaTime);
        }
    }
}
