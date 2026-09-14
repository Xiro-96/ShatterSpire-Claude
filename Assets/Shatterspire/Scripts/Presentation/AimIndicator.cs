using UnityEngine;

namespace Shatterspire
{
    /// <summary>Welche Form eine Aktion auf dem Boden hat.</summary>
    public enum AimShape
    {
        /// <summary>Nichts anzeigen - etwa bei einer Aktion, die um den Helden selbst wirkt.</summary>
        None,
        /// <summary>Eine Bahn nach vorn: Schuesse, Pfeile, Ladungen.</summary>
        Line,
        /// <summary>Ein breites Band nach vorn: Aschewelle, Wirbel, Sturm.</summary>
        Wedge,
        /// <summary>Ein Kreis an einer Stelle im Raum: Richturteil, Schmiedesturz, Zeitriss.</summary>
        Circle,
        /// <summary>Ein Kreis um den Helden selbst.</summary>
        Around
    }

    /// <summary>Was eine Aktion trifft, in Zahlen - Grundlage fuer die Anzeige.</summary>
    public readonly struct AimDescription
    {
        public readonly AimShape Shape;
        /// <summary>Reichweite nach vorn, oder Abstand der Kreismitte.</summary>
        public readonly float Range;
        /// <summary>Halbe Breite der Bahn, oder Radius des Kreises.</summary>
        public readonly float Width;

        public AimDescription(AimShape shape, float range, float width)
        {
            Shape = shape;
            Range = range;
            Width = width;
        }

        public static readonly AimDescription Nothing = new(AimShape.None, 0f, 0f);
    }

    /// <summary>
    /// Die Zielanzeige auf dem Boden: eine Bahn, ein Band oder ein Kreis, solange gezielt wird.
    ///
    /// Der Anlass: "man sollte den Schuss separat steuern koennen, so wie in Brawl Stars". Dort ist
    /// die Steuerung nicht deshalb gut, weil der Stick anders rechnet - sie ist gut, weil man
    /// waehrend des Zielens **sieht**, wohin es geht, und erst beim Loslassen ausloest. Ohne Anzeige
    /// ist jedes Zielen ein Versuch: die Reichweite einer Faehigkeit stand bisher nur im Code.
    ///
    /// Die Formen kommen aus derselben Quelle wie die Wirkung (WeaponSystem.DescribeAim), damit die
    /// Anzeige nicht behaupten kann, was die Aktion nicht tut.
    /// </summary>
    public sealed class AimIndicator : MonoBehaviour
    {
        private Transform band;
        private Transform head;
        private Transform ring;
        private Transform ownerRing;
        private Color accent;
        private bool visible;

        public static AimIndicator Attach(Transform owner, Color color)
        {
            var go = new GameObject("Aim Indicator");
            go.transform.SetParent(owner, false);
            go.transform.localPosition = Vector3.zero;
            var indicator = go.AddComponent<AimIndicator>();
            indicator.accent = color;
            indicator.Build(go.transform);
            indicator.Hide();
            return indicator;
        }

        private void Build(Transform root)
        {
            var fill = accent;
            fill.a = 0.3f;
            var edge = Color.Lerp(accent, Color.white, 0.4f);
            edge.a = 0.65f;

            band = Quad(root, "Aim Band", fill);
            head = Quad(root, "Aim Head", edge);
            ring = Ring(root, "Aim Ring", edge);
            ownerRing = Ring(root, "Aim Owner Ring", fill);
        }

        private static Transform Quad(Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Flach auf den Boden gelegt, ein Stueck ueber ihm, damit nichts durchblitzt.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(color);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go.transform;
        }

        private static Transform Ring(Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(color, 0.76f);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go.transform;
        }

        /// <summary>
        /// Zeigt die Anzeige fuer eine Aktion. <paramref name="direction"/> ist die Zielrichtung in
        /// der Welt, <paramref name="target"/> die Stelle, an der ein Kreis liegt.
        /// </summary>
        public void Show(AimDescription aim, Vector3 direction, Vector3 target)
        {
            if (aim.Shape == AimShape.None)
            {
                Hide();
                return;
            }
            visible = true;
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 0.0001f) flat = transform.parent ? transform.parent.forward : Vector3.forward;
            flat.Normalize();
            var yaw = Quaternion.LookRotation(flat, Vector3.up).eulerAngles.y;

            var showBand = aim.Shape is AimShape.Line or AimShape.Wedge;
            var showCircle = aim.Shape is AimShape.Circle or AimShape.Around;

            if (band) band.gameObject.SetActive(showBand);
            if (head) head.gameObject.SetActive(showBand);
            if (ring) ring.gameObject.SetActive(showCircle);
            if (ownerRing) ownerRing.gameObject.SetActive(true);

            if (showBand)
            {
                var width = Mathf.Max(0.35f, aim.Width) * 2f;
                band.localRotation = Quaternion.Euler(90f, yaw, 0f);
                // Die Bahn beginnt an der Figur und reicht bis zur Reichweite; der Mittelpunkt liegt
                // deshalb auf halber Strecke.
                band.localPosition = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.04f, aim.Range * 0.5f);
                band.localScale = new Vector3(width, aim.Range, 1f);

                head.localRotation = Quaternion.Euler(90f, yaw, 0f);
                head.localPosition = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.05f, aim.Range);
                head.localScale = new Vector3(width * 1.15f, Mathf.Min(1.2f, aim.Range * 0.18f), 1f);
            }

            if (showCircle)
            {
                var centre = aim.Shape == AimShape.Around
                    ? transform.position
                    : target;
                ring.position = new Vector3(centre.x, transform.position.y + 0.05f, centre.z);
                ring.rotation = Quaternion.Euler(90f, 0f, 0f);
                ring.localScale = Vector3.one * Mathf.Max(0.5f, aim.Width) * 2f;
            }

            // Ein kleiner Ring um die Figur: er sagt, dass gerade gezielt wird, auch wenn die Bahn
            // gerade hinter einer Wand endet.
            ownerRing.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ownerRing.localPosition = new Vector3(0f, 0.04f, 0f);
            ownerRing.localScale = Vector3.one * 1.9f;
        }

        public void Hide()
        {
            if (!visible && band && !band.gameObject.activeSelf) return;
            visible = false;
            if (band) band.gameObject.SetActive(false);
            if (head) head.gameObject.SetActive(false);
            if (ring) ring.gameObject.SetActive(false);
            if (ownerRing) ownerRing.gameObject.SetActive(false);
        }
    }
}
