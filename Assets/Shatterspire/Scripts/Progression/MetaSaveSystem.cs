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

        /// <summary>Prestige-Schritte je Held, nach <see cref="HeroClassId"/>. Seit Version 6.</summary>
        public int[] heroPrestige = Array.Empty<int>();

        /// <summary>Helden-Abzeichen je Held, nach <see cref="HeroClassId"/>. Seit Version 6.</summary>
        public int[] heroBadges = Array.Empty<int>();
    }

    public static class MetaSaveSystem
    {
        private const int CurrentVersion = 6;
        private const string Key = "shatterspire.meta.v6";
        private const string KeyV5 = "shatterspire.meta.v5";
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

        /// <summary>
        /// Ein Selbsttest liest keinen Spielstand und schreibt keinen. Jeder Lauf beginnt mit einem
        /// frischen Helden - ohne Prestige, ohne Relikte -, und kein Lauf veraendert den naechsten.
        /// Sonst wuerde jeder Durchgang den folgenden leichter machen, und zwei Messungen waeren nicht
        /// mehr vergleichbar. Kein Spielzustand, sondern die Frage, ob dieses Programm etwas behalten
        /// darf - deshalb statisch.
        /// </summary>
        public static bool Ephemeral { get; set; }

        private static MetaSaveData Read()
        {
            if (Ephemeral) return new MetaSaveData { version = CurrentVersion };
            if (TryRead(Key, out var current)) return current;
            // Aeltere Staende hochziehen statt wegwerfen. Wer schon Shards und
            // Upgrades hat, soll sie behalten.
            if (TryRead(KeyV5, out var v5)) return Migrate(v5);
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
            if (data.version < 6) GrantPrestigeFromExperience(data);
            data.version = CurrentVersion;
            Save(data);
            Debug.Log($"SHATTERSPIRE: Spielstand auf Version {CurrentVersion} migriert, "
                      + "Shards und Upgrades erhalten.");
            return data;
        }

        // ── Prestige ────────────────────────────────────────────────────────

        public static int PrestigeStep(MetaSaveData data, HeroClassId hero)
            => HeroPrestige.Clamp(Entry(data?.heroPrestige, hero));

        public static int Badges(MetaSaveData data, HeroClassId hero) => Mathf.Max(0, Entry(data?.heroBadges, hero));

        public static void AddBadges(MetaSaveData data, HeroClassId hero, int amount)
        {
            if (data == null || amount <= 0) return;
            data.heroBadges = WithEntry(data.heroBadges, hero, Badges(data, hero) + amount);
        }

        /// <summary>Reicht es fuer den naechsten Schritt?</summary>
        public static bool CanBuyPrestige(MetaSaveData data, HeroClassId hero)
        {
            if (data == null) return false;
            var step = PrestigeStep(data, hero);
            return step < HeroPrestige.MaximumStep
                   && data.shards >= HeroPrestige.ShardCost(step)
                   && Badges(data, hero) >= HeroPrestige.BadgeCost(step);
        }

        /// <summary>Kauft den naechsten Schritt im uebergebenen Stand, ohne zu speichern.</summary>
        public static bool TryBuyPrestige(MetaSaveData data, HeroClassId hero)
        {
            if (!CanBuyPrestige(data, hero)) return false;
            var step = PrestigeStep(data, hero);
            data.shards -= HeroPrestige.ShardCost(step);
            data.heroBadges = WithEntry(data.heroBadges, hero, Badges(data, hero) - HeroPrestige.BadgeCost(step));
            data.heroPrestige = WithEntry(data.heroPrestige, hero, step + 1);
            return true;
        }

        public static bool PurchasePrestige(HeroClassId hero)
        {
            var data = Load();
            if (!TryBuyPrestige(data, hero)) return false;
            Save(data);
            return true;
        }

        /// <summary>
        /// Vor Version 6 stiegen Helden ueber Erfahrung. Die erreichte Stufe wird in Prestige
        /// umgerechnet - wer seinen Helden ausgebaut hatte, verliert nichts davon.
        /// </summary>
        public static void GrantPrestigeFromExperience(MetaSaveData data)
        {
            if (data == null) return;
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            {
                var steps = HeroPrestige.StepsFromLegacyLevel(HeroLevel(data, hero));
                if (steps > PrestigeStep(data, hero)) data.heroPrestige = WithEntry(data.heroPrestige, hero, steps);
            }
        }

        private static int Entry(int[] list, HeroClassId hero)
        {
            var index = (int)hero;
            return list != null && index >= 0 && index < list.Length ? list[index] : 0;
        }

        /// <summary>Setzt einen Eintrag und verlaengert die Liste, falls noetig - alte Staende kennen neue Helden nicht.</summary>
        private static int[] WithEntry(int[] list, HeroClassId hero, int value)
        {
            var index = (int)hero;
            var result = list ?? Array.Empty<int>();
            if (result.Length <= index)
            {
                var grown = new int[index + 1];
                Array.Copy(result, grown, result.Length);
                result = grown;
            }
            result[index] = value;
            return result;
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
        public static (MetaSaveData Save, int Score, int RankPoints, RunMode? UnlockedPath, int Badges) RecordClimb(
            in ClimbResult result, int shardsEarned)
        {
            var data = Load();
            var score = ClimbScore.Evaluate(result);
            // Abzeichen statt Erfahrung: der Held waechst nicht mehr von selbst, sondern beim Schmied.
            // Die Abzeichen gibt es an den Boss-Etagen, auch bei einem gescheiterten Lauf - erreicht
            // ist erreicht.
            var badges = HeroPrestige.BadgesForClimb(result.FloorsCleared);
            AddBadges(data, result.Hero, badges);
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
            return (data, score, rankPoints, unlocked, badges);
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
            data.heroPrestige ??= Array.Empty<int>();
            data.heroBadges ??= Array.Empty<int>();
            return data;
        }

        private static void Save(MetaSaveData data)
        {
            if (Ephemeral) return;
            data = Sanitize(data);
            data.version = CurrentVersion;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
