using System;
using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Steuert einen Aufstieg: baut jede Etage aus einem Seed, setzt Gruppe, Kamera und Gegner,
    /// und entscheidet nach jeder Etage ueber Belohnung, Route und das Ende des Pfads.
    /// </summary>
    public sealed class RunDirector : MonoBehaviour
    {
        private const int MaximumKnockouts = 3;

        private Transform player;
        private Health playerHealth;
        private PlayerController playerController;
        private EnemySpawner spawner;
        private PrototypeHUD hud;
        private CameraController cameraController;
        private CompanionBot[] companions = Array.Empty<CompanionBot>();
        private GameObject floorRoot;
        private FloorNavigation navigation;
        private int roomIndex;
        private int shards;
        private int floorsCleared;
        private int bossesDefeated;
        private int enemiesDefeated;
        private int runSeed;
        private RoomKind currentKind;
        private bool ended;
        private bool transitioning;
        private bool downed;
        private int knockouts;
        private RunConfig config;
        private PlayerBuild build;

        public void Configure(Transform playerTransform, EnemySpawner enemySpawner, PrototypeHUD prototypeHud,
            RunConfig runConfig, CameraController runCamera = null, CompanionBot[] team = null)
        {
            player = playerTransform;
            playerHealth = player.GetComponent<Health>();
            playerController = player.GetComponent<PlayerController>();
            spawner = enemySpawner;
            hud = prototypeHud;
            config = runConfig ?? new RunConfig();
            cameraController = runCamera;
            companions = team ?? Array.Empty<CompanionBot>();
            build = player.GetComponent<PlayerBuild>();
            // Ein Seed je Aufstieg, jede Etage leitet ihren eigenen daraus ab. So ist ein ganzer
            // Aufstieg spaeter reproduzierbar - fuer Fehlersuche und fuer Co-op-Clients.
            runSeed = Environment.TickCount;
            spawner.WaveCleared += CompleteRoom;
            playerHealth.Died += OnPlayerDied;
            GameEvents.EntityDied += OnEntityDied;
            GameEvents.RaiseKnockoutChanged(0, MaximumKnockouts, 0f, false);
            StartRoom(RoomKind.Combat);
        }

        private void OnDestroy()
        {
            if (spawner) spawner.WaveCleared -= CompleteRoom;
            if (playerHealth) playerHealth.Died -= OnPlayerDied;
            GameEvents.EntityDied -= OnEntityDied;
        }

        private void StartRoom(RoomKind kind)
        {
            if (ended) return;
            transitioning = false;
            roomIndex++;
            currentKind = PathCatalog.IsBossFloor(roomIndex) ? RoomKind.Boss
                : kind == RoomKind.Boss ? RoomKind.Combat : kind;

            if (floorRoot) Destroy(floorRoot);
            var seed = unchecked(runSeed * 486187739 + roomIndex * 7919);
            var layout = FloorLayoutGenerator.Generate(seed, roomIndex, currentKind);
            navigation = new FloorNavigation(layout);
            AuthoredArt.ApplyFloorTheme(roomIndex);
            floorRoot = AuthoredArt.BuildFloor(layout, null);

            PlaceParty(layout);
            spawner.BeginFloor(navigation, roomIndex);
            GameEvents.RaiseRoomStarted(roomIndex, currentKind);

            if (currentKind == RoomKind.Boss)
            {
                spawner.SpawnBoss(layout.ExitPoint, roomIndex);
                GameEvents.RaiseObjectiveChanged(0, 1, "DEFEAT THE SPIRE WARDEN");
                GameEvents.RaiseObjectiveTargetChanged(layout.ExitPoint, "SPIRE WARDEN", true);
                return;
            }

            spawner.SpawnCamps(layout, currentKind);
            var objective = floorRoot.AddComponent<FloorObjectiveController>();
            objective.Configure(player, spawner, layout, roomIndex, currentKind, CompleteRoom);
        }

        private void PlaceParty(FloorLayout layout)
        {
            var spawn = layout.SpawnPoint;
            if (playerController)
            {
                playerController.SetNavigation(navigation);
                playerController.Teleport(spawn);
            }
            else
            {
                player.position = spawn;
            }
            player.rotation = Quaternion.identity;

            for (var i = 0; i < companions.Length; i++)
            {
                if (!companions[i]) continue;
                companions[i].SetNavigation(navigation);
                var side = i % 2 == 0 ? -1f : 1f;
                companions[i].Teleport(navigation.ClampToWalkable(spawn + new Vector3(side * 2.4f, 0f, -1.6f), 0.5f));
            }

            if (!cameraController) return;
            cameraController.SetBounds(layout.Bounds);
            cameraController.Snap();
        }

        private void CompleteRoom()
        {
            if (ended || transitioning) return;
            transitioning = true;
            spawner.Clear();
            var baseReward = currentKind switch { RoomKind.Elite => 14, RoomKind.Boss => 50, RoomKind.Treasure => 8, _ => 6 };
            var tier = Mathf.Max(0, (roomIndex - 1) / 5);
            shards += Mathf.RoundToInt(baseReward * (1f + tier * 0.35f));
            floorsCleared++;
            if (currentKind == RoomKind.Boss) bossesDefeated++;
            GameEvents.RaiseRoomCompleted(roomIndex, currentKind);

            if (PathCatalog.IsFinalFloor(config.Mode, roomIndex))
            {
                // Pfad durchgespielt: keine Wahl mehr zwischen Extrahieren und Aufsteigen.
                EndRun(true);
                return;
            }
            if (currentKind == RoomKind.Boss)
            {
                hud.ShowAscensionChoice(roomIndex, shards, true, () => EndRun(true), Ascend);
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
            var result = new ClimbResult(floorsCleared, bossesDefeated, enemiesDefeated,
                earned, victory, config.Mode, config.Hero);
            var record = MetaSaveSystem.RecordClimb(result, earned);
            GameEvents.RaiseRunEnded(victory, earned);
            hud.ShowRunEnd(victory, earned, record.Save, roomIndex, result, record.Score, record.RankPoints);
        }

        private void OnEntityDied(Health value)
        {
            // Fuer die Wertung zaehlen nur gefallene Gegner. Der Spieler stirbt in dieser Liste auch.
            if (value && value.Team == TeamId.Enemy) enemiesDefeated++;
        }
    }
}
