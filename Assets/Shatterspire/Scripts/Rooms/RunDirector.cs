using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    public sealed class RunDirector : MonoBehaviour
    {
        private Transform player;
        private Health playerHealth;
        private EnemySpawner spawner;
        private PrototypeHUD hud;
        private GameObject roomDecor;
        private int roomIndex;
        private int shards;
        private RoomKind currentKind;
        private bool ended;
        private bool transitioning;
        private bool downed;
        private int knockouts;
        private const int MaximumKnockouts = 3;
        private RunConfig config;
        private PlayerBuild build;

        public void Configure(Transform playerTransform, EnemySpawner enemySpawner, PrototypeHUD prototypeHud, RunConfig runConfig)
        {
            player = playerTransform;
            playerHealth = player.GetComponent<Health>();
            spawner = enemySpawner;
            hud = prototypeHud;
            config = runConfig ?? new RunConfig();
            build = player.GetComponent<PlayerBuild>();
            spawner.WaveCleared += CompleteRoom;
            playerHealth.Died += OnPlayerDied;
            GameEvents.RaiseKnockoutChanged(0, MaximumKnockouts, 0f, false);
            StartRoom(RoomKind.Combat);
        }

        private void OnDestroy()
        {
            if (spawner) spawner.WaveCleared -= CompleteRoom;
            if (playerHealth) playerHealth.Died -= OnPlayerDied;
        }

        private void StartRoom(RoomKind kind)
        {
            if (ended) return;
            transitioning = false;
            roomIndex++;
            currentKind = roomIndex % 5 == 0 ? RoomKind.Boss : kind;
            player.position = currentKind == RoomKind.Boss ? new Vector3(0f, 0f, -9f) : new Vector3(0f, 0f, -11f);
            player.rotation = Quaternion.identity;
            AuthoredArt.ApplyFloorTheme(roomIndex);
            BuildLayout(roomIndex, currentKind);
            GameEvents.RaiseRoomStarted(roomIndex, currentKind);
            if (currentKind == RoomKind.Boss)
            {
                GameEvents.RaiseObjectiveChanged(0, 1, "DEFEAT THE SPIRE WARDEN");
                GameEvents.RaiseObjectiveTargetChanged(Vector3.zero, string.Empty, false);
                spawner.SpawnRoom(currentKind, roomIndex);
                return;
            }

            var objective = roomDecor.AddComponent<FloorObjectiveController>();
            objective.Configure(player, spawner, roomIndex, currentKind, CompleteRoom);
            spawner.SpawnObjectiveRoom(currentKind, roomIndex, objective.CorePositions);
        }

        private void CompleteRoom()
        {
            if (ended || transitioning) return;
            transitioning = true;
            spawner.Clear();
            var baseReward = currentKind switch { RoomKind.Elite => 14, RoomKind.Boss => 50, RoomKind.Treasure => 8, _ => 6 };
            var tier = Mathf.Max(0, (roomIndex - 1) / 5);
            shards += Mathf.RoundToInt(baseReward * (1f + tier * 0.35f));
            GameEvents.RaiseRoomCompleted(roomIndex, currentKind);
            if (currentKind == RoomKind.Boss)
            {
                var canAscend = config.Mode == RunMode.EndlessTower || roomIndex < 15;
                hud.ShowAscensionChoice(roomIndex, shards, canAscend, () => EndRun(true), Ascend);
                return;
            }
            var healing = currentKind == RoomKind.Treasure ? 35f : currentKind == RoomKind.Mystery ? 18f : 12f;
            if (build && build.HasDawnSeed) healing += 10f;
            playerHealth.Heal(healing);
            hud.ShowFloorUpgrade(() => hud.ShowRoutes(roomIndex + 1, StartRoom));
        }

        private void Ascend()
        {
            if (ended) return;
            playerHealth.Heal(playerHealth.Maximum * 0.3f + (build && build.HasDawnSeed ? 10f : 0f));
            PrototypeVfx.SpawnExplosion(player.position + Vector3.up * 0.7f, 3.2f, new Color(0.68f, 0.24f, 1f));
            hud.ShowFloorUpgrade(() => hud.ShowRoutes(roomIndex + 1, StartRoom));
        }

        private void OnPlayerDied()
        {
            if (ended || downed) return;
            knockouts++;
            if (knockouts >= MaximumKnockouts)
            {
                GameEvents.RaiseKnockoutChanged(knockouts, MaximumKnockouts, 0f, true);
                EndRun(false);
                return;
            }
            StartCoroutine(ReviveRoutine());
        }

        private IEnumerator ReviveRoutine()
        {
            downed = true;
            const float duration = 3.5f;
            var elapsed = 0f;
            while (elapsed < duration && !ended)
            {
                elapsed += Time.deltaTime;
                GameEvents.RaiseKnockoutChanged(knockouts, MaximumKnockouts, Mathf.Clamp01(elapsed / duration), true);
                yield return null;
            }
            if (ended) yield break;
            playerHealth.Revive(0.55f);
            downed = false;
            GameEvents.RaiseKnockoutChanged(knockouts, MaximumKnockouts, 0f, false);
            PrototypeVfx.SpawnExplosion(player.position + Vector3.up * 0.6f, 2.6f, new Color(0.25f, 1f, 0.55f));
        }

        private void EndRun(bool victory)
        {
            if (ended) return;
            ended = true;
            var earned = victory ? shards : Mathf.RoundToInt(shards * 0.65f);
            if (build && build.HasFortunePrism) earned = Mathf.RoundToInt(earned * 1.25f);
            var save = MetaSaveSystem.RecordRun(victory, earned, roomIndex);
            GameEvents.RaiseRunEnded(victory, earned);
            hud.ShowRunEnd(victory, earned, save, roomIndex);
        }

        private void BuildLayout(int index, RoomKind kind)
        {
            if (roomDecor) Destroy(roomDecor);
            roomDecor = new GameObject("Room Layout " + index);
            var accent = kind switch
            {
                RoomKind.Elite => new Color(1f, 0.08f, 0.48f),
                RoomKind.Boss => new Color(1f, 0.36f, 0.04f),
                RoomKind.Treasure => new Color(1f, 0.75f, 0.08f),
                RoomKind.Mystery => new Color(0.62f, 0.15f, 1f),
                _ => new Color(0.08f, 0.78f, 1f)
            };
            if (AuthoredArt.BuildRoomDecor(roomDecor.transform, index, kind, accent)) return;

            var obstacleCount = kind == RoomKind.Boss ? 4 : 3 + index % 4;
            for (var i = 0; i < obstacleCount; i++)
            {
                var angle = (i / (float)obstacleCount) * Mathf.PI * 2f + index * 0.55f;
                var radius = kind == RoomKind.Boss ? 10f : 5.5f + (i % 2) * 3f;
                var position = new Vector3(Mathf.Cos(angle) * radius, 0.65f, Mathf.Sin(angle) * radius);
                var pillar = PrototypeFactory.Primitive(PrimitiveType.Cylinder, "Rune Plinth", position,
                    new Vector3(1.35f + i % 2 * 0.35f, 0.68f, 1.35f + i % 2 * 0.35f), StylizedArt.Ink,
                    keepCollider: true);
                pillar.transform.SetParent(roomDecor.transform);
                var crystal = PrototypeFactory.Primitive(PrimitiveType.Cube, "Spire Crystal", position + Vector3.up * 1.35f,
                    new Vector3(0.62f, 1.35f, 0.62f), accent, true);
                crystal.transform.rotation = Quaternion.Euler(12f, 35f + i * 19f, 45f);
                crystal.transform.SetParent(roomDecor.transform);
                Destroy(crystal.GetComponent<Collider>());
                var halo = PrototypeFactory.Primitive(PrimitiveType.Cylinder, "Crystal Halo", position + Vector3.up * 0.74f,
                    new Vector3(1.65f, 0.035f, 1.65f), Color.Lerp(accent, Color.white, 0.25f), true);
                halo.transform.SetParent(roomDecor.transform);
                Destroy(halo.GetComponent<Collider>());
            }
        }
    }
}
