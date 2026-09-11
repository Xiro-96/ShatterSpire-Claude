using UnityEngine;

namespace Shatterspire
{
    public sealed class CameraController : MonoBehaviour
    {
        private const float Pitch = 51.5f;

        private static CameraController instance;
        private Transform target;
        private Vector3 velocity;
        private Vector3 lookVelocity;
        private Vector3 previousTargetPosition;
        private Vector3 lookAhead;
        private Vector3 offset = new(0f, 11.4f, -10.2f);
        private float shake;
        private Area bounds;
        private bool hasBounds;
        private Camera view;

        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }
        public static void Impulse(float strength) { if (instance) instance.shake = Mathf.Max(instance.shake, strength); }

        public void Configure(Transform value)
        {
            target = value;
            view = GetComponent<Camera>();
            if (target) previousTargetPosition = target.position;
        }

        /// <summary>Sichtbereich der aktuellen Etage. Die Kamera zeigt nicht ueber dessen Rand hinaus.</summary>
        public void SetBounds(Area floorBounds)
        {
            bounds = new Area(floorBounds.MinX - 2f, floorBounds.MaxX + 2f, floorBounds.MinZ - 2f, floorBounds.MaxZ + 2f);
            hasBounds = true;
        }

        /// <summary>Springt ohne Nachziehen ans Ziel, etwa nach dem Wechsel auf eine neue Etage.</summary>
        public void Snap()
        {
            if (!target) return;
            lookAhead = Vector3.zero;
            lookVelocity = Vector3.zero;
            velocity = Vector3.zero;
            previousTargetPosition = target.position;
            transform.position = Clamp(target.position) + offset;
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        private Vector3 Clamp(Vector3 focus)
        {
            if (!hasBounds || !view || !view.orthographic) return focus;
            var halfDepth = view.orthographicSize / Mathf.Sin(Pitch * Mathf.Deg2Rad);
            var halfWidth = view.orthographicSize * view.aspect;
            // Der Bildmittelpunkt trifft den Boden nicht unter dem Fokus, sondern um diesen Betrag
            // versetzt - abhaengig von Hoehe und Rueckversatz der Kamera.
            var centerShift = offset.z + offset.y / Mathf.Tan(Pitch * Mathf.Deg2Rad);
            var centerZ = Area.ClampAxis(focus.z + centerShift, bounds.MinZ + halfDepth, bounds.MaxZ - halfDepth);
            focus.z = centerZ - centerShift;
            focus.x = Area.ClampAxis(focus.x, bounds.MinX + halfWidth, bounds.MaxX - halfWidth);
            return focus;
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
            var focus = Clamp(target.position + lookAhead);
            transform.position = Vector3.SmoothDamp(transform.position, focus + offset, ref velocity, 0.12f) + jitter;
            transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            shake = Mathf.MoveTowards(shake, 0f, 2.8f * Time.unscaledDeltaTime);
        }
    }
}
