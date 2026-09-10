using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        private readonly List<EnemyAgent> living = new();
        private Transform player;
        private bool completionOnClear;
        private bool spawningObjectiveEncounter;
        private int encounterTotal;
        private int encounterDefeated;
        private bool bossEncounter;
        private bool clearReported;
        private int activeFloor = 1;
        public event Action WaveCleared;
        public event Action EnemiesCleared;
        public int LivingCount => living.Count;
        public bool IsSpawning => spawningObjectiveEncounter;

        public void Configure(Transform target) => player = target;

        public void SpawnRoom(RoomKind kind, int roomIndex)
        {
            StopAllCoroutines();
            Clear();
            completionOnClear = kind == RoomKind.Boss;
            clearReported = kind == RoomKind.Treasure || kind == RoomKind.Mystery;
            activeFloor = Mathf.Max(1, roomIndex);
            StartCoroutine(SpawnRoutine(kind, roomIndex));
        }

        public void SpawnObjectiveRoom(RoomKind kind, int floorIndex, IReadOnlyList<Vector3> corePositions)
        {
            StopAllCoroutines();
            Clear();
            completionOnClear = false;
            // Encounters are now activated locally by FloorObjectiveController as
            // the party reaches each tower section. Keeping this entry point makes
            // the room/spawner boundary network-friendly.
        }

        public void SpawnObjectiveEncounter(Vector3 center, int floorIndex, int encounterIndex, RoomKind kind)
        {
            if (spawningObjectiveEncounter || living.Count > 0) return;
            completionOnClear = false;
            activeFloor = Mathf.Max(1, floorIndex);
            StartCoroutine(SpawnObjectiveEncounterRoutine(center, floorIndex, encounterIndex, kind));
        }

        private IEnumerator SpawnObjectiveEncounterRoutine(Vector3 center, int floorIndex, int encounterIndex, RoomKind kind)
        {
            spawningObjectiveEncounter = true;
            clearReported = false;
            bossEncounter = false;
            encounterDefeated = 0;
            if (kind == RoomKind.Treasure || kind == RoomKind.Mystery && UnityEngine.Random.value < 0.5f)
            {
                encounterTotal = 0;
                GameEvents.RaiseWaveChanged(0, 0);
                PublishEncounter();
                PrototypeVfx.SpawnExplosion(center + Vector3.up * 0.5f, kind == RoomKind.Treasure ? 3.4f : 2.8f,
                    kind == RoomKind.Treasure ? new Color(1f, 0.72f, 0.1f) : new Color(0.68f, 0.24f, 1f));
                player.GetComponent<Health>()?.Heal(kind == RoomKind.Treasure ? 24f : 12f);
                player.GetComponent<LevelSystem>()?.AddExperience(kind == RoomKind.Treasure ? 22 : 14);
                yield return new WaitForSeconds(0.85f);
                spawningObjectiveEncounter = false;
                clearReported = true;
                EnemiesCleared?.Invoke();
                yield break;
            }
            var count = kind == RoomKind.Elite
                ? 9 + Mathf.Min(2, floorIndex - 1)
                : 6 + Mathf.Min(3, floorIndex);
            encounterTotal = count;
            PublishEncounter();
            var waveCount = count >= 7 ? 2 : 1;
            var spawned = 0;
            for (var wave = 0; wave < waveCount; wave++)
            {
                GameEvents.RaiseWaveChanged(wave + 1, waveCount);
                var remaining = count - spawned;
                var wavesRemaining = waveCount - wave;
                var inWave = Mathf.CeilToInt(remaining / (float)wavesRemaining);
                var positions = new List<Vector3>(inWave);
                var kinds = new List<EnemyKind>(inWave);
                for (var localIndex = 0; localIndex < inWave; localIndex++)
                {
                    var i = spawned + localIndex;
                    var angle = encounterIndex * 41f + i * (360f / count) + wave * 17f;
                    var radius = 4.35f + (i % 3) * 0.72f;
                    var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    var spawnPosition = ClampSpawn(center + offset);
                    if (player && (spawnPosition - player.position).sqrMagnitude < 3.4f * 3.4f)
                    {
                        var away = center - player.position;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.1f) away = Vector3.forward;
                        spawnPosition = ClampSpawn(center + away.normalized * radius);
                    }
                    var enemyKind = kind == RoomKind.Elite && i == 0
                        ? EnemyKind.Elite
                        : i == count - 1 && encounterIndex > 0 ? EnemyKind.Brute
                        : ResolveKind(floorIndex, kind, encounterIndex * 5 + i);
                    positions.Add(spawnPosition);
                    kinds.Add(enemyKind);
                    PrototypeVfx.SpawnEnemyArrival(spawnPosition, enemyKind is EnemyKind.Elite or EnemyKind.Brute);
                }

                // The complete formation is readable before it becomes dangerous.
                // This gives the arrival the authored, arcade-like cadence that the
                // old one-by-one spawns were missing.
                yield return new WaitForSeconds(wave == 0 ? 0.58f : 0.72f);
                for (var localIndex = 0; localIndex < positions.Count; localIndex++)
                {
                    Spawn(kinds[localIndex], positions[localIndex]);
                    yield return new WaitForSeconds(0.075f);
                }
                spawned += inWave;

                if (wave >= waveCount - 1) continue;
                while (living.Count > 0) yield return null;
                yield return new WaitForSeconds(0.9f);
            }
            spawningObjectiveEncounter = false;
            GameEvents.RaiseWaveChanged(0, 0);
            PublishEncounter();
            ReportClearIfReady();
        }

        private static Vector3 ClampSpawn(Vector3 position)
        {
            const float radius = 13.55f;
            var flat = new Vector2(position.x, position.z);
            if (flat.sqrMagnitude <= radius * radius) return position;
            flat = flat.normalized * radius;
            return new Vector3(flat.x, position.y, flat.y);
        }

        private IEnumerator SpawnRoutine(RoomKind kind, int roomIndex)
        {
            if (kind == RoomKind.Treasure || kind == RoomKind.Mystery)
            {
                yield return new WaitForSeconds(1.2f);
                player.GetComponent<Health>().Heal(kind == RoomKind.Treasure ? 20f : 10f);
                player.GetComponent<LevelSystem>().AddExperience(kind == RoomKind.Treasure ? 24 : 14);
                WaveCleared?.Invoke();
                yield break;
            }
            if (kind == RoomKind.Boss)
            {
                clearReported = false;
                bossEncounter = true;
                encounterDefeated = 0;
                encounterTotal = 1;
                Spawn(EnemyKind.IronWarden, new Vector3(0f, 0f, 7f));
                PublishEncounter();
                yield break;
            }
            var count = 4 + roomIndex * 2;
            if (kind == RoomKind.Elite)
            {
                Spawn(EnemyKind.Elite, new Vector3(0f, 0f, 6f));
                count = Mathf.Max(3, roomIndex);
            }
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + UnityEngine.Random.Range(-0.25f, 0.25f);
                var radius = UnityEngine.Random.Range(6.5f, 11.5f);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var roll = UnityEngine.Random.value;
                var enemyKind = roll < 0.5f ? EnemyKind.Crawler : roll < 0.78f ? EnemyKind.Shooter : EnemyKind.Brute;
                Spawn(enemyKind, position);
                yield return new WaitForSeconds(0.08f);
            }
        }

        private static EnemyKind ResolveKind(int floorIndex, RoomKind kind, int seed)
        {
            if (kind == RoomKind.Elite && seed == 0) return EnemyKind.Brute;
            var roll = Mathf.Repeat(seed * 0.37f + floorIndex * 0.19f, 1f);
            if (roll < 0.48f) return EnemyKind.Crawler;
            if (roll < 0.78f) return EnemyKind.Shooter;
            return EnemyKind.Brute;
        }

        private void Spawn(EnemyKind kind, Vector3 position)
        {
            var enemy = EnemyFactory.Create(kind, position, player, activeFloor);
            enemy.Defeated += OnDefeated;
            living.Add(enemy);
        }

        private void OnDefeated(EnemyAgent enemy)
        {
            enemy.Defeated -= OnDefeated;
            living.Remove(enemy);
            encounterDefeated = Mathf.Min(encounterTotal, encounterDefeated + 1);
            PublishEncounter();
            ReportClearIfReady();
        }

        private void LateUpdate()
        {
            var removed = 0;
            for (var i = living.Count - 1; i >= 0; i--)
            {
                if (living[i]) continue;
                living.RemoveAt(i);
                removed++;
            }
            if (removed == 0) return;
            encounterDefeated = Mathf.Min(encounterTotal, encounterDefeated + removed);
            PublishEncounter();
            ReportClearIfReady();
        }

        private void ReportClearIfReady()
        {
            if (clearReported || living.Count != 0 || spawningObjectiveEncounter) return;
            clearReported = true;
            EnemiesCleared?.Invoke();
            if (completionOnClear) WaveCleared?.Invoke();
        }

        private void PublishEncounter()
        {
            if (encounterTotal <= 0)
            {
                GameEvents.RaiseEncounterChanged(0, 0, false);
                return;
            }
            GameEvents.RaiseEncounterChanged(Mathf.Max(0, encounterTotal - encounterDefeated),
                encounterTotal, bossEncounter);
        }

        public void ResetStalledObjectiveEncounter()
        {
            if (living.Count > 0) return;
            StopAllCoroutines();
            spawningObjectiveEncounter = false;
            encounterTotal = 0;
            encounterDefeated = 0;
            bossEncounter = false;
            clearReported = true;
            GameEvents.RaiseWaveChanged(0, 0);
            GameEvents.RaiseEncounterChanged(0, 0, false);
        }

        public void Clear()
        {
            spawningObjectiveEncounter = false;
            clearReported = true;
            foreach (var enemy in living)
                if (enemy)
                {
                    enemy.Defeated -= OnDefeated;
                    Destroy(enemy.gameObject);
                }
            living.Clear();
            encounterTotal = 0;
            encounterDefeated = 0;
            bossEncounter = false;
            GameEvents.RaiseWaveChanged(0, 0);
            GameEvents.RaiseEncounterChanged(0, 0, false);
        }
    }
}
