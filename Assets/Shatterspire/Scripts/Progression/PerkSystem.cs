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
        UltimateSurge, UltimateAfterglow, RangerHomingBarrage, GuardianMoltenQuake, ArcanistEventHorizon,
        // KORR, der Bomber.
        BomberShortFuse, BomberClusterCharge, BomberLongCord, BomberStickyCluster, BomberChainFeed,
        BomberSmokeStep,
        // LYRA, die Paladin.
        PaladinWideGround, PaladinLongVigil, PaladinWardStep, PaladinTwinWave, PaladinIronBrace
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
        private static readonly HeroClassId[] BomberOnly = { HeroClassId.Bomber };
        private static readonly HeroClassId[] PaladinOnly = { HeroClassId.Paladin };

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
            P(PerkId.BomberSmokeStep, "DROP STEP", "Dashing drops a live charge where you were standing.", PerkRarity.Rare, ActionSlot.Dash, BomberOnly),
            P(PerkId.PaladinWardStep, "WARD STEP", "Dashing leaves a small patch of Hallowed Ground behind.", PerkRarity.Rare, ActionSlot.Dash, PaladinOnly),

            // ULTIMATE
            P(PerkId.UltimateSurge, "SURGE CELL", "Your Ultimate charges 30% faster.", PerkRarity.Rare, ActionSlot.Ultimate),
            P(PerkId.UltimateAfterglow, "AFTERGLOW", "Casting your Ultimate heals you for 30% of your max health.", PerkRarity.Epic, ActionSlot.Ultimate),
            P(PerkId.RangerHomingBarrage, "LONG FOCUS", "Hunters Focus starts with 7 instead of 5 seconds.", PerkRarity.Epic, ActionSlot.Ultimate, RangerOnly),
            P(PerkId.GuardianMoltenQuake, "MOLTEN CRATER", "Forge Plunge burns, and so does its crater.", PerkRarity.Epic, ActionSlot.Ultimate, GuardianOnly),
            P(PerkId.ArcanistEventHorizon, "EVENT HORIZON", "The Time Rift lasts 8.5 instead of 6.5 seconds.", PerkRarity.Epic, ActionSlot.Ultimate, ArcanistOnly),
            P(PerkId.BomberShortFuse, "SHORT FUSE", "Thrown charges detonate twice as fast.", PerkRarity.Rare, ActionSlot.Light, BomberOnly),
            P(PerkId.BomberClusterCharge, "CLUSTER CHARGE", "Every charge splits into two smaller ones on landing.", PerkRarity.Epic, ActionSlot.Light, BomberOnly),
            P(PerkId.BomberStickyCluster, "SHAPED CHARGE", "The Sticky Mine hits 40% harder.", PerkRarity.Rare, ActionSlot.Heavy, BomberOnly),
            P(PerkId.BomberLongCord, "LONG CORD", "The Blast Cord lays five charges instead of three.", PerkRarity.Epic, ActionSlot.Skill, BomberOnly),
            P(PerkId.BomberChainFeed, "CHAIN FEED", "The Chain Detonator keeps feeding for 10 instead of 6 seconds.", PerkRarity.Epic, ActionSlot.Ultimate, BomberOnly),
            P(PerkId.PaladinTwinWave, "TWIN WAVE", "The Oathblade finisher sends a second wave and heals twice as much.", PerkRarity.Rare, ActionSlot.Light, PaladinOnly),
            P(PerkId.PaladinIronBrace, "IRON BRACE", "Shield Brace holds everything from the front, and gives all of it back.", PerkRarity.Epic, ActionSlot.Heavy, PaladinOnly),
            P(PerkId.PaladinWideGround, "WIDE GROUND", "Hallowed Ground covers a wider circle.", PerkRarity.Rare, ActionSlot.Skill, PaladinOnly),
            P(PerkId.PaladinLongVigil, "LONG VIGIL", "Aegis stands for 9 instead of 7 seconds.", PerkRarity.Epic, ActionSlot.Ultimate, PaladinOnly),

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
        /// <summary>
        /// Aktion samt Name beim Helden, etwa "SCHWER · ERDBRECHER". Beide Haelften laufen einzeln
        /// durch die Sprachschicht - zusammengesetzt wuerde der Text keinen Tabelleneintrag treffen
        /// und englisch stehen bleiben.
        /// </summary>
        public static string SlotLabel(ActionSlot slot, HeroClassId hero) => slot switch
        {
            ActionSlot.Light => Loc.T("LIGHT") + " · " + Loc.T(HeroCatalog.LightAttackName(hero)),
            ActionSlot.Heavy => Loc.T("HEAVY") + " · " + Loc.T(HeroCatalog.HeavyAttackName(hero)),
            ActionSlot.Skill => Loc.T("SKILL") + " · " + Loc.T(HeroCatalog.SkillName(hero)),
            ActionSlot.Ultimate => Loc.T("ULTIMATE") + " · " + Loc.T(HeroCatalog.UltimateName(hero)),
            _ => Loc.T(SlotLabel(slot))
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
        /// <summary>
        /// Schadensfaktor einschliesslich der Wirkungen, die vom Zustand abhaengen. Berechnet statt
        /// gespeichert: so wirkt die Zornspule ueberall, wo Schaden entsteht, ohne dass jede
        /// Angriffsstelle im Kampfcode sie einzeln abfragen muesste.
        /// </summary>
        public float DamageMultiplier =>
            storedDamageMultiplier * (LowHealthFury ? 1.15f : 1f) * (InForgeCrater ? 1.25f : 1f);

        /// <summary>Der Held steht in seinem eigenen Schmiedekrater. Wird vom Krater selbst gesetzt.</summary>
        public bool InForgeCrater { get; set; }
        private float storedDamageMultiplier = 1f;
        private Health cachedHealth;

        /// <summary>Zornspule liegt an und der Held ist unter 40 Prozent Leben.</summary>
        private bool LowHealthFury
        {
            get
            {
                if (!HasVengeanceCoil) return false;
                if (!cachedHealth) cachedHealth = GetComponent<Health>();
                return cachedHealth && cachedHealth.IsAlive && cachedHealth.Normalized < 0.4f;
            }
        }
        public float AttackSpeedMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier => storedMoveSpeedMultiplier * focusSpeedMultiplier;
        private float storedMoveSpeedMultiplier = 1f;
        private float focusSpeedMultiplier = 1f;

        /// <summary>Zusaetzliches Tempo, solange Rex' Jaegerblick laeuft.</summary>
        public void SetFocusSpeed(float value) => focusSpeedMultiplier = Mathf.Max(0.1f, value);
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
        public bool HasVengeanceCoil { get; private set; }
        /// <summary>Saugstein: heilt einen Anteil des verursachten Schadens, wie die Saugrune.</summary>
        public bool HasSiphonStone { get; private set; }
        /// <summary>Faktor auf das Gold aus Gegnern. Die Goldader hebt ihn an.</summary>
        public float GoldMultiplier { get; private set; } = 1f;
        public HeroClassId HeroClass { get; private set; } = HeroClassId.Ranger;
        public event Action Changed;

        public bool Has(PerkId id) => perks.Contains(id);

        public void ConfigureRun(HeroClassId hero, RunConfig config, MetaSaveData meta)
        {
            HeroClass = hero;
            // Die Stufe dieses Helden. Multiplikativ neben den gemeinsamen Meta-Upgrades: das eine
            // kommt vom Spieler, das andere vom Helden, und beides soll sich zaehlen lassen.
            storedDamageMultiplier *= 1f + HeroProgress.DamageBonus(MetaSaveSystem.HeroLevel(meta, hero));
            storedDamageMultiplier *= 1f + Mathf.Clamp(meta?.mightLevel ?? 0, 0, 10) * 0.04f;
            storedMoveSpeedMultiplier *= 1f + Mathf.Clamp(meta?.agilityLevel ?? 0, 0, 10) * 0.02f;
            if (config == null) return;
            if (config.HasRelic(RelicId.WindstepSigil)) ExtraDashCharges++;
            if (config.HasRelic(RelicId.HuntersMark)) CritChance += 0.1f;
            if (config.HasRelic(RelicId.ArcBattery)) HeavyChargeMultiplier *= 1.25f;
            if (config.HasRelic(RelicId.IronHeart)) GetComponent<Health>()?.IncreaseMaximum(30f, true);
            if (config.HasRelic(RelicId.SwiftBoots)) storedMoveSpeedMultiplier *= 1.12f;
            if (config.HasRelic(RelicId.FocusCrystal)) SkillCooldownMultiplier *= 0.8f;
            if (config.HasRelic(RelicId.SurgeCore)) UltimateChargeMultiplier *= 1.2f;
            if (config.HasRelic(RelicId.TwinCharge)) AttackSpeedMultiplier *= 1.1f;
            if (config.HasRelic(RelicId.GoldVein)) GoldMultiplier *= 1.3f;
            if (config.HasRelic(RelicId.GuardPlate))
            {
                // Schutzplatte laeuft ueber denselben Haken, mit dem der Schildtraeger Treffer
                // abfaengt - eine Stelle fuer alles, was Schaden vor dem Abzug veraendert.
                var own = GetComponent<Health>();
                if (own) own.AddDamageFilter((_, amount) => amount * 0.9f);
            }
            HasEmberLens = config.HasRelic(RelicId.EmberLens);
            HasDawnSeed = config.HasRelic(RelicId.DawnSeed);
            HasFortunePrism = config.HasRelic(RelicId.FortunePrism);
            HasVengeanceCoil = config.HasRelic(RelicId.VengeanceCoil);
            HasSiphonStone = config.HasRelic(RelicId.SiphonStone);
            Changed?.Invoke();
        }

        /// <summary>
        /// Wirkung einer beim Haendler gekauften Ware. Bewusst getrennt von <see cref="Apply"/>:
        /// Perks gibt es je Aufstieg genau einmal, Waren beliebig oft - deshalb duerfen sie nicht
        /// ueber dieselbe Menge laufen, die Doppelungen abweist.
        /// </summary>
        public void ApplyPurchase(ShopOfferId id)
        {
            switch (id)
            {
                case ShopOfferId.Whetstone: storedDamageMultiplier *= 1.12f; break;
                case ShopOfferId.OiledGears: AttackSpeedMultiplier *= 1.08f; break;
                case ShopOfferId.IronRation: GetComponent<Health>().IncreaseMaximum(25f, true); break;
                case ShopOfferId.FocusLens: CritChance = Mathf.Min(0.85f, CritChance + 0.06f); break;
                case ShopOfferId.Counterweight: HeavyDamageMultiplier *= 1.18f; break;
                case ShopOfferId.ForgedBlade:
                    storedDamageMultiplier *= 1.3f;
                    AttackSpeedMultiplier *= 0.94f;
                    break;
            }
            Changed?.Invoke();
        }

        /// <summary>
        /// Wirkung einer Wette aus dem Raetselraum. Laeuft ueber Wirkung und Betrag, nicht ueber die
        /// Kennung: der angezeigte Text wird aus denselben zwei Feldern gebaut, deshalb kann die
        /// Anzeige nicht von der Wirkung abweichen.
        ///
        /// Gold und Lebensanteile gehoeren nicht hierher - die haelt der Altar selbst, weil sie an
        /// Geldbeutel und Lebensbalken haengen und nicht am Aufbau.
        /// </summary>
        public void ApplyWager(in Wager wager)
        {
            switch (wager.Effect)
            {
                case WagerEffect.Damage: storedDamageMultiplier *= wager.Amount; break;
                case WagerEffect.AttackSpeed: AttackSpeedMultiplier *= wager.Amount; break;
                case WagerEffect.MoveSpeed: storedMoveSpeedMultiplier *= wager.Amount; break;
                case WagerEffect.UltimateCharge: UltimateChargeMultiplier *= wager.Amount; break;
                case WagerEffect.MaxHealth:
                    GetComponent<Health>()?.IncreaseMaximum(wager.Amount, wager.Amount > 0f);
                    break;
            }
            Changed?.Invoke();
        }

        public void Apply(PerkDefinition perk)
        {
            if (!perks.Add(perk.Id)) return;
            switch (perk.Id)
            {
                case PerkId.DamageUp: storedDamageMultiplier *= 1.25f; break;
                case PerkId.AttackSpeed: AttackSpeedMultiplier *= 1.22f; break;
                case PerkId.CritChance: CritChance += 0.12f; break;
                case PerkId.CritDamage: CritMultiplier += 0.5f; break;
                case PerkId.MovementSpeed:
                    storedMoveSpeedMultiplier *= 1.15f;
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
