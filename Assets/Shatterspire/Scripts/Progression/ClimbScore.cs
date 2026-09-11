using System;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Was ein einzelner Aufstieg eingebracht hat.</summary>
    [Serializable]
    public readonly struct ClimbResult
    {
        public readonly int FloorsCleared;
        public readonly int BossesDefeated;
        public readonly int EnemiesDefeated;
        public readonly int ShardsSecured;
        /// <summary>Freiwillig extrahiert oder den Turm ausgespielt statt gefallen.</summary>
        public readonly bool Extracted;
        public readonly RunMode Mode;
        public readonly HeroClassId Hero;

        public ClimbResult(int floorsCleared, int bossesDefeated, int enemiesDefeated,
            int shardsSecured, bool extracted, RunMode mode, HeroClassId hero)
        {
            FloorsCleared = Mathf.Max(0, floorsCleared);
            BossesDefeated = Mathf.Max(0, bossesDefeated);
            EnemiesDefeated = Mathf.Max(0, enemiesDefeated);
            ShardsSecured = Mathf.Max(0, shardsSecured);
            Extracted = extracted;
            Mode = mode;
            Hero = hero;
        }
    }

    /// <summary>
    /// Bewertet einen Aufstieg. Die Formel ist absichtlich flach und ablesbar — der
    /// Spieler soll im Endbildschirm nachvollziehen koennen, woher seine Punkte
    /// kommen, sonst ist der Rang keine Motivation sondern eine Zahl.
    ///
    /// Bewusst ohne Zeitkomponente: die wuerde zum Durchrennen einladen und dem
    /// Rest des Designs — Perks sammeln, Etage abschliessen — entgegenlaufen.
    /// </summary>
    public static class ClimbScore
    {
        public const int PerFloor = 1000;
        public const int PerBoss = 2500;
        public const int PerEnemy = 25;
        public const int PerShard = 2;

        /// <summary>Sichern lohnt sich. Extract ist eine Entscheidung, kein Rueckzug.</summary>
        public const float ExtractedMultiplier = 1.15f;
        /// <summary>Gefallen kostet, nimmt aber nicht alles — sonst wird jeder Run wertlos.</summary>
        public const float DefeatMultiplier = 0.7f;

        public static int Evaluate(in ClimbResult result)
        {
            var raw = result.FloorsCleared * PerFloor
                      + result.BossesDefeated * PerBoss
                      + result.EnemiesDefeated * PerEnemy
                      + result.ShardsSecured * PerShard;
            var multiplier = result.Extracted ? ExtractedMultiplier : DefeatMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(raw * multiplier));
        }

        /// <summary>Aufschluesselung fuer den Endbildschirm, in Anzeigereihenfolge.</summary>
        public static (string Label, int Points)[] Breakdown(in ClimbResult result) => new[]
        {
            ($"FLOORS  {result.FloorsCleared}", result.FloorsCleared * PerFloor),
            ($"BOSSES  {result.BossesDefeated}", result.BossesDefeated * PerBoss),
            ($"ENEMIES  {result.EnemiesDefeated}", result.EnemiesDefeated * PerEnemy),
            ($"SHARDS  {result.ShardsSecured}", result.ShardsSecured * PerShard)
        };
    }
}
