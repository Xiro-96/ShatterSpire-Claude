using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    // Bomber kommt ans Ende: gespeicherte Werte (lastHero) behalten damit ihre Bedeutung.
    // Neue Helden kommen ans Ende, damit gespeicherte Zahlen ihre Bedeutung behalten -
    // die Heldenstufen liegen nach diesem Index im Spielstand.
    public enum HeroClassId { Ranger, Guardian, Arcanist, Bomber, Paladin }
    /// <summary>Schwierigkeitspfad wie in R.I.S.E.: Brave 5 Etagen, Heroic 15, Legendary ohne Ende.</summary>
    public enum RunMode { Brave, Heroic, Legendary }
    public enum FloorTheme { ForgottenCourt, EmberFoundry, AstralArchive }
    public enum MetaUpgradeId { Vitality, Might, Agility }
    // Neue Relikte kommen ans Ende, damit gespeicherte Zahlen ihre Bedeutung behalten.
    // Alle wirken auf jeden Helden: jeder hat Licht, Schwer, Faehigkeit, Dash und Ultimate.
    public enum RelicId
    {
        WindstepSigil, HuntersMark, DawnSeed, EmberLens, ArcBattery, FortunePrism,
        IronHeart, SwiftBoots, VengeanceCoil, SiphonStone, FocusCrystal, SurgeCore,
        TwinCharge, GuardPlate, GoldVein
    }

    [Serializable]
    public sealed class RunConfig
    {
        public HeroClassId Hero = HeroClassId.Ranger;
        public RunMode Mode = RunMode.Heroic;
        public List<RelicId> Relics = new();

        public RunConfig Clone()
            => new() { Hero = Hero, Mode = Mode, Relics = new List<RelicId>(Relics) };

        public bool HasRelic(RelicId relic) => Relics != null && Relics.Contains(relic);
    }

    /// <summary>Keeps the selected preparation through a deliberate scene reload.</summary>
    public static class RunLaunchSettings
    {
        private static RunConfig pending;
        public static bool HasPendingRun => pending != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => pending = null;

        public static void Prepare(RunConfig config) => pending = config?.Clone();

        public static RunConfig Consume()
        {
            var result = pending?.Clone();
            pending = null;
            return result;
        }

        public static void Clear() => pending = null;
    }

    public static class HeroCatalog
    {
        public static string Name(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "XIRO",
            HeroClassId.Bomber => "KORR",
            HeroClassId.Guardian => "BRAX",
            HeroClassId.Arcanist => "ORION",
            _ => "REX"
        };

        public static string Role(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "OATH WARDEN",
            HeroClassId.Bomber => "BLAST ENGINEER",
            HeroClassId.Guardian => "FORGE GUARDIAN",
            HeroClassId.Arcanist => "RIFT ARCANIST",
            _ => "RIFT RANGER"
        };

        public static string Kit(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "OATHBLADE  ·  BLADE GUARD  ·  ASH WAVE",
            HeroClassId.Bomber => "THROWN CHARGES  ·  STICKY MINE  ·  BLAST CORD",
            HeroClassId.Guardian => "HAMMER COMBO  ·  PERFECT SLAM  ·  BULL RUSH",
            HeroClassId.Arcanist => "ARC BOLTS  ·  GRAVITY BURST  ·  BLACK STAR",
            _ => "RIFT ARROWS  ·  PIERCING DRAW  ·  ARROW STORM"
        };

        public static Color Accent(HeroClassId hero) => hero switch
        {
            // Saeuregruen: unterscheidet sich klar von Orange, Violett und Tuerkis der anderen drei.
            HeroClassId.Bomber => new Color(0.72f, 0.93f, 0.12f),
            // Warmes Weissgold. Das einzige helle Akzentlicht im Turm - es soll wie Schutz
            // aussehen und nicht wie noch eine Schadensfarbe.
            HeroClassId.Paladin => new Color(1f, 0.93f, 0.66f),
            HeroClassId.Guardian => new Color(1f, 0.52f, 0.08f),
            HeroClassId.Arcanist => new Color(0.66f, 0.28f, 1f),
            _ => new Color(0.04f, 0.88f, 0.92f)
        };

        public static float BaseHealth(HeroClassId hero) => hero switch
        {
            HeroClassId.Bomber => 98f,
            // Mehr als Brax: sie haelt nicht nur sich, sondern stellt sich vor die Gruppe.
            HeroClassId.Paladin => 160f,
            HeroClassId.Guardian => 145f,
            HeroClassId.Arcanist => 92f,
            _ => 105f
        };

        public static float BaseSpeed(HeroClassId hero) => hero switch
        {
            HeroClassId.Bomber => 6f,
            // Die langsamste im Spiel. Wer Raeume sichert, kommt nicht ueberall hin.
            HeroClassId.Paladin => 4.9f,
            HeroClassId.Guardian => 5.25f,
            HeroClassId.Arcanist => 5.8f,
            _ => 6.25f
        };

        public static float BaseDamage(HeroClassId hero) => hero switch
        {
            // Niedriger als bei den anderen: seine Bomben treffen mehrere und zuenden verzoegert,
            // also zaehlt nicht der einzelne Schlag.
            HeroClassId.Bomber => 10f,
            // Weniger als Brax: ihr Wert liegt darin, was sie der Gruppe abnimmt, nicht im Schlag.
            HeroClassId.Paladin => 14f,
            HeroClassId.Guardian => 17f,
            HeroClassId.Arcanist => 12.5f,
            _ => 11.5f
        };

        public static float SkillCooldown(HeroClassId hero) => hero switch
        {
            HeroClassId.Bomber => 7.5f,
            HeroClassId.Paladin => 9f,
            HeroClassId.Guardian => 6.5f,
            HeroClassId.Arcanist => 8f,
            _ => 7f
        };

        public static string LightAttackName(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "OATHBLADE",
            HeroClassId.Bomber => "THROWN CHARGE",
            HeroClassId.Guardian => "HAMMER",
            HeroClassId.Arcanist => "ARC BOLT",
            _ => "RIFT ARROW"
        };

        public static string HeavyAttackName(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "BLADE GUARD",
            HeroClassId.Bomber => "STICKY MINE",
            HeroClassId.Guardian => "GROUND BREAKER",
            HeroClassId.Arcanist => "GRAVITY BURST",
            _ => "PIERCING DRAW"
        };

        public static string SkillName(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "ASH WAVE",
            HeroClassId.Bomber => "BLAST CORD",
            HeroClassId.Guardian => "BULL RUSH",
            HeroClassId.Arcanist => "BLACK STAR",
            _ => "ARROW STORM"
        };

        public static string UltimateName(HeroClassId hero) => hero switch
        {
            HeroClassId.Paladin => "AEGIS",
            HeroClassId.Bomber => "CHAIN DETONATOR",
            HeroClassId.Guardian => "FORGE PLUNGE",
            HeroClassId.Arcanist => "TIME RIFT",
            _ => "HUNTERS FOCUS"
        };
    }

    public static class FloorCatalog
    {
        public static FloorTheme ThemeFor(int floor)
            => (FloorTheme)Mathf.Abs((Mathf.Max(1, floor) - 1) % 3);

        public static string Name(FloorTheme theme) => theme switch
        {
            FloorTheme.EmberFoundry => "THE EMBER FOUNDRY",
            FloorTheme.AstralArchive => "THE ASTRAL ARCHIVE",
            _ => "THE FORGOTTEN COURT"
        };
    }

    public static class RelicCatalog
    {
        public static string Name(RelicId relic) => relic switch
        {
            RelicId.WindstepSigil => "WINDSTEP SIGIL",
            RelicId.HuntersMark => "HUNTER'S MARK",
            RelicId.DawnSeed => "DAWN SEED",
            RelicId.EmberLens => "EMBER LENS",
            RelicId.ArcBattery => "ARC BATTERY",
            RelicId.FortunePrism => "FORTUNE PRISM",
            RelicId.IronHeart => "IRON HEART",
            RelicId.SwiftBoots => "SWIFT BOOTS",
            RelicId.VengeanceCoil => "VENGEANCE COIL",
            RelicId.SiphonStone => "SIPHON STONE",
            RelicId.FocusCrystal => "FOCUS CRYSTAL",
            RelicId.SurgeCore => "SURGE CORE",
            RelicId.TwinCharge => "TWIN CHARGE",
            RelicId.GuardPlate => "GUARD PLATE",
            _ => "GOLD VEIN"
        };

        public static string Description(RelicId relic) => relic switch
        {
            RelicId.WindstepSigil => "+1 dash charge",
            RelicId.HuntersMark => "+10% critical chance",
            RelicId.DawnSeed => "Heal 10 after every floor",
            RelicId.EmberLens => "Heavy attacks erupt on impact",
            RelicId.ArcBattery => "Heavy meter charges 25% faster",
            RelicId.FortunePrism => "+25% secured shards",
            RelicId.IronHeart => "+30 maximum health",
            RelicId.SwiftBoots => "+12% movement speed",
            RelicId.VengeanceCoil => "+15% damage below 40% health",
            RelicId.SiphonStone => "Heal 3% of damage dealt",
            RelicId.FocusCrystal => "Skill cooldown 20% shorter",
            RelicId.SurgeCore => "Ultimate charges 20% faster",
            RelicId.TwinCharge => "Light attacks 10% faster",
            RelicId.GuardPlate => "Take 10% less damage",
            _ => "+30% gold from enemies"
        };
    }
}
