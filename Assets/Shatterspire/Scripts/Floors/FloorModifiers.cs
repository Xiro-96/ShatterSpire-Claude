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
        /// Der Topf, aus dem gezogen wird. Die Ruhe steht zweimal darin: ohne stille Etagen
        /// verliert die Anomalie ihre Bedeutung, weil es keinen Normalzustand mehr gibt.
        /// </summary>
        private static readonly FloorModifierId[] Pool =
        {
            FloorModifierId.None, FloorModifierId.None, FloorModifierId.Overload,
            FloorModifierId.Brittle, FloorModifierId.Swarm, FloorModifierId.Bounty,
            FloorModifierId.Warded
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
            // Die Raumart geht als Salz ein: die drei Routen derselben Etage tragen dadurch
            // verschiedene Anomalien, und die Wahl hat zwei Achsen statt einer.
            return Pool[RunRandom.Index(runSeed, floor, (int)kind + 1, Pool.Length)];
        }

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
            var text = new StringBuilder();
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0) text.Append(i == parts.Count - 1 ? "\n" : "  ·  ");
                text.Append(parts[i]);
            }
            return text.ToString();
        }

        private static void Append(List<string> parts, string label, float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            parts.Add($"{Loc.T(label)} ×{Loc.Number(factor)}");
        }
    }
}
