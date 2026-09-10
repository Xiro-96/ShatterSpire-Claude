using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Statblock eines Gegner-Archetyps. Das Angriffs*muster* bleibt vorerst Code
    /// in <see cref="EnemyAgent"/> — Verhalten als Daten gehört zu Etage 3
    /// zusammen mit den Elite-Modifiern. Hier stehen nur die Zahlen.
    /// </summary>
    [CreateAssetMenu(menuName = "Shatterspire/Enemy Definition", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identität")]
        [SerializeField] private EnemyKind kind = EnemyKind.Crawler;

        [Header("Grundwerte")]
        [SerializeField, Min(1f)] private float health = 28f;
        [SerializeField, Min(0f)] private float speed = 3.5f;
        [SerializeField, Min(0f)] private float attackRange = 1.35f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;

        [Header("Trefferreaktion")]
        [Tooltip("Anteil der Trefferkraft, der als Rückstoß ankommt. Niedrig = schwer, " +
                 "Crawler 0,58 bis Iron Warden 0,08.")]
        [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0.58f;
        [SerializeField, Min(0f)] private float staggerSeconds = 0.09f;
        [Tooltip("Mindestabstand zu anderen Gegnern, damit Pulks sich nicht überlagern.")]
        [SerializeField, Min(0f)] private float separationSpacing = 1.05f;

        [Header("Timing")]
        [Tooltip("Vorwarnzeit, bevor der Angriff trifft. Der Spieler braucht sie zum Ausweichen.")]
        [SerializeField, Min(0f)] private float telegraphSeconds = 0.32f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.05f;

        [Header("Körper")]
        [SerializeField, Min(0.1f)] private float colliderHeight = 1.3f;
        [SerializeField, Min(0.05f)] private float colliderRadius = 0.42f;

        public EnemyKind Kind => kind;
        public float Health => health;
        public float Speed => speed;
        public float AttackRange => attackRange;
        public float AttackDamage => attackDamage;
        public float KnockbackResistance => knockbackResistance;
        public float StaggerSeconds => staggerSeconds;
        public float SeparationSpacing => separationSpacing;
        public float TelegraphSeconds => telegraphSeconds;
        public float AttackCooldown => attackCooldown;
        public float ColliderHeight => colliderHeight;
        public float ColliderRadius => colliderRadius;

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(in EnemyStats stats)
        {
            kind = stats.Kind;
            health = stats.Health;
            speed = stats.Speed;
            attackRange = stats.AttackRange;
            attackDamage = stats.AttackDamage;
            knockbackResistance = stats.KnockbackResistance;
            staggerSeconds = stats.StaggerSeconds;
            separationSpacing = stats.SeparationSpacing;
            telegraphSeconds = stats.TelegraphSeconds;
            attackCooldown = stats.AttackCooldown;
            colliderHeight = stats.ColliderHeight;
            colliderRadius = stats.ColliderRadius;
        }
    }
}
