using UnityEngine;

namespace Shatterspire
{
    /// <summary>Wie gut der schwere Angriff losgelassen wurde.</summary>
    public enum HeavyTiming
    {
        /// <summary>Daneben. Der Schlag kommt, aber ohne Aufschlag.</summary>
        Loose,

        /// <summary>Knapp am Fenster. Es hat sich gelohnt, darauf zu achten.</summary>
        Good,

        /// <summary>Im Fenster. Der beste Schlag, den dieser Held hat.</summary>
        Perfect
    }

    /// <summary>
    /// Die Norm, an der alle fuenf Klassen gemessen werden, und die Zahlen des perfekten Moments.
    ///
    /// Anlass war eine Messung, nicht ein Gefuehl. Die fuenf Klassen standen so da:
    ///
    ///   Klasse      Schaden/s  Reichweite  gebunden  Kampftempo  Zeit bis Heavy
    ///   REX              44,6       15,00        0 %        6,25         1,80 s
    ///   BRAX             48,9        3,25       51 %        3,36         2,90 s
    ///   ORION            40,5       15,00        0 %        5,80         2,16 s
    ///   KORR             45,2        6,50        0 %        6,00         2,31 s
    ///   XIRO             49,5        2,60       70 %        2,51         2,00 s
    ///
    /// Ein Crawler laeuft 3,50. XIRO kam im Kampf auf 2,51 und hatte die kuerzeste Reichweite von
    /// allen - er verlor jede Sekunde eine ganze Einheit auf sein Ziel und schlug nach drei Hieben
    /// ins Leere. Genau das war die Meldung "Autohits sehr schlecht als recht". Ein Zweihaender, der
    /// kuerzer reicht als ein Kriegshammer, war zusaetzlich einfach falsch.
    ///
    /// Reine Rechnung, ohne Unity. Die Tests fahren sie nach.
    /// </summary>
    public static class ActionBalance
    {
        // ── Der perfekte Moment ─────────────────────────────────────────────

        /// <summary>Wie lange sich der schwere Angriff auflaedt, bis er von selbst ausloest.</summary>
        public const float HeavyChargeSeconds = 1.2f;

        /// <summary>
        /// Das Fenster, als Anteil der Ladezeit. Stand auf 0,50 bis 0,76, also 312 ms - treffbar,
        /// aber nur wenn man weiss, wann es anfaengt. Jetzt etwas breiter, weil es auf einem Telefon
        /// mit einem Daumen auf dem Knopf getroffen werden muss.
        /// </summary>
        public const float PerfectStart = 0.46f;
        public const float PerfectEnd = 0.78f;

        /// <summary>
        /// So weit darf man am Fenster vorbei sein und bekommt noch etwas dafuer.
        ///
        /// Vorher gab es nur "perfekt" und "nichts". Wer 50 ms zu frueh loslaesst, hat nicht falsch
        /// gespielt - er hat fast richtig gespielt, und das ist der Unterschied zwischen einer
        /// Mechanik, die man lernt, und einer, die man aufgibt.
        /// </summary>
        public const float GoodMargin = 0.1f;

        public const float PerfectMultiplier = 4.5f;
        public const float GoodMultiplier = 3.6f;

        /// <summary>Spanne fuer alles ausserhalb: von zu frueh bis ganz durchgehalten.</summary>
        public const float LooseMinimum = 2f;
        public const float LooseMaximum = 3f;

        public static HeavyTiming Judge(float normalized)
        {
            if (normalized >= PerfectStart && normalized <= PerfectEnd) return HeavyTiming.Perfect;
            if (normalized >= PerfectStart - GoodMargin && normalized <= PerfectEnd + GoodMargin)
                return HeavyTiming.Good;
            return HeavyTiming.Loose;
        }

        /// <summary>Schadensfaktor des schweren Angriffs fuer diesen Moment.</summary>
        public static float Multiplier(float normalized) => Judge(normalized) switch
        {
            HeavyTiming.Perfect => PerfectMultiplier,
            HeavyTiming.Good => GoodMultiplier,
            _ => Mathf.Lerp(LooseMinimum, LooseMaximum, Mathf.Clamp01(normalized))
        };

        /// <summary>
        /// Darf bei diesem Ladestand schon losgelassen werden?
        ///
        /// Vor dem Fenster bringt Loslassen nichts - es kostet nur den vollen Balken. Ein Tipp auf
        /// dem Telefon ist aber Druck und Loslassen in einem Bild, und genau das passierte dabei.
        /// Wer zu frueh loslaesst, laedt weiter und loest am Ende der Ladung aus.
        /// </summary>
        public static bool CanRelease(float normalized) => normalized >= PerfectStart - GoodMargin;

        /// <summary>Wie lange das Fenster offen steht, in Sekunden.</summary>
        public static float WindowSeconds => (PerfectEnd - PerfectStart) * HeavyChargeSeconds;

        /// <summary>Wann es aufgeht, in Sekunden nach dem Druck.</summary>
        public static float WindowOpensAt => PerfectStart * HeavyChargeSeconds;

        // ── Der Balken des schweren Angriffs ────────────────────────────────

        /// <summary>
        /// So lange soll es bei jedem Helden dauern, bis der schwere Angriff bereit ist - wenn er
        /// dabei trifft.
        /// </summary>
        public const float HeavyFillSeconds = 2f;

        /// <summary>Wie lange ein leichter Angriff dieses Helden braucht, gemittelt ueber sein Kombo.</summary>
        public static float LightActionSeconds(HeroClassId hero) => hero switch
        {
            // Summe der drei Pausen geteilt durch drei, so wie sie in WeaponSystem stehen.
            HeroClassId.Guardian => (0.38f + 0.46f + 0.64f) / 3f,
            HeroClassId.Paladin => (0.3f + 0.3f + 0.42f) / 3f,
            HeroClassId.Arcanist => (0.34f + 0.34f + 0.42f) / 3f,
            HeroClassId.Bomber => (0.34f + 0.34f + 0.5f) / 3f,
            _ => (0.25f + 0.25f + 0.42f) / 3f
        };

        /// <summary>
        /// Wie viel ein leichter Treffer am Balken des schweren Angriffs fuellt.
        ///
        /// Stand bei allen fuenf auf 17, also sechs Treffer. Weil die Helden aber in verschiedenem
        /// Tempo schlagen, hiess dieselbe Zahl eine verschiedene Wartezeit: REX war nach 1,80 s
        /// bereit, BRAX erst nach 2,90 s. Der schwere Angriff ist die Aktion mit dem perfekten
        /// Moment - wer ihn seltener bekommt, bekommt weniger vom Spiel.
        ///
        /// Jetzt haengt die Fuellung am Tempo des Helden, und alle fuenf brauchen dieselben zwei
        /// Sekunden.
        /// </summary>
        public static float HeavyFillPerHit(HeroClassId hero)
            => 100f * LightActionSeconds(hero) / HeavyFillSeconds;

        /// <summary>Wie lange dieser Held bis zum schweren Angriff braucht. Muss bei allen gleich sein.</summary>
        public static float SecondsToHeavy(HeroClassId hero)
            => 100f / HeavyFillPerHit(hero) * LightActionSeconds(hero);

        // ── Die Norm fuer den Nahkampf ──────────────────────────────────────

        /// <summary>
        /// Wie lange ein Nahkaempfer an einem weglaufenden Gegner bleibt, bis dieser aus seiner
        /// Reichweite heraus ist.
        ///
        /// Das ist die Zahl, an der sich entscheidet, ob sich ein Nahkaempfer gut anfuehlt. Er darf
        /// langsam sein - dann muss er weit reichen. Er darf kurz reichen - dann muss er schnell
        /// sein. Beides kurz ist unspielbar, und genau das war XIRO.
        /// </summary>
        public static float ContactSeconds(HeroClassId hero, float reach, float combatSpeed, float enemySpeed)
        {
            var deficit = enemySpeed - combatSpeed;
            var margin = reach - MeleeApproach.IdealGapFor(hero);
            if (margin <= 0f) return 0f;
            return deficit <= 0.01f ? float.PositiveInfinity : margin / deficit;
        }

        /// <summary>
        /// Wie weit der leichte Angriff eines Nahkaempfers hoechstens reicht, in Einheiten vom
        /// Helden aus gemessen. Fernkaempfer stehen mit 0 darin - sie haben keine Schlagweite.
        ///
        /// Das ist die Quelle, nicht eine Kopie: die Kombos in <see cref="WeaponSystem"/> rechnen
        /// ihre Radien daraus aus. Vorher standen die Zahlen nur dort, verstreut ueber drei
        /// Kombostufen, und niemandem fiel auf, dass der Zweihaender kuerzer reichte als der Hammer.
        /// </summary>
        public static float MeleeReach(HeroClassId hero) => hero switch
        {
            // Zweihaender: die laengste Waffe im Spiel, also die weiteste Reichweite. Er ist
            // ausserdem der langsamste Held - die Reichweite ist sein Ausgleich dafuer.
            HeroClassId.Paladin => 3.4f,
            // Kriegshammer: kurz gefasst und schwer, dafuer trifft die dritte Stufe rundum.
            HeroClassId.Guardian => 3.25f,
            _ => 0f
        };

        /// <summary>So lange muss jeder Nahkaempfer mindestens am Crawler bleiben.</summary>
        public const float MinimumContactSeconds = 2f;

        /// <summary>Tempo des schnellsten gewoehnlichen Gegners. Daran wird gemessen.</summary>
        public const float RunnerSpeed = 3.5f;

        /// <summary>
        /// Tempo eines Helden, der gerade angreift: teils gebunden, teils frei. Der gebundene Anteil
        /// kommt aus <see cref="MeleeApproach.BoundSeconds"/> und der Laenge des Kombos.
        /// </summary>
        public static float CombatSpeed(float baseSpeed, float boundShare)
        {
            var share = Mathf.Clamp01(boundShare);
            return baseSpeed * (share * MeleeApproach.BoundSpeed + (1f - share));
        }
    }
}
