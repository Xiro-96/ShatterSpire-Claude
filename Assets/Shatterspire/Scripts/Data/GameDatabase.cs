using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der einzige Einstiegspunkt in die Inhaltsdaten. Liegt bewusst als einzige
    /// Datei unter <c>Resources/</c> — alles andere hängt als Referenz daran und
    /// landet dadurch im Build, ohne selbst in <c>Resources/</c> liegen zu müssen.
    /// Damit lässt sich in Etage 7 auf Addressables umstellen, ohne jedes Asset
    /// anzufassen.
    /// </summary>
    [CreateAssetMenu(menuName = "Shatterspire/Game Database", fileName = "ShatterspireDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "ShatterspireDatabase";

        [SerializeField] private List<HeroDefinition> heroes = new();
        [SerializeField] private List<EnemyDefinition> enemies = new();
        [SerializeField] private List<RelicDefinition> relics = new();
        [SerializeField] private List<PerkAsset> perks = new();

        private Dictionary<HeroClassId, HeroDefinition> heroLookup;
        private Dictionary<EnemyKind, EnemyDefinition> enemyLookup;
        private Dictionary<RelicId, RelicDefinition> relicLookup;
        private Dictionary<PerkId, PerkAsset> perkLookup;

        private static GameDatabase cached;

        public IReadOnlyList<HeroDefinition> Heroes => heroes;
        public IReadOnlyList<EnemyDefinition> Enemies => enemies;
        public IReadOnlyList<RelicDefinition> Relics => relics;
        public IReadOnlyList<PerkAsset> Perks => perks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

        /// <summary>
        /// Lädt die Datenbank oder gibt <c>null</c> zurück, wenn der Migrationslauf
        /// noch nicht stattgefunden hat. Aufrufer sollen den Fall behandeln, damit
        /// ein frisch geklontes Repository nicht mit NullReference startet.
        /// </summary>
        public static GameDatabase Instance
        {
            get
            {
                if (cached) return cached;
                cached = Resources.Load<GameDatabase>(ResourcePath);
                if (!cached)
                    Debug.LogError($"GameDatabase: '{ResourcePath}' nicht in Resources gefunden. " +
                                   "Im Editor einmal SHATTERSPIRE > Kataloge zu Assets migrieren ausführen.");
                return cached;
            }
        }

        public HeroDefinition Hero(HeroClassId id)
        {
            heroLookup ??= Build(heroes, definition => definition.Id);
            return heroLookup.TryGetValue(id, out var found) ? found : Missing<HeroDefinition>(id);
        }

        public EnemyDefinition Enemy(EnemyKind kind)
        {
            enemyLookup ??= Build(enemies, definition => definition.Kind);
            return enemyLookup.TryGetValue(kind, out var found) ? found : Missing<EnemyDefinition>(kind);
        }

        public RelicDefinition Relic(RelicId id)
        {
            relicLookup ??= Build(relics, definition => definition.Id);
            return relicLookup.TryGetValue(id, out var found) ? found : Missing<RelicDefinition>(id);
        }

        public PerkAsset Perk(PerkId id)
        {
            perkLookup ??= Build(perks, definition => definition.Id);
            return perkLookup.TryGetValue(id, out var found) ? found : Missing<PerkAsset>(id);
        }

        /// <summary>
        /// Prüft, ob für jeden Enum-Wert genau eine Definition vorliegt. Wird vom
        /// Migrationslauf und von den Tests benutzt, damit ein vergessener Eintrag
        /// auffällt, bevor er im Spiel als fehlender Gegner erscheint.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            Check<HeroClassId, HeroDefinition>(problems, "Held", heroes, definition => definition.Id);
            Check<EnemyKind, EnemyDefinition>(problems, "Gegner", enemies, definition => definition.Kind);
            Check<RelicId, RelicDefinition>(problems, "Relic", relics, definition => definition.Id);
            Check<PerkId, PerkAsset>(problems, "Perk", perks, definition => definition.Id);
            return problems;
        }

        /// <summary>Nur für den einmaligen Migrationslauf im Editor.</summary>
        public void Fill(List<HeroDefinition> heroList, List<EnemyDefinition> enemyList,
            List<RelicDefinition> relicList, List<PerkAsset> perkList)
        {
            heroes = heroList;
            enemies = enemyList;
            relics = relicList;
            perks = perkList;
            heroLookup = null;
            enemyLookup = null;
            relicLookup = null;
            perkLookup = null;
        }

        private static Dictionary<TKey, TValue> Build<TKey, TValue>(List<TValue> source,
            System.Func<TValue, TKey> keyOf) where TValue : ScriptableObject
        {
            var map = new Dictionary<TKey, TValue>();
            foreach (var entry in source)
            {
                if (!entry) continue;
                map[keyOf(entry)] = entry;
            }
            return map;
        }

        private static TValue Missing<TValue>(object key) where TValue : ScriptableObject
        {
            Debug.LogError($"GameDatabase: keine {typeof(TValue).Name} für '{key}'. " +
                           "Migrationslauf erneut ausführen oder Eintrag ergänzen.");
            return null;
        }

        private static void Check<TKey, TValue>(List<string> problems, string label, List<TValue> source,
            System.Func<TValue, TKey> keyOf) where TValue : ScriptableObject
        {
            var seen = new HashSet<TKey>();
            for (var i = 0; i < source.Count; i++)
            {
                if (!source[i])
                {
                    problems.Add($"{label}: Eintrag {i} ist leer.");
                    continue;
                }
                if (!seen.Add(keyOf(source[i])))
                    problems.Add($"{label}: '{keyOf(source[i])}' ist doppelt vergeben.");
            }
            foreach (TKey value in System.Enum.GetValues(typeof(TKey)))
                if (!seen.Contains(value)) problems.Add($"{label}: '{value}' fehlt.");
        }
    }
}
