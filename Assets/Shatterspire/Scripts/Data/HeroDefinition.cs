using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Alle Werte einer spielbaren Klasse als Asset. Ersetzt schrittweise die
    /// switch-Ketten in <see cref="HeroCatalog"/> und <see cref="WeaponSystem"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Shatterspire/Hero Definition", fileName = "Hero_")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [Header("Identität")]
        [SerializeField] private HeroClassId id = HeroClassId.Ranger;
        [SerializeField] private string displayName = "REX";
        [SerializeField] private string role = "RIFT RANGER";
        [SerializeField, TextArea(1, 2)] private string kitSummary = "";
        [SerializeField] private Color accent = Color.cyan;

        [Header("Grundwerte")]
        [SerializeField, Min(1f)] private float baseHealth = 105f;
        [SerializeField, Min(0.1f)] private float baseSpeed = 6.25f;
        [SerializeField, Min(0.1f)] private float baseDamage = 11.5f;
        [SerializeField, Min(0.1f)] private float skillCooldown = 7f;

        [Header("Fähigkeitsnamen")]
        [SerializeField] private string lightAttackName = "RIFT ARROW";
        [SerializeField] private string heavyAttackName = "PIERCING DRAW";
        [SerializeField] private string skillName = "ARROW STORM";

        public HeroClassId Id => id;
        public string DisplayName => displayName;
        public string Role => role;
        public string KitSummary => kitSummary;
        public Color Accent => accent;
        public float BaseHealth => baseHealth;
        public float BaseSpeed => baseSpeed;
        public float BaseDamage => baseDamage;
        public float SkillCooldown => skillCooldown;
        public string LightAttackName => lightAttackName;
        public string HeavyAttackName => heavyAttackName;
        public string SkillName => skillName;

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(HeroClassId heroId, string name, string heroRole, string kit, Color heroAccent,
            float health, float speed, float damage, float cooldown, string light, string heavy, string skill)
        {
            id = heroId;
            displayName = name;
            role = heroRole;
            kitSummary = kit;
            accent = heroAccent;
            baseHealth = health;
            baseSpeed = speed;
            baseDamage = damage;
            skillCooldown = cooldown;
            lightAttackName = light;
            heavyAttackName = heavy;
            skillName = skill;
        }
    }
}
