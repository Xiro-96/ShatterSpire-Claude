using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der Ring am Boden, der zeigt, wie weit der schwere Angriff geladen ist - und wann der
    /// perfekte Moment offen steht.
    ///
    /// Er steht hier und nicht im HUD, weil man im Kampf auf seine Figur schaut und nicht in die
    /// untere rechte Ecke. Vorher lag die einzige Anzeige dort: ein goldener Bogen auf einem zweiten
    /// Ring, 28 px weiter aussen als der Fuellring, den man mit ihm vergleichen musste - und auf dem
    /// Telefon liegt der Daumen genau darauf.
    ///
    /// Zwei Zustaende, nicht mehr: ein Ring, der sich zusammenzieht, und ein Aufblitzen, wenn das
    /// Fenster aufgeht. Ein Verlauf waere genauer und im Gefecht unlesbar.
    /// </summary>
    public sealed class HeavyChargeRing : MonoBehaviour
    {
        /// <summary>Durchmesser am Anfang der Ladung. Weit genug, um neben der Figur zu liegen.</summary>
        private const float StartDiameter = 4.6f;

        /// <summary>Durchmesser, wenn das Fenster aufgeht. Der Ring sitzt dann an den Fuessen.</summary>
        private const float WindowDiameter = 1.9f;

        private static readonly Color Waiting = new(0.72f, 0.82f, 1f, 0.8f);
        private static readonly Color Open = new(1f, 0.84f, 0.16f, 0.95f);

        private Transform ring;
        private Renderer surface;

        public static HeavyChargeRing Attach(Transform owner)
        {
            var host = new GameObject("Heavy Charge Ring");
            host.transform.SetParent(owner, false);
            var component = host.AddComponent<HeavyChargeRing>();
            component.Build();
            return component;
        }

        private void Build()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Charge Decal";
            PrototypeFactory.RemoveCollider(marker.GetComponent<Collider>());
            surface = marker.GetComponent<Renderer>();
            surface.sharedMaterial = PrototypeFactory.CreateRadialDecal(Waiting, 0.74f);
            surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            surface.receiveShadows = false;
            ring = marker.transform;
            ring.SetParent(transform, false);
            ring.localPosition = new Vector3(0f, 0.05f, 0f);
            Hide();
        }

        /// <summary>
        /// Zeigt den Ring fuer diesen Ladestand. <paramref name="open"/> ist wahr, solange der
        /// perfekte Moment laeuft.
        /// </summary>
        public void Show(float charge, bool open)
        {
            if (!ring) return;
            if (!ring.gameObject.activeSelf) ring.gameObject.SetActive(true);
            // Bis zum Fenster zieht sich der Ring zusammen, danach bleibt er stehen: die Bewegung
            // ist die Ankuendigung, der Stillstand ist das Fenster.
            var toWindow = ActionBalance.PerfectStart <= 0f ? 1f
                : Mathf.Clamp01(charge / ActionBalance.PerfectStart);
            var diameter = Mathf.Lerp(StartDiameter, WindowDiameter, toWindow);
            // Im Fenster pulsiert er, damit "jetzt" auch ohne Farbe zu sehen ist - auf einem
            // Telefon im Sonnenlicht ist Gold nicht immer Gold.
            if (open) diameter *= 1f + Mathf.Sin(Time.time * 26f) * 0.06f;
            ring.localScale = new Vector3(diameter, diameter, 1f);
            // Weltlage jedes Bild neu: der Ring haengt am Helden, und dessen Drehung darf ihn nicht
            // mitkippen.
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            if (surface) surface.sharedMaterial = PrototypeFactory.CreateRadialDecal(open ? Open : Waiting, 0.74f);
        }

        public void Hide()
        {
            if (ring && ring.gameObject.activeSelf) ring.gameObject.SetActive(false);
        }
    }
}
