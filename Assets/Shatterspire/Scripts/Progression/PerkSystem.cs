using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shatterspire
{
    public enum PerkId
    {
        DamageUp, AttackSpeed, CritChance, CritDamage, MovementSpeed, ExtraDash,
        Ricochet, Piercing, ExplosiveShot, FireBullet, IceBullet, LightningBullet,
        PoisonBullet, Vampirism, Shield, Multishot, HomingShot, DashExplosion,
        Execution, UltimateCooldown
    }

    public enum PerkRarity { Common, Rare, Epic, Legendary }

    [Serializable]
    public sealed class PerkDefinition
    {
        public PerkId Id;
        public string Name;
        public string Description;
        public PerkRarity Rarity;
        public Color Color;

        public PerkDefinition(PerkId id, string name, string description, PerkRarity rarity, Color color)
        {
            Id = id; Name = name; Description = description; Rarity = rarity; Color = color;
        }
    }

    public static class PerkCatalog
    {
        private static readonly Color Common = new(0.72f, 0.77f, 0.82f);
        private static readonly Color Rare = new(0.18f, 0.65f, 1f);
        private static readonly Color Epic = new(0.75f, 0.3f, 1f);
        private static readonly Color Legendary = new(1f, 0.58f, 0.1f);

        public static readonly IReadOnlyList<PerkDefinition> All = new List<PerkDefinition>
        {
            P(PerkId.DamageUp,"TEMPERED POWER","+25% all attack damage.",PerkRarity.Common),
            P(PerkId.AttackSpeed,"BATTLE RHYTHM","+22% attack speed.",PerkRarity.Common),
            P(PerkId.CritChance,"DEADEYE","+12% critical chance.",PerkRarity.Rare),
            P(PerkId.CritDamage,"HOLLOW POINT","+50% critical damage.",PerkRarity.Common),
            P(PerkId.MovementSpeed,"KINETIC BOOTS","+15% movement speed.",PerkRarity.Common),
            P(PerkId.ExtraDash,"RAPID CHARGE","Every Light hit charges Heavy faster.",PerkRarity.Rare),
            P(PerkId.Ricochet,"SEEKING ECHO","Projectiles bounce; melee finishers echo.",PerkRarity.Rare),
            P(PerkId.Piercing,"SUNDER","Projectiles pierce; melee gains reach.",PerkRarity.Rare),
            P(PerkId.ExplosiveShot,"VOLATILE IMPACT","Attacks erupt for area damage.",PerkRarity.Epic),
            P(PerkId.FireBullet,"EMBER CORE","Attacks ignite; combines with explosions.",PerkRarity.Rare),
            P(PerkId.IceBullet,"CRYO CORE","Attacks slow and may freeze enemies.",PerkRarity.Rare),
            P(PerkId.LightningBullet,"STORM CORE","Every 5th hit calls lightning.",PerkRarity.Rare),
            P(PerkId.PoisonBullet,"TOXIN CORE","Attacks apply stacking poison damage.",PerkRarity.Rare),
            P(PerkId.Vampirism,"SIPHON RUNE","Heal for 4% of damage dealt.",PerkRarity.Epic),
            P(PerkId.Shield,"AEGIS BATTERY","Gain 25 maximum health and heal it.",PerkRarity.Common),
            P(PerkId.Multishot,"TWIN FANG","Adds a projectile or melee aftershock.",PerkRarity.Epic),
            P(PerkId.HomingShot,"SEEKER LINK","Projectiles curve toward nearby enemies.",PerkRarity.Rare),
            P(PerkId.DashExplosion,"PERFECT ECHO","Perfect Heavy fires two additional rift bolts.",PerkRarity.Rare),
            P(PerkId.Execution,"FINISHER","Deal double damage below 20% enemy health.",PerkRarity.Epic),
            P(PerkId.UltimateCooldown,"OVERCHARGED CELL","Ultimate charge gained +40%.",PerkRarity.Epic)
        };

        private static PerkDefinition P(PerkId id, string name, string text, PerkRarity rarity)
        {
            var color = rarity switch { PerkRarity.Rare => Rare, PerkRarity.Epic => Epic, PerkRarity.Legendary => Legendary, _ => Common };
            return new PerkDefinition(id, name, text, rarity, color);
        }

        public static List<PerkDefinition> RollThree(HashSet<PerkId> owned)
        {
            var pool = All.Where(p => !owned.Contains(p.Id)).OrderBy(_ => UnityEngine.Random.value).ToList();
            if (pool.Count < 3) pool = All.OrderBy(_ => UnityEngine.Random.value).ToList();
            return pool.Take(3).ToList();
        }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerBuild : MonoBehaviour
    {
        private readonly HashSet<PerkId> perks = new();
        public IReadOnlyCollection<PerkId> Perks => perks;
        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackSpeedMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float CritChance { get; private set; } = 0.05f;
        public float CritMultiplier { get; private set; } = 1.5f;
        public float HeavyDamageMultiplier { get; private set; } = 1f;
        public float HeavyChargeMultiplier { get; private set; } = 1f;
        public float UltimateChargeMultiplier { get; private set; } = 1f;
        public int ExtraDashCharges { get; private set; }
        public int ProjectileCount => Has(PerkId.Multishot) ? 2 : 1;
        public int Pierces => Has(PerkId.Piercing) ? 1 : 0;
        public int Ricochets => Has(PerkId.Ricochet) ? 1 : 0;
        public bool IsInferno => Has(PerkId.FireBullet) && Has(PerkId.ExplosiveShot);
        public bool IsShatter => Has(PerkId.IceBullet) && Has(PerkId.CritChance);
        public bool IsChainStorm => Has(PerkId.LightningBullet) && Has(PerkId.Ricochet);
        public bool HasEmberLens { get; private set; }
        public bool HasDawnSeed { get; private set; }
        public bool HasFortunePrism { get; private set; }
        public HeroClassId HeroClass { get; private set; } = HeroClassId.Ranger;
        public event Action Changed;

        public bool Has(PerkId id) => perks.Contains(id);

        public void ConfigureRun(HeroClassId hero, RunConfig config, MetaSaveData meta)
        {
            HeroClass = hero;
            DamageMultiplier *= 1f + Mathf.Clamp(meta?.mightLevel ?? 0, 0, 10) * 0.04f;
            MoveSpeedMultiplier *= 1f + Mathf.Clamp(meta?.agilityLevel ?? 0, 0, 10) * 0.02f;
            if (config == null) return;
            if (config.HasRelic(RelicId.WindstepSigil)) ExtraDashCharges++;
            if (config.HasRelic(RelicId.HuntersMark)) CritChance += 0.1f;
            if (config.HasRelic(RelicId.ArcBattery)) HeavyChargeMultiplier *= 1.25f;
            HasEmberLens = config.HasRelic(RelicId.EmberLens);
            HasDawnSeed = config.HasRelic(RelicId.DawnSeed);
            HasFortunePrism = config.HasRelic(RelicId.FortunePrism);
            Changed?.Invoke();
        }

        public void Apply(PerkDefinition perk)
        {
            if (!perks.Add(perk.Id)) return;
            switch (perk.Id)
            {
                case PerkId.DamageUp: DamageMultiplier *= 1.25f; break;
                case PerkId.AttackSpeed: AttackSpeedMultiplier *= 1.22f; break;
                case PerkId.CritChance: CritChance += 0.12f; break;
                case PerkId.CritDamage: CritMultiplier += 0.5f; break;
                case PerkId.MovementSpeed: MoveSpeedMultiplier *= 1.15f; break;
                case PerkId.ExtraDash: ExtraDashCharges++; break;
                case PerkId.UltimateCooldown: UltimateChargeMultiplier *= 1.4f; break;
                case PerkId.Shield: GetComponent<Health>().IncreaseMaximum(25f, true); break;
            }
            Changed?.Invoke();
            GameEvents.RaisePerkSelected(perk);
        }
    }
}
