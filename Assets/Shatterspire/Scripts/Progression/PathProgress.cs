using System;

namespace Shatterspire
{
    /// <summary>
    /// Welche Pfade offen sind. In Project R.I.S.E. werden die Pfade schwerer und laenger, und der
    /// naechste oeffnet sich erst, wenn man den vorigen geschafft hat - den zweiten mit mindestens
    /// zwei Helden. Man soll in die schweren Pfade hineinwachsen, nicht in sie hineinstolpern.
    ///
    /// Gespeichert wird je Pfad, welche Helden ihn geschafft haben, als Bitmaske in
    /// <see cref="MetaSaveData.pathClears"/>. Ein Pfad gilt als geschafft, wenn der Lauf seine
    /// letzte Etage erreicht - ein vorzeitiges Aussteigen nach einem Boss zaehlt nicht.
    /// </summary>
    public static class PathProgress
    {
        /// <summary>So viele verschiedene Helden muessen Heroic geschafft haben, bevor Legendary aufgeht.</summary>
        public const int HeroesForLegendary = 2;

        public static bool IsUnlocked(MetaSaveData data, RunMode path) => path switch
        {
            RunMode.Brave => true,
            RunMode.Heroic => HeroesCleared(data, RunMode.Brave) >= 1,
            _ => HeroesCleared(data, RunMode.Heroic) >= HeroesForLegendary
        };

        /// <summary>Was fehlt, damit der Pfad aufgeht. Leer, wenn er offen ist.</summary>
        public static string Requirement(RunMode path) => path switch
        {
            // Kurz, weil es unter dem Pfadnamen auf einem 460 px breiten Knopf steht. Die erste
            // Fassung hatte 48 Zeichen - vorsorglich gekuerzt, nicht nach einem gesehenen Ueberlauf.
            RunMode.Heroic => "Clear Brave first.",
            RunMode.Legendary => "Clear Heroic with 2 heroes.",
            _ => string.Empty
        };

        /// <summary>Der schwerste offene Pfad.</summary>
        public static RunMode Highest(MetaSaveData data)
            => IsUnlocked(data, RunMode.Legendary) ? RunMode.Legendary
                : IsUnlocked(data, RunMode.Heroic) ? RunMode.Heroic
                : RunMode.Brave;

        /// <summary>Wie viele verschiedene Helden diesen Pfad geschafft haben.</summary>
        public static int HeroesCleared(MetaSaveData data, RunMode path)
        {
            var mask = Mask(data, path);
            var count = 0;
            for (var bit = 0; bit < 31; bit++)
                if ((mask & (1 << bit)) != 0) count++;
            return count;
        }

        public static bool HasCleared(MetaSaveData data, RunMode path, HeroClassId hero)
            => (Mask(data, path) & (1 << (int)hero)) != 0;

        /// <summary>Hat dieser Lauf seinen Pfad bis zur letzten Etage geschafft?</summary>
        public static bool IsClear(in ClimbResult result)
            => result.Extracted
               && !PathCatalog.IsEndless(result.Mode)
               && result.FloorsCleared >= PathCatalog.FloorCount(result.Mode);

        /// <summary>
        /// Traegt einen Lauf ein. Gibt den Pfad zurueck, der dadurch neu aufgegangen ist, sonst null.
        /// </summary>
        public static RunMode? Record(MetaSaveData data, in ClimbResult result)
        {
            if (data == null || !IsClear(result)) return null;
            var before = Highest(data);
            MarkCleared(data, result.Mode, result.Hero);
            var after = Highest(data);
            return after != before ? after : null;
        }

        public static void MarkCleared(MetaSaveData data, RunMode path, HeroClassId hero)
        {
            if (data == null) return;
            var index = (int)path;
            if (data.pathClears == null || data.pathClears.Length <= index)
            {
                var grown = new int[index + 1];
                if (data.pathClears != null) Array.Copy(data.pathClears, grown, data.pathClears.Length);
                data.pathClears = grown;
            }
            data.pathClears[index] |= 1 << (int)hero;
        }

        private static int Mask(MetaSaveData data, RunMode path)
        {
            var index = (int)path;
            return data?.pathClears != null && index < data.pathClears.Length ? data.pathClears[index] : 0;
        }
    }
}
