using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Nur mit dem Startparameter -shatterspire-capture [Ordner]: startet einen Aufstieg mit Brax, spielt seine
    /// Hammer-Kombo automatisch vor, nimmt eine Bildfolge auf und beendet das Spiel. So laesst sich eine Animation
    /// pruefen, ohne dass jemand spielen und Screenshots schicken muss. Ohne den Parameter wird die Klasse nie erzeugt.
    /// </summary>
    public sealed class CaptureDemo : MonoBehaviour
    {
        private const string Flag = "-shatterspire-capture";
        private Transform player;
        private PlayerInputRouter input;
        private Camera view;
        private string folder;
        private bool framing;

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        public void Configure(GameObject hero, CameraController runCamera)
        {
            player = hero.transform;
            input = hero.GetComponent<PlayerInputRouter>();
            view = runCamera ? runCamera.GetComponent<Camera>() : Camera.main;
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, Flag);
            folder = index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-")
                ? args[index + 1]
                : Path.Combine(Application.persistentDataPath, "captures");
            Directory.CreateDirectory(folder);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            // Bots ausblenden, eigene Kamera flach und nah: Arme, Hammer und Drehung sollen erkennbar sein.
            foreach (var bot in FindObjectsByType<CompanionBot>(FindObjectsSortMode.None)) bot.gameObject.SetActive(false);
            if (view && view.TryGetComponent<CameraController>(out var follow)) follow.enabled = false;
            framing = true;
            input.ScriptedAim = new Vector3(0.75f, 0f, -1f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("idle");

            input.ScriptedAttack = true;
            var start = Time.unscaledTime;
            for (var i = 0; i < 16; i++)
            {
                while (Time.unscaledTime - start < i * 0.1f) yield return null;
                yield return Shot($"f{i:00}");
            }
            input.ScriptedAttack = false;
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("SHATTERSPIRE Capture fertig: " + folder);
            Application.Quit();
        }

        private void LateUpdate()
        {
            if (!framing || !view || !player) return;
            view.orthographicSize = 2.1f;
            view.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            view.transform.position = player.position + Vector3.up * 1.35f - view.transform.forward * 16f;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Debug.Log("SHATTERSPIRE Capture: " + name);
        }
    }
}
