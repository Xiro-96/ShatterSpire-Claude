using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Kurzes Einfrieren im Moment des Einschlags. Der Camera-Impulse war bisher
    /// die eine Haelfte des Trefferfeedbacks — das ist die andere. Ohne sie hat ein
    /// Treffer keinerlei Gewicht, egal wie hoch der Schaden ist.
    /// </summary>
    public sealed class Hitstop : MonoBehaviour
    {
        /// <summary>Sperre nach einem Stop, damit eine Salve nicht zur Dauerzeitlupe wird.</summary>
        private const float CooldownSeconds = 0.09f;

        private static Hitstop instance;
        private float restoreAt;
        private float nextAllowed;
        private float appliedScale;
        private bool active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() => instance = null;

        public static void Freeze(float seconds, float scale = 0.06f)
        {
            if (seconds <= 0f) return;
            // Pausiert das HUD gerade — Perk-Auswahl, Routen, Run-Ende — dann Finger
            // weg. Sonst wuerde das Zuruecksetzen auf 1 die Pause aufheben.
            if (Time.timeScale <= 0f) return;
            if (!instance)
            {
                var host = new GameObject("Hitstop");
                instance = host.AddComponent<Hitstop>();
            }
            if (Time.unscaledTime < instance.nextAllowed) return;
            instance.Begin(seconds, scale);
        }

        private void Begin(float seconds, float scale)
        {
            appliedScale = Mathf.Clamp(scale, 0f, 1f);
            restoreAt = Time.unscaledTime + seconds;
            nextAllowed = restoreAt + CooldownSeconds;
            Time.timeScale = appliedScale;
            active = true;
        }

        private void Update()
        {
            if (!active) return;
            // Hat in der Zwischenzeit etwas anderes die Zeit uebernommen, still
            // zuruecktreten statt darueberzuschreiben.
            if (!Mathf.Approximately(Time.timeScale, appliedScale))
            {
                active = false;
                return;
            }
            if (Time.unscaledTime < restoreAt) return;
            Time.timeScale = 1f;
            active = false;
        }

        private void OnDestroy()
        {
            // Wird die Szene mitten im Stop neu geladen, darf die Zeit nicht
            // eingefroren zurueckbleiben.
            if (active && Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = 1f;
            if (instance == this) instance = null;
        }
    }
}
