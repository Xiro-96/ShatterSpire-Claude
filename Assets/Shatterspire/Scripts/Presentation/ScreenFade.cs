using UnityEngine;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>
    /// Schwarze Blende ueber dem ganzen Bild. Gebraucht wird sie fuer die Aufzugsfahrt: der Schnitt
    /// von einer Etage zur naechsten darf nicht als Schnitt zu sehen sein, sonst zerfaellt der
    /// Aufstieg in einzelne Raeume statt sich als ein Turm zu lesen.
    ///
    /// Laeuft ueber unskalierte Zeit, damit die Blende auch waehrend einer angehaltenen Wahl arbeitet.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private Image cover;
        private float target;
        private float speed = 2.5f;

        /// <summary>Die Blende deckt gerade vollstaendig ab.</summary>
        public bool Opaque => cover && cover.color.a >= 0.995f;

        /// <summary>Die Blende ist vollstaendig offen.</summary>
        public bool Clear => !cover || cover.color.a <= 0.005f;

        public static ScreenFade Attach(Transform canvas)
        {
            var go = new GameObject("Screen Fade", typeof(RectTransform), typeof(Image), typeof(ScreenFade));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var fade = go.GetComponent<ScreenFade>();
            fade.cover = go.GetComponent<Image>();
            fade.cover.color = new Color(0f, 0f, 0f, 0f);
            // Nicht anfassbar: die Blende darf keine Tipps auf Knoepfe abfangen.
            fade.cover.raycastTarget = false;
            return fade;
        }

        /// <summary>Blende auf einen Zielwert fahren. 1 ist ganz schwarz.</summary>
        public void To(float value, float seconds = 0.4f)
        {
            target = Mathf.Clamp01(value);
            speed = 1f / Mathf.Max(0.05f, seconds);
        }

        /// <summary>Sofort setzen, ohne Fahrt.</summary>
        public void Set(float value)
        {
            target = Mathf.Clamp01(value);
            if (!cover) return;
            var color = cover.color;
            color.a = target;
            cover.color = color;
        }

        private void Update()
        {
            if (!cover) return;
            var color = cover.color;
            if (Mathf.Approximately(color.a, target)) return;
            color.a = Mathf.MoveTowards(color.a, target, speed * Time.unscaledDeltaTime);
            cover.color = color;
        }
    }
}
