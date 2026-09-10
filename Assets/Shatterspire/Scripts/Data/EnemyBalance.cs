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
        public const float HealthPerFloor = 0.13f;
        /// <summary>Zusätzlicher Schaden pro Etage, multiplikativ.</summary>
        public const float DamagePerFloor = 0.065f;
        /// <summary>Zusätzliche Bewegung pro Etage, gedeckelt auf <see cref="MaximumSpeedBonus"/>.</summary>
        public const float SpeedPerFloor = 0.012f;
        public const float MaximumSpeedBonus = 0.22f;

        private static readonly EnemyStats[] Table =
        {
            //                       kind                  hp     spd   range  dmg   knock  stag   space  tele   cool   height radius
            new(EnemyKind.Crawler,     28f,  3.5f,  1.35f, 10f,  0.58f, 0.09f,  1.05f, 0.32f, 1.05f, 1.3f,  0.42f),
            new(EnemyKind.Shooter,     24f,  2.5f,  8.5f,   8f,  0.58f, 0.09f,  1.05f, 0.58f, 1.85f, 1.3f,  0.42f),
            new(EnemyKind.Brute,       85f,  1.65f, 1.8f,  18f,  0.28f, 0.065f, 1.65f, 0.82f, 2.25f, 2.2f,  0.7f),
            new(EnemyKind.Elite,      260f,  2.25f, 2.1f,  22f,  0.18f, 0.045f, 1.65f, 0.78f, 2.05f, 2.2f,  0.7f),
            // Iron Warden: Vorwarnzeit und Abklingzeit sind hier die Phase-1-Werte.
            // Die Phasen-Verkürzung (0,72 / 0,58 bzw. 1,15) bleibt Verhalten im Code.
            new(EnemyKind.IronWarden, 1100f, 1.75f, 2.5f,  24f,  0.08f, 0.025f, 1.65f, 0.86f, 1.8f,  3.2f,  1.1f)
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
