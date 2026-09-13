using System.Collections;
using System.IO;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Lichtet die Lobby ab und beendet sich.
    ///
    /// Die gewoehnliche Vorfuehrung springt am Menue vorbei direkt in einen Aufstieg - Heldenkarte,
    /// Stufe, Rang und Relikte sind damit auf keinem Bild zu pruefen, und gerade dort steht der
    /// Fortschritt, der ueber Laeufe hinweg bleibt.
    /// </summary>
    public sealed class MenuCapture : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var folder = CaptureDemo.Folder;
            if (string.IsNullOrEmpty(folder)) yield break;
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2f);
            for (var i = 0; i < 3; i++)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"menu{i:00}.png"));
                Debug.Log($"SHATTERSPIRE Menue-Aufnahme: menu{i:00}");
                yield return new WaitForSecondsRealtime(0.6f);
            }
            Debug.Log("SHATTERSPIRE Menue-Aufnahme fertig: " + folder);
            Application.Quit();
        }
    }
}
