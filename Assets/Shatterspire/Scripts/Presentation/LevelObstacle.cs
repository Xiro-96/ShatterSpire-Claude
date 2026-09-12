using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Markiert einen Kollider als feste Etagengeometrie - Wand oder Deckung. Geschosse schlagen hier
    /// ein statt hindurchzufliegen; ohne diese Marke muesste jede Stelle im Kampfcode Ebenen und
    /// Kollider-Namen kennen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelObstacle : MonoBehaviour
    {
    }
}
