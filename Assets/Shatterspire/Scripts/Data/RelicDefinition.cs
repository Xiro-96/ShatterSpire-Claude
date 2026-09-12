using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Ein Relic aus der Run-Vorbereitung. Die Effekte sind hier Zahlen statt
    /// verstreuter <c>if (config.HasRelic(...))</c>-Zweige in
    /// <see cref="PlayerBuild.ConfigureRun"/> und <see cref="RunDirector"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Shatterspire/Relic Definition", fileName = "Relic_")]
    public sealed class RelicDefinition : ScriptableObject
    {
        [Header("Identität")]
        [SerializeField] private RelicId id = RelicId.WindstepSigil;
        [SerializeField] private string displayName = "WINDSTEP SIGIL";
        [SerializeField, TextArea(1, 2)] private string description = "+1 dash charge";

        [Header("Effekte")]
        [SerializeField, Min(0)] private int extraDashCharges;
        [SerializeField, Range(0f, 1f)] private float critChanceBonus;
        [SerializeField, Min(1f)] private float heavyChargeMultiplier = 1f;
        [Tooltip("Zusätzliche Heilung nach jeder Etage, flach.")]
        [SerializeField, Min(0f)] private float floorHealBonus;
        [Tooltip("Faktor auf die gesicherten Shards am Run-Ende.")]
        [SerializeField, Min(1f)] private float shardMultiplier = 1f;
        [Tooltip("Heavy-Angriffe detonieren beim Einschlag.")]
        [SerializeField] private bool heavyErupts;
        [Tooltip("Zusätzliches maximales Leben, flach.")]
        [SerializeField, Min(0f)] private float maximumHealthBonus;
        [SerializeField, Min(0.1f)] private float moveSpeedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float attackSpeedMultiplier = 1f;
        [Tooltip("Faktor auf die Abklingzeit der Fähigkeit, kleiner ist schneller.")]
        [SerializeField, Min(0.1f)] private float skillCooldownMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float ultimateChargeMultiplier = 1f;
        [Tooltip("Faktor auf das Gold aus Gegnern.")]
        [SerializeField, Min(0.1f)] private float goldMultiplier = 1f;
        [Tooltip("Faktor auf erlittenen Schaden, kleiner ist besser.")]
        [SerializeField, Range(0.1f, 1f)] private float damageTakenMultiplier = 1f;
        [Tooltip("Anteil des verursachten Schadens, der heilt.")]
        [SerializeField, Range(0f, 0.5f)] private float lifestealFraction;
        [Tooltip("Zusätzlicher Schadensanteil unter 40 % Leben.")]
        [SerializeField, Range(0f, 1f)] private float lowHealthDamageBonus;

        public RelicId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int ExtraDashCharges => extraDashCharges;
        public float CritChanceBonus => critChanceBonus;
        public float HeavyChargeMultiplier => heavyChargeMultiplier;
        public float FloorHealBonus => floorHealBonus;
        public float ShardMultiplier => shardMultiplier;
        public bool HeavyErupts => heavyErupts;
        public float MaximumHealthBonus => maximumHealthBonus;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float SkillCooldownMultiplier => skillCooldownMultiplier;
        public float UltimateChargeMultiplier => ultimateChargeMultiplier;
        public float GoldMultiplier => goldMultiplier;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public float LifestealFraction => lifestealFraction;
        public float LowHealthDamageBonus => lowHealthDamageBonus;

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(RelicId relicId, string name, string text, int dashCharges, float critBonus,
            float chargeMultiplier, float healBonus, float shards, bool erupts,
            float healthBonus = 0f, float moveSpeed = 1f, float attackSpeed = 1f, float skillCooldown = 1f,
            float ultimateCharge = 1f, float gold = 1f, float damageTaken = 1f, float lifesteal = 0f,
            float lowHealthDamage = 0f)
        {
            id = relicId;
            displayName = name;
            description = text;
            extraDashCharges = dashCharges;
            critChanceBonus = critBonus;
            heavyChargeMultiplier = chargeMultiplier;
            floorHealBonus = healBonus;
            shardMultiplier = shards;
            heavyErupts = erupts;
            maximumHealthBonus = healthBonus;
            moveSpeedMultiplier = moveSpeed;
            attackSpeedMultiplier = attackSpeed;
            skillCooldownMultiplier = skillCooldown;
            ultimateChargeMultiplier = ultimateCharge;
            goldMultiplier = gold;
            damageTakenMultiplier = damageTaken;
            lifestealFraction = lifesteal;
            lowHealthDamageBonus = lowHealthDamage;
        }
    }
}
