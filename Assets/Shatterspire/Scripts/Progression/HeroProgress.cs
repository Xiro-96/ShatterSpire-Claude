using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Dauerhafter Fortschritt je Held.
    ///
    /// Bisher war aller Fortschritt zwischen den Laeufen gemeinsam: Splitter, drei Meta-Upgrades,
    /// Relikte - wer den Wächter spielte, machte damit auch den Jäger staerker. Ein Held, den man
    /// oft spielt, sollte sich davon abheben, und ein neuer Held sollte etwas zu erreichen haben.
    /// Das ist der Grund, warum man in R.I.S.E. einen Lieblingshelden hat und nicht einen
    /// Lieblings-Statblock.
    ///
    /// Die Zahlen sind bewusst klein gehalten: der Unterschied zwischen Stufe 1 und Stufe 15 soll
    /// spuerbar sein, aber kein Ersatz fuer Spielen. Der groessere Teil der Belohnung ist der Rang
    /// am Helden, nicht der Aufschlag auf den Balken.
    /// </summary>
    public static class HeroProgress
    {
        /// <summary>Hoechste Stufe. Darueber sammelt der Held weiter, aber ohne Wirkung.</summary>
        public const int MaximumLevel = 15;

        /// <summary>Erfahrung fuer den Sprung von Stufe 1 auf 2.</summary>
        public const int FirstStep = 900;

        /// <summary>
        /// Wie stark die Kosten je Stufe wachsen. 1,22 heisst: Stufe 15 kostet rund das
        /// Sechzehnfache von Stufe 2, und der ganze Weg liegt bei etwa 90 000 Erfahrung.
        /// </summary>
        public const float Growth = 1.22f;

        /// <summary>Leben je Stufe ueber der ersten, als Anteil.</summary>
        public const float HealthPerLevel = 0.025f;

        /// <summary>Schaden je Stufe ueber der ersten, als Anteil.</summary>
        public const float DamagePerLevel = 0.02f;

        /// <summary>
        /// Erfahrung aus einem Aufstieg. Die Punktzahl ist bereits das Mass fuer einen guten Lauf -
        /// Etagen, Bosse, Gegner, Serie, und der Faktor fuers Aussteigen. Ein eigenes Mass daneben
        /// waere eine zweite Wahrheit, die irgendwann von der ersten abweicht.
        /// </summary>
        public static int ExperienceFor(in ClimbResult result)
            => Mathf.Max(0, ClimbScore.Evaluate(result) / 10);

        /// <summary>Erfahrung, die von Stufe <paramref name="level"/> zur naechsten fehlt.</summary>
        public static int StepCost(int level)
        {
            if (level < 1) level = 1;
            if (level >= MaximumLevel) return 0;
            return Mathf.RoundToInt(FirstStep * Mathf.Pow(Growth, level - 1));
        }

        /// <summary>Gesamte Erfahrung, die bis zum Erreichen von <paramref name="level"/> noetig ist.</summary>
        public static int TotalFor(int level)
        {
            var total = 0;
            for (var step = 1; step < Mathf.Clamp(level, 1, MaximumLevel); step++) total += StepCost(step);
            return total;
        }

        public static int LevelFor(int experience)
        {
            var level = 1;
            var remaining = Mathf.Max(0, experience);
            while (level < MaximumLevel)
            {
                var cost = StepCost(level);
                if (remaining < cost) break;
                remaining -= cost;
                level++;
            }
            return level;
        }

        /// <summary>Fortschritt innerhalb der laufenden Stufe, 0 bis 1. Auf der letzten immer 1.</summary>
        public static float ProgressInLevel(int experience)
        {
            var level = LevelFor(experience);
            if (level >= MaximumLevel) return 1f;
            var cost = StepCost(level);
            if (cost <= 0) return 1f;
            return Mathf.Clamp01((experience - TotalFor(level)) / (float)cost);
        }

        /// <summary>Erfahrung bis zur naechsten Stufe, 0 auf der letzten.</summary>
        public static int ToNextLevel(int experience)
        {
            var level = LevelFor(experience);
            if (level >= MaximumLevel) return 0;
            return Mathf.Max(0, TotalFor(level + 1) - experience);
        }

        public static float HealthBonus(int level) => (Mathf.Clamp(level, 1, MaximumLevel) - 1) * HealthPerLevel;

        public static float DamageBonus(int level) => (Mathf.Clamp(level, 1, MaximumLevel) - 1) * DamagePerLevel;

        /// <summary>
        /// Der Rang am Helden. Reine Auszeichnung ohne Zahlenwirkung - das ist Absicht: was man
        /// vorzeigt, muss nicht auch staerker machen, sonst wird jede Auszeichnung zur Pflicht.
        /// </summary>
        public static string Title(int level) => level switch
        {
            >= MaximumLevel => "LEGEND",
            >= 10 => "MASTER",
            >= 5 => "VETERAN",
            >= 3 => "ADEPT",
            _ => "NOVICE"
        };

        public static Color TitleColor(int level) => level switch
        {
            >= MaximumLevel => new Color(1f, 0.83f, 0.22f),
            >= 10 => new Color(0.72f, 0.38f, 1f),
            >= 5 => new Color(0.18f, 0.72f, 1f),
            >= 3 => new Color(0.42f, 0.86f, 0.56f),
            _ => new Color(0.6f, 0.64f, 0.7f)
        };
    }
}
