#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Nur im Editor: nimmt beim Spielen automatisch Bilder auf und legt sie unter
    /// Screenshots/ im Projektordner ab.
    ///
    /// Grund: Grafik-Aenderungen liessen sich bisher nur pruefen, indem jemand das
    /// Spiel startet, einen Screenshot macht und ihn schickt. Drei Runden hintereinander
    /// wurde dabei blind an Werten gedreht, deren Wirkung niemand sehen konnte. Mit
    /// diesen Dateien reicht ein Druck auf Play - das Bild liegt danach auf der Platte.
    ///
    /// Im Player-Build existiert die Klasse nicht.
    /// </summary>
    public sealed class EditorCaptureAgent : MonoBehaviour
    {
        private string label;
        private float[] delays;
        private bool captureEachFloor;

        public static void Attach(GameObject host, string captureLabel, bool eachFloor, params float[] captureDelays)
        {
            if (!host) return;
            var agent = host.AddComponent<EditorCaptureAgent>();
            agent.label = captureLabel;
            agent.captureEachFloor = eachFloor;
            agent.delays = captureDelays;
        }

        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));

        private void Start()
        {
            Directory.CreateDirectory(Folder);
            if (delays != null)
                foreach (var delay in delays)
                    StartCoroutine(CaptureAfter(delay, $"{label}_{delay:0}s"));
            if (captureEachFloor) GameEvents.RoomStarted += OnRoomStarted;
        }

        private void OnDestroy()
        {
            if (captureEachFloor) GameEvents.RoomStarted -= OnRoomStarted;
        }

        private void OnRoomStarted(int floor, RoomKind kind)
        {
            // Die erste Etage startet schon waehrend RunDirector.Configure, also bevor
            // dieser Agent existiert. Sie ist durch die zeitgesteuerte Aufnahme abgedeckt.
            if (isActiveAndEnabled) StartCoroutine(CaptureAfter(2.5f, $"floor_{floor:00}_{kind}"));
        }

        private IEnumerator CaptureAfter(float seconds, string name)
        {
            // Echtzeit, damit Hitstop und pausierende Menues die Aufnahme nicht verschieben.
            yield return new WaitForSecondsRealtime(seconds);
            var path = Path.Combine(Folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"SHATTERSPIRE Screenshot: {path}");
        }
    }
}
#endif
