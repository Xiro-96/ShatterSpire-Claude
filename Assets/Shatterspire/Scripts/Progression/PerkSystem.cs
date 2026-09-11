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
        Execution,
        // Upgrades, die eine Aktion veraendern.
        HeavyRapidCharge, HeavyPower, PerfectEcho, SkillHaste, SkillOvercharge,
        RangerSplitFinisher, RangerRailShot, RangerArrowRain, RangerPartingShot,
        GuardianCleaveWave, GuardianEarthsplitter, GuardianBulwark, GuardianShoulderCharge,
        ArcanistVoidBurst, ArcanistCollapse, ArcanistLingeringStar, ArcanistPhaseRift,
        UltimateSurge, UltimateAfterglow, RangerHomingBarrage, GuardianMoltenQuake, ArcanistEventHorizon
    }

    public enum PerkRarity { Common, Rare, Epic, Legendary }

    /// <summary>Die Aktion, die ein Upgrade veraendert. Passive wirken auf alle Aktionen.</summary>
    public enum ActionSlot { Light, Heavy, Skill, Dash, Ultimate, Passive }

    [Serializable]
    public sealed class PerkDefinition
    {
        public PerkId Id;
        public string Name;
        public string Description;
        public PerkRarity Rarity;
        public Color Color;
        public ActionSlot Slot;
        /// <summary>Leer: fuer jeden Helden. Sonst nur fuer die genannten.</summary>
        public HeroClassId[] Heroes;

        public PerkDefinition(PerkId id, string name, string description, PerkRarity rarity, Color color,
            ActionSlot slot = ActionSlot.Passive, HeroClassId[] heroes = null)
        {
            Id = id; Name = name; Description = description; Rarity = rarity; Color = color;
            Slot = slot;
            Heroes = heroes ?? Array.Empty<HeroClassId>();
        }

        public bool IsHeroSpecific => Heroes.Length > 0;
        public bool AvailableFor(HeroClassId hero) => Heroes.Length == 0 || Array.IndexOf(Heroes, hero) >= 0;
    }

    /// <summary>
    /// Upgrades nach dem Vorbild von R.I.S.E.: jedes gehoert zu einer Aktion - Light, Heavy, Skill oder
    /// Dash - und veraendert, wie sie sich spielt. Heldenspezifische Upgrades gibt es fuer jede Aktion.
    /// Die Wirkung steht dort, wo die Aktion ausgefuehrt wird: WeaponSystem, PlayerController, Projectile.
    /// </summary>
    public static class PerkCatalog
    {
        private static readonly Color Common = new(0.72f, 0.77f, 0.82f);
        private static readonly Color Rare = new(0.18f, 0.65f, 1f);
        private static readonly Color Epic = new(0.75f, 0.3f, 1f);
        private static readonly Color Legendary = new(1f, 0.58f, 0.1f);
        // Nur Helden mit Projektilen: Zielsuche und Einschlaege wirken auf Geschosse.
        private static readonly HeroClassId[] Shooters = { HeroClassId.Ranger, HeroClassId.Arcanist };
        private static readonly HeroClassId[] RangerOnly = { HeroClassId.Ranger };
        private static readonly HeroClassId[] GuardianOnly = { HeroClassId.Guardian };
        private static readonly HeroClassId[] ArcanistOnly = { HeroClassId.Arcanist };

        public static readonly IReadOnlyList<PerkDefinition> All = new List<PerkDefinition>
        {
            // LIGHT
            P(PerkId.AttackSpeed, "BATTLE RHYTHM", "Light attacks are 22% faster.", PerkRarity.Common, ActionSlot.Light),
            P(PerkId.Piercing, "SUNDER", "Shots pierce one more enemy. Hammer swings reach further.", PerkRarity.Rare, ActionSlot.Light),
            P(PerkId.Ricochet, "SEEKING ECHO", "Shots bounce to a second enemy. Hammer finishers echo.", PerkRarity.Rare, ActionSlot.Light),
            P(PerkId.Multishot, "TWIN FANG", "Fires a second projectile. Hammer swings leave an aftershock.", PerkRarity.Epic, ActionSlot.Light),
            P(PerkId.HomingShot, "SEEKER LINK", "Shots curve toward nearby enemies.", PerkRarity.Rare, ActionSlot.Light, Shooters),
            P(PerkId.RangerSplitFinisher, "SPLIT FINISHER", "Every third Rift Arrow splits into a fan of three.", PerkRarity.Rare, ActionSlot.Light, RangerOnly),
            P(PerkId.GuardianCleaveWave, "CLEAVING WAVE", "The Hammer finisher sends a ground wave forward.", PerkRarity.Rare, ActionSlot.Light, GuardianOnly),
            P(PerkId.ArcanistVoidBurst, "VOID BURST", "Arc Bolts burst on impact and hit nearby enemies.", PerkRarity.Rare, ActionSlot.Light, ArcanistOnly),

            // HEAVY
            P(PerkId.HeavyRapidCharge, "RAPID CHARGE", "Light hits fill the Heavy meter 30% faster.", PerkRarity.Common, ActionSlot.Heavy),
            P(PerkId.HeavyPower, "CRUSHING FORCE", "Heavy attacks deal 40% more damage.", PerkRarity.Common, ActionSlot.Heavy),
            P(PerkId.PerfectEcho, "PERFECT ECHO", "A Perfect Heavy strikes again: a second blast or two extra arrows.", PerkRarity.Epic, ActionSlot.Heavy),
            P(PerkId.RangerRailShot, "RAIL SHOT", "Piercing Draw always fires three arrows that pierce everything.", PerkRarity.Epic, ActionSlot.Heavy, RangerOnly),
            P(PerkId.GuardianEarthsplitter, "EARTHSPLITTER", "Ground Breaker tears a line of eruptions along your aim.", PerkRarity.Epic, ActionSlot.Heavy, GuardianOnly),
            P(PerkId.ArcanistCollapse, "COLLAPSING STAR", "Gravity Burst detonates a second time further along your aim.", PerkRarity.Epic, ActionSlot.Heavy, ArcanistOnly),

            // SKILL
            P(PerkId.SkillHaste, "QUICK CAST", "Skill cooldown is 25% shorter.", PerkRarity.Common, ActionSlot.Skill),
            P(PerkId.SkillOvercharge, "OVERCHARGE", "Every Skill cast charges your Ultimate by 10%.", PerkRarity.Rare, ActionSlot.Skill),
            P(PerkId.RangerArrowRain, "ARROW RAIN", "Arrow Storm fires five wider volleys that follow your aim.", PerkRarity.Rare, ActionSlot.Skill, RangerOnly),
            P(PerkId.GuardianBulwark, "BULWARK", "Bull Rush ends in a slam and makes you invulnerable for 1.5 s.", PerkRarity.Rare, ActionSlot.Skill, GuardianOnly),
            P(PerkId.ArcanistLingeringStar, "LINGERING STAR", "Black Star pulses seven times and drifts toward your aim.", PerkRarity.Rare, ActionSlot.Skill, ArcanistOnly),

            // DASH
            P(PerkId.ExtraDash, "SECOND WIND", "+1 Dash charge.", PerkRarity.Rare, ActionSlot.Dash),
            P(PerkId.DashExplosion, "STORM STEP", "Every Dash ends in a lightning burst.", PerkRarity.Rare, ActionSlot.Dash),
            P(PerkId.MovementSpeed, "KINETIC BOOTS", "+15% movement speed. Dash recharges 25% faster.", PerkRarity.Common, ActionSlot.Dash),
            P(PerkId.RangerPartingShot, "PARTING SHOT", "Dashing fires a fan of five arrows toward your aim.", PerkRarity.Rare, ActionSlot.Dash, RangerOnly),
            P(PerkId.GuardianShoulderCharge, "SHOULDER CHARGE", "Dashing slams every enemy in your path.", PerkRarity.Rare, ActionSlot.Dash, GuardianOnly),
            P(PerkId.ArcanistPhaseRift, "PHASE RIFT", "Dashing leaves a rift behind that detonates.", PerkRarity.Rare, ActionSlot.Dash, ArcanistOnly),

            // ULTIMATE
            P(PerkId.UltimateSurge, "SURGE CELL", "Your Ultimate charges 30% faster.", PerkRarity.Rare, ActionSlot.Ultimate),
            P(PerkId.UltimateAfterglow, "AFTERGLOW", "Casting your Ultimate heals you for 30% of your max health.", PerkRarity.Epic, ActionSlot.Ultimate),
            P(PerkId.RangerHomingBarrage, "HOMING BARRAGE", "Rift Barrage arrows seek out enemies.", PerkRarity.Epic, ActionSlot.Ultimate, RangerOnly),
            P(PerkId.GuardianMoltenQuake, "MOLTEN QUAKE", "Forge Quake shockwaves set enemies on fire.", PerkRarity.Epic, ActionSlot.Ultimate, GuardianOnly),
            P(PerkId.ArcanistEventHorizon, "EVENT HORIZON", "Singularity pulls enemies into its center.", PerkRarity.Epic, ActionSlot.Ultimate, ArcanistOnly),

            // PASSIVE
            P(PerkId.DamageUp, "TEMPERED POWER", "+25% damage for every action.", PerkRarity.Common, ActionSlot.Passive),
            P(PerkId.CritChance, "DEADEYE", "+12% critical chance.", PerkRarity.Rare, ActionSlot.Passive),
            P(PerkId.CritDamage, "HOLLOW POINT", "+50% critical damage.", PerkRarity.Common, ActionSlot.Passive),
            P(PerkId.ExplosiveShot, "VOLATILE IMPACT", "Shots erupt for area damage on impact.", PerkRarity.Epic, ActionSlot.Passive, Shooters),
            P(PerkId.FireBullet, "EMBER CORE", "Attacks ignite. Combines with Volatile Impact.", PerkRarity.Rare, ActionSlot.Passive),
            P(PerkId.IceBullet, "CRYO CORE", "Attacks slow enemies. Critical hits shatter with Deadeye.", PerkRarity.Rare, ActionSlot.Passive),
            P(PerkId.LightningBullet, "STORM CORE", "Attacks may call chain lightning.", PerkRarity.Rare, ActionSlot.Passive),
            P(PerkId.PoisonBullet, "TOXIN CORE", "Attacks apply stacking poison damage.", PerkRarity.Rare, ActionSlot.Passive),
            P(PerkId.Vampirism, "SIPHON RUNE", "Heal for 4% of damage dealt.", PerkRarity.Epic, ActionSlot.Passive),
            P(PerkId.Shield, "AEGIS BATTERY", "Gain 25 maximum health and heal it.", PerkRarity.Common, ActionSlot.Passive),
            P(PerkId.Execution, "FINISHER", "Deal double damage to enemies below 20% health.", PerkRarity.Epic, ActionSlot.Passive)
        };

        private static PerkDefinition P(PerkId id, string name, string text, PerkRarity rarity, ActionSlot slot,
            HeroClassId[] heroes = null)
        {
            var color = rarity switch { PerkRarity.Rare => Rare, PerkRarity.Epic => Epic, PerkRarity.Legendary => Legendary, _ => Common };
            return new PerkDefinition(id, name, text, rarity, color, slot, heroes);
        }

        public static PerkDefinition Find(PerkId id)
        {
            foreach (var perk in All)
                if (perk.Id == id) return perk;
            return null;
        }

        public static string SlotLabel(ActionSlot slot) => slot switch
        {
            ActionSlot.Light => "LIGHT",
            ActionSlot.Heavy => "HEAVY",
            ActionSlot.Skill => "SKILL",
            ActionSlot.Dash => "DASH",
            ActionSlot.Ultimate => "ULTIMATE",
            _ => "PASSIVE"
        };

        /// <summary>Aktion samt Name beim Helden, etwa "HEAVY · GROUND BREAKER".</summary>
        public static string SlotLabel(ActionSlot slot, HeroClassId hero) => slot switch
        {
            ActionSlot.Light => "LIGHT · " + HeroCatalog.LightAttackName(hero),
            ActionSlot.Heavy => "HEAVY · " + HeroCatalog.HeavyAttackName(hero),
            ActionSlot.Skill => "SKILL · " + HeroCatalog.SkillName(hero),
            ActionSlot.Ultimate => "ULTIMATE · " + HeroCatalog.UltimateName(hero),
            _ => SlotLabel(slot)
        };

        /// <summary>
        /// Drei verschiedene Upgrades, die der Held nutzen kann - wenn moeglich fuer drei verschiedene
        /// Aktionen, also hoechstens ein passives. Heldenspezifische Upgrades kommen etwas haeufiger,
        /// seltene etwas seltener. Bereits gewaehlte erscheinen erst, wenn keine neuen mehr uebrig sind.
        /// </summary>
        public static List<PerkDefinition> RollThree(HeroClassId hero, ICollection<PerkId> owned, System.Random random)
        {
            random ??= new System.Random();
            var available = All.Where(perk => perk.AvailableFor(hero)).ToList();
            var fresh = Shuffle(available.Where(perk => owned == null || !owned.Contains(perk.Id)).ToList(), random);
            var result = new List<PerkDefinition>(3);

            foreach (var perk in fresh)
            {
                if (result.Count >= 3) break;
                if (result.Any(chosen => chosen.Slot == perk.Slot)) continue;
                result.Add(perk);
            }
            foreach (var perk in fresh)
            {
                if (result.Count >= 3) break;
                if (!result.Contains(perk)) result.Add(perk);
            }
            foreach (var perk in Shuffle(available, random))
            {
                if (result.Count >= 3) break;
                if (!result.Contains(perk)) result.Add(perk);
            }
            return result;
        }

        private static List<PerkDefinition> Shuffle(List<PerkDefinition> perks, System.Random random)
        {
            var keys = new Dictionary<PerkDefinition, double>(perks.Count);
            foreach (var perk in perks)
            {
                var weight = perk.Rarity switch
                {
                    PerkRarity.Legendary => 1.8,
                    PerkRarity.Epic => 1.35,
                    PerkRarity.Rare => 1.1,
                    _ => 1.0
                };
                if (perk.Heroes.Length == 1) weight *= 0.7;
                if (perk.Slot == ActionSlot.Passive) weight *= 1.25;
                keys[perk] = random.NextDouble() * weight;
            }
            return perks.OrderBy(perk => keys[perk]).ToList();
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
        public float SkillCooldownMultiplier { get; private set; } = 1f;
        public float DashRechargeMultiplier { get; private set; } = 1f;
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
                case PerkId.MovementSpeed:
                    MoveSpeedMultiplier *= 1.15f;
                    DashRechargeMultiplier *= 1.25f;
                    break;
                case PerkId.ExtraDash: ExtraDashCharges++; break;
                case PerkId.HeavyRapidCharge: HeavyChargeMultiplier *= 1.3f; break;
                case PerkId.HeavyPower: HeavyDamageMultiplier *= 1.4f; break;
                case PerkId.SkillHaste: SkillCooldownMultiplier *= 0.75f; break;
                case PerkId.UltimateSurge: UltimateChargeMultiplier *= 1.3f; break;
                case PerkId.Shield: GetComponent<Health>().IncreaseMaximum(25f, true); break;
            }
            Changed?.Invoke();
            GameEvents.RaisePerkSelected(perk);
        }
    }
}
