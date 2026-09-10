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

        public RelicId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public int ExtraDashCharges => extraDashCharges;
        public float CritChanceBonus => critChanceBonus;
        public float HeavyChargeMultiplier => heavyChargeMultiplier;
        public float FloorHealBonus => floorHealBonus;
        public float ShardMultiplier => shardMultiplier;
        public bool HeavyErupts => heavyErupts;

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(RelicId relicId, string name, string text, int dashCharges, float critBonus,
            float chargeMultiplier, float healBonus, float shards, bool erupts)
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
        }
    }
}
