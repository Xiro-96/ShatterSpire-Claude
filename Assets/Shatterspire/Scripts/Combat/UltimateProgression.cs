using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Ultimate wird im Lauf verdient, nicht mitgebracht.
    ///
    /// In Project R.I.S.E. schaltet das Aufsteigen im Lauf Upgrade-, Trait- und Ultimate-Plaetze
    /// frei. Hier kam die Ultimate bisher mit voller Staerke ab der ersten Sekunde - der Anfang
    /// eines Laufs fuehlte sich dadurch leicht an, und spaeter wurde sie nicht mehr besser.
    ///
    /// Angebunden ist sie an die Etage, nicht an die Laufstufe: die Laufstufe steigt nur bei Kernen
    /// ohne Verteidiger, also zufaellig. Die Upgrade-Wahl kommt dagegen nach jeder Etage - "ab
    /// Etage 3" heisst "nach zwei Upgrades", fuer jeden Spieler gleich und vorher ansagbar.
    ///
    /// Reine Rechnung ohne Zustand: die Etage liefert der Aufrufer, damit im Koop jeder Spieler
    /// dieselbe Antwort bekommt.
    /// </summary>
    public static class UltimateProgression
    {
        /// <summary>Ab dieser Etage ist die Ultimate da.</summary>
        public const int UnlockFloor = 3;

        /// <summary>Staerke auf der Etage der Freischaltung, als Anteil der vollen Wirkung.</summary>
        public const float StartPower = 0.6f;

        /// <summary>Zuwachs je weiterer Etage. Volle Staerke also fuenf Etagen nach der Freischaltung.</summary>
        public const float PowerPerFloor = 0.08f;

        /// <param name="unlockFloor">Ab welcher Etage - das Prestige des Helden kann sie vorziehen.</param>
        public static bool IsUnlocked(int floor, int unlockFloor = UnlockFloor) => floor >= unlockFloor;

        /// <summary>Wirkungsanteil auf dieser Etage - 0 vor der Freischaltung, hoechstens 1.</summary>
        /// <param name="startPower">Staerke bei der Freischaltung - das Prestige kann sie anheben.</param>
        public static float Power(int floor, int unlockFloor = UnlockFloor, float startPower = StartPower)
            => IsUnlocked(floor, unlockFloor)
                ? Mathf.Min(1f, startPower + (floor - unlockFloor) * PowerPerFloor)
                : 0f;

        /// <summary>Die erste Etage, auf der die Ultimate mit voller Staerke wirkt.</summary>
        public static int FullPowerFloor
            => UnlockFloor + Mathf.CeilToInt((1f - StartPower) / PowerPerFloor - 0.0001f);
    }
}
