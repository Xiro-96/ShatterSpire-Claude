using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    public enum CompanionRole { Guardian, Support, Ranger }

    /// <summary>
    /// Offline-Stellvertreter fuer die zwei Mitspieler. Jede Rolle hat im Kampf ihren eigenen Platz,
    /// statt dass alle drei als Klumpen um den Spieler stehen:
    /// der Guardian geht vorn zwischen Spieler und Gegner, der Ranger haelt Abstand von der Seite,
    /// der Support bleibt hinter dem Spieler und heilt. Bewegung laeuft ueber die Raumnavigation
    /// der Etage, damit die Bots durch Gaenge folgen statt durch Waende.
    /// </summary>
    public sealed class CompanionBot : MonoBehaviour
    {
        private const float PartySpacing = 2.2f;
        private const float LeaderSpacing = 1.8f;
        private const float ThreatRange = 11f;

        private static readonly List<CompanionBot> ActiveCompanions = new();
        private Transform leader;
        private Transform muzzle;
        private StylizedCharacterMotion motion;
        private Health leaderHealth;
        private FloorNavigation navigation;
        private CompanionRole role;
        private Color accent;
        private float side = 1f;
        private float nextAttack;
        private float nextSupportPulse;
        private Vector3 lastLeaderPosition;
        private Vector3 leaderHeading = Vector3.forward;
        private Vector3 previousPosition;
        private float healingUntil;

        public string DisplayName { get; private set; }
        public CompanionRole Role => role;
        public Color Accent => accent;
        /// <summary>Was der Bot gerade tut, fuer die Team-Leiste im HUD.</summary>
        public string Status { get; private set; } = "FOLLOWING";
        public float SupportReadyNormalized =>
            role != CompanionRole.Support ? 1f : Mathf.Clamp01(1f - (nextSupportPulse - Time.time) / 8f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveCompanions.Clear();

        private void OnEnable()
        {
            if (!ActiveCompanions.Contains(this)) ActiveCompanions.Add(this);
        }

        private void OnDisable() => ActiveCompanions.Remove(this);

        public void Configure(Transform player, Vector3 offset, CompanionRole companionRole,
            Color color, string championName)
        {
            leader = player;
            leaderHealth = player.GetComponent<Health>();
            side = offset.x < 0f ? -1f : 1f;
            role = companionRole;
            accent = color;
            DisplayName = championName;
            lastLeaderPosition = player.position;
            previousPosition = transform.position;
            if (!AuthoredArt.TryBuildCompanion(transform, role, color, out muzzle))
                muzzle = StylizedArt.BuildRex(transform);
            motion = GetComponent<StylizedCharacterMotion>();
        }

        public void SetNavigation(FloorNavigation value) => navigation = value;

        public void Teleport(Vector3 position)
        {
            transform.position = position;
            previousPosition = position;
            if (leader) lastLeaderPosition = leader.position;
        }

        private void Update()
        {
            if (!leader || Time.timeScale <= 0f) return;
            TrackLeaderHeading();
            // Zurueckgeblieben, etwa nach einem Etagenwechsel: nicht quer ueber die Etage laufen.
            if (FlatDistance(transform.position, leader.position) > 30f)
            {
                var fallback = leader.position - leaderHeading * 2f + Vector3.Cross(Vector3.up, leaderHeading) * side * 2f;
                Teleport(navigation != null ? navigation.ClampToWalkable(fallback, 0.5f) : fallback);
            }

            var threat = FindEngagedEnemy(leader.position, ThreatRange, TeamId.Enemy);
            Status = Time.time < healingUntil ? "HEALING"
                : !threat ? "FOLLOWING"
                : role switch { CompanionRole.Guardian => "FRONTLINE", CompanionRole.Support => "COVERING", _ => "FLANKING" };
            switch (role)
            {
                case CompanionRole.Guardian: GuardianUpdate(threat); break;
                case CompanionRole.Support: SupportUpdate(threat); break;
                default: RangerUpdate(threat); break;
            }
        }

        private void LateUpdate() => previousPosition = transform.position;

        private void TrackLeaderHeading()
        {
            var moved = leader.position - lastLeaderPosition;
            moved.y = 0f;
            lastLeaderPosition = leader.position;
            if (moved.sqrMagnitude > 0.0004f)
                leaderHeading = Vector3.Slerp(leaderHeading, moved.normalized, 0.08f).normalized;
        }

        private void GuardianUpdate(Health threat)
        {
            // Vorn zwischen Spieler und Gegner. Laeuft nicht weiter als 12 Einheiten vom Spieler weg.
            if (!threat || FlatDistance(transform.position, leader.position) > 12f)
            {
                MoveTo(Slot(), 6.1f);
                FaceMovement();
                return;
            }

            var delta = threat.transform.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 2.3f * 2.3f)
                MoveTo(threat.transform.position - delta.normalized * 1.75f, 6.5f);
            Face(delta);
            if (Time.time < nextAttack || delta.sqrMagnitude > 2.75f * 2.75f) return;

            nextAttack = Time.time + 1.6f;
            motion?.PulseAttack(1.25f);
            var impact = transform.position + transform.forward * 1.35f;
            CombatUtility.Explode(impact, 1.8f, 8f, TeamId.Enemy, DamageType.Physical, gameObject);
            PrototypeVfx.SpawnExplosion(impact + Vector3.up * 0.25f, 1.65f, accent);
            CameraController.Impulse(0.045f);
        }

        private void RangerUpdate(Health threat)
        {
            // Seitlich zur Bedrohung, mit Abstand - schiesst an Guardian und Spieler vorbei.
            MoveTo(threat ? FlankSlot(threat.transform.position, 3.6f) : Slot(), 6.2f);
            var target = FindEngagedEnemy(transform.position, 12f, TeamId.Enemy);
            if (!target)
            {
                FaceMovement();
                return;
            }
            var delta = target.transform.position - transform.position;
            delta.y = 0f;
            Face(delta);
            if (Time.time < nextAttack) return;
            nextAttack = Time.time + 0.58f;
            var direction = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
            var start = muzzle ? muzzle.position : transform.position + Vector3.up + direction * 0.7f;
            Projectile.Spawn(start, direction, new Projectile.Payload
            {
                Owner = gameObject,
                TargetTeam = TeamId.Enemy,
                Damage = 5.5f,
                Type = DamageType.Physical,
                RemainingPierces = 1,
                VisualScale = 0.92f,
                VisualKind = ProjectileVisualKind.Arrow
            }, 21f);
            PrototypeVfx.SpawnMuzzle(start, direction);
            motion?.PulseAttack(0.7f);
        }

        private void SupportUpdate(Health threat)
        {
            // Hinter dem Spieler, von der Bedrohung abgewandt.
            MoveTo(threat ? CoverSlot(threat.transform.position, 3.4f) : Slot(), 5.7f);
            var target = FindEngagedEnemy(transform.position, 10f, TeamId.Enemy);
            if (target)
            {
                var delta = target.transform.position - transform.position;
                delta.y = 0f;
                Face(delta);
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + 1.25f;
                    var direction = delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward;
                    var start = muzzle ? muzzle.position : transform.position + Vector3.up * 1.1f + direction * 0.65f;
                    Projectile.Spawn(start, direction, new Projectile.Payload
                    {
                        Owner = gameObject,
                        TargetTeam = TeamId.Enemy,
                        Damage = 3.2f,
                        Type = DamageType.Holy,
                        RemainingRicochets = 1,
                        VisualScale = 0.9f
                    }, 20f);
                    PrototypeVfx.SpawnMuzzle(start, direction);
                    motion?.PulseAttack(0.68f);
                }
            }
            else
            {
                FaceMovement();
            }

            if (!leaderHealth || !leaderHealth.IsAlive || leaderHealth.Normalized >= 0.72f || Time.time < nextSupportPulse) return;
            nextSupportPulse = Time.time + 8f;
            healingUntil = Time.time + 1.2f;
            leaderHealth.Heal(10f);
            PrototypeVfx.SpawnExplosion(leader.position + Vector3.up * 0.6f, 1.8f, new Color(0.3f, 1f, 0.62f));
        }

        /// <summary>Ruhige Aufstellung, ausgerichtet an der Laufrichtung des Spielers.</summary>
        private Vector3 Slot()
        {
            var right = Vector3.Cross(Vector3.up, leaderHeading);
            return role switch
            {
                CompanionRole.Guardian => leader.position + leaderHeading * 2.6f + right * side * 1.6f,
                CompanionRole.Support => leader.position - leaderHeading * 3.2f + right * side * 0.8f,
                _ => leader.position - leaderHeading * 0.8f + right * side * 3.4f
            };
        }

        private Vector3 FlankSlot(Vector3 threat, float distance)
        {
            var toThreat = FlatDirection(threat - leader.position);
            var right = Vector3.Cross(Vector3.up, toThreat);
            return leader.position + right * side * distance - toThreat * 1.2f;
        }

        private Vector3 CoverSlot(Vector3 threat, float distance)
        {
            var toThreat = FlatDirection(threat - leader.position);
            return leader.position - toThreat * distance + Vector3.Cross(Vector3.up, toThreat) * side * 0.8f;
        }

        private void MoveTo(Vector3 destination, float speed)
        {
            destination.y = 0f;
            if (FlatDistance(transform.position, destination) > 0.25f)
            {
                var waypoint = navigation != null ? navigation.NextWaypoint(transform.position, destination) : destination;
                var distance = FlatDistance(transform.position, destination);
                var step = (distance > 8f ? 12f : speed) * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position,
                    new Vector3(waypoint.x, transform.position.y, waypoint.z), step);
            }
            ApplyPartySeparation();
            if (navigation != null) transform.position = navigation.ClampToWalkable(transform.position, 0.45f);
        }

        private void ApplyPartySeparation()
        {
            var correction = Vector3.zero;
            if (leader)
            {
                var fromLeader = transform.position - leader.position;
                fromLeader.y = 0f;
                if (fromLeader.sqrMagnitude < LeaderSpacing * LeaderSpacing)
                    correction += (fromLeader.sqrMagnitude > 0.01f ? fromLeader.normalized : Vector3.right) * 0.16f;
            }

            for (var i = 0; i < ActiveCompanions.Count; i++)
            {
                var other = ActiveCompanions[i];
                if (!other || other == this) continue;
                var away = transform.position - other.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f || away.sqrMagnitude >= PartySpacing * PartySpacing) continue;
                correction += away.normalized * 0.13f;
            }
            transform.position += correction;
        }

        private void FaceMovement()
        {
            var moved = transform.position - previousPosition;
            moved.y = 0f;
            if (moved.sqrMagnitude > 0.0004f) Face(moved);
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 15f * Time.deltaTime);
        }

        /// <summary>
        /// Naechster Gegner, der schon kaempft. Ruhende Lager bleiben unberuehrt, bis der Spieler sie
        /// weckt - sonst schoessen die Bots durch Waende und zoegen Lager aus dem Nachbarraum.
        /// </summary>
        private static Health FindEngagedEnemy(Vector3 point, float radius, TeamId team)
        {
            Health best = null;
            var bestDistance = radius * radius;
            foreach (var candidate in Health.Active)
            {
                if (!candidate || !candidate.IsAlive || candidate.Team != team) continue;
                var offset = candidate.transform.position - point;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance > bestDistance) continue;
                var agent = candidate.GetComponent<EnemyAgent>();
                if (agent && agent.IsIdle) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static Vector3 FlatDirection(Vector3 value)
        {
            value.y = 0f;
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.forward;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }

    public sealed class WorldFacingLabel : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main) transform.rotation = Camera.main.transform.rotation;
        }
    }
}
