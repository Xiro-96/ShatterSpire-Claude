using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Ein Perk als Asset.
    ///
    /// ÜBERGANGSZUSTAND: Die Laufzeit arbeitet weiter mit der bestehenden Klasse
    /// <see cref="PerkDefinition"/>, weil die durch <see cref="GameEvents"/>,
    /// <see cref="PlayerBuild"/> und das HUD läuft. <see cref="ToRuntime"/> ist die
    /// Brücke. Beim Umstellen der Aufrufstellen entfällt entweder diese Klasse oder
    /// <see cref="PerkDefinition"/> — nicht beide behalten.
    /// </summary>
    [CreateAssetMenu(menuName = "Shatterspire/Perk", fileName = "Perk_")]
    public sealed class PerkAsset : ScriptableObject
    {
        [SerializeField] private PerkId id = PerkId.DamageUp;
        [SerializeField] private string displayName = "TEMPERED POWER";
        [SerializeField, TextArea(1, 3)] private string description = "";
        [SerializeField] private PerkRarity rarity = PerkRarity.Common;
        [SerializeField] private Color color = Color.white;

        public PerkId Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public PerkRarity Rarity => rarity;
        public Color Color => color;

        public PerkDefinition ToRuntime() => new(id, displayName, description, rarity, color);

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(PerkDefinition source)
        {
            id = source.Id;
            displayName = source.Name;
            description = source.Description;
            rarity = source.Rarity;
            color = source.Color;
        }
    }
}
