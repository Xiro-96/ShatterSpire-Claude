using UnityEngine;

namespace Shatterspire
{
    /// <summary>Haelt eine Schrift in der Welt zur Kamera gedreht, damit sie lesbar bleibt.</summary>
    public sealed class WorldFacingLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
        }
    }
}
