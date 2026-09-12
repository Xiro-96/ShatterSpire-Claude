#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Einmaliger Umzug der hart kodierten Kataloge in Assets. Liest die Werte aus
    /// <see cref="HeroCatalog"/>, <see cref="EnemyBalance"/>, <see cref="RelicCatalog"/>
    /// und <see cref="PerkCatalog"/> — also aus genau dem Code, aus dem das Spiel
    /// gerade rechnet. Damit wandern die austarierten Zahlen ohne Abtippen.
    ///
    /// Der Lauf ist wiederholbar: bestehende Assets werden aktualisiert, nicht
    /// ersetzt, damit ihre GUIDs und alle Referenzen darauf erhalten bleiben.
    /// </summary>
    internal static class ContentMigration
    {
        private const string ContentRoot = "Assets/Shatterspire/Content";
        private const string ResourcesRoot = "Assets/Shatterspire/Resources";
        private const string HeroFolder = ContentRoot + "/Heroes";
        private const string EnemyFolder = ContentRoot + "/Enemies";
        private const string RelicFolder = ContentRoot + "/Relics";
        private const string PerkFolder = ContentRoot + "/Perks";
        private const string DatabasePath = ResourcesRoot + "/" + GameDatabase.ResourcePath + ".asset";

        [MenuItem("SHATTERSPIRE/Kataloge zu Assets migrieren", false, 20)]
        public static void Migrate()
        {
            EnsureFolder(HeroFolder);
            EnsureFolder(EnemyFolder);
            EnsureFolder(RelicFolder);
            EnsureFolder(PerkFolder);
            EnsureFolder(ResourcesRoot);

            // Bewusst ohne Start/StopAssetEditing: bei so wenigen Assets bringt das
            // Batching nichts, kann aber dazu führen, dass frisch erzeugte Assets
            // innerhalb des Blocks nicht ladbar sind.
            var heroes = MigrateHeroes();
            var enemies = MigrateEnemies();
            var relics = MigrateRelics();
            var perks = MigratePerks();

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            database.Fill(heroes, enemies, relics, perks);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report();
        }

        private static List<HeroDefinition> MigrateHeroes()
        {
            var result = new List<HeroDefinition>();
            foreach (HeroClassId hero in System.Enum.GetValues(typeof(HeroClassId)))
            {
                var asset = LoadOrCreate<HeroDefinition>($"{HeroFolder}/Hero_{hero}.asset");
                asset.Fill(
                    hero,
                    HeroCatalog.Name(hero),
                    HeroCatalog.Role(hero),
                    HeroCatalog.Kit(hero),
                    HeroCatalog.Accent(hero),
                    HeroCatalog.BaseHealth(hero),
                    HeroCatalog.BaseSpeed(hero),
                    HeroCatalog.BaseDamage(hero),
                    HeroCatalog.SkillCooldown(hero),
                    HeroCatalog.LightAttackName(hero),
                    HeroCatalog.HeavyAttackName(hero),
                    HeroCatalog.SkillName(hero));
                EditorUtility.SetDirty(asset);
                result.Add(asset);
            }
            return result;
        }

        private static List<EnemyDefinition> MigrateEnemies()
        {
            var result = new List<EnemyDefinition>();
            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
            {
                var stats = EnemyBalance.For(kind);
                var asset = LoadOrCreate<EnemyDefinition>($"{EnemyFolder}/Enemy_{kind}.asset");
                asset.Fill(stats);
                EditorUtility.SetDirty(asset);
                result.Add(asset);
            }
            return result;
        }

        private static List<RelicDefinition> MigrateRelics()
        {
            var result = new List<RelicDefinition>();
            foreach (RelicId relic in System.Enum.GetValues(typeof(RelicId)))
            {
                var asset = LoadOrCreate<RelicDefinition>($"{RelicFolder}/Relic_{relic}.asset");
                // Die Effekte stehen heute als if-Zweige in PlayerBuild.ConfigureRun
                // und RunDirector. Hier werden sie einmalig zu Zahlen.
                asset.Fill(
                    relic,
                    RelicCatalog.Name(relic),
                    RelicCatalog.Description(relic),
                    dashCharges: relic == RelicId.WindstepSigil ? 1 : 0,
                    critBonus: relic == RelicId.HuntersMark ? 0.1f : 0f,
                    chargeMultiplier: relic == RelicId.ArcBattery ? 1.25f : 1f,
                    healBonus: relic == RelicId.DawnSeed ? 10f : 0f,
                    shards: relic == RelicId.FortunePrism ? 1.25f : 1f,
                    erupts: relic == RelicId.EmberLens,
                    healthBonus: relic == RelicId.IronHeart ? 30f : 0f,
                    moveSpeed: relic == RelicId.SwiftBoots ? 1.12f : 1f,
                    attackSpeed: relic == RelicId.TwinCharge ? 1.1f : 1f,
                    skillCooldown: relic == RelicId.FocusCrystal ? 0.8f : 1f,
                    ultimateCharge: relic == RelicId.SurgeCore ? 1.2f : 1f,
                    gold: relic == RelicId.GoldVein ? 1.3f : 1f,
                    damageTaken: relic == RelicId.GuardPlate ? 0.9f : 1f,
                    lifesteal: relic == RelicId.SiphonStone ? 0.03f : 0f,
                    lowHealthDamage: relic == RelicId.VengeanceCoil ? 0.15f : 0f);
                EditorUtility.SetDirty(asset);
                result.Add(asset);
            }
            return result;
        }

        private static List<PerkAsset> MigratePerks()
        {
            var result = new List<PerkAsset>();
            foreach (var perk in PerkCatalog.All)
            {
                var asset = LoadOrCreate<PerkAsset>($"{PerkFolder}/Perk_{perk.Id}.asset");
                asset.Fill(perk);
                EditorUtility.SetDirty(asset);
                result.Add(asset);
            }
            return result;
        }

        private static void Report()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (!database)
            {
                Debug.LogError("SHATTERSPIRE: Migration fehlgeschlagen, Datenbank nicht gefunden.");
                EditorUtility.DisplayDialog("Migration fehlgeschlagen",
                    "Die Datenbank konnte nicht erzeugt werden. Details in der Console.", "OK");
                return;
            }

            var problems = database.Validate();
            var summary =
                $"{database.Heroes.Count} Helden\n" +
                $"{database.Enemies.Count} Gegner\n" +
                $"{database.Relics.Count} Relics\n" +
                $"{database.Perks.Count} Perks";

            if (problems.Count == 0)
            {
                Debug.Log($"SHATTERSPIRE: Migration fertig.\n{summary}\nDatenbank: {DatabasePath}");
                EditorUtility.DisplayDialog("Migration fertig",
                    $"{summary}\n\nAlles vollständig. Jetzt die EditMode-Tests laufen lassen — " +
                    "sie vergleichen die Assets Wert für Wert mit dem Code.", "OK");
                Selection.activeObject = database;
                return;
            }

            foreach (var problem in problems) Debug.LogWarning("SHATTERSPIRE Migration: " + problem);
            EditorUtility.DisplayDialog("Migration mit Lücken",
                $"{summary}\n\n{problems.Count} Problem(e), Details in der Console:\n\n" +
                string.Join("\n", problems.Take(6)), "OK");
            Selection.activeObject = database;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
