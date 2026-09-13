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

        /// <summary>
        /// Die Waechter in der Reihenfolge, in der man ihnen begegnet. Ein Heroic-Aufstieg hat drei
        /// Boss-Etagen und damit drei verschiedene Kaempfe - vorher dreimal denselben.
        /// </summary>
        private static readonly EnemyKind[] Bosses =
            { EnemyKind.IronWarden, EnemyKind.RiftTwin, EnemyKind.ChoirWarden };

        /// <summary>
        /// Wer auf dieser Boss-Etage wartet. Bewusst an der Etage und nicht am Seed: dass Etage 10
        /// der Zwilling ist, soll man lernen und sich darauf einstellen koennen. Ein Aufstieg ohne
        /// Ende dreht die Folge weiter.
        /// </summary>
        public static EnemyKind BossFor(int floor)
        {
            var index = Mathf.Max(0, floor / BossInterval - 1);
            return Bosses[index % Bosses.Length];
        }

        public static string BossName(EnemyKind boss) => boss switch
        {
            EnemyKind.RiftTwin => "RIFT TWIN",
            EnemyKind.ChoirWarden => "CHOIR WARDEN",
            _ => "SPIRE WARDEN"
        };

        /// <summary>
        /// Welche Routen am Aufzug angeboten werden. Immer Kampf und Elite, dazu als drittes
        /// abwechselnd Schatz oder Raetsel - eine sichere, eine harte und eine offene Wahl.
        ///
        /// Aus dem Lauf-Seed gezogen und nicht aus <see cref="UnityEngine.Random"/>: im Co-op
        /// muessen alle drei Spieler dieselben drei Knoepfe vor sich haben, bevor abgestimmt wird.
        /// </summary>
        public static RoomKind[] RoutesFor(int runSeed, int floor)
        {
            if (IsBossFloor(floor)) return new[] { RoomKind.Boss };
            var third = RunRandom.Chance(runSeed, floor, 97, 0.5f) ? RoomKind.Treasure : RoomKind.Mystery;
            return new[] { RoomKind.Combat, RoomKind.Elite, third };
        }

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
            => IsEndless(path)
                ? $"{Loc.T("FLOOR")} {floor}"
                : $"{Loc.T("FLOOR")} {floor} / {FloorCount(path)}";

        public static Color Accent(RunMode path) => path switch
        {
            RunMode.Brave => new Color(0.1f, 0.86f, 0.62f),
            RunMode.Heroic => new Color(1f, 0.64f, 0.12f),
            _ => new Color(0.68f, 0.3f, 1f)
        };
    }
}
