using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Statblock eines Archetyps, unabhängig von Unity-Assets lesbar.</summary>
    public readonly struct EnemyStats
    {
        public readonly EnemyKind Kind;
        public readonly float Health;
        public readonly float Speed;
        public readonly float AttackRange;
        public readonly float AttackDamage;
        public readonly float KnockbackResistance;
        public readonly float StaggerSeconds;
        public readonly float SeparationSpacing;
        public readonly float TelegraphSeconds;
        public readonly float AttackCooldown;
        public readonly float ColliderHeight;
        public readonly float ColliderRadius;

        public EnemyStats(EnemyKind kind, float health, float speed, float attackRange, float attackDamage,
            float knockbackResistance, float staggerSeconds, float separationSpacing,
            float telegraphSeconds, float attackCooldown, float colliderHeight, float colliderRadius)
        {
            Kind = kind;
            Health = health;
            Speed = speed;
            AttackRange = attackRange;
            AttackDamage = attackDamage;
            KnockbackResistance = knockbackResistance;
            StaggerSeconds = staggerSeconds;
            SeparationSpacing = separationSpacing;
            TelegraphSeconds = telegraphSeconds;
            AttackCooldown = attackCooldown;
            ColliderHeight = colliderHeight;
            ColliderRadius = colliderRadius;
        }
    }

    /// <summary>
    /// Einzige Quelle der Gegner-Zahlen, aus den verstreuten switch-Ketten in
    /// <see cref="EnemyAgent"/> und <see cref="EnemyFactory"/> herausgezogen.
    ///
    /// ÜBERGANGSZUSTAND: Diese Klasse existiert, damit der Migrationslauf und die
    /// Tests dieselben Werte lesen können, aus denen das Spiel gerade rechnet.
    /// Sobald <see cref="EnemyAgent"/> seine Werte aus <see cref="EnemyDefinition"/>
    /// bezieht, ist sie ersatzlos zu löschen.
    /// </summary>
    public static class EnemyBalance
    {
        /// <summary>Zusätzliche Leben pro Etage, multiplikativ. Wird in Etage 3 zur Kurve.</summary>
        public const float HealthPerFloor = 0.15f;
        /// <summary>Zusätzlicher Schaden pro Etage, multiplikativ.</summary>
        public const float DamagePerFloor = 0.065f;
        /// <summary>Zusätzliche Bewegung pro Etage, gedeckelt auf <see cref="MaximumSpeedBonus"/>.</summary>
        public const float SpeedPerFloor = 0.012f;
        public const float MaximumSpeedBonus = 0.22f;

        /// <summary>
        /// Grundhaerte auf jedem Pfad, schon ab Etage 1.
        ///
        /// Vorher war Etage 1 der absolute Tiefpunkt: ein frischer Rex schoss einen Crawler in gut
        /// einer Sekunde um, und der Anfang fuehlte sich leicht an. Die Staerke soll aus dem Lauf
        /// kommen - Upgrades, Prestige, Schmied - und nicht von vornherein da sein.
        ///
        /// Beide Werte sind mit dem Umbau auf echte Mitglieder der Gruppe gestiegen, und zwar
        /// gerechnet, nicht geschaetzt (siehe <see cref="PartyBalance"/>):
        /// Leben um das 1,30-fache, weil die Gruppe jetzt das 1,30-fache an Schaden austeilt und ein
        /// Kampf sonst schneller vorbei waere als heute. Schaden um das 1,5-fache, weil sich die
        /// Gegner auf drei Ziele verteilen statt auf eines - mit stehender Gruppe faengt der Spieler
        /// noch rund 60 % der heutigen Bedrohung ab, allein dagegen das Anderthalbfache.
        /// </summary>
        public const float BaseHealthScale = 1.75f;
        public const float BaseDamageScale = 1.8f;

        /// <summary>
        /// Wie viel zaeher die Gegner auf einem Pfad sind. Vorher unterschieden sich die Pfade nur in
        /// der Zahl der Etagen; Etage 1 war auf Legendary dieselbe wie auf Brave. In R.I.S.E. werden
        /// die Pfade schwerer und laenger, und man soll in sie hineinwachsen.
        /// </summary>
        public static float PathHealth(RunMode path) => path switch
        {
            RunMode.Brave => 1f,
            RunMode.Heroic => 1.2f,
            _ => 1.45f
        };

        public static float PathDamage(RunMode path) => path switch
        {
            RunMode.Brave => 1f,
            RunMode.Heroic => 1.15f,
            _ => 1.35f
        };

        /// <summary>Leben eines Gegners gegenueber seinem Tabellenwert, auf dieser Etage und diesem Pfad.</summary>
        public static float HealthScale(RunMode path, int floor)
            => BaseHealthScale * PathHealth(path) * (1f + Mathf.Max(0, floor - 1) * HealthPerFloor);

        /// <summary>Schaden eines Gegners gegenueber seinem Tabellenwert, auf dieser Etage und diesem Pfad.</summary>
        public static float DamageScale(RunMode path, int floor)
            => BaseDamageScale * PathDamage(path) * (1f + Mathf.Max(0, floor - 1) * DamagePerFloor);

        private static readonly EnemyStats[] Table =
        {
            //                       kind                  hp     spd   range  dmg   knock  stag   space  tele   cool   height radius
            new(EnemyKind.Crawler,     50f,  3.5f,  1.35f, 10f,  0.58f, 0.09f,  1.05f, 0.32f, 1.05f, 1.3f,  0.42f),
            new(EnemyKind.Shooter,     42f,  2.5f,  8.5f,   8f,  0.58f, 0.09f,  1.05f, 0.58f, 1.85f, 1.3f,  0.42f),
            new(EnemyKind.Brute,       160f,  1.65f, 1.8f,  18f,  0.28f, 0.065f, 1.65f, 0.82f, 2.25f, 2.2f,  0.7f),
            new(EnemyKind.Elite,      460f,  2.25f, 2.1f,  22f,  0.18f, 0.045f, 1.65f, 0.78f, 2.05f, 2.2f,  0.7f),
            // Iron Warden: Vorwarnzeit und Abklingzeit sind hier die Phase-1-Werte.
            // Die Phasen-Verkürzung (0,72 / 0,58 bzw. 1,15) bleibt Verhalten im Code.
            new(EnemyKind.IronWarden, 1900f, 1.75f, 2.5f,  24f,  0.08f, 0.025f, 1.65f, 0.86f, 1.8f,  3.2f,  1.1f),
            // Schildtraeger: haelt von vorn fast alles aus, ist aber langsam. Das Leben ist bewusst
            // niedriger als beim Brute - die Haerte kommt aus der Deckung, nicht aus dem Balken.
            new(EnemyKind.Shieldbearer, 130f, 1.75f, 1.9f,  16f,  0.14f, 0.05f,  1.5f,  0.62f, 1.9f,  2.0f,  0.62f),
            // Armbruster: wenig Leben, dafuer weite Reichweite und ein Schuss, der wehtut, wenn man
            // in der Linie stehen bleibt.
            new(EnemyKind.Marksman,     46f, 2.35f, 12f,    26f,  0.6f,  0.1f,   1.05f, 0.9f,  2.5f,  1.35f, 0.42f),
            // Splitterzwilling: weniger Leben als der Warden und schneller. Er haelt nicht stand,
            // er ist nie da, wo man hinschlaegt - die Haerte kommt aus dem Versetzen.
            new(EnemyKind.RiftTwin,    1280f, 3.1f,  2.2f,  20f,  0.1f,  0.03f,  1.65f, 0.5f,  1.5f,  2.9f,  0.95f),
            // Chorwaechter: steht fast still und laesst andere kaempfen. Das meiste Leben im Spiel,
            // aber er ist nur angreifbar, wenn seine Rufe gefallen sind.
            new(EnemyKind.ChoirWarden, 2200f, 1.1f,  3.2f,  22f,  0.05f, 0.02f,  1.8f,  1f,    2.6f,  3.4f,  1.15f)
        };

        public static IReadOnlyList<EnemyStats> All => Table;

        public static EnemyStats For(EnemyKind kind)
        {
            for (var i = 0; i < Table.Length; i++)
                if (Table[i].Kind == kind) return Table[i];
            Debug.LogError($"EnemyBalance: kein Statblock für {kind}, benutze Crawler.");
            return Table[0];
        }
    }
}
