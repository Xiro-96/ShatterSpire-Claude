using UnityEngine;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>
    /// Zeigt Gegner am Bildrand an, die gerade zum Angriff ausholen und dabei ausserhalb des Bildes
    /// stehen, und faerbt das Bild rot, wenn es dem Helden schlecht geht.
    ///
    /// Beides ist Lesbarkeit, nicht Schmuck. Auf einem Telefon ist der Blickwinkel eng: ein
    /// Armbrustbolzen von jemandem, den man nie gesehen hat, liest sich als unfair und nicht als
    /// schwer. Und wer nicht merkt, dass er fast tot ist, stirbt ohne die Chance, etwas dagegen zu
    /// tun - deshalb gehoert der letzte Rest Leben auch ins Bild und nicht nur in den Balken.
    /// </summary>
    public sealed class ThreatMarkers : MonoBehaviour
    {
        private const int MaximumMarkers = 6;
        /// <summary>Weiter entfernte Gegner sind keine unmittelbare Gefahr und wuerden nur zumalen.</summary>
        private const float Range = 26f;
        /// <summary>Abstand der Marken vom Bildrand, als Anteil der halben Bildgroesse.</summary>
        private const float EdgeInset = 0.86f;
        /// <summary>Ab diesem Anteil Leben beginnt die rote Faerbung.</summary>
        private const float CriticalHealth = 0.35f;

        private Transform hero;
        private Health heroHealth;
        private Camera view;
        private RectTransform canvasRect;
        private Image[] markers;
        private Image vignette;
        private float nextHeartbeat;
        private int reported;

        public void Configure(Transform player, Camera runCamera, Canvas canvas)
        {
            hero = player;
            heroHealth = player ? player.GetComponent<Health>() : null;
            view = runCamera;
            canvasRect = (RectTransform)canvas.transform;

            // Die rote Faerbung liegt hinter allem anderen, damit sie Knoepfe und Zahlen nicht truebt.
            vignette = Create("Critical Vignette", new Color(0.75f, 0.04f, 0.05f, 0f), new Vector2(0f, 0f));
            var vignetteRect = (RectTransform)vignette.transform;
            vignetteRect.anchorMin = Vector2.zero;
            vignetteRect.anchorMax = Vector2.one;
            vignetteRect.offsetMin = Vector2.zero;
            vignetteRect.offsetMax = Vector2.zero;
            vignette.sprite = UiIconFactory.Disc();
            vignette.transform.SetAsFirstSibling();

            markers = new Image[MaximumMarkers];
            for (var i = 0; i < MaximumMarkers; i++)
            {
                markers[i] = Create("Threat Marker " + i, new Color(1f, 0.32f, 0.12f, 0f), new Vector2(46f, 46f));
                markers[i].sprite = UiIconFactory.Chevron();
                markers[i].gameObject.SetActive(false);
            }
        }

        private Image Create(string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void LateUpdate()
        {
            if (!hero || !view || markers == null) return;
            UpdateMarkers();
            UpdateCritical();
        }

        private void UpdateMarkers()
        {
            var used = 0;
            var telegraphing = 0;
            var inRange = 0;
            var half = canvasRect.rect.size * 0.5f;
            foreach (var agent in EnemyAgent.Active)
            {
                if (used >= markers.Length) break;
                if (!agent || !agent.IsTelegraphing) continue;
                telegraphing++;
                var offset = agent.transform.position - hero.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > Range * Range) continue;
                inRange++;

                var viewport = view.WorldToViewportPoint(agent.transform.position + Vector3.up);
                // Im Bild? Dann sieht man den Gegner selbst und braucht keine Marke.
                if (viewport.z > 0f && viewport.x > 0.04f && viewport.x < 0.96f &&
                    viewport.y > 0.04f && viewport.y < 0.96f) continue;

                // Richtung im Bildraum: die Marke sitzt auf dem Rand und zeigt nach draussen.
                var direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                if (viewport.z <= 0f) direction = -direction;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
                direction.Normalize();

                var marker = markers[used++];
                marker.gameObject.SetActive(true);
                // Auf das Rechteck des Bildschirms projizieren, nicht auf einen Kreis: sonst kleben
                // die Marken in den Ecken zusammen.
                var scale = Mathf.Min(
                    half.x * EdgeInset / Mathf.Max(0.0001f, Mathf.Abs(direction.x)),
                    half.y * EdgeInset / Mathf.Max(0.0001f, Mathf.Abs(direction.y)));
                ((RectTransform)marker.transform).anchoredPosition = direction * scale;
                marker.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
                // Pulsieren: eine stehende Marke uebersieht man im Kampf.
                var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
                marker.color = new Color(1f, 0.32f, 0.12f, pulse);
            }
            for (var i = used; i < markers.Length; i++)
                if (markers[i].gameObject.activeSelf) markers[i].gameObject.SetActive(false);
            // Einmal melden, dass die Anzeige wirklich greift: auf einem Einzelbild ist eine Marke
            // am Rand leicht zu uebersehen, im Log steht sie schwarz auf weiss.
            if (telegraphing > 0 && reported < 4)
            {
                reported++;
                Debug.Log($"SHATTERSPIRE Randmarke: {telegraphing} holen aus, {inRange} in Reichweite, " +
                          $"{used} Marken gesetzt. Flaeche {half * 2f}.");
            }
        }

        private void UpdateCritical()
        {
            if (!vignette) return;
            var normalized = heroHealth && heroHealth.IsAlive ? heroHealth.Normalized : 1f;
            if (normalized >= CriticalHealth || !heroHealth || !heroHealth.IsAlive)
            {
                var faded = vignette.color;
                faded.a = Mathf.MoveTowards(faded.a, 0f, 2f * Time.deltaTime);
                vignette.color = faded;
                return;
            }
            // Je weniger Leben, desto kraeftiger und desto schneller der Puls.
            var severity = 1f - normalized / CriticalHealth;
            var beat = 0.9f + severity * 1.4f;
            var alpha = (0.1f + severity * 0.16f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * beat * Mathf.PI)));
            var color = vignette.color;
            color.a = alpha;
            vignette.color = color;

            if (Time.time < nextHeartbeat) return;
            nextHeartbeat = Time.time + Mathf.Lerp(1.05f, 0.5f, severity);
            Sfx.Play2D(Sound.Heartbeat, 0.35f + severity * 0.4f);
        }
    }
}
