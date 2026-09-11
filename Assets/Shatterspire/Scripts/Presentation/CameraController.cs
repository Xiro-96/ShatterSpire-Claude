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
        private float arenaHalfExtent = 15f;
        private Camera view;
        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }
        public static void Impulse(float strength) { if (instance) instance.shake = Mathf.Max(instance.shake, strength); }
        /// <param name="halfExtent">Halbe Kantenlaenge der begehbaren Flaeche. Die Kamera
        /// zeigt nie darueber hinaus. Echte Raeume aus Etage 3 geben ihre eigenen Grenzen mit.</param>
        public void Configure(Transform value, float halfExtent = 15f)
        {
            target = value;
            arenaHalfExtent = Mathf.Max(1f, halfExtent);
            view = GetComponent<Camera>();
            if (target) previousTargetPosition = target.position;
        }
        /// <summary>
        /// Der Spieler startet jede Etage bei z = -11, nahe am Rand. Ohne Begrenzung sah die
        /// Kamera rund 5 Einheiten ueber die Kante hinaus - das dunkle Viertel unten im Bild.
        /// Am Rand steht der Spieler dafuer nicht mehr mittig, so wie in jedem Top-Down-Spiel
        /// mit Raumgrenzen.
        /// </summary>
        private Vector3 ClampToArena(Vector3 focus)
        {
            if (!view || !view.orthographic) return focus;
            const float pitch = 51.5f;
            var halfDepth = view.orthographicSize / Mathf.Sin(pitch * Mathf.Deg2Rad);
            var halfWidth = view.orthographicSize * view.aspect;
            // Der Bildmittelpunkt trifft den Boden nicht unter dem Fokus, sondern um diesen
            // Betrag versetzt - abhaengig von Hoehe und Rueckversatz der Kamera.
            var centerShift = offset.z + offset.y / Mathf.Tan(pitch * Mathf.Deg2Rad);
            focus.z = ClampAxis(focus.z, arenaHalfExtent - halfDepth, -centerShift);
            focus.x = ClampAxis(focus.x, arenaHalfExtent - halfWidth, 0f);
            return focus;
        }

        private static float ClampAxis(float value, float room, float shift)
        {
            // Ist die Arena schmaler als das Bild, einfach mittig bleiben.
            if (room <= 0f) return shift;
            return Mathf.Clamp(value, -room + shift, room + shift);
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
            var focus = ClampToArena(target.position + lookAhead);
            transform.position = Vector3.SmoothDamp(transform.position,
                focus + offset, ref velocity, 0.12f) + jitter;
            transform.rotation = Quaternion.Euler(51.5f, 0f, 0f);
            shake = Mathf.MoveTowards(shake, 0f, 2.8f * Time.unscaledDeltaTime);
        }
    }
}
