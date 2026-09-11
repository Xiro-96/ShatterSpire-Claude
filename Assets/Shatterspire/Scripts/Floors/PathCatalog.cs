using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die drei Schwierigkeitspfade nach dem Vorbild von R.I.S.E.: vor dem Start gewaehlt, mit fester
    /// Laenge - Brave 5 Etagen, Heroic 15, Legendary ohne Ende. Brave ist bewusst kurz genug fuer
    /// eine Sitzung am Telefon.
    /// </summary>
    public static class PathCatalog
    {
        public const int BossInterval = 5;

        public static int FloorCount(RunMode path) => path switch
        {
            RunMode.Brave => 5,
            RunMode.Heroic => 15,
            _ => 0
        };

        public static bool IsEndless(RunMode path) => FloorCount(path) <= 0;

        public static bool IsBossFloor(int floor) => floor > 0 && floor % BossInterval == 0;

        /// <summary>Nach dieser Etage endet der Aufstieg ohne Wahl zwischen Extrahieren und Aufsteigen.</summary>
        public static bool IsFinalFloor(RunMode path, int floor) => !IsEndless(path) && floor >= FloorCount(path);

        public static string Name(RunMode path) => path switch
        {
            RunMode.Brave => "BRAVE",
            RunMode.Heroic => "HEROIC",
            _ => "LEGENDARY"
        };

        // Kurz gehalten: die Menue-Knoepfe sind 520 px breit, laengere Texte brechen dreizeilig um.
        public static string Summary(RunMode path) => path switch
        {
            RunMode.Brave => "5 FLOORS  ·  SHORT CLIMB",
            RunMode.Heroic => "15 FLOORS  ·  3 WARDENS",
            _ => "ENDLESS  ·  RISING REWARDS"
        };

        public static string FloorCounter(RunMode path, int floor)
            => IsEndless(path) ? $"FLOOR {floor}" : $"FLOOR {floor} / {FloorCount(path)}";

        public static Color Accent(RunMode path) => path switch
        {
            RunMode.Brave => new Color(0.1f, 0.86f, 0.62f),
            RunMode.Heroic => new Color(1f, 0.64f, 0.12f),
            _ => new Color(0.68f, 0.3f, 1f)
        };
    }
}
