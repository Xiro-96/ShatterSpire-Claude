using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Anomalien einer Etage. Neue kommen ans Ende, damit gespeicherte Zahlen ihre Bedeutung behalten.
    /// </summary>
    public enum FloorModifierId { None, Overload, Brittle, Swarm, Bounty, Warded }

    /// <summary>
    /// Was eine Anomalie an den Zahlen einer Etage dreht. Bewusst nur Faktoren und kein Verhalten:
    /// so laesst sich jede Anomalie ohne Unity nachrechnen, und die Anzeige kann ihre Beschreibung
    /// aus denselben Zahlen bauen, aus denen das Spiel rechnet - ein Text, der nicht luegen kann.
    /// </summary>
    public readonly struct FloorModifier
    {
        public readonly FloorModifierId Id;
        public readonly string Name;
        public readonly float EnemyHealth;
        public readonly float EnemyDamage;
        public readonly float EnemySpeed;
        public readonly float EnemyCount;
        public readonly float Gold;
        public readonly float Shards;
        public readonly Color Accent;

        public FloorModifier(FloorModifierId id, string name, Color accent, float enemyHealth = 1f,
            float enemyDamage = 1f, float enemySpeed = 1f, float enemyCount = 1f, float gold = 1f,
            float shards = 1f)
        {
            Id = id;
            Name = name;
            Accent = accent;
            EnemyHealth = enemyHealth;
            EnemyDamage = enemyDamage;
            EnemySpeed = enemySpeed;
            EnemyCount = enemyCount;
            Gold = gold;
            Shards = shards;
        }

        public bool IsCalm => Id == FloorModifierId.None;
    }

    /// <summary>
    /// Die Anomalien und ihre Zuteilung.
    ///
    /// Der Grund fuer das Ganze: die Wahl am Aufzug versprach drei verschiedene Raeume, unterschied
    /// sich aber nur in Gold und Heilung - man waehlte eine Beschriftung, nicht ein Spiel. Jetzt
    /// traegt jede Route ihre eigene Anomalie, die vor der Wahl offen im Bild steht. Damit ist die
    /// Entscheidung zweiachsig: welcher Raum, und welches Risiko.
    ///
    /// Die Zuteilung ist eine reine Funktion aus Lauf-Seed, Etage und Raumart. Kein
    /// <see cref="Random"/>: im Co-op muessen alle drei Spieler dieselben drei Angebote sehen, und
    /// ein Lauf-Seed muss eine ganze Etagenfolge reproduzieren koennen.
    /// </summary>
    public static class FloorModifierCatalog
    {
        private static readonly FloorModifier[] Table =
        {
            new(FloorModifierId.None, "CALM FLOOR", new Color(0.52f, 0.57f, 0.64f)),
            // Ueberladung: das Tempo steigt, nicht der Lebensbalken. Die Etage wird hektisch, nicht laenger.
            new(FloorModifierId.Overload, "OVERLOAD", new Color(1f, 0.52f, 0.1f),
                enemyDamage: 1.15f, enemySpeed: 1.3f, gold: 1.5f),
            // Glasbruch: beide Seiten sterben schnell. Die kuerzesten und schaerfsten Kaempfe im Spiel.
            new(FloorModifierId.Brittle, "GLASS BREAK", new Color(0.35f, 0.92f, 1f),
                enemyHealth: 0.5f, enemyDamage: 1.6f, gold: 1.35f),
            // Schwarm: viele schwache Gegner. Flaechenschaden und der Bomber gluehen hier auf.
            new(FloorModifierId.Swarm, "SWARM", new Color(0.3f, 0.85f, 0.45f),
                enemyHealth: 0.55f, enemyDamage: 0.85f, enemyCount: 1.85f, gold: 1.3f),
            // Kopfgeld: die Etage zahlt, das Toeten dauert. Wer sparen will, nimmt sie.
            new(FloorModifierId.Bounty, "BOUNTY", new Color(1f, 0.82f, 0.2f),
                enemyHealth: 1.25f, gold: 2.3f),
            // Bannkreis: zaeh und langsam. Zahlt in Splittern, also in Meta-Fortschritt statt in Gold.
            new(FloorModifierId.Warded, "WARDED", new Color(0.68f, 0.72f, 0.82f),
                enemyHealth: 1.5f, enemySpeed: 0.75f, shards: 1.35f)
        };

        /// <summary>
        /// Der Topf, aus dem die drei Routen einer Etage ziehen - ohne Zuruecklegen.
        ///
        /// Vorher zog jede Route fuer sich aus einem Topf, in dem die Ruhe zweimal stand. In 3,8 %
        /// aller Routenwahlen trugen dadurch alle drei Karten dieselbe Anomalie, und die Wahl hatte
        /// genau die zweite Achse verloren, fuer die es Anomalien gibt - auf einem Heroic-Aufstieg
        /// mit einem Drittel Wahrscheinlichkeit mindestens einmal. Jetzt sind die drei immer
        /// verschieden. Die Ruhe steht nur noch einmal darin: jede zweite Etage bietet sie als
        /// sichere Route an, die andere Haelfte hat keine.
        /// </summary>
        private static readonly FloorModifierId[] Pool =
        {
            FloorModifierId.None, FloorModifierId.Overload, FloorModifierId.Brittle,
            FloorModifierId.Swarm, FloorModifierId.Bounty, FloorModifierId.Warded
        };

        public static IReadOnlyList<FloorModifier> All => Table;

        public static FloorModifier For(FloorModifierId id)
        {
            for (var i = 0; i < Table.Length; i++)
                if (Table[i].Id == id) return Table[i];
            return Table[0];
        }

        /// <summary>
        /// Welche Anomalie eine Route traegt. Gleiche Eingaben, gleiches Ergebnis - immer.
        ///
        /// Etage 1 und Boss-Etagen bleiben ruhig: der erste Raum ist die Stelle, an der man die
        /// Steuerung lernt, und der Warden hat seine Phasen schon als eigenen Haken.
        /// </summary>
        public static FloorModifierId Offer(int runSeed, int floor, RoomKind kind)
        {
            if (floor <= 1 || kind == RoomKind.Boss) return FloorModifierId.None;
            return OffersFor(runSeed, floor)[RouteSlot(kind)];
        }

        /// <summary>
        /// Die drei Anomalien einer Etage, in der Reihenfolge der Routen am Aufzug: Kampf, Elite,
        /// dann Schatz oder Geheimnis. Ohne Zuruecklegen gezogen, also immer drei verschiedene.
        /// </summary>
        public static FloorModifierId[] OffersFor(int runSeed, int floor)
        {
            var bag = new List<FloorModifierId>(Pool);
            var offers = new FloorModifierId[RouteSlots];
            for (var slot = 0; slot < RouteSlots; slot++)
            {
                var index = RunRandom.Index(runSeed, floor, 300 + slot, bag.Count);
                offers[slot] = bag[index];
                bag.RemoveAt(index);
            }
            return offers;
        }

        private const int RouteSlots = 3;

        /// <summary>
        /// Welcher Platz am Aufzug eine Raumart ist. Schatz und Geheimnis teilen sich den dritten:
        /// eine Etage bietet immer nur einen der beiden an.
        /// </summary>
        private static int RouteSlot(RoomKind kind) => kind switch
        {
            RoomKind.Combat => 0,
            RoomKind.Elite => 1,
            _ => 2
        };

        /// <summary>
        /// Die Wirkung in Worten, aus den Faktoren selbst gebaut. Wer eine Zahl in
        /// <see cref="Table"/> aendert, aendert damit auch den Text - er kann nicht veralten.
        /// </summary>
        public static string Effects(in FloorModifier modifier)
        {
            if (modifier.IsCalm) return Loc.T("NO ANOMALY");
            var parts = new List<string>(4);
            Append(parts, "ENEMY HEALTH", modifier.EnemyHealth);
            Append(parts, "ENEMY DAMAGE", modifier.EnemyDamage);
            Append(parts, "ENEMY SPEED", modifier.EnemySpeed);
            Append(parts, "ENEMY COUNT", modifier.EnemyCount);
            Append(parts, "GOLD", modifier.Gold);
            Append(parts, "SHARDS", modifier.Shards);
            // Eine Wirkung je Zeile. Vorher standen sie mit Punkten hintereinander, und die Karte
            // brach mitten in der Aufzaehlung um.
            return string.Join("\n", parts);
        }

        /// <summary>
        /// Eine Wirkung als Zeile: Name, Aenderung in Prozent, und die Farbe dessen, was sie fuer den
        /// Spieler bedeutet.
        ///
        /// Vorher stand hier ein Faktor - "GEGNERTEMPO ×0,75". Ob das hilft oder schadet, musste man
        /// selbst ausrechnen, und bei drei Karten mit je drei Zahlen tat das niemand. Jetzt sagt die
        /// Farbe es: rot schadet, gruen hilft, gold ist die Belohnung.
        /// </summary>
        private static void Append(List<string> parts, string label, float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            var color = Verdict(label, factor) switch
            {
                EffectVerdict.Harmful => HarmfulColor,
                EffectVerdict.Helpful => HelpfulColor,
                _ => RewardColor
            };
            parts.Add($"<color={color}>{Loc.T(label)} {Percent(factor)}</color>");
        }

        private const string HarmfulColor = "#FF7B7B";
        private const string HelpfulColor = "#8CF5A0";
        private const string RewardColor = "#FFD36B";

        /// <summary>Was eine Wirkung fuer den Spieler bedeutet.</summary>
        public enum EffectVerdict { Harmful, Helpful, Reward }

        /// <summary>
        /// Mehr Gegnerleben, -schaden, -tempo oder -zahl schadet, weniger hilft. Mehr Gold oder
        /// Splitter ist Belohnung, weniger schadet.
        /// </summary>
        public static EffectVerdict Verdict(string label, float factor)
        {
            var reward = label == "GOLD" || label == "SHARDS";
            if (reward) return factor >= 1f ? EffectVerdict.Reward : EffectVerdict.Harmful;
            return factor > 1f ? EffectVerdict.Harmful : EffectVerdict.Helpful;
        }

        /// <summary>Ein Faktor als ganze Prozent mit Vorzeichen: 1,5 wird "+50%", 0,75 wird "-25%".</summary>
        public static string Percent(float factor)
        {
            var percent = Mathf.RoundToInt((factor - 1f) * 100f);
            return (percent > 0 ? "+" : percent < 0 ? "-" : string.Empty) + Mathf.Abs(percent) + "%";
        }
    }
}
