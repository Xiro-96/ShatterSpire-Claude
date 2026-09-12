using System;
using UnityEngine;

namespace Shatterspire
{
    public static class GameEvents
    {
        public static event Action<Health> HealthChanged;
        public static event Action<Health> EntityDied;
        public static event Action<int, int, int> ExperienceChanged;
        public static event Action<int> LevelUp;
        public static event Action<PerkDefinition> PerkSelected;
        public static event Action<int, RoomKind> RoomStarted;
        public static event Action<int, RoomKind> RoomCompleted;
        public static event Action<int, int, string> ObjectiveChanged;
        /// <summary>Anomalie der begonnenen Etage.</summary>
        public static event Action<FloorModifierId> AnomalyChanged;
        public static event Action<Vector3, string, bool> ObjectiveTargetChanged;
        public static event Action<int, int, bool> EncounterChanged;
        public static event Action<int, int> WaveChanged;
        public static event Action<float, float, bool, bool> HeavyAttackChanged;
        public static event Action<int, int, float, bool> KnockoutChanged;
        public static event Action<bool, int> RunEnded;

        public static void RaiseHealthChanged(Health value) => HealthChanged?.Invoke(value);
        public static void RaiseEntityDied(Health value) => EntityDied?.Invoke(value);
        public static void RaiseExperienceChanged(int level, int current, int required) => ExperienceChanged?.Invoke(level, current, required);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
        public static void RaisePerkSelected(PerkDefinition perk) => PerkSelected?.Invoke(perk);
        public static void RaiseRoomStarted(int index, RoomKind kind) => RoomStarted?.Invoke(index, kind);
        public static void RaiseRoomCompleted(int index, RoomKind kind) => RoomCompleted?.Invoke(index, kind);
        public static void RaiseObjectiveChanged(int current, int required, string instruction) => ObjectiveChanged?.Invoke(current, required, instruction);
        public static void RaiseAnomalyChanged(FloorModifierId anomaly) => AnomalyChanged?.Invoke(anomaly);
        public static void RaiseObjectiveTargetChanged(Vector3 position, string label, bool visible)
            => ObjectiveTargetChanged?.Invoke(position, label, visible);
        public static void RaiseEncounterChanged(int remaining, int total, bool boss)
            => EncounterChanged?.Invoke(remaining, total, boss);
        public static void RaiseWaveChanged(int current, int total)
            => WaveChanged?.Invoke(current, total);
        public static void RaiseHeavyAttackChanged(float meter, float charge, bool charging, bool perfect)
            => HeavyAttackChanged?.Invoke(meter, charge, charging, perfect);
        public static void RaiseKnockoutChanged(int skulls, int maximum, float reviveProgress, bool downed)
            => KnockoutChanged?.Invoke(skulls, maximum, reviveProgress, downed);
        public static void RaiseRunEnded(bool victory, int shards) => RunEnded?.Invoke(victory, shards);

        public static void Reset()
        {
            HealthChanged = null;
            EntityDied = null;
            ExperienceChanged = null;
            LevelUp = null;
            PerkSelected = null;
            RoomStarted = null;
            RoomCompleted = null;
            ObjectiveChanged = null;
            AnomalyChanged = null;
            ObjectiveTargetChanged = null;
            EncounterChanged = null;
            WaveChanged = null;
            HeavyAttackChanged = null;
            KnockoutChanged = null;
            RunEnded = null;
        }
    }
}
