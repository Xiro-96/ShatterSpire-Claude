using System;
using System.IO;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der Selbsttest: das Spiel spielt einen Aufstieg ohne Menschen und schreibt mit, was passiert.
    ///
    /// Anlass war eine Woche, in der drei von sieben Commits Reparaturen waren - der Dash auf den
    /// Gegner, das Richturteil, das von selbst losging, die Begleiter auf dem eigenen Knopf. Alle
    /// drei hat der Spieler auf dem Telefon gefunden, und alle drei lagen im Zusammenspiel, das keine
    /// Rechnung sieht. Seit dem Gruppen-Umbau kann ein Kopf jeden Helden steuern, auch den eigenen,
    /// durch dieselbe Schnittstelle wie der Daumen. Also spielt ein Kopf, und ein Protokoll schreibt
    /// mit.
    ///
    /// Nur mit dem Startparameter <c>-shatterspire-autoplay [Ordner]</c>. Weitere:
    ///   -shatterspire-hero Ranger|Guardian|Arcanist|Bomber|Paladin
    ///   -shatterspire-path Brave|Heroic|Legendary
    ///   -shatterspire-floors N     (wie viele Etagen, Vorgabe 5)
    ///   -shatterspire-minutes N    (Obergrenze in echter Zeit, Vorgabe 12)
    ///   -shatterspire-seed N       (fester Lauf, damit zwei Durchgaenge vergleichbar sind)
    ///
    /// Der Selbsttest liest und schreibt keinen Spielstand (<see cref="MetaSaveSystem.Ephemeral"/>):
    /// jeder Lauf beginnt mit einem frischen Helden, und kein Lauf veraendert den naechsten.
    /// </summary>
    public static class Autoplay
    {
        private const string Flag = "-shatterspire-autoplay";

        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0;

        public static string Folder
        {
            get
            {
                var value = Arg(Flag);
                return !string.IsNullOrEmpty(value) ? value : Path.Combine(Application.persistentDataPath, "autoplay");
            }
        }

        public static HeroClassId Hero
            => Enum.TryParse<HeroClassId>(Arg("-shatterspire-hero"), true, out var hero) ? hero : HeroClassId.Ranger;

        public static RunMode RunPath => Enum.TryParse<RunMode>(Arg("-shatterspire-path"), true, out var path) ? path : RunMode.Brave;

        public static int Floors => int.TryParse(Arg("-shatterspire-floors"), out var floors) ? Mathf.Max(1, floors) : 5;

        public static float Minutes
            => float.TryParse(Arg("-shatterspire-minutes"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var minutes) ? Mathf.Max(1f, minutes) : 12f;

        /// <summary>Der Wert hinter einem Startparameter, oder leer.</summary>
        private static string Arg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("-")
                ? args[index + 1]
                : string.Empty;
        }
    }

    /// <summary>
    /// Welchen Knopf der Autopilot in einem Auswahlfenster drueckt. Eine reine Rechnung, damit sie
    /// sich ohne laufendes Spiel pruefen laesst - die Fenster halten das Spiel an, und ein falscher
    /// Knopf hier hiesse ein Selbsttest, der im ersten Fenster stehen bleibt.
    /// </summary>
    public static class AutoPilotChoices
    {
        /// <summary>
        /// Der Knopf fuer dieses Fenster, oder -1 fuer "nichts druecken".
        /// </summary>
        /// <param name="buttons">Wie viele Knoepfe das Fenster hat.</param>
        /// <param name="floor">Die Etage, die gerade endet - daran wechselt die Route.</param>
        public static int ButtonFor(ModalKind kind, int buttons, int floor)
        {
            if (buttons <= 0) return -1;
            return kind switch
            {
                // Das erste Upgrade. Welches, ist fuer den Selbsttest gleich; dass es gewaehlt wird, nicht.
                ModalKind.Perk => 0,
                // Der letzte Knopf fuehrt weiter. Einkaufen laesst der Autopilot aus: ohne Waren misst
                // er die Grundschwierigkeit, und zwei Laeufe bleiben vergleichbar. Was ein Mensch
                // kauft, haengt an Vorlieben - ein echter Aufstieg ist also eher leichter als dieser.
                ModalKind.Shop => buttons - 1,
                // Die Routen im Wechsel, damit ueber einen Aufstieg mehr als eine Raumart vorkommt.
                ModalKind.Routes => Mathf.Abs(floor) % buttons,
                // Weiterklettern, wenn es geht. Der erste Knopf waere das Aussteigen.
                ModalKind.Ascension => buttons >= 2 ? 1 : 0,
                // Das Ende eines Aufstiegs und die Pause bedient der Autopilot nicht - das Ende
                // meldet das Protokoll, eine Pause darf im Selbsttest gar nicht erst aufgehen.
                _ => -1
            };
        }
    }
}
