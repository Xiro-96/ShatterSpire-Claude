using System;
using UnityEngine;

namespace Shatterspire
{
    [Serializable]
    public sealed class MetaSaveData
    {
        public int shards;
        public int runs;
        public int victories;
        public int bestFloor;
        public int vitalityLevel;
        public int mightLevel;
        public int agilityLevel;
        public int[] equippedRelics = { 0, 1 };
    }

    public static class MetaSaveSystem
    {
        private const string Key = "shatterspire.meta.v2";
        private const string LegacyKey = "shatterspire.meta.v1";

        public static MetaSaveData Load()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                try { return Sanitize(JsonUtility.FromJson<MetaSaveData>(PlayerPrefs.GetString(Key))); }
                catch { return new MetaSaveData(); }
            }
            if (!PlayerPrefs.HasKey(LegacyKey)) return new MetaSaveData();
            try
            {
                var migrated = Sanitize(JsonUtility.FromJson<MetaSaveData>(PlayerPrefs.GetString(LegacyKey)));
                Save(migrated);
                return migrated;
            }
            catch { return new MetaSaveData(); }
        }

        public static MetaSaveData RecordRun(bool victory, int shards, int floorReached)
        {
            var data = Load();
            data.runs++;
            if (victory) data.victories++;
            data.shards += Mathf.Max(0, shards);
            data.bestFloor = Mathf.Max(data.bestFloor, floorReached);
            Save(data);
            return data;
        }

        public static int UpgradeLevel(MetaSaveData data, MetaUpgradeId id) => id switch
        {
            MetaUpgradeId.Vitality => data.vitalityLevel,
            MetaUpgradeId.Might => data.mightLevel,
            _ => data.agilityLevel
        };

        public static int UpgradeCost(MetaSaveData data, MetaUpgradeId id)
            => 35 + UpgradeLevel(data, id) * 30;

        public static bool Purchase(MetaUpgradeId id)
        {
            var data = Load();
            var cost = UpgradeCost(data, id);
            if (data.shards < cost || UpgradeLevel(data, id) >= 10) return false;
            data.shards -= cost;
            switch (id)
            {
                case MetaUpgradeId.Vitality: data.vitalityLevel++; break;
                case MetaUpgradeId.Might: data.mightLevel++; break;
                case MetaUpgradeId.Agility: data.agilityLevel++; break;
            }
            Save(data);
            return true;
        }

        public static void SaveRelics(System.Collections.Generic.IReadOnlyList<RelicId> relics)
        {
            var data = Load();
            var count = Mathf.Min(3, relics?.Count ?? 0);
            data.equippedRelics = new int[count];
            for (var i = 0; i < count; i++) data.equippedRelics[i] = (int)relics[i];
            Save(data);
        }

        private static MetaSaveData Sanitize(MetaSaveData data)
        {
            data ??= new MetaSaveData();
            data.equippedRelics ??= System.Array.Empty<int>();
            return data;
        }

        private static void Save(MetaSaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Sanitize(data)));
            PlayerPrefs.Save();
        }
    }
}
