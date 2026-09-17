using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Prestige je Held: 25 Schritte in fuenf Raengen, bezahlt beim Schmied mit Splittern und
    /// Helden-Abzeichen.
    ///
    /// In Project R.I.S.E. werden Helden auf genau diese Art dauerhaft staerker: fuenf Stufen mit
    /// je fuenf Unterstufen, bezahlt mit Abzeichen und Gold, und die Abzeichen gibt es an
    /// Etagen-Meilensteinen. Hier stiegen Helden vorher von selbst ueber Erfahrung - man bekam
    /// die Staerke, ohne sie sich zu nehmen, und der Schmied hatte mit dem Helden nichts zu tun.
    ///
    /// Jeder volle Rang schaltet etwas frei. Daher kommt "spaeter stark durch den Schmied": der
    /// Anfang eines Aufstiegs ist fuer alle gleich schwer, ein ausgebauter Held bringt mehr mit.
    ///
    /// Reine Rechnung ohne Zustand - der Schritt kommt aus dem Spielstand des Spielers.
    /// </summary>
    public static class HeroPrestige
    {
        public const int MaximumStep = 25;
        public const int StepsPerRank = 5;

        /// <summary>Leben je Schritt, als Anteil des Grundlebens. Voll ausgebaut +37,5 %.</summary>
        public const float HealthPerStep = 0.015f;

        /// <summary>Schaden je Schritt, als Anteil. Voll ausgebaut +30 %.</summary>
        public const float DamagePerStep = 0.012f;

        // ── Freischaltungen am Ende jedes Rangs ─────────────────────────────
        public const int StartUpgradeStep = 5;
        public const int EarlyUltimateStep = 10;
        public const int FourthCardStep = 15;
        public const int FullForceStep = 20;
        public const int LegendStep = 25;

        public static int Clamp(int step) => Mathf.Clamp(step, 0, MaximumStep);

        /// <summary>Rang 0 ist der frische Held, 1 bis 5 die Raenge.</summary>
        public static int Rank(int step) => Clamp(step) <= 0 ? 0 : (Clamp(step) - 1) / StepsPerRank + 1;

        /// <summary>Stufe innerhalb des Rangs, 1 bis 5. Beim frischen Helden 0.</summary>
        public static int SubStep(int step) => Clamp(step) <= 0 ? 0 : (Clamp(step) - 1) % StepsPerRank + 1;

        public static string RankName(int step) => Rank(step) switch
        {
            0 => "NOVICE",
            1 => "ADEPT",
            2 => "VETERAN",
            3 => "MASTER",
            4 => "CHAMPION",
            _ => "LEGEND"
        };

        public static Color RankColor(int step) => Rank(step) switch
        {
            0 => new Color(0.6f, 0.64f, 0.7f),
            1 => new Color(0.42f, 0.86f, 0.56f),
            2 => new Color(0.18f, 0.72f, 1f),
            3 => new Color(0.72f, 0.38f, 1f),
            4 => new Color(1f, 0.46f, 0.2f),
            _ => new Color(1f, 0.83f, 0.22f)
        };

        /// <summary>Splitter fuer den Schritt von <paramref name="step"/> zum naechsten. 0 am Ende.</summary>
        public static int ShardCost(int step) => Clamp(step) >= MaximumStep ? 0 : 50 + 20 * Clamp(step);

        /// <summary>Abzeichen fuer den Schritt von <paramref name="step"/> zum naechsten. 0 am Ende.</summary>
        public static int BadgeCost(int step)
            => Clamp(step) >= MaximumStep ? 0 : 2 + 2 * (Clamp(step) / StepsPerRank);

        public static float HealthBonus(int step) => Clamp(step) * HealthPerStep;
        public static float DamageBonus(int step) => Clamp(step) * DamagePerStep;

        // ── Abzeichen ───────────────────────────────────────────────────────

        /// <summary>
        /// Abzeichen fuer das Erreichen dieser Etage. Nur Boss-Etagen sind Meilensteine; spaete zahlen
        /// mehr, weil sie schwerer zu erreichen sind.
        /// </summary>
        public static int BadgesForFloor(int floor)
        {
            if (!PathCatalog.IsBossFloor(floor)) return 0;
            return floor switch
            {
                5 => 3,
                10 => 4,
                _ => 5
            };
        }

        /// <summary>Alle Abzeichen eines Aufstiegs, der so viele Etagen geschafft hat.</summary>
        public static int BadgesForClimb(int floorsCleared)
        {
            var total = 0;
            for (var floor = 1; floor <= floorsCleared; floor++) total += BadgesForFloor(floor);
            return total;
        }

        // ── Freischaltungen ─────────────────────────────────────────────────

        public static bool HasStartUpgrade(int step) => Clamp(step) >= StartUpgradeStep;

        public static int UltimateUnlockFloor(int step)
            => Clamp(step) >= EarlyUltimateStep ? UltimateProgression.UnlockFloor - 1 : UltimateProgression.UnlockFloor;

        public static int UpgradeChoices(int step) => Clamp(step) >= FourthCardStep ? 4 : 3;

        public static float UltimateStartPower(int step)
            => Clamp(step) >= FullForceStep ? 0.8f : UltimateProgression.StartPower;

        public static int ExtraDashCharges(int step) => Clamp(step) >= LegendStep ? 1 : 0;

        /// <summary>Was genau bei diesem Schritt dazukommt - leer, wenn es nur Werte sind.</summary>
        public static string UnlockAt(int step) => step switch
        {
            StartUpgradeStep => "Every climb starts with an upgrade",
            EarlyUltimateStep => "Ultimate from floor 2",
            FourthCardStep => "Upgrade choices show 4 cards",
            FullForceStep => "Ultimate starts at 80% power",
            LegendStep => "+1 dash charge",
            _ => string.Empty
        };

        /// <summary>Der naechste Schritt mit einer Freischaltung, oder 0, wenn keiner mehr kommt.</summary>
        public static int NextUnlockStep(int step)
        {
            for (var next = Clamp(step) + 1; next <= MaximumStep; next++)
                if (!string.IsNullOrEmpty(UnlockAt(next))) return next;
            return 0;
        }

        /// <summary>
        /// Umrechnung der frueheren Heldenstufe 1 bis 15 in Prestige-Schritte. Hoechste Stufe wird
        /// volles Prestige - wer seinen Helden ausgebaut hatte, soll das nicht noch einmal muessen.
        /// </summary>
        public static int StepsFromLegacyLevel(int level)
            => Mathf.RoundToInt((Mathf.Clamp(level, 1, HeroProgress.MaximumLevel) - 1)
                                * (float)MaximumStep / (HeroProgress.MaximumLevel - 1));
    }
}
