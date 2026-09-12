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
        /// <summary>Wegenetz der laufenden Etage. Wird von der automatischen Vorfuehrung gebraucht.</summary>
        public FloorNavigation Navigation => navigation;
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
                GameEvents.RaiseObjectiveTargetChanged(layout.ExitPoint, Loc.T("SPIRE WARDEN"), true);
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
            StartCoroutine(RideLift());
        }

        private void Ascend()
        {
            if (ended) return;
            playerHealth.Heal(playerHealth.Maximum * 0.3f + (build && build.HasDawnSeed ? 10f : 0f));
            PrototypeVfx.SpawnExplosion(player.position + Vector3.up * 0.7f, 3.2f, new Color(0.68f, 0.24f, 1f));
            StartCoroutine(RideLift());
        }

        /// <summary>
        /// Startet die Aufzugsfahrt von aussen. Nur fuer die automatische Vorfuehrung: eine Etage
        /// wirklich zu schaffen dauert Minuten, und die Fahrt ist die Stelle mit dem Risiko - sie
        /// greift in den CharacterController und in die Blende ein.
        /// </summary>
        public void RideLiftForCapture()
        {
            if (!ended) StartCoroutine(RideLift());
        }

        /// <summary>
        /// Die Aufzugsfahrt zwischen zwei Etagen.
        ///
        /// Vorher war der Etagenwechsel ein harter Schnitt: eine Etage verschwand, die naechste war
        /// da. Der Aufstieg zerfiel dadurch in einzelne Raeume statt sich als ein Turm zu lesen - und
        /// der Aufzug ist in R.I.S.E. genau der Moment, in dem die Gruppe durchatmet und sichtbar
        /// steigt. Jetzt: die Gruppe hebt mit der Plattform ab, das Bild wird schwarz, die Wahl
        /// faellt waehrend der Fahrt, und oben kommt sie auf der naechsten Etage an.
        /// </summary>
        private IEnumerator RideLift()
        {
            if (ended) yield break;
            var riders = Riders();
            var lifted = new Vector3[riders.Length];
            for (var i = 0; i < riders.Length; i++) lifted[i] = riders[i].position;

            Sfx.Play2D(Sound.LiftRise);
            const float climbSeconds = 1.5f;
            const float climbHeight = 11f;
            var elapsed = 0f;
            while (elapsed < climbSeconds)
            {
                var t = elapsed / climbSeconds;
                // Langsam anfahren, gleichmaessig weiter: so liest es sich als Maschine, nicht als Sprung.
                var height = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 1.35f)) * climbHeight;
                for (var i = 0; i < riders.Length; i++)
                {
                    if (!riders[i]) continue;
                    var target = new Vector3(lifted[i].x, lifted[i].y + height, lifted[i].z);
                    // Der Held geht ueber dieselbe Sperre wie beim Schmiedesturz: sonst zieht ihn
                    // sein eigener Controller im selben Bild wieder herunter.
                    if (riders[i] == player && playerController) playerController.Airborne(target);
                    else riders[i].position = target;
                }
                // Die letzte halbe Sekunde schliesst die Blende.
                if (t > 0.66f) hud.Fade(1f, 0.45f);
                elapsed += Time.deltaTime;
                yield return null;
            }
            hud.Fade(1f, 0.15f);
            while (!hud.FadeOpaque) yield return null;

            // Oben angekommen: jetzt faellt die Wahl, danach steht die neue Etage.
            // Sperre loesen, bevor die naechste Etage die Gruppe setzt.
            if (playerController) playerController.Land(player.position);
            hud.ShowFloorUpgrade(() => hud.ShowRoutes(roomIndex + 1, kind =>
            {
                StartRoom(kind);
                Sfx.Play2D(Sound.LiftArrive);
                hud.Fade(0f, 0.55f);
            }));
        }

        /// <summary>
        /// Wer mitfaehrt: der Held und die Bots. Die Kamera bleibt aussen vor - sie folgt dem Helden
        /// ohnehin jedes Bild, und wuerde man sie zusaetzlich anheben, fuehre sie doppelt.
        /// </summary>
        private Transform[] Riders()
        {
            var list = new System.Collections.Generic.List<Transform>();
            if (player) list.Add(player);
            foreach (var companion in companions)
                if (companion) list.Add(companion.transform);
            return list.ToArray();
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
