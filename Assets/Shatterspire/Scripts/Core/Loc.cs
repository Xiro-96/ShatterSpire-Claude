using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public enum Language { German, English }

    /// <summary>
    /// Sprache der Oberflaeche. Deutsch ist der Standard, Englisch bleibt umschaltbar.
    ///
    /// Die Tabelle ist nach dem englischen Original verschluesselt, nicht nach erfundenen Schluesseln.
    /// Das hat einen praktischen Grund: die Texte stehen an rund hundertfuenfzig Stellen im Code, und
    /// ein fehlender Eintrag faellt so nicht stumm aus, sondern erscheint sichtbar auf Englisch und
    /// kann nachgetragen werden. Zusammengesetzte Anzeigen uebersetzen ihre festen Teile einzeln.
    /// </summary>
    public static class Loc
    {
        private const string Preference = "shatterspire.language";
        private static Language language = Load();

        public static Language Language
        {
            get => language;
            set
            {
                if (language == value) return;
                language = value;
                PlayerPrefs.SetInt(Preference, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Wird ausgeloest, wenn die Sprache wechselt - die Oberflaeche baut sich dann neu auf.</summary>
        public static event System.Action Changed;

        public static string LanguageName => language == Language.German ? "DEUTSCH" : "ENGLISH";

        public static void Toggle() => Language = language == Language.German ? Language.English : Language.German;

        private static Language Load()
            => (Language)Mathf.Clamp(PlayerPrefs.GetInt(Preference, (int)Language.German), 0, 1);

        /// <summary>Uebersetzt einen Oberflaechentext. Ohne Eintrag bleibt das Original stehen.</summary>
        public static string T(string english)
        {
            if (language == Language.English || string.IsNullOrEmpty(english)) return english;
            return German.TryGetValue(english, out var translated) ? translated : english;
        }

        /// <summary>Raumart, wie sie im Kopf der Etage steht.</summary>
        public static string Of(RoomKind kind) => T(kind.ToString().ToUpperInvariant());

        /// <summary>Rolle eines Bots in der Team-Leiste.</summary>
        public static string Of(CompanionRole role) => T(role.ToString().ToUpperInvariant());

        private static readonly Dictionary<string, string> German = new()
        {
            // ── Etage und Ziele ─────────────────────────────────────────
            { "FLOOR", "ETAGE" },
            { "FLOOR 1 / 15", "ETAGE 1 / 15" },
            { "FIND THE RIFT CELLS", "FINDE DIE ENERGIEKERNE" },
            { "FIND THE POWER CORES", "FINDE DIE ENERGIEKERNE" },
            { "FIND THE NEXT POWER CORE", "FINDE DEN NÄCHSTEN KERN" },
            { "DEFEND POWER CORE", "VERTEIDIGE KERN" },
            { "STAND IN THE RING TO ACTIVATE", "IM RING STEHEN BLEIBEN" },
            { "TOWER LIFT OPEN", "AUFZUG OFFEN" },
            { "TOWER LIFT", "TURMAUFZUG" },
            { "FLOOR SECURED", "ETAGE GESICHERT" },
            { "POWER CORE", "ENERGIEKERN" },
            { "ACTIVATE", "AKTIVIEREN" },
            { "TOWER PATH CLEARED", "TURMPFAD GESCHAFFT" },
            { "THE TEAM RETURNS WITH SECURED SHARDS", "DAS TEAM KEHRT MIT GESICHERTEN SPLITTERN ZURÜCK" },

            // ── Raumarten ───────────────────────────────────────────────
            { "COMBAT", "KAMPF" },
            { "ELITE", "ELITE" },
            { "TREASURE", "SCHATZ" },
            { "MYSTERY", "GEHEIMNIS" },
            { "BOSS", "BOSS" },

            // ── Boss ────────────────────────────────────────────────────
            { "IRON WARDEN  ·  BOSS ENGAGED", "EISENWÄCHTER  ·  BOSSKAMPF" },
            { "IRON WARDEN  ·  PHASE", "EISENWÄCHTER  ·  PHASE" },
            { "DEFEAT THE SPIRE WARDEN", "BESIEGE DEN TURMWÄCHTER" },
            { "SPIRE WARDEN", "TURMWÄCHTER" },

            // ── Kampf-HUD ───────────────────────────────────────────────
            { "ULTIMATE READY", "ULTIMATE BEREIT" },
            { "TAP ULTIMATE", "ULTIMATE ANTIPPEN" },
            { "PRESS R", "R DRÜCKEN" },
            { "LIGHT HITS CHARGE HEAVY", "LEICHTE TREFFER LADEN DEN SCHWEREN" },
            { "RELEASE IN GOLD", "IM GOLDENEN FENSTER LOSLASSEN" },
            { "PERFECT!  RELEASE", "PERFEKT!  LOSLASSEN" },
            { "PERFECT!  RELEASE RMB", "PERFEKT!  RECHTE MAUSTASTE LOSLASSEN" },
            { "NO UPGRADES YET", "NOCH KEINE VERBESSERUNGEN" },
            { "UPGRADES SHOWN ON YOUR ACTIONS", "VERBESSERUNGEN STEHEN AN DEINEN AKTIONEN" },
            { "TEAM LIVES", "TEAM-LEBEN" },
            { "ALLY REVIVING", "VERBÜNDETER BELEBT" },
            { "LV", "STUFE" },
            { "DASH", "DASH" },

            // ── Bot-Zustaende ───────────────────────────────────────────
            { "FOLLOWING", "FOLGT" },
            { "HEALING", "HEILT" },
            { "FRONTLINE", "FRONT" },
            { "COVERING", "DECKT" },
            { "FLANKING", "FLANKE" },
            { "GUARDIAN", "WÄCHTER" },
            { "SUPPORT", "UNTERSTÜTZUNG" },
            { "RANGER", "JÄGER" },

            // ── Menue ───────────────────────────────────────────────────
            { "CLIMB  ·  ADAPT  ·  RISK IT ALL", "STEIGEN  ·  ANPASSEN  ·  ALLES WAGEN" },
            { "BEGIN CLIMB", "AUFSTIEG STARTEN" },
            { "CHOOSE YOUR PATH", "WÄHLE DEINEN PFAD" },
            { "HIGHEST RANK", "BESTER RANG" },
            { "SHARDS", "SPLITTER" },
            { "TOKENS", "MARKEN" },
            { "META FORGE", "SCHMIEDE" },
            { "RELIC LOADOUT", "RELIKTE" },
            { "SHIFT COMPLETE", "SCHICHT ABGESCHLOSSEN" },
            { "BACK TO LOBBY", "ZURÜCK ZUR LOBBY" },
            { "DONE", "FERTIG" },
            { "MAXIMUM", "MAXIMUM" },
            { "UPGRADE", "VERBESSERN" },
            { "YOU", "DU" },
            { "BOT", "BOT" },
            { "ENDLESS  ·  RISING REWARDS", "ENDLOS  ·  STEIGENDE BELOHNUNGEN" },
            { "LANGUAGE", "SPRACHE" },

            // ── Haendler ────────────────────────────────────────────────
            { "TRADER", "HÄNDLER" },
            { "GOLD", "GOLD" },
            { "SPEND GOLD OR KEEP IT FOR LATER", "GOLD AUSGEBEN ODER FÜR SPÄTER BEHALTEN" },
            { "SOLD OUT", "AUSVERKAUFT" },
            { "OWNED", "BESITZ" },
            { "CONTINUE CLIMB", "WEITER AUFSTEIGEN" },
            { "WHETSTONE", "WETZSTEIN" },
            { "All damage +12%.", "Gesamter Schaden +12 %." },
            { "OILED GEARS", "GEÖLTES GETRIEBE" },
            { "Attack speed +8%.", "Angriffsgeschwindigkeit +8 %." },
            { "IRON RATION", "EISERNE RATION" },
            { "Maximum health +25, healed.", "Maximales Leben +25, sofort geheilt." },
            { "FOCUS LENS", "FOKUSLINSE" },
            { "Critical chance +6%.", "Kritische Chance +6 %." },
            { "COUNTERWEIGHT", "GEGENGEWICHT" },
            { "Heavy attack damage +18%.", "Schaden schwerer Angriffe +18 %." },
            { "FORGED WEAPON", "GESCHMIEDETE WAFFE" },
            { "Damage +30%, attack speed -6%. Once per climb.", "Schaden +30 %, Angriffsgeschwindigkeit -6 %. Einmal je Aufstieg." },

            // ── Relikte ──────────────────────────────────────
            { "RELICS", "RELIKTE" },
            { "+ EMPTY", "+ LEER" },
            { "EQUIPPED  ·  CARRIED INTO EVERY CLIMB", "AUSGERÜSTET  ·  GILT FÜR JEDEN AUFSTIEG" },
            { "WINDSTEP SIGIL", "WINDSCHRITT-SIEGEL" },
            { "+1 dash charge", "+1 Dash-Ladung" },
            { "HUNTER'S MARK", "JÄGERMAL" },
            { "+10% critical chance", "+10 % kritische Chance" },
            { "DAWN SEED", "MORGENSAAT" },
            { "Heal 10 after every floor", "Heilt 10 nach jeder Etage" },
            { "EMBER LENS", "GLUTLINSE" },
            { "Heavy attacks erupt on impact", "Schwere Angriffe detonieren beim Einschlag" },
            { "ARC BATTERY", "LICHTBOGEN-BATTERIE" },
            { "Heavy meter charges 25% faster", "Der schwere Balken lädt 25 % schneller" },
            { "FORTUNE PRISM", "GLÜCKSPRISMA" },
            { "+25% secured shards", "+25 % gesicherte Splitter" },
            { "IRON HEART", "EISERNES HERZ" },
            { "+30 maximum health", "+30 maximales Leben" },
            { "SWIFT BOOTS", "FLINKE STIEFEL" },
            { "+12% movement speed", "+12 % Laufgeschwindigkeit" },
            { "VENGEANCE COIL", "ZORNSPULE" },
            { "+15% damage below 40% health", "+15 % Schaden unter 40 % Leben" },
            { "SIPHON STONE", "SAUGSTEIN" },
            { "Heal 3% of damage dealt", "Heilt 3 % des verursachten Schadens" },
            { "FOCUS CRYSTAL", "FOKUSKRISTALL" },
            { "Skill cooldown 20% shorter", "Abklingzeit der Fähigkeit 20 % kürzer" },
            { "SURGE CORE", "STROMKERN" },
            { "Ultimate charges 20% faster", "Ultimate lädt 20 % schneller" },
            { "TWIN CHARGE", "DOPPELLADUNG" },
            { "Light attacks 10% faster", "Leichte Angriffe 10 % schneller" },
            { "GUARD PLATE", "SCHUTZPLATTE" },
            { "Take 10% less damage", "10 % weniger erlittener Schaden" },
            { "GOLD VEIN", "GOLDADER" },
            { "+30% gold from enemies", "+30 % Gold aus Gegnern" },

            // ── Lobby und Schmiede ────────────────────────
            { "ARROW KEYS HINT", "← →  HELD      ↑ ↓  PFAD      R  RELIKTE      F  SCHMIEDE      ENTER  AUFSTIEG" },
            { "CLIMB", "AUFSTIEG" },
            { "SHIFT", "SCHICHT" },
            { "ENDS IN", "ENDET IN" },
            { "TO", "BIS" },
            { "RANK POINTS", "RANGPUNKTE" },
            { "RANK", "RANG" },
            { "LEVEL", "STUFE" },
            { "HP", "LEBEN" },
            { "PERMANENT, CAPPED BONUSES", "DAUERHAFT, MIT OBERGRENZE" },
            { "+5 MAX HP / LEVEL", "+5 MAX. LEBEN / STUFE" },
            { "+4% DAMAGE / LEVEL", "+4 % SCHADEN / STUFE" },
            { "+2% MOVE SPEED / LEVEL", "+2 % LAUFTEMPO / STUFE" },
            { "FIVE FLOORS  ·  ONE BOSS", "FÜNF ETAGEN  ·  EIN BOSS" },
            { "FIFTEEN FLOORS  ·  THREE BOSSES", "FÜNFZEHN ETAGEN  ·  DREI BOSSE" },
            { "CLIMB AGAIN", "NOCHMAL AUFSTEIGEN" },
            { "MAIN MENU", "HAUPTMENÜ" },
            { "LEVEL UP · CHOOSE AN UPGRADE", "STUFE AUFGESTIEGEN · VERBESSERUNG WÄHLEN" },
            { "FLOOR CLEARED · CHOOSE AN UPGRADE", "ETAGE GESCHAFFT · VERBESSERUNG WÄHLEN" },
            { "EVERY UPGRADE CHANGES ONE OF YOUR ACTIONS", "JEDE VERBESSERUNG VERÄNDERT EINE DEINER AKTIONEN" },

            // ── Helden ──────────────────────────────────────────────────
            { "FORGE GUARDIAN", "SCHMIEDE-WÄCHTER" },
            { "RIFT RANGER", "RISS-JÄGER" },
            { "DAWN WEAVER", "MORGENWEBERIN" },
            { "SPIRE", "TURM" },

            // ── Heldenaktionen und Etagen ───────────────
            { "RIFT ARCANIST", "RISS-MAGIER" },
            { "HAMMER COMBO  ·  PERFECT SLAM  ·  BULL RUSH", "HAMMER-KOMBO  ·  PERFEKTER SCHLAG  ·  STURMANGRIFF" },
            { "ARC BOLTS  ·  GRAVITY BURST  ·  BLACK STAR", "LICHTBOGEN  ·  GRAVITATIONSSTOSS  ·  SCHWARZER STERN" },
            { "RIFT ARROWS  ·  PIERCING DRAW  ·  ARROW STORM", "RISSPFEILE  ·  DURCHBOHRENDER ZUG  ·  PFEILSTURM" },
            { "HAMMER", "HAMMER" },
            { "ARC BOLT", "LICHTBOGEN" },
            { "GROUND BREAKER", "ERDBRECHER" },
            { "GRAVITY BURST", "GRAVITATIONSSTOSS" },
            { "BULL RUSH", "STURMANGRIFF" },
            { "BLACK STAR", "SCHWARZER STERN" },
            { "FORGE QUAKE", "SCHMIEDEBEBEN" },
            { "SINGULARITY", "SINGULARITÄT" },
            { "RIFT BARRAGE", "RISSALVE" },
            { "THE EMBER FOUNDRY", "DIE GLUTSCHMIEDE" },
            { "THE ASTRAL ARCHIVE", "DAS ASTRALE ARCHIV" },
            { "THE FORGOTTEN COURT", "DER VERGESSENE HOF" },
            { "THE VERDANT FORGE", "DIE GRÜNE SCHMIEDE" },
            { "THE ASCENSION GATE", "DAS AUFSTIEGSTOR" },
            { "5 FLOORS  ·  SHORT CLIMB", "5 ETAGEN  ·  KURZER AUFSTIEG" },
            { "15 FLOORS  ·  3 WARDENS", "15 ETAGEN  ·  3 WÄCHTER" },

            // ── Aktionsnamen ────────────────────────────────────────────
            { "LIGHT", "LEICHT" },
            { "HEAVY", "SCHWER" },
            { "SKILL", "FÄHIGKEIT" },
            { "ULTIMATE", "ULTIMATE" },
            { "PASSIVE", "PASSIV" },
            { "LIGHT · ", "LEICHT · " },
            { "HEAVY · ", "SCHWER · " },
            { "SKILL · ", "FÄHIGKEIT · " },
            { "ULTIMATE · ", "ULTIMATE · " },
            { "HEAVY · GROUND BREAKER", "SCHWER · ERDBRECHER" },

            // ── Verbesserungen: Namen ───────────────────────────────────
            { "TEMPERED POWER", "GEHÄRTETE KRAFT" },
            { "TEMPERED EDGE", "GEHÄRTETE SCHNEIDE" },
            { "VITAL CORE", "LEBENSKERN" },
            { "WIND GLYPH", "WINDGLYPHE" },
            { "BATTLE RHYTHM", "KAMPFRHYTHMUS" },
            { "CRUSHING FORCE", "ZERMALMENDE KRAFT" },
            { "RAPID CHARGE", "SCHNELLE LADUNG" },
            { "OVERCHARGE", "ÜBERLADUNG" },
            { "QUICK CAST", "SCHNELLZAUBER" },
            { "SURGE CELL", "STROMZELLE" },
            { "AEGIS BATTERY", "AEGIS-BATTERIE" },
            { "SECOND WIND", "ZWEITER ATEM" },
            { "SIPHON RUNE", "SAUGRUNE" },
            { "FINISHER", "ABSCHLUSS" },
            { "DEADEYE", "SCHARFSCHÜTZE" },
            { "HOLLOW POINT", "HOHLSPITZE" },
            { "SPLIT FINISHER", "GETEILTER ABSCHLUSS" },
            { "TWIN FANG", "ZWILLINGSZAHN" },
            { "SUNDER", "SPALTUNG" },
            { "CLEAVING WAVE", "SPALTWELLE" },
            { "EARTHSPLITTER", "ERDSPALTER" },
            { "MOLTEN QUAKE", "GLUTBEBEN" },
            { "EMBER CORE", "GLUTKERN" },
            { "CRYO CORE", "FROSTKERN" },
            { "TOXIN CORE", "GIFTKERN" },
            { "STORM CORE", "STURMKERN" },
            { "STORM STEP", "STURMSCHRITT" },
            { "KINETIC BOOTS", "WUCHTSTIEFEL" },
            { "PHASE RIFT", "PHASENRISS" },
            { "SHOULDER CHARGE", "SCHULTERSTOSS" },
            { "PARTING SHOT", "ABSCHIEDSSCHUSS" },
            { "SEEKER LINK", "SUCHERBINDUNG" },
            { "SEEKING ECHO", "SUCHENDES ECHO" },
            { "HOMING BARRAGE", "ZIELSUCHENDE SALVE" },
            { "RAIL SHOT", "SCHIENENSCHUSS" },
            { "ARROW RAIN", "PFEILREGEN" },
            { "ARROW STORM", "PFEILSTURM" },
            { "RIFT ARROW", "RISSPFEIL" },
            { "PIERCING DRAW", "DURCHBOHRENDER ZUG" },
            { "VOID BURST", "LEERENSTOSS" },
            { "VOLATILE IMPACT", "INSTABILER EINSCHLAG" },
            { "COLLAPSING STAR", "KOLLABIERENDER STERN" },
            { "LINGERING STAR", "VERWEILENDER STERN" },
            { "EVENT HORIZON", "EREIGNISHORIZONT" },
            { "PERFECT ECHO", "PERFEKTES ECHO" },
            { "AFTERGLOW", "NACHGLUT" },
            { "BULWARK", "BOLLWERK" },
            { "SPLINTER", "SPLITTERUNG" },

            // ── Verbesserungen: Beschreibungen ──────────────────────────
            { "Heavy attacks deal 40% more damage.", "Schwere Angriffe machen 40 % mehr Schaden." },
            { "Light attacks are 22% faster.", "Leichte Angriffe sind 22 % schneller." },
            { "Light hits fill the Heavy meter 30% faster.", "Leichte Treffer laden den schweren Balken 30 % schneller." },
            { "Your Ultimate charges 30% faster.", "Deine Ultimate lädt 30 % schneller." },
            { "Skill cooldown is 25% shorter.", "Die Abklingzeit der Fähigkeit ist 25 % kürzer." },
            { "Every Skill cast charges your Ultimate by 10%.", "Jede Fähigkeit lädt die Ultimate um 10 %." },
            { "Gain 25 maximum health and heal it.", "25 Leben mehr, sofort geheilt." },
            { "Heal for 4% of damage dealt.", "Heilt um 4 % des verursachten Schadens." },
            { "Deal double damage to enemies below 20% health.", "Doppelter Schaden gegen Gegner unter 20 % Leben." },
            { "Casting your Ultimate heals you for 30% of your max health.", "Die Ultimate heilt dich um 30 % deines Lebens." },
            { "Shots pierce one more enemy. Hammer swings reach further.", "Schüsse durchbohren einen Gegner mehr. Hammerschläge reichen weiter." },
            { "Shots bounce to a second enemy. Hammer finishers echo.", "Schüsse springen auf einen zweiten Gegner. Hammer-Abschlüsse hallen nach." },
            { "Fires a second projectile. Hammer swings leave an aftershock.", "Feuert ein zweites Geschoss. Hammerschläge lassen ein Nachbeben zurück." },
            { "Shots erupt for area damage on impact.", "Schüsse brechen beim Einschlag in Flächenschaden auf." },
            { "Shots curve toward nearby enemies.", "Schüsse ziehen zu nahen Gegnern." },
            { "Attacks ignite. Combines with Volatile Impact.", "Angriffe entzünden. Wirkt mit Instabilem Einschlag zusammen." },
            { "Attacks slow enemies. Critical hits shatter with Deadeye.", "Angriffe verlangsamen. Kritische Treffer zersplittern mit Scharfschütze." },
            { "Attacks apply stacking poison damage.", "Angriffe vergiften, der Schaden stapelt sich." },
            { "Attacks may call chain lightning.", "Angriffe rufen manchmal Kettenblitze." },
            { "Every Dash ends in a lightning burst.", "Jeder Dash endet in einem Blitzschlag." },
            { "Dashing leaves a rift behind that detonates.", "Der Dash lässt einen Riss zurück, der detoniert." },
            { "Dashing slams every enemy in your path.", "Der Dash rammt jeden Gegner auf deinem Weg." },
            { "Dashing fires a fan of five arrows toward your aim.", "Der Dash feuert fünf Pfeile in deine Zielrichtung." },
            { "Bull Rush ends in a slam and makes you invulnerable for 1.5 s.", "Der Sturmangriff endet in einem Schlag und macht dich 1,5 s unverwundbar." },
            { "A Perfect Heavy strikes again: a second blast or two extra arrows.", "Ein perfekter schwerer Schlag trifft erneut: ein zweiter Stoß oder zwei zusätzliche Pfeile." },
            { "The Hammer finisher sends a ground wave forward.", "Der Hammer-Abschluss schickt eine Bodenwelle nach vorn." },
            { "Forge Quake shockwaves set enemies on fire.", "Die Schockwellen des Schmiedebebens setzen Gegner in Brand." },
            { "Ground Breaker tears a line of eruptions along your aim.", "Der Erdbrecher reißt eine Reihe von Ausbrüchen in deine Zielrichtung." },
            { "Every third Rift Arrow splits into a fan of three.", "Jeder dritte Risspfeil teilt sich in drei." },
            { "Piercing Draw always fires three arrows that pierce everything.", "Der durchbohrende Zug feuert immer drei Pfeile, die alles durchdringen." },
            { "Arrow Storm fires five wider volleys that follow your aim.", "Der Pfeilsturm feuert fünf breitere Salven, die deinem Ziel folgen." },
            { "Rift Barrage arrows seek out enemies.", "Die Pfeile der Rissalve suchen sich Gegner." },
            { "Arc Bolts burst on impact and hit nearby enemies.", "Lichtbogen platzen beim Einschlag und treffen Gegner in der Nähe." },
            { "Gravity Burst detonates a second time further along your aim.", "Der Gravitationsstoß detoniert ein zweites Mal weiter in Zielrichtung." },
            { "Black Star pulses seven times and drifts toward your aim.", "Der Schwarze Stern pulsiert siebenmal und zieht in deine Zielrichtung." },
            { "Singularity pulls enemies into its center.", "Die Singularität zieht Gegner in ihre Mitte." }
        };
    }
}
