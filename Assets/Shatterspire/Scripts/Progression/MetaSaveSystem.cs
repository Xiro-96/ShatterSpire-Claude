using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    [Serializable]
    public sealed class MetaSaveData
    {
        /// <summary>
        /// Bewusst 0 als Standard, nicht die aktuelle Version: JsonUtility laesst
        /// Felder unberuehrt, die im JSON fehlen. Stuende hier 3, waere ein alter
        /// Spielstand nicht von einem neuen zu unterscheiden.
        /// </summary>
        public int version;

        public int shards;
        /// <summary>Zweite Waehrung. Kommt ueber den Rang am Shift-Ende, nicht ueber Grind.</summary>
        public int tokens;
        public int runs;
        public int victories;
        public int bestFloor;
        public int vitalityLevel;
        public int mightLevel;
        public int agilityLevel;
        public int[] equippedRelics = { 0, 1 };

        /// <summary>Welchem Shift die <see cref="climbScores"/> gehoeren. -1 = noch keiner.</summary>
        public int shiftIndex = -1;
        /// <summary>Die besten Aufstiege des laufenden Shifts, absteigend.</summary>
        public int[] climbScores = Array.Empty<int>();
        /// <summary>Hoechster je erreichter Rang, ueberlebt den Shift-Reset.</summary>
        public int bestRankTier;
        public int lifetimeBestScore;

        /// <summary>
        /// Beim Shift-Wechsel hinterlegte Belohnung, die das Hauptmenue noch
        /// anzeigen soll. -1 im Rang bedeutet: nichts offen.
        /// </summary>
        public int pendingShiftRank = -1;
        public int pendingShiftTokens;

        /// <summary>Zuletzt in der Lobby gewaehlt. Alte Staende ohne die Felder starten mit Ranger und Heroic.</summary>
        public int lastHero;
        public int lastPath = (int)RunMode.Heroic;

        /// <summary>
        /// Erfahrung je Held, nach <see cref="HeroClassId"/> geordnet. Kann kuerzer sein als die
        /// Zahl der Helden - ein alter Stand kennt neuere Helden nicht, und ein Feld, das fehlt,
        /// laesst JsonUtility unberuehrt.
        /// </summary>
        public int[] heroExperience = Array.Empty<int>();

        /// <summary>
        /// Je Pfad (nach <see cref="RunMode"/>) eine Bitmaske der Helden, die ihn geschafft haben.
        /// Seit Version 5. Siehe <see cref="PathProgress"/>.
        /// </summary>
        public int[] pathClears = Array.Empty<int>();
    }

    public static class MetaSaveSystem
    {
        private const int CurrentVersion = 5;
        private const string Key = "shatterspire.meta.v5";
        private const string KeyV4 = "shatterspire.meta.v4";
        private const string KeyV3 = "shatterspire.meta.v3";
        private const string KeyV2 = "shatterspire.meta.v2";
        private const string KeyV1 = "shatterspire.meta.v1";

        public static MetaSaveData Load()
        {
            var data = Read();
            // Der Rollover muss beim Laden passieren, nicht beim Run-Ende: sonst
            // waere ein Shift beliebig verlaengerbar, indem man einfach nicht
            // mehr spielt.
            if (ApplyShiftRollover(data)) Save(data);
            return data;
        }

        private static MetaSaveData Read()
        {
            if (TryRead(Key, out var current)) return current;
            // Aeltere Staende hochziehen statt wegwerfen. Wer schon Shards und
            // Upgrades hat, soll sie behalten.
            if (TryRead(KeyV4, out var v4)) return Migrate(v4);
            if (TryRead(KeyV3, out var v3)) return Migrate(v3);
            if (TryRead(KeyV2, out var v2)) return Migrate(v2);
            if (TryRead(KeyV1, out var v1)) return Migrate(v1);
            return new MetaSaveData { version = CurrentVersion };
        }

        private static bool TryRead(string key, out MetaSaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(key)) return false;
            try { data = Sanitize(JsonUtility.FromJson<MetaSaveData>(PlayerPrefs.GetString(key))); }
            catch { data = null; }
            return data != null;
        }

        private static MetaSaveData Migrate(MetaSaveData data)
        {
            // Vor Version 3 gab es Shift und Rang nicht, vor Version 4 keine Heldenstufen.
            // shiftIndex bleibt -1, damit der erste Rollover nur registriert und nichts auszahlt;
            // die Erfahrung beginnt bei null, ohne dass Shards oder Upgrades verloren gehen.
            if (data.version < 5) GrantLegacyClears(data);
            data.version = CurrentVersion;
            Save(data);
            Debug.Log($"SHATTERSPIRE: Spielstand auf Version {CurrentVersion} migriert, "
                      + "Shards und Upgrades erhalten.");
            return data;
        }

        /// <summary>
        /// Vor Version 5 wurde nicht festgehalten, welcher Held welchen Pfad geschafft hat. Wer aber
        /// schon einmal fuenf Etagen weit kam, hat Brave nachweislich durchgespielt - dem soll die
        /// neue Sperre nichts wegnehmen, was er sich verdient hat. Gutgeschrieben wird dem zuletzt
        /// gewaehlten Helden, weil der Stand nicht mehr hergibt.
        /// </summary>
        public static void GrantLegacyClears(MetaSaveData data)
        {
            if (data == null) return;
            var hero = Enum.IsDefined(typeof(HeroClassId), data.lastHero) ? (HeroClassId)data.lastHero : HeroClassId.Ranger;
            if (data.bestFloor >= PathCatalog.FloorCount(RunMode.Brave))
                PathProgress.MarkCleared(data, RunMode.Brave, hero);
            if (data.bestFloor >= PathCatalog.FloorCount(RunMode.Heroic))
                PathProgress.MarkCleared(data, RunMode.Heroic, hero);
        }

        /// <summary>Gibt true zurueck, wenn gespeichert werden muss.</summary>
        private static bool ApplyShiftRollover(MetaSaveData data)
        {
            var current = ShiftCalendar.CurrentIndex;
            if (data.shiftIndex == current) return false;

            var hadPreviousShift = data.shiftIndex >= 0;
            var closingPoints = RankTable.PointsFrom(data.climbScores);
            if (hadPreviousShift && closingPoints > 0)
            {
                var tier = RankTable.TierFor(closingPoints);
                var reward = RankTable.TokensFor(tier);
                data.tokens += reward;
                data.bestRankTier = Mathf.Max(data.bestRankTier, (int)tier);
                data.pendingShiftRank = (int)tier;
                data.pendingShiftTokens = reward;
            }

            data.shiftIndex = current;
            data.climbScores = Array.Empty<int>();
            return true;
        }

        /// <summary>
        /// Traegt einen abgeschlossenen Aufstieg ein und gibt den gespeicherten
        /// Stand samt Punktzahl zurueck, damit der Endbildschirm ihn zeigen kann.
        /// </summary>
        public static (MetaSaveData Save, int Score, int RankPoints, RunMode? UnlockedPath) RecordClimb(
            in ClimbResult result, int shardsEarned)
        {
            var data = Load();
            var score = ClimbScore.Evaluate(result);
            AddHeroExperience(data, result.Hero, HeroProgress.ExperienceFor(result));
            var unlocked = PathProgress.Record(data, result);

            data.runs++;
            if (result.Extracted) data.victories++;
            data.shards += Mathf.Max(0, shardsEarned);
            data.bestFloor = Mathf.Max(data.bestFloor, result.FloorsCleared);
            data.lifetimeBestScore = Mathf.Max(data.lifetimeBestScore, score);

            var scores = new List<int>(data.climbScores ?? Array.Empty<int>()) { score };
            data.climbScores = KeepBest(scores, RankTable.ClimbCount);

            var rankPoints = RankTable.PointsFrom(data.climbScores);
            data.bestRankTier = Mathf.Max(data.bestRankTier, (int)RankTable.TierFor(rankPoints));

            Save(data);
            return (data, score, rankPoints, unlocked);
        }

        /// <summary>Rangpunkte des laufenden Shifts.</summary>
        public static int RankPoints(MetaSaveData data) => RankTable.PointsFrom(data?.climbScores);

        public static RankTier CurrentRank(MetaSaveData data) => RankTable.TierFor(RankPoints(data));

        /// <summary>
        /// Holt eine offene Shift-Belohnung ab und loescht sie. Ohne das Loeschen
        /// wuerde das Hauptmenue sie bei jedem Start erneut ankuendigen.
        /// </summary>
        public static bool ConsumeShiftReward(out RankTier tier, out int tokens)
        {
            var data = Load();
            tier = RankTier.Splinter;
            tokens = 0;
            if (data.pendingShiftRank < 0) return false;
            tier = (RankTier)Mathf.Clamp(data.pendingShiftRank, 0, (int)RankTier.Spire);
            tokens = data.pendingShiftTokens;
            data.pendingShiftRank = -1;
            data.pendingShiftTokens = 0;
            Save(data);
            return true;
        }

        private static int[] KeepBest(List<int> scores, int count)
        {
            scores.Sort();
            scores.Reverse();
            var keep = Mathf.Min(count, scores.Count);
            var result = new int[keep];
            for (var i = 0; i < keep; i++) result[i] = Mathf.Max(0, scores[i]);
            return result;
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

        public static void SaveRelics(IReadOnlyList<RelicId> relics)
        {
            var data = Load();
            var count = Mathf.Min(3, relics?.Count ?? 0);
            data.equippedRelics = new int[count];
            for (var i = 0; i < count; i++) data.equippedRelics[i] = (int)relics[i];
            Save(data);
        }

        public static void SaveLobbySelection(HeroClassId hero, RunMode path)
        {
            var data = Load();
            if (data.lastHero == (int)hero && data.lastPath == (int)path) return;
            data.lastHero = (int)hero;
            data.lastPath = (int)path;
            Save(data);
        }

        /// <summary>Erfahrung eines Helden, 0 wenn der Stand ihn noch nicht kennt.</summary>
        public static int HeroExperience(MetaSaveData data, HeroClassId hero)
        {
            var index = (int)hero;
            var list = data?.heroExperience;
            return list != null && index >= 0 && index < list.Length ? Mathf.Max(0, list[index]) : 0;
        }

        public static int HeroLevel(MetaSaveData data, HeroClassId hero)
            => HeroProgress.LevelFor(HeroExperience(data, hero));

        /// <summary>
        /// Traegt Erfahrung ein und verlaengert die Liste, falls noetig. Ein alter Stand kennt
        /// neuere Helden nicht - die Liste waechst dann mit, statt den Eintrag zu verwerfen.
        /// </summary>
        private static void AddHeroExperience(MetaSaveData data, HeroClassId hero, int amount)
        {
            if (amount <= 0) return;
            var needed = (int)hero + 1;
            var list = data.heroExperience ?? Array.Empty<int>();
            if (list.Length < needed)
            {
                var grown = new int[needed];
                Array.Copy(list, grown, list.Length);
                list = grown;
            }
            list[(int)hero] = Mathf.Max(0, list[(int)hero]) + amount;
            data.heroExperience = list;
        }

        private static MetaSaveData Sanitize(MetaSaveData data)
        {
            data ??= new MetaSaveData();
            data.equippedRelics ??= Array.Empty<int>();
            data.climbScores ??= Array.Empty<int>();
            data.heroExperience ??= Array.Empty<int>();
            data.pathClears ??= Array.Empty<int>();
            return data;
        }

        private static void Save(MetaSaveData data)
        {
            data = Sanitize(data);
            data.version = CurrentVersion;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
