using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Zahlen der Relikte, die etwas tun statt nur einen Wert zu verschieben.
    ///
    /// Die ersten fuenfzehn Relikte waren fast alle "+X %" auf eine Zahl. Die zehn neuen haben je
    /// ein eigenes Verb: einen Tod abfangen, beim Abschuss bersten, beim Dash schneiden, mit dem
    /// Lauf wachsen. Ihre Wirkung sitzt im Kampfcode; ihre Zahlen stehen hier, damit sie an einer
    /// Stelle stehen und sich ohne laufendes Spiel nachrechnen lassen.
    /// </summary>
    public static class RelicRules
    {
        // ── Letzter Atem ────────────────────────────────────────────────────
        /// <summary>So viel Leben bleibt mindestens, wenn der Letzte Atem einen Tod abfaengt.</summary>
        public const float LastBreathHealth = 0.3f;
        /// <summary>Kurze Unverwundbarkeit danach - sonst faellt der naechste Schlag im selben Moment.</summary>
        public const float LastBreathGrace = 1.2f;

        /// <summary>Faengt dieser Treffer den Tod ab? Nur einmal je Aufstieg, und nur, wenn er toedlich waere.</summary>
        public static bool LastBreathCatches(bool alreadyUsed, float incoming, float current)
            => !alreadyUsed && current > 0f && incoming >= current;

        /// <summary>Leben danach: mindestens 30 %, aber nie weniger, als man vorher hatte.</summary>
        public static float LastBreathHealthAfter(float current, float maximum)
            => Mathf.Max(current, maximum * LastBreathHealth);

        // ── Splitterbersten ─────────────────────────────────────────────────
        public const float SplinterBurstRadius = 2.4f;
        /// <summary>Schaden eines Berstens, in Vielfachen des Grundschadens.</summary>
        public const float SplinterBurstDamage = 1.2f;
        /// <summary>
        /// Hoechstens so viele Bersten je Sekunde. Ein Bersten kann den naechsten Gegner toeten, der
        /// wieder berstet - ohne Deckel raeumt ein Schwarm sich selbst ab.
        /// </summary>
        public const int SplinterBurstsPerSecond = 6;

        // ── Adrenalin ───────────────────────────────────────────────────────
        public const float AdrenalineSpeed = 1.25f;
        public const float AdrenalineSeconds = 2f;
        /// <summary>Nur Abschuesse in dieser Naehe zaehlen - im Koop auch die der anderen.</summary>
        public const float AdrenalineRange = 12f;

        // ── Blutpakt ────────────────────────────────────────────────────────
        public const float BloodPactDamage = 1.25f;
        public const float BloodPactHealthLoss = 0.25f;

        // ── Sturmglocke ─────────────────────────────────────────────────────
        /// <summary>Jeder so vielte leichte Angriff ruft den Blitz.</summary>
        public const int StormBellEvery = 5;
        public const float StormBellRadius = 2.2f;
        public const float StormBellDamage = 2.2f;

        // ── Funkenschild ────────────────────────────────────────────────────
        /// <summary>So lange nach einem Dash haelt der Schild. Danach verfaellt er ungenutzt.</summary>
        public const float SparkWardSeconds = 3f;

        // ── Phantomklinge ───────────────────────────────────────────────────
        public const float PhantomEdgeDamage = 0.7f;
        public const float PhantomEdgeRadius = 1.3f;
        public const int PhantomEdgeCuts = 3;

        // ── Ruhiges Herz ────────────────────────────────────────────────────
        public const float SteadyHeartHeal = 0.08f;

        // ── Ueberfluss ──────────────────────────────────────────────────────
        /// <summary>Anteil des Schwerbalkens, den die Faehigkeit fuellt.</summary>
        public const float OverflowHeavyFill = 0.5f;

        // ── Kriegsbanner ────────────────────────────────────────────────────
        public const float WarBannerPerFloor = 0.05f;
        public const float WarBannerMaximum = 0.4f;

        /// <summary>
        /// Zusaetzlicher Schaden auf dieser Etage. Auf Etage 1 nichts - das Banner waechst mit dem
        /// Lauf, genau wie der Anfang schwer und das Ende stark sein soll.
        /// </summary>
        public static float WarBannerBonus(int floor)
            => Mathf.Min(WarBannerMaximum, Mathf.Max(0, floor - 1) * WarBannerPerFloor);
    }
}
