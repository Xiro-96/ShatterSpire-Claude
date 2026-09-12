using System;
using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    [RequireComponent(typeof(Health), typeof(StatusReceiver))]
    public sealed class EnemyAgent : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<EnemyAgent> ActiveAgents = new();
        /// <summary>Alle lebenden Gegner. Gebraucht von Flaechenwirkungen, die Gegner selbst ansprechen.</summary>
        public static System.Collections.Generic.IReadOnlyList<EnemyAgent> Active => ActiveAgents;
        private enum State { Idle, Chase, Telegraph, Attack, Dead }
        private EnemyKind kind;
        private EnemyStats stats;
        private const float ArrivalGraceSeconds = 0.75f;
        private float spawnedAt;
        private const float LeashRadius = 18f;
        private FloorNavigation navigation;
        private Vector3 home;
        private float aggroRadius = 8f;
        private float busyUntil;
        private bool dormant;
        private bool dormantOnFloor;
        private Health health;
        private StatusReceiver status;
        private StylizedCharacterMotion motion;
        private Transform target;
        private State state;
        private float speed;
        private float attackRange;
        private float attackDamage;
        private float attackReadyAt;
        private bool eliteExplosive;
        private bool eliteVampiric;
        private int bossAttackIndex;
        private int announcedBossPhase;
        private float hitStaggerUntil;
        private float strafeDirection;
        /// <summary>
        /// Lauf-Seed, Etage und ein Salz, das genau diesen Gegner meint. Daraus kommt alles, was der
        /// Gegner streut - im Co-op muss derselbe Gegner bei allen drei Spielern derselbe sein.
        /// </summary>
        private int runSeed;
        private int floorIndex;
        private int salt;
        // Vier Fragen an denselben Gegner. Jede bekommt ihren eigenen Abstand im Salzraum: sonst
        // zoege der Nachbar im Lager fuer seine erste Frage dieselbe Zahl wie dieser Gegner fuer
        // seine zweite, und die Antworten waeren ueber das ganze Lager hinweg gekoppelt.
        private const int SaltElite = 1000000;
        private const int SaltStrafe = 2000000;
        private const int SaltReady = 3000000;
        private const int SaltPose = 4000000;
        private Vector3 knockbackVelocity;
        // Schildtraeger: Deckung nach vorn. Wer von der Seite oder von hinten trifft, trifft voll;
        // wer von vorn hart genug zuschlaegt, bricht die Deckung fuer ein paar Sekunden auf.
        private const float GuardArcDegrees = 110f;
        private const float GuardedDamageFraction = 0.18f;
        private const float FlankedDamageBonus = 1.3f;
        private const float GuardBreakSeconds = 2.6f;
        private float guardBreakDamage;
        private float guardBrokenUntil;
        private float regainGuardAt;
        private bool guarding;
        public event Action<EnemyAgent> Defeated;
        /// <summary>Wird aufmerksam und greift an. Der Spawner alarmiert darueber das restliche Lager.</summary>
        public event Action<EnemyAgent> Engaged;
        public EnemyKind Kind => kind;
        /// <summary>Salz dieses Gegners. Der Spawner braucht es fuer die Beute beim Tod.</summary>
        public int Salt => salt;
        /// <summary>Kurz nach dem Erscheinen: fuer die Zielhilfe noch kein gueltiges Ziel.</summary>
        public bool IsArriving => Time.time - spawnedAt < ArrivalGraceSeconds;
        /// <summary>Wartet im Lager und hat noch niemanden bemerkt.</summary>
        public bool IsIdle => state == State.Idle;
        /// <summary>
        /// Holt gerade zum Angriff aus. Das HUD zeigt solche Gegner am Bildrand an, wenn sie
        /// ausserhalb des Bildes stehen - auf einem Telefon ist der Blickwinkel eng, und ein
        /// Armbrustbolzen aus dem Nichts liest sich als unfair, nicht als schwer.
        /// </summary>
        public bool IsTelegraphing => state == State.Telegraph;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveAgents.Clear();

        private void OnEnable()
        {
            if (!ActiveAgents.Contains(this)) ActiveAgents.Add(this);
        }

        private void OnDisable() => ActiveAgents.Remove(this);

        /// <summary>
        /// Traegt dieser Elite die explosive oder die vampirische Eigenschaft? Reine Rechnung aus
        /// (Seed, Etage, Salz) und nicht aus <see cref="UnityEngine.Random"/>: wer im Co-op einem
        /// explosiven Elite ausweicht, muss das auch bei den anderen Spielern tun.
        /// </summary>
        public static bool EliteIsExplosive(int runSeed, int floor, int salt)
            => RunRandom.Chance(runSeed, floor, salt + SaltElite, 0.5f);

        /// <summary>
        /// Setzt den Gegner auf. Die Anomalie der Etage greift nach der Tiefenskalierung an,
        /// nicht davor: sonst wuerde sie sich mit der Etagenkurve multiplizieren und auf Etage 14
        /// ein Vielfaches dessen bedeuten, was auf Etage 2 angekuendigt war.
        ///
        /// seed und enemySalt sagen, welcher Gegner das ist: sie ersetzen den Wuerfel bei allem,
        /// was zwischen den Spielern uebereinstimmen muss.
        /// </summary>
        public void Configure(EnemyKind value, Transform player, int floor, in FloorModifier modifier,
            int seed, int enemySalt)
        {
            kind = value;
            target = player;
            runSeed = seed;
            floorIndex = floor;
            salt = enemySalt;
            health = GetComponent<Health>();
            status = GetComponent<StatusReceiver>();
            motion = GetComponent<StylizedCharacterMotion>();
            stats = EnemyBalance.For(kind);
            speed = stats.Speed;
            attackRange = stats.AttackRange;
            attackDamage = stats.AttackDamage;
            health.Configure(TeamId.Enemy, stats.Health);
            if (kind == EnemyKind.Elite)
            {
                eliteExplosive = EliteIsExplosive(runSeed, floorIndex, salt);
                eliteVampiric = !eliteExplosive;
            }

            var depth = Mathf.Max(0, floor - 1);
            var healthScale = 1f + depth * EnemyBalance.HealthPerFloor;
            var damageScale = 1f + depth * EnemyBalance.DamagePerFloor;
            health.IncreaseMaximum(health.Maximum * (healthScale - 1f), true);
            attackDamage *= damageScale;
            speed *= 1f + Mathf.Min(EnemyBalance.MaximumSpeedBonus, depth * EnemyBalance.SpeedPerFloor);
            if (!modifier.IsCalm)
            {
                // Der Boss bleibt von der Anomalie unberuehrt: seine Phasen sind auf feste Werte
                // gebaut, und halbes Leben wuerde Phase 3 ueberspringen.
                if (kind != EnemyKind.IronWarden)
                {
                    // Dieselbe Form wie die Etagenkurve darueber: der Gegner ist hier noch voll
                    // geheilt, deshalb zieht ein negativer Betrag Maximum und Stand gemeinsam nach.
                    health.IncreaseMaximum(health.Maximum * (modifier.EnemyHealth - 1f), true);
                    attackDamage *= modifier.EnemyDamage;
                    speed *= modifier.EnemySpeed;
                }
            }
            health.Died += Die;
            health.Damaged += OnDamaged;
            if (kind == EnemyKind.Shieldbearer)
            {
                // Ein aufgeladener Heavy liegt weit ueber diesem Wert, ein Light-Treffer weit darunter.
                // So bricht die Deckung genau dann, wenn jemand richtig ausgeholt hat.
                // Nach der Anomalie gerechnet: bei halbem Leben muss auch die Schwelle halb sein,
                // sonst bricht die Deckung des Schildtraegers im Glasbruch nie.
                guardBreakDamage = health.Maximum * 0.42f;
                health.DamageFilter = FilterGuardedDamage;
            }
            strafeDirection = RunRandom.Chance(runSeed, floorIndex, salt + SaltStrafe, 0.5f) ? 1f : -1f;
            spawnedAt = Time.time;
            // Der erste Schlag kommt versetzt, damit ein Lager nicht wie ein Mann zuschlaegt:
            // 0,35 bis 0,85 Sekunden in Hundertstelschritten, gezogen statt gewuerfelt.
            attackReadyAt = Time.time + 0.35f + RunRandom.Index(runSeed, floorIndex, salt + SaltReady, 51) * 0.01f;
            state = State.Chase;
        }

        /// <summary>
        /// Ordnet den Gegner einer Etage zu. Lagergegner starten ruhend und greifen erst an, wenn jemand
        /// in ihren Aggro-Radius kommt oder sie getroffen werden.
        /// </summary>
        public void SetBehaviour(FloorNavigation floorNavigation, Vector3 homePosition, bool startIdle)
        {
            navigation = floorNavigation;
            home = homePosition;
            aggroRadius = kind switch
            {
                EnemyKind.Shooter => 10f,
                EnemyKind.Marksman => 13f,
                EnemyKind.IronWarden => 12f,
                _ => 8f
            };
            if (startIdle)
            {
                state = State.Idle;
                // Lager schlafen: leichte Skelette liegen meist am Boden, schwere stehen reglos. Wer naeher
                // kommt, weckt sie - erst stehen sie auf, dann greifen sie an.
                // Zwei von drei leichten Skeletten liegen. Auch das gezogen: ein Gegner, der beim
                // einen Spieler liegt und beim anderen steht, wacht auch verschieden auf.
                dormantOnFloor = kind is EnemyKind.Crawler or EnemyKind.Shooter
                    && RunRandom.Chance(runSeed, floorIndex, salt + SaltPose, 2f / 3f);
                dormant = motion && motion.HoldPose(dormantOnFloor ? PresenceMotion.InactiveFloor : PresenceMotion.InactiveStanding);
                return;
            }
            // Verteidiger steigen aus dem Boden und koennen dabei noch nicht angreifen.
            Busy(motion ? motion.PlayPresence(PresenceMotion.SpawnGround, 1.7f, 0.9f) : 0f);
        }

        private void Busy(float seconds)
        {
            if (seconds <= 0f) return;
            busyUntil = Mathf.Max(busyUntil, Time.time + seconds);
            attackReadyAt = Mathf.Max(attackReadyAt, busyUntil + 0.2f);
        }

        public void Engage(bool alertCamp = true)
        {
            if (state != State.Idle) return;
            state = State.Chase;
            attackReadyAt = Mathf.Max(attackReadyAt, Time.time + 0.4f);
            if (motion && kind == EnemyKind.IronWarden)
            {
                // Der Warden provoziert lang, bevor der Kampf beginnt.
                Busy(motion.PlayPresence(PresenceMotion.TauntLong, 1.1f, 1.6f));
                CameraController.Impulse(0.12f);
            }
            else if (motion && dormant)
            {
                // Wer den Spieler bemerkt und steht, provoziert; der Rest des Lagers steht auf.
                var wake = alertCamp && !dormantOnFloor ? PresenceMotion.Taunt
                    : dormantOnFloor ? PresenceMotion.AwakenFloor : PresenceMotion.AwakenStanding;
                Busy(motion.PlayPresence(wake, 1.6f, 1.1f));
            }
            dormant = false;
            RaiseGuard();
            if (alertCamp) Engaged?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (!health) return;
            health.DamageFilter = null;
            health.Died -= Die;
            health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            if (state == State.Dead || !target) return;
            ApplyKnockback();
            if (kind == EnemyKind.Shieldbearer) UpdateGuardPose();
            if (Time.time < hitStaggerUntil || Time.time < busyUntil) return;
            var targetHealth = target.GetComponent<Health>();
            if (!targetHealth || !targetHealth.IsAlive) return;
            var offset = target.position - transform.position;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (state == State.Idle)
            {
                IdleUpdate(distance);
                return;
            }
            if (state == State.Chase && ShouldReturnHome())
            {
                state = State.Idle;
                return;
            }
            if (state == State.Chase)
            {
                var direction = SteerTowards(target.position, offset);
                var movement = Vector3.zero;
                if (kind is EnemyKind.Shooter or EnemyKind.Marksman)
                {
                    // Ohne freie Sicht bringt Abstandhalten nichts - dann wird die Deckung umlaufen.
                    if (!CanSeeTarget()) movement = direction;
                    else if (distance > attackRange * 0.86f) movement = direction;
                    else if (distance < attackRange * 0.52f) movement = -direction;
                    else movement = Vector3.Cross(Vector3.up, direction) * strafeDirection * 0.56f;
                }
                else if (distance > attackRange * 0.86f)
                {
                    movement = direction;
                }

                if (movement.sqrMagnitude > 0.01f)
                {
                    movement = (movement + SeparationForce() * 1.35f).normalized;
                    transform.position += movement * (speed * status.SpeedMultiplier * Time.deltaTime);
                    ClampToArena();
                }
                if (offset.sqrMagnitude > 0.05f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset), 10f * Time.deltaTime);
                if (distance <= attackRange && Time.time >= attackReadyAt && HasFiringLine())
                    StartCoroutine(AttackRoutine());
            }
        }

        /// <summary>Freie Sicht auf das Ziel. Ohne Etagendaten gilt die Sicht als frei.</summary>
        private bool CanSeeTarget()
            => navigation == null || !target || navigation.HasLineOfSight(transform.position, target.position);

        /// <summary>Fernkaempfer schiessen nicht in eine Deckung. Nahkampf braucht die Pruefung nicht.</summary>
        private bool HasFiringLine()
            => kind is not (EnemyKind.Shooter or EnemyKind.Marksman) || CanSeeTarget();

        private void IdleUpdate(float distanceToTarget)
        {
            // Ruhend: zum Lager zurueck und dort warten. Angriff erst, wenn der Spieler nahe kommt und
            // im selben Raum steht - nicht durch Waende hindurch.
            var toHome = home - transform.position;
            toHome.y = 0f;
            if (!dormant && toHome.sqrMagnitude > 1.6f * 1.6f)
            {
                var step = SteerTowards(home, toHome);
                transform.position += step * (speed * 0.7f * status.SpeedMultiplier * Time.deltaTime);
                ClampToArena();
                if (step.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(step), 6f * Time.deltaTime);
            }
            if (distanceToTarget > aggroRadius) return;
            if (navigation != null && navigation.RoomAt(target.position) != navigation.RoomAt(transform.position) &&
                distanceToTarget > aggroRadius * 0.45f) return;
            Engage();
        }

        private bool ShouldReturnHome()
        {
            if (navigation == null || kind == EnemyKind.IronWarden) return false;
            // Nur umkehren, wenn der Spieler das Lager weit hinter sich gelassen hat.
            return FlatDistance(target.position, home) > LeashRadius &&
                   FlatDistance(transform.position, home) > LeashRadius * 0.6f;
        }

        private Vector3 SteerTowards(Vector3 destination, Vector3 fallback)
        {
            if (navigation != null)
            {
                var waypoint = navigation.NextWaypoint(transform.position, destination);
                var delta = waypoint - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0004f) return delta.normalized;
            }
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.01f ? fallback.normalized : transform.forward;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Haelt den Gegner vollstaendig an. Nutzt denselben Riegel wie die Trefferreaktion - Bewegung,
        /// Vorwarnung und Angriff laufen darueber, also genuegt ein Zeitstempel fuer eine echte
        /// Betaeubung. Ein laufender Angriff wird zusaetzlich abgebrochen.
        /// </summary>
        public void Stun(float seconds)
        {
            if (state == State.Dead || seconds <= 0f) return;
            hitStaggerUntil = Mathf.Max(hitStaggerUntil, Time.time + seconds);
            if (state is State.Telegraph or State.Attack)
            {
                StopAllCoroutines();
                state = State.Chase;
                attackReadyAt = Mathf.Max(attackReadyAt, Time.time + seconds);
            }
            motion?.PulseHit();
        }

        private void OnDamaged(DamageInfo damage)
        {
            if (state == State.Dead) return;
            if (state == State.Idle) Engage();
            var force = damage.Force;
            force.y = 0f;
            // In Deckung steht der Schildtraeger fest; erst ein gebrochener Schild laesst ihn wanken.
            if (guarding) force *= 0.2f;
            knockbackVelocity += force * stats.KnockbackResistance;
            hitStaggerUntil = Mathf.Max(hitStaggerUntil, Time.time + stats.StaggerSeconds);
        }

        private void ApplyKnockback()
        {
            if (knockbackVelocity.sqrMagnitude < 0.0025f) return;
            transform.position += knockbackVelocity * Time.deltaTime;
            knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 13f * Time.deltaTime);
            ClampToArena();
        }

        private Vector3 SeparationForce()
        {
            var force = Vector3.zero;
            var desiredSpacing = stats.SeparationSpacing;
            for (var i = 0; i < ActiveAgents.Count; i++)
            {
                var other = ActiveAgents[i];
                if (!other || other == this || other.state == State.Dead) continue;
                var away = transform.position - other.transform.position;
                away.y = 0f;
                var distanceSqr = away.sqrMagnitude;
                if (distanceSqr < 0.001f || distanceSqr >= desiredSpacing * desiredSpacing) continue;
                force += away.normalized * (1f - Mathf.Sqrt(distanceSqr) / desiredSpacing);
            }
            return force;
        }

        private void ClampToArena()
        {
            if (navigation != null)
            {
                transform.position = navigation.ClampToWalkable(transform.position, 0.45f);
                return;
            }
            const float radius = 14.55f;
            var flat = new Vector2(transform.position.x, transform.position.z);
            if (flat.sqrMagnitude <= radius * radius) return;
            flat = flat.normalized * radius;
            transform.position = new Vector3(flat.x, transform.position.y, flat.y);
        }

        private IEnumerator AttackRoutine()
        {
            if (kind == EnemyKind.IronWarden)
            {
                yield return BossAttackRoutine();
                yield break;
            }

            switch (kind)
            {
                case EnemyKind.Crawler:
                    yield return CrawlerLunge();
                    break;
                case EnemyKind.Shooter:
                    yield return ShooterBurst();
                    break;
                case EnemyKind.Brute:
                    yield return BruteSlam();
                    break;
                case EnemyKind.Elite:
                    yield return EliteAttack();
                    break;
                case EnemyKind.Shieldbearer:
                    yield return ShieldBash();
                    break;
                case EnemyKind.Marksman:
                    yield return MarksmanShot();
                    break;
            }
        }

        private IEnumerator CrawlerLunge()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 2.8f, 0.72f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PlayMotion(AttackMotion.Swing, 0.9f);
            var elapsed = 0f;
            while (elapsed < 0.13f && state != State.Dead)
            {
                transform.position += direction * (12.5f * Time.deltaTime);
                ClampToArena();
                elapsed += Time.deltaTime;
                yield return null;
            }
            CombatUtility.Explode(transform.position + direction * 0.45f, 1.15f,
                attackDamage, TeamId.Player, DamageType.Physical, gameObject);
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator ShooterBurst()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 9.5f, 0.5f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PlayMotion(AttackMotion.Cast, 0.8f);
            for (var i = 0; i < 3 && state != State.Dead; i++)
            {
                var spread = (i - 1) * 6.5f;
                Shoot(Quaternion.Euler(0f, spread, 0f) * direction);
                yield return new WaitForSeconds(0.075f);
            }
            transform.position += Vector3.Cross(Vector3.up, direction) * strafeDirection * 0.75f;
            ClampToArena();
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator BruteSlam()
        {
            state = State.Telegraph;
            var telegraph = PrototypeVfx.SpawnTelegraph(transform.position, 2.45f, false);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PlayMotion(AttackMotion.Smash, 1.25f);
            CombatUtility.Explode(transform.position, 2.45f, attackDamage,
                TeamId.Player, DamageType.Physical, gameObject);
            PrototypeVfx.SpawnShockwave(transform.position, 2.7f, new Color(1f, 0.42f, 0.08f));
            CameraController.Impulse(0.11f);
            FinishAttack(stats.AttackCooldown);
        }

        private IEnumerator EliteAttack()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = eliteExplosive
                ? PrototypeVfx.SpawnTelegraph(transform.position, 3.25f, false)
                : PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 5.6f, 1.15f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            motion?.PlayMotion(eliteExplosive ? AttackMotion.Summon : AttackMotion.Leap, 1.45f);
            if (eliteExplosive)
            {
                CombatUtility.Explode(transform.position, 3.25f, attackDamage,
                    TeamId.Player, DamageType.Fire, gameObject);
                PrototypeVfx.SpawnShockwave(transform.position, 3.8f, new Color(1f, 0.12f, 0.52f));
                for (var i = 0; i < 8; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                    Shoot(shotDirection, DamageType.Void, attackDamage * 0.42f);
                }
            }
            else
            {
                transform.position += direction * 3.4f;
                ClampToArena();
                CombatUtility.Explode(transform.position, 2.15f, attackDamage,
                    TeamId.Player, DamageType.Void, gameObject);
                health.Heal(16f);
            }
            CameraController.Impulse(0.14f);
            FinishAttack(stats.AttackCooldown);
        }

        /// <summary>
        /// Schildstoss: kurze Vorwarnung, dann ein Schub nach vorn. Waehrend des Stosses ist die Deckung
        /// offen - genau das ist das Zeitfenster, in dem sich ein Gegenangriff von vorn lohnt.
        /// </summary>
        private IEnumerator ShieldBash()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, 3.2f, 1.1f);
            yield return new WaitForSeconds(stats.TelegraphSeconds);
            if (!BeginAttack(telegraph)) yield break;

            guarding = false;
            motion?.PlayMotion(AttackMotion.Stab, 1.1f);
            var elapsed = 0f;
            while (elapsed < 0.16f && state != State.Dead)
            {
                transform.position += direction * (7.5f * Time.deltaTime);
                ClampToArena();
                elapsed += Time.deltaTime;
                yield return null;
            }
            CombatUtility.Explode(transform.position + direction * 0.9f, 1.45f,
                attackDamage, TeamId.Player, DamageType.Physical, gameObject);
            PrototypeVfx.SpawnShockwave(transform.position + direction * 0.9f, 1.7f, GuardColor);
            FinishAttack(stats.AttackCooldown);
        }

        /// <summary>
        /// Armbrustschuss: die Linie folgt dem Ziel, waehrend gespannt wird, und rastet auf dem letzten
        /// Drittel ein. Wer bis dahin nicht aus der Linie ist, wird getroffen - Ausweichen zaehlt, nicht Deckung.
        /// </summary>
        private IEnumerator MarksmanShot()
        {
            state = State.Telegraph;
            var direction = FlatDirectionToTarget();
            var telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction, attackRange + 2f, 0.55f);
            motion?.PlayMotion(AttackMotion.Draw, 0.8f);

            var trackingSeconds = stats.TelegraphSeconds * 0.62f;
            var elapsed = 0f;
            while (elapsed < stats.TelegraphSeconds && state != State.Dead)
            {
                // Wer waehrend des Spannens hinter eine Deckung tritt, bekommt den Schuss nicht ab.
                if (!CanSeeTarget())
                {
                    if (telegraph) Destroy(telegraph);
                    FinishAttack(0.7f);
                    yield break;
                }
                if (elapsed < trackingSeconds)
                {
                    direction = FlatDirectionToTarget();
                    transform.rotation = Quaternion.LookRotation(direction);
                    AimTelegraph(telegraph, direction, attackRange + 2f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!BeginAttack(telegraph)) yield break;

            motion?.PlayMotion(AttackMotion.Release, 1.2f);
            PrototypeVfx.SpawnMuzzle(transform.position + Vector3.up * 0.9f + direction * 0.6f, direction);
            Projectile.Spawn(transform.position + Vector3.up * 0.9f + direction, direction, new Projectile.Payload
            {
                Owner = gameObject,
                TargetTeam = TeamId.Player,
                Damage = attackDamage,
                Type = DamageType.Physical,
                VisualScale = 0.8f,
                RemainingPierces = 1
            }, 26f);
            FinishAttack(stats.AttackCooldown);
        }

        /// <summary>Legt die Telegraph-Linie neu aus, ohne sie zu ersetzen - sonst flackert sie beim Nachfuehren.</summary>
        private void AimTelegraph(GameObject telegraph, Vector3 direction, float length)
        {
            if (!telegraph) return;
            var origin = transform.position;
            telegraph.transform.position = origin + direction * (length * 0.5f) + Vector3.up * 0.038f;
            telegraph.transform.rotation = Quaternion.Euler(90f, Quaternion.LookRotation(direction).eulerAngles.y, 0f);
        }

        private static readonly Color GuardColor = new(0.42f, 0.68f, 1f);

        /// <summary>Deckung wieder aufnehmen: Schild hoch, aber nur im Oberkoerper, damit die Beine weiterlaufen.</summary>
        private void RaiseGuard()
        {
            if (kind != EnemyKind.Shieldbearer || state == State.Dead) return;
            if (Time.time < guardBrokenUntil) return;
            guarding = true;
            regainGuardAt = 0f;
            motion?.HoldPose(PresenceMotion.Guard, true);
        }

        /// <summary>
        /// Haelt die Deckung ueber die Zeit: nur waehrend der Verfolgung - ein schlafender Lagergegner
        /// soll liegen bleiben - und erneuert die Haltung, nachdem eine Trefferreaktion sie ueberschrieben hat.
        /// </summary>
        private void UpdateGuardPose()
        {
            if (state != State.Chase || Time.time < guardBrokenUntil) return;
            if (!guarding)
            {
                RaiseGuard();
                return;
            }
            if (regainGuardAt <= 0f || Time.time < regainGuardAt) return;
            regainGuardAt = 0f;
            motion?.HoldPose(PresenceMotion.Guard, true);
        }

        /// <summary>
        /// Rechnet einen Treffer gegen die Deckung. Von vorn bleibt fast nichts uebrig, von der Seite
        /// oder von hinten trifft es voll und etwas darueber. Ein harter Treffer von vorn bricht die
        /// Deckung auf. Der Rueckgabewert ist der Schaden, der wirklich zaehlt.
        /// </summary>
        private float FilterGuardedDamage(DamageInfo damage)
        {
            if (state == State.Dead) return damage.Amount;
            var from = damage.Source ? damage.Source.transform.position : damage.HitPoint;
            var toAttacker = from - transform.position;
            toAttacker.y = 0f;
            var frontal = toAttacker.sqrMagnitude > 0.01f &&
                          Vector3.Angle(transform.forward, toAttacker) <= GuardArcDegrees * 0.5f;
            if (!guarding || Time.time < guardBrokenUntil || !frontal)
                return frontal ? damage.Amount : damage.Amount * FlankedDamageBonus;

            if (damage.Amount >= guardBreakDamage)
            {
                // Deckung gebrochen: offenes Fenster, in dem alles voll durchgeht.
                guarding = false;
                regainGuardAt = 0f;
                guardBrokenUntil = Time.time + GuardBreakSeconds;
                hitStaggerUntil = Mathf.Max(hitStaggerUntil, Time.time + 0.45f);
                motion?.PlayPresence(PresenceMotion.GuardBreak, 1f, 0.7f);
                Sfx.Play(Sound.GuardBreak, transform.position + Vector3.up * 0.9f);
                PrototypeVfx.SpawnShockwave(transform.position + Vector3.up * 0.9f, 2.2f, GuardColor);
                CameraController.Impulse(0.1f);
                return damage.Amount;
            }

            // Der metallische Abpraller ist die wichtigste Rueckmeldung des Schildtraegers: er sagt
            // "hier nicht", noch bevor die kleine Schadenszahl gelesen ist.
            Sfx.Play(Sound.Block, damage.HitPoint);
            PrototypeVfx.SpawnHit(damage.HitPoint, -toAttacker.normalized, DamageType.Lightning, false);
            // Health spielt gleich danach die Trefferreaktion und ueberschreibt damit die Haltung.
            // Kurz darauf geht der Schild wieder hoch - das liest sich als Zucken, nicht als Aussetzer.
            regainGuardAt = Time.time + 0.24f;
            return damage.Amount * GuardedDamageFraction;
        }

        private bool BeginAttack(GameObject telegraph)
        {
            if (telegraph) Destroy(telegraph);
            if (state == State.Dead) return false;
            state = State.Attack;
            return true;
        }

        private void FinishAttack(float cooldown)
        {
            if (state == State.Dead) return;
            attackReadyAt = Time.time + cooldown;
            state = State.Chase;
            RaiseGuard();
        }

        private Vector3 FlatDirectionToTarget()
        {
            var direction = target ? target.position - transform.position : transform.forward;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
        }

        private IEnumerator BossAttackRoutine()
        {
            state = State.Telegraph;
            var phase = health.Normalized > 0.66f ? 1 : health.Normalized > 0.33f ? 2 : 3;
            if (phase != announcedBossPhase)
            {
                announcedBossPhase = phase;
                GameEvents.RaiseObjectiveChanged(0, 1, Loc.T("IRON WARDEN  ·  PHASE") + " " + phase);
                PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.7f,
                    2.2f + phase * 0.35f, phase == 3 ? new Color(1f, 0.08f, 0.03f) : new Color(1f, 0.48f, 0.08f));
                CameraController.Impulse(phase == 3 ? 0.24f : 0.12f);
                if (phase > 1 && motion)
                {
                    // Phasenwechsel: der Warden haelt inne und provoziert, bevor er haerter angreift.
                    var roar = motion.PlayPresence(PresenceMotion.TauntLong, 1.3f, 1.2f);
                    if (roar > 0f) yield return new WaitForSeconds(roar);
                    if (state == State.Dead) yield break;
                }
            }

            var patternCount = phase == 1 ? 2 : 3;
            var pattern = bossAttackIndex++ % patternCount;
            var targetPoint = target.position;
            targetPoint.y = 0f;
            var direction = targetPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();

            GameObject telegraph;
            if (pattern == 0)
                telegraph = PrototypeVfx.SpawnTelegraph(targetPoint, phase == 3 ? 3.2f : 2.65f, false);
            else if (pattern == 1)
                telegraph = PrototypeVfx.SpawnTelegraphLine(transform.position, direction,
                    phase == 3 ? 13f : 10f, phase == 3 ? 1.6f : 1.15f);
            else
                telegraph = PrototypeVfx.SpawnTelegraph(transform.position, phase == 3 ? 5.8f : 4.8f, false);

            // Phase 1 kommt aus dem Statblock, die Verkürzung in Phase 2 und 3 ist
            // Verhalten und bleibt bewusst hier.
            var warning = phase == 3 ? 0.58f : phase == 2 ? 0.72f : stats.TelegraphSeconds;
            yield return new WaitForSeconds(warning);
            if (state == State.Dead)
            {
                if (telegraph) Destroy(telegraph);
                yield break;
            }
            if (telegraph) Destroy(telegraph);

            state = State.Attack;
            motion?.PlayMotion(pattern == 0 ? AttackMotion.Smash : pattern == 1 ? AttackMotion.Leap : AttackMotion.Summon, 1.35f + phase * 0.08f);
            if (pattern == 0)
            {
                CombatUtility.Explode(targetPoint, phase == 3 ? 3.2f : 2.65f,
                    attackDamage + phase * 2f, TeamId.Player, DamageType.Physical, gameObject);
            }
            else if (pattern == 1)
            {
                var dashDistance = phase == 3 ? 7.2f : 5.4f;
                transform.position += direction * dashDistance;
                ClampToArena();
                CombatUtility.Explode(transform.position, phase == 3 ? 2.7f : 2.15f,
                    attackDamage, TeamId.Player, DamageType.Fire, gameObject);
            }
            else
            {
                var shots = phase == 3 ? 16 : 12;
                for (var i = 0; i < shots; i++)
                {
                    var shotDirection = Quaternion.Euler(0f, i * (360f / shots) + bossAttackIndex * 11f, 0f) * Vector3.forward;
                    Projectile.Spawn(transform.position + Vector3.up + shotDirection, shotDirection,
                        new Projectile.Payload
                        {
                            Owner = gameObject,
                            TargetTeam = TeamId.Player,
                            Damage = phase == 3 ? 14f : 11f,
                            Type = DamageType.Lightning,
                            VisualScale = phase == 3 ? 1.25f : 1f
                        }, phase == 3 ? 10f : 8f);
                }
            }

            attackReadyAt = Time.time + BossCooldown();
            state = State.Chase;
        }

        private void MeleeAttack()
        {
            motion?.PlayMotion(kind == EnemyKind.IronWarden ? AttackMotion.Smash : AttackMotion.Swing, kind == EnemyKind.IronWarden ? 1.4f : 0.85f);
            PrototypeVfx.SpawnExplosion(transform.position + transform.forward, attackRange, new Color(1f, 0.18f, 0.08f));
            if (Vector3.Distance(transform.position, target.position) <= attackRange + 0.7f)
            {
                var targetHealth = target.GetComponent<Health>();
                targetHealth.TakeDamage(new DamageInfo(attackDamage, DamageType.Physical, gameObject, target.position, transform.forward * 5f));
                if (eliteVampiric) health.Heal(attackDamage * 0.5f);
            }
        }

        private void Shoot(Vector3 direction, DamageType type = DamageType.Fire, float damage = -1f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();
            Projectile.Spawn(transform.position + Vector3.up * 0.8f + direction, direction, new Projectile.Payload
            {
                Owner = gameObject, TargetTeam = TeamId.Player,
                Damage = damage < 0f ? attackDamage : damage, Type = type,
                VisualScale = kind == EnemyKind.Elite ? 1.2f : 0.9f
            }, 10f);
        }

        private float BossCooldown() => health.Normalized > 0.33f ? stats.AttackCooldown : 1.15f;

        private void Die()
        {
            if (state == State.Dead) return;
            state = State.Dead;
            guarding = false;
            if (health) health.DamageFilter = null;
            StopAllCoroutines();
            if (eliteExplosive) CombatUtility.Explode(transform.position, 3.5f, 16f, TeamId.Player, DamageType.Fire, gameObject);
            var bodyRenderer = GetComponentInChildren<Renderer>();
            PrototypeVfx.SpawnDeath(transform.position, bodyRenderer ? bodyRenderer.material.color : Color.magenta);
            Defeated?.Invoke(this);
            // Todesanimation statt sofort verschwinden. Collider und Lebensbalken aus, damit der Koerper
            // weder im Weg steht noch als Ziel zaehlt.
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            foreach (var bar in GetComponentsInChildren<Canvas>()) bar.enabled = false;
            var fall = motion ? motion.PlayPresence(PresenceMotion.Death, 1.4f, 0.9f) : 0f;
            Destroy(gameObject, fall);
        }
    }
}
