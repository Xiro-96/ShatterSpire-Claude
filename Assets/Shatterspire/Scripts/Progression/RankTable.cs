using UnityEngine;

namespace Shatterspire
{
    public enum RankTier { Splinter, Ember, Iron, Rift, Astral, Spire }

    /// <summary>
    /// Der Rang entsteht aus den <see cref="ClimbCount"/> besten Aufstiegen des
    /// laufenden Shifts, nicht aus dem letzten. Das nimmt dem einzelnen Run die
    /// Haerte — ein misslungener Aufstieg kostet keinen Rang, er zaehlt nur nicht —
    /// und belohnt trotzdem Konstanz statt eines Glueckstreffers.
    /// </summary>
    public static class RankTable
    {
        /// <summary>So viele Bestwerte gehen in den Rang ein.</summary>
        public const int ClimbCount = 3;

        /// <summary>
        /// Schwellen in Rangpunkten, also der Summe der drei besten Climbs.
        /// Kalibriert am Standard-Spire: ein vollstaendiger Durchgang bis Etage 15
        /// mit drei Bossen liegt bei etwa 32 000 Punkten, drei davon bei rund
        /// 96 000 — das ist die Spitze der Leiter.
        /// </summary>
        private static readonly int[] Thresholds = { 0, 9000, 24000, 45000, 70000, 96000 };

        /// <summary>Tokens beim Shift-Ende, nach erreichtem Rang.</summary>
        private static readonly int[] TokenRewards = { 0, 25, 60, 120, 220, 400 };

        public static string Name(RankTier tier) => tier switch
        {
            RankTier.Ember => "EMBER",
            RankTier.Iron => "IRON",
            RankTier.Rift => "RIFT",
            RankTier.Astral => "ASTRAL",
            RankTier.Spire => "SPIRE",
            _ => "SPLINTER"
        };

        public static Color Accent(RankTier tier) => tier switch
        {
            RankTier.Ember => new Color(1f, 0.52f, 0.12f),
            RankTier.Iron => new Color(0.72f, 0.78f, 0.85f),
            RankTier.Rift => new Color(0.06f, 0.88f, 0.92f),
            RankTier.Astral => new Color(0.66f, 0.3f, 1f),
            RankTier.Spire => new Color(1f, 0.83f, 0.22f),
            _ => new Color(0.5f, 0.55f, 0.62f)
        };

        public static RankTier TierFor(int rankPoints)
        {
            var tier = RankTier.Splinter;
            for (var i = Thresholds.Length - 1; i >= 0; i--)
            {
                if (rankPoints < Thresholds[i]) continue;
                tier = (RankTier)i;
                break;
            }
            return tier;
        }

        public static int ThresholdOf(RankTier tier)
            => Thresholds[Mathf.Clamp((int)tier, 0, Thresholds.Length - 1)];

        public static int TokensFor(RankTier tier)
            => TokenRewards[Mathf.Clamp((int)tier, 0, TokenRewards.Length - 1)];

        public static bool IsHighest(RankTier tier) => (int)tier >= Thresholds.Length - 1;

        /// <summary>Punkte bis zum naechsten Rang, 0 wenn der hoechste erreicht ist.</summary>
        public static int PointsToNext(int rankPoints)
        {
            var tier = TierFor(rankPoints);
            if (IsHighest(tier)) return 0;
            return Mathf.Max(0, Thresholds[(int)tier + 1] - rankPoints);
        }

        /// <summary>Fortschritt innerhalb des aktuellen Rangs, 0 bis 1.</summary>
        public static float ProgressInTier(int rankPoints)
        {
            var tier = TierFor(rankPoints);
            if (IsHighest(tier)) return 1f;
            var floor = Thresholds[(int)tier];
            var ceiling = Thresholds[(int)tier + 1];
            if (ceiling <= floor) return 1f;
            return Mathf.Clamp01((rankPoints - floor) / (float)(ceiling - floor));
        }

        /// <summary>
        /// Rangpunkte aus einer Liste von Climb-Ergebnissen: die besten
        /// <see cref="ClimbCount"/> zaehlen, der Rest wird ignoriert.
        /// </summary>
        public static int PointsFrom(System.Collections.Generic.IReadOnlyList<int> climbScores)
        {
            if (climbScores == null || climbScores.Count == 0) return 0;
            // Kleine Liste, kleines n - ein simpler Auswahl-Durchlauf ist hier
            // billiger und klarer als zu sortieren.
            var best = new int[ClimbCount];
            foreach (var score in climbScores)
            {
                var candidate = Mathf.Max(0, score);
                for (var slot = 0; slot < best.Length; slot++)
                {
                    if (candidate <= best[slot]) continue;
                    (best[slot], candidate) = (candidate, best[slot]);
                }
            }
            var total = 0;
            foreach (var score in best) total += score;
            return total;
        }
    }
}
