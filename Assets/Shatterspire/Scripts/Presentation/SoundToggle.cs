using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Stummschalten mit M waehrend eines Aufstiegs. Bewusst eine eigene kleine Komponente und keine
    /// Zeile im Eingabe-Router: Lautstaerke ist kein Spielbefehl, der im Co-op verschickt werden muss.
    /// Auf dem Telefon fehlt die Taste - dort gehoert der Schalter spaeter ins Menue.
    /// </summary>
    public sealed class SoundToggle : MonoBehaviour
    {
        private float restore = 1f;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.M)) return;
            if (Sfx.Volume > 0f)
            {
                restore = Sfx.Volume;
                Sfx.Volume = 0f;
                Debug.Log("SHATTERSPIRE Ton: stumm (M)");
                return;
            }
            Sfx.Volume = restore <= 0f ? 1f : restore;
            Debug.Log($"SHATTERSPIRE Ton: an, Lautstaerke {Sfx.Volume:0.00} (M)");
        }
    }
}
