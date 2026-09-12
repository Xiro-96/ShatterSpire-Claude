using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Gegner einer Etage, in zwei Arten:
    /// Lager warten in den Raeumen und greifen erst an, wenn man ihnen nahe kommt - wie die
    /// Gegnergruppen in den Arealen von R.I.S.E. Verteidigungswellen ruecken beim Aktivieren eines
    /// Power Cores an. Dazu der Warden auf Boss-Etagen.
    ///
    /// Vorher erschienen alle Gegner als Wellen rund um den Spieler.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        private readonly List<EnemyAgent> camps = new();
        private readonly List<EnemyAgent> encounter = new();
        private readonly List<EnemyAgent> bosses = new();
        private readonly Dictionary<EnemyAgent, int> campOf = new();
        private Transform player;
        private FloorNavigation navigation;
        private int activeFloor = 1;
        /// <summary>
        /// Anomalie der laufenden Etage. Liegt am Spawner und nicht in einer statischen Ablage:
        /// im Co-op laeuft jede Instanz ihre eigene Etage, und zwischen zwei Etagen muss der Wert
        /// sauber wechseln.
        /// </summary>
        private FloorModifier modifier = FloorModifierCatalog.For(FloorModifierId.None);
        private bool spawningEncounter;
        private int encounterTotal;
        private int encounterDefeated;
        private bool encounterClearReported = true;
        private bool bossClearReported = true;

        /// <summary>Der Warden der Boss-Etage ist besiegt.</summary>
        public event Action WaveCleared;

        /// <summary>Die Verteidigungswelle eines Power Cores ist geschlagen.</summary>
        public event Action EnemiesCleared;

        public int EncounterCount => encounter.Count;
        public int LivingCount => camps.Count + encounter.Count + bosses.Count;
        public bool IsSpawning => spawningEncounter;

        public void Configure(Transform target) => player = target;

        public void BeginFloor(FloorNavigation floorNavigation, int floor)
            => BeginFloor(floorNavigation, floor, FloorModifierId.None);

        public void BeginFloor(FloorNavigation floorNavigation, int floor, FloorModifierId anomaly)
        {
            Clear();
            navigation = floorNavigation;
            activeFloor = Mathf.Max(1, floor);
            modifier = FloorModifierCatalog.For(anomaly);
        }

        public void SpawnCamps(FloorLayout layout, RoomKind kind)
        {
            foreach (var room in layout.Rooms)
            {
                if (room.CampSize <= 0) continue;
                // Aufgerundet: bei Faktor 1,85 soll aus einem Zweierlager ein Viererlager werden,
                // nicht ein Dreier. Der Schwarm muss sich als Schwarm anfuehlen.
                var campSize = Mathf.Max(1, Mathf.CeilToInt(room.CampSize * modifier.EnemyCount));
                for (var i = 0; i < campSize; i++)
                {
                    var angle = i * (360f / campSize) + room.Index * 23f;
                    var radius = 2.2f + (i % 2) * 1.3f;
                    var position = room.CampCenter + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    var enemyKind = kind == RoomKind.Elite && i == 0 && room.Role != RoomRole.Lift
                        ? EnemyKind.Elite
                        : i == campSize - 1 && campSize >= 4 ? EnemyKind.Brute
                        : ResolveKind(activeFloor, kind, room.Index * 5 + i);
                    // Jeder bleibt an seinem Platz im Lager, statt zur Mitte zu schlurfen:
                    // schlafende Skelette liegen dort, wo sie liegen.
                    var spot = Walkable(position);
                    var agent = Spawn(enemyKind, spot, spot, true);
                    camps.Add(agent);
                    campOf[agent] = room.Index;
                    agent.Engaged += OnCampEngaged;
                }
            }
            Debug.Log($"SHATTERSPIRE Lager: {camps.Count} Gegner auf Etage {activeFloor}, "
                      + $"Anomalie {modifier.Id} (Zahl x{modifier.EnemyCount:0.##}).");
        }

        public void SpawnBoss(Vector3 position, int floorIndex)
        {
            activeFloor = Mathf.Max(1, floorIndex);
            bossClearReported = false;
            // Der Warden wartet in seinem Raum und greift an, sobald die Gruppe eintritt.
            bosses.Add(Spawn(EnemyKind.IronWarden, position, position, true));
            GameEvents.RaiseEncounterChanged(1, 1, true);
        }

        public void SpawnObjectiveEncounter(Vector3 center, int floorIndex, int encounterIndex, RoomKind kind)
        {
            if (spawningEncounter || encounter.Count > 0) return;
            activeFloor = Mathf.Max(1, floorIndex);
            StartCoroutine(SpawnObjectiveEncounterRoutine(center, floorIndex, encounterIndex, kind));
        }

        private IEnumerator SpawnObjectiveEncounterRoutine(Vector3 center, int floorIndex, int encounterIndex, RoomKind kind)
        {
            spawningEncounter = true;
            encounterClearReported = false;
            encounterDefeated = 0;

            var noDefenders = kind == RoomKind.Treasure || (kind == RoomKind.Mystery && UnityEngine.Random.value < 0.5f);
            if (noDefenders)
            {
                encounterTotal = 0;
                PublishEncounter();
                PrototypeVfx.SpawnExplosion(center + Vector3.up * 0.5f, kind == RoomKind.Treasure ? 3.4f : 2.8f,
                    kind == RoomKind.Treasure ? new Color(1f, 0.72f, 0.1f) : new Color(0.68f, 0.24f, 1f));
                if (player)
                {
                    player.GetComponent<Health>()?.Heal(kind == RoomKind.Treasure ? 24f : 12f);
                    player.GetComponent<LevelSystem>()?.AddExperience(kind == RoomKind.Treasure ? 22 : 14);
                }
                yield return new WaitForSeconds(0.85f);
                spawningEncounter = false;
                ReportEncounterClearIfReady();
                yield break;
            }

            // Spieltest 11.09.: drei bis vier Verteidiger hielten nur 3 bis 5 Sekunden. Jetzt immer zwei
            // Wellen, zusammen mit fast doppelt so viel Leben je Gegner.
            var count = kind == RoomKind.Elite
                ? 7 + Mathf.Min(5, floorIndex / 2)
                : 5 + Mathf.Min(5, floorIndex / 2);
            count = Mathf.Max(1, Mathf.RoundToInt(count * modifier.EnemyCount));
            encounterTotal = count;
            PublishEncounter();
            var waveCount = count >= 5 ? 2 : 1;
            Debug.Log($"SHATTERSPIRE Verteidiger: Core {encounterIndex + 1} auf Etage {floorIndex}, {count} Gegner in {waveCount} Welle(n).");
            var spawned = 0;
            for (var wave = 0; wave < waveCount; wave++)
            {
                GameEvents.RaiseWaveChanged(wave + 1, waveCount);
                var inWave = Mathf.CeilToInt((count - spawned) / (float)(waveCount - wave));
                var positions = new List<Vector3>(inWave);
                var kinds = new List<EnemyKind>(inWave);
                for (var localIndex = 0; localIndex < inWave; localIndex++)
                {
                    var i = spawned + localIndex;
                    var angle = encounterIndex * 41f + i * (360f / count) + wave * 17f;
                    var radius = 5.2f + (i % 3) * 0.7f;
                    var position = Walkable(center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius);
                    if (player && FlatDistance(position, player.position) < 3.4f)
                    {
                        var away = center - player.position;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.1f) away = Vector3.forward;
                        position = Walkable(center + away.normalized * radius);
                    }
                    var enemyKind = kind == RoomKind.Elite && i == 0 ? EnemyKind.Elite
                        : i == count - 1 && count >= 4 ? EnemyKind.Brute
                        : ResolveKind(floorIndex, kind, encounterIndex * 5 + i);
                    positions.Add(position);
                    kinds.Add(enemyKind);
                    PrototypeVfx.SpawnEnemyArrival(position,
                        enemyKind is EnemyKind.Elite or EnemyKind.Brute or EnemyKind.Shieldbearer);
                }

                // Die ganze Formation ist sichtbar, bevor sie gefaehrlich wird.
                yield return new WaitForSeconds(wave == 0 ? 0.58f : 0.72f);
                for (var localIndex = 0; localIndex < positions.Count; localIndex++)
                {
                    encounter.Add(Spawn(kinds[localIndex], positions[localIndex], center, false));
                    yield return new WaitForSeconds(0.075f);
                }
                spawned += inWave;

                if (wave >= waveCount - 1) continue;
                while (encounter.Count > 0) yield return null;
                yield return new WaitForSeconds(0.9f);
            }
            spawningEncounter = false;
            GameEvents.RaiseWaveChanged(0, 0);
            PublishEncounter();
            ReportEncounterClearIfReady();
        }

        private EnemyAgent Spawn(EnemyKind kind, Vector3 position, Vector3 home, bool idle)
        {
            var enemy = EnemyFactory.Create(kind, Walkable(position), player, activeFloor, modifier);
            enemy.SetBehaviour(navigation, home, idle);
            enemy.Defeated += OnDefeated;
            return enemy;
        }

        private Vector3 Walkable(Vector3 position)
            => navigation != null ? navigation.ClampToWalkable(position, 0.6f) : position;

        private void OnCampEngaged(EnemyAgent source)
        {
            // Ein Lager kaempft gemeinsam: wird einer aufmerksam, greifen alle an.
            if (!campOf.TryGetValue(source, out var room)) return;
            foreach (var agent in camps)
                if (agent && agent != source && campOf.TryGetValue(agent, out var other) && other == room)
                    agent.Engage(false);
        }

        private void OnDefeated(EnemyAgent enemy)
        {
            enemy.Defeated -= OnDefeated;
            enemy.Engaged -= OnCampEngaged;
            // Gold fuer den Aufstieg. Der Spawner kennt den Spieler ohnehin, deshalb faellt die
            // Belohnung hier und nicht in einer statischen Kasse.
            // Lebenskugel fallen lassen. Liegt beim Spawner, weil er den Spieler kennt und den
            // Gegner in derselben Meldung hat.
            if (player) HealthOrb.TryDrop(enemy.Kind, enemy.transform.position, player);
            if (player && player.TryGetComponent<RunWallet>(out var wallet))
            {
                var reward = RunWallet.RewardFor(enemy.Kind, activeFloor) * modifier.Gold;
                if (player.TryGetComponent<PlayerBuild>(out var build))
                    reward *= build.GoldMultiplier;
                wallet.Earn(Mathf.Max(1, Mathf.RoundToInt(reward)));
            }
            camps.Remove(enemy);
            campOf.Remove(enemy);
            if (encounter.Remove(enemy))
            {
                encounterDefeated = Mathf.Min(encounterTotal, encounterDefeated + 1);
                PublishEncounter();
                ReportEncounterClearIfReady();
            }
            if (bosses.Remove(enemy))
            {
                GameEvents.RaiseEncounterChanged(0, 0, false);
                ReportBossClearIfReady();
            }
        }

        private void LateUpdate()
        {
            camps.RemoveAll(agent => !agent);
            var removed = encounter.RemoveAll(agent => !agent);
            if (removed > 0)
            {
                encounterDefeated = Mathf.Min(encounterTotal, encounterDefeated + removed);
                PublishEncounter();
                ReportEncounterClearIfReady();
            }
            if (bosses.RemoveAll(agent => !agent) > 0) ReportBossClearIfReady();
        }

        private void ReportEncounterClearIfReady()
        {
            if (encounterClearReported || encounter.Count > 0 || spawningEncounter) return;
            encounterClearReported = true;
            EnemiesCleared?.Invoke();
        }

        private void ReportBossClearIfReady()
        {
            if (bossClearReported || bosses.Count > 0) return;
            bossClearReported = true;
            WaveCleared?.Invoke();
        }

        private void PublishEncounter()
        {
            if (encounterTotal <= 0)
            {
                GameEvents.RaiseEncounterChanged(0, 0, false);
                return;
            }
            GameEvents.RaiseEncounterChanged(Mathf.Max(0, encounterTotal - encounterDefeated), encounterTotal, false);
        }

        public void ResetStalledObjectiveEncounter()
        {
            if (encounter.Count > 0) return;
            StopAllCoroutines();
            spawningEncounter = false;
            encounterTotal = 0;
            encounterDefeated = 0;
            encounterClearReported = true;
            GameEvents.RaiseWaveChanged(0, 0);
            GameEvents.RaiseEncounterChanged(0, 0, false);
        }

        public void Clear()
        {
            StopAllCoroutines();
            spawningEncounter = false;
            encounterClearReported = true;
            bossClearReported = true;
            DestroyAll(camps);
            DestroyAll(encounter);
            DestroyAll(bosses);
            campOf.Clear();
            encounterTotal = 0;
            encounterDefeated = 0;
            GameEvents.RaiseWaveChanged(0, 0);
            GameEvents.RaiseEncounterChanged(0, 0, false);
        }

        private void DestroyAll(List<EnemyAgent> agents)
        {
            foreach (var enemy in agents)
            {
                if (!enemy) continue;
                enemy.Defeated -= OnDefeated;
                enemy.Engaged -= OnCampEngaged;
                Destroy(enemy.gameObject);
            }
            agents.Clear();
        }

        private static EnemyKind ResolveKind(int floorIndex, RoomKind kind, int seed)
        {
            if (kind == RoomKind.Elite && seed == 0) return EnemyKind.Brute;
            var roll = Mathf.Repeat(seed * 0.37f + floorIndex * 0.19f, 1f);
            // Zwei Rollen, die Stellung erzwingen statt nur Druck zu machen: der Schildtraeger muss
            // umlaufen oder mit einem schweren Schlag aufgebrochen werden, dem Armbruster muss man aus
            // der Linie gehen. Auf Etage 1 sind beide selten, ab Etage 2 gehoeren sie zur Mischung.
            if (floorIndex <= 1)
            {
                if (roll < 0.40f) return EnemyKind.Crawler;
                if (roll < 0.64f) return EnemyKind.Shooter;
                if (roll < 0.78f) return EnemyKind.Marksman;
                if (roll < 0.90f) return EnemyKind.Shieldbearer;
                return EnemyKind.Brute;
            }
            if (roll < 0.30f) return EnemyKind.Crawler;
            if (roll < 0.48f) return EnemyKind.Shooter;
            if (roll < 0.66f) return EnemyKind.Marksman;
            if (roll < 0.84f) return EnemyKind.Shieldbearer;
            return EnemyKind.Brute;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
