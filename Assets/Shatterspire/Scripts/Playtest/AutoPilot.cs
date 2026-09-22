using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Spielt den eigenen Helden im Selbsttest. Steht an der Stelle, an der sonst der
    /// <see cref="PlayerInputRouter"/> steht, und drueckt dieselben Knoepfe.
    ///
    /// Er folgt dem Zielpfeil - demselben Signal, dem ein Mensch folgt: Kern, Ring, Aufzug. Unterwegs
    /// kaempft er gegen alles, was wach ist und nah genug, mit derselben Kampflogik wie die Begleiter
    /// (<see cref="CombatBrain"/>), nur in der Spielweise eines Spielers. Die Auswahlfenster bedient
    /// er ueber <see cref="AutoPilotChoices"/>.
    ///
    /// Er will nicht gut spielen, sondern gewoehnlich. Er soll dort haengen bleiben, wo ein Mensch
    /// haengen bliebe - genau das soll das Protokoll sehen.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AutoPilot : MonoBehaviour, IPlayerInputSource
    {
        /// <summary>Wie oft ein Ziel neu gewaehlt wird. Bild fuer Bild pendelt er zwischen zweien.</summary>
        private const float DecisionSeconds = 0.18f;

        /// <summary>Gegner, die naeher sind, ziehen ihn in den Kampf. Weiter weg geht er seinem Ziel nach.</summary>
        private const float FightRadius = 10f;

        /// <summary>So nah am Ziel gilt er als angekommen. Der Ring eines Kerns misst 3,75.</summary>
        private const float ArriveRadius = 0.9f;

        /// <summary>
        /// Wie nah er an ein Ziel der Etage geht. Kern und Aufzug wirken schon ab 3,75 bzw. 2,5
        /// Einheiten; mit 0,9 drueckte er gegen den Kristall in der Mitte, und das Protokoll hielt
        /// es fuer Festhaengen.
        /// </summary>
        private const float GoalArriveRadius = 1.6f;

        /// <summary>Er will laufen - der Stick ist gedrueckt. Wer angekommen ist, will es nicht.</summary>
        public bool WantsToMove => Move.sqrMagnitude > 0.1f;

        /// <summary>So kurz liest er ein Auswahlfenster, bevor er drueckt - wie ein Mensch.</summary>
        private const float ModalReadSeconds = 0.6f;

        private readonly CombatBrain brain = new(CombatBrain.Player);
        private PlayerController controller;
        private WeaponSystem weapon;
        private Health health;
        private PrototypeHUD hud;
        private HeroClassId heroClass;
        private Vector3? goal;
        private Health target;
        private float nextDecision;
        private int floor = 1;
        private float nextModalPress;
        private Vector3 aimDirection = Vector3.forward;
        private Vector3 stuckAnchor;
        private float stuckSince;
        private float nextUnstick;

        // ── Die Knoepfe ─────────────────────────────────────────────────────
        public Vector2 Move { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool HeavyHeld { get; private set; }
        public bool HeavyPressed { get; private set; }
        public bool HeavyReleased { get; private set; }
        public bool DashPressed { get; private set; }
        public bool AutoAim { get; private set; }
        public bool ManualAim { get; private set; }
        public bool SkillHeld { get; private set; }
        public bool SkillReleased { get; private set; }
        public bool UltimateHeld { get; private set; }
        public bool UltimateReleased { get; private set; }

        /// <summary>Wie viele schwere Angriffe er losgelassen hat. Mehr ausgeloeste waeren ein Fehler.</summary>
        public int HeavyReleasesIssued => brain.HeavyReleasesIssued;

        /// <summary>Wie oft er einem angekuendigten Schlag ausgewichen ist.</summary>
        public int Dodges => brain.Dodges;

        /// <summary>Wann er zuletzt ausgewichen ist, in Spielzeit.</summary>
        public float LastDodgeAt => brain.LastDodgeAt;

        /// <summary>Wie oft er sich freidashen musste, weil er an einer Stelle hing.</summary>
        public int UnstickDashes { get; private set; }

        /// <summary>Was er gerade tut - fuer das Protokoll.</summary>
        public string Doing { get; private set; } = "START";

        /// <summary>Hat er gerade ein Ziel, auf das er zulaeuft?</summary>
        public bool HasGoal => goal.HasValue;

        /// <summary>Kaempft er gerade?</summary>
        public bool Fighting { get; private set; }

        public void Follow(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) aimDirection = direction.normalized;
        }

        /// <summary>Wird aufgerufen, wenn der Held und das HUD fertig sind.</summary>
        public void Configure(HeroClassId hero, PrototypeHUD hudReference)
        {
            heroClass = hero;
            hud = hudReference;
            controller = GetComponent<PlayerController>();
            weapon = GetComponent<WeaponSystem>();
            health = GetComponent<Health>();
            stuckAnchor = transform.position;
            stuckSince = Time.time;
        }

        private void OnEnable()
        {
            GameEvents.ObjectiveTargetChanged += OnObjectiveTarget;
            GameEvents.RoomStarted += OnRoomStarted;
        }

        private void OnDisable()
        {
            GameEvents.ObjectiveTargetChanged -= OnObjectiveTarget;
            GameEvents.RoomStarted -= OnRoomStarted;
        }

        private void OnObjectiveTarget(Vector3 position, string label, bool visible)
            => goal = visible ? position : null;

        private void OnRoomStarted(int index, RoomKind kind)
        {
            floor = index;
            goal = null;
            target = null;
        }

        private void Update()
        {
            ReleaseAllButtons();
            PressModal();
            if (!controller || !health || !health.IsAlive || Time.timeScale <= 0f) return;
            if (hud && hud.OpenModal != ModalKind.None) return;

            var holding = new CombatIntent();
            if (brain.FinishHeavy(weapon, ref holding))
            {
                Press(holding);
                Steer(target ? CombatPosition(target) : goal);
                Aim(target);
                Doing = "HEAVY";
                return;
            }

            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + DecisionSeconds;
                target = CombatBrain.ChooseTarget(transform);
            }
            if (target && !Targeting.IsTargetable(target, TeamId.Enemy)) target = null;

            var distance = target ? CombatBrain.FlatDistance(transform.position, target.transform.position) : float.MaxValue;
            Fighting = target && distance <= FightRadius;
            var fallen = Fighting ? null : FallenCompanionNearby();
            var destination = Fighting ? CombatPosition(target) : fallen ? fallen.position : goal;
            Doing = Fighting ? "FIGHT" : fallen ? "REVIVE" : goal.HasValue ? "OBJECTIVE" : "IDLE";
            if (brain.HoldOff(transform.position, out var clear))
            {
                destination = clear;
                Doing = "EVADE";
            }

            Steer(destination, Fighting || fallen || Doing == "EVADE" ? ArriveRadius : GoalArriveRadius);
            Aim(Fighting ? target : null);
            var dodge = new CombatIntent();
            if (brain.Dodge(transform, ref dodge))
            {
                Press(dodge);
                return;
            }
            if (Fighting)
            {
                var intent = new CombatIntent();
                brain.Fight(transform, health, weapon, heroClass, target, distance, ref intent);
                Press(intent);
            }
            Unstick(destination);
        }

        // ── Auswahlfenster ──────────────────────────────────────────────────

        private void PressModal()
        {
            if (!hud) return;
            var kind = hud.OpenModal;
            if (kind == ModalKind.None)
            {
                nextModalPress = Time.unscaledTime + ModalReadSeconds;
                return;
            }
            // Die Fenster halten das Spiel an - hier zaehlt die echte Zeit.
            if (Time.unscaledTime < nextModalPress) return;
            nextModalPress = Time.unscaledTime + ModalReadSeconds;
            var index = kind == ModalKind.Routes && hud.OpenRouteOptions.Count == hud.OpenModalButtons
                ? AutoPilotChoices.RouteFor(hud.OpenRouteOptions, floor, Autoplay.Routes)
                : AutoPilotChoices.ButtonFor(kind, hud.OpenModalButtons, floor);
            if (index < 0) return;
            Debug.Log($"SHATTERSPIRE Autopilot: {kind}, Knopf {index + 1} von {hud.OpenModalButtons}, Etage {floor}.");
            hud.PressModalForCapture(index);
        }

        // ── Bewegung ────────────────────────────────────────────────────────

        /// <summary>
        /// Wo er im Kampf stehen will: der Nahkaempfer geht heran, der Fernkaempfer haelt seinen
        /// Abstand - nicht seine volle Reichweite, sonst steht er bei jedem Schritt des Gegners draussen.
        /// </summary>
        private Vector3 CombatPosition(Health threat)
        {
            var range = HeroCatalog.EngageRange(heroClass);
            var keep = range * (HeroCatalog.IsMelee(heroClass) ? 0.7f : 0.72f);
            var fromThreat = transform.position - threat.transform.position;
            fromThreat.y = 0f;
            if (fromThreat.sqrMagnitude < 0.04f) fromThreat = -transform.forward;
            return threat.transform.position + fromThreat.normalized * keep;
        }

        /// <summary>Aus dem Ziel wird ein Stick - ueber das Wegenetz der Etage, wie bei den Begleitern.</summary>
        private void Steer(Vector3? destination, float arrive = ArriveRadius)
        {
            if (!destination.HasValue)
            {
                Move = Vector2.zero;
                return;
            }
            var navigation = controller.Navigation;
            var waypoint = navigation != null
                ? navigation.NextWaypoint(transform.position, destination.Value)
                : destination.Value;
            var toWaypoint = waypoint - transform.position;
            toWaypoint.y = 0f;
            var toGoal = destination.Value - transform.position;
            toGoal.y = 0f;
            if (toGoal.magnitude <= arrive || toWaypoint.sqrMagnitude < 0.0001f)
            {
                Move = Vector2.zero;
                return;
            }
            // Weich ankommen, aber nie kriechen: unter einem Drittel Stick waere er ein Hindernis.
            var strength = Mathf.Clamp(toGoal.magnitude / 1.2f, 0.35f, 1f);
            var direction = toWaypoint.normalized * strength;
            Move = new Vector2(direction.x, direction.z);
        }

        /// <summary>
        /// Haengt er an einer Stelle, obwohl er laufen will, dasht er quer heraus. Das Protokoll
        /// zaehlt es mit: jedes Mal ist eine Stelle, an der ein Mensch genauso haengen koennte.
        /// </summary>
        private void Unstick(Vector3? destination)
        {
            if (Time.time - stuckSince < 2.5f) return;
            var moved = CombatBrain.FlatDistance(transform.position, stuckAnchor);
            var wantsToMove = destination.HasValue && Move.sqrMagnitude > 0.1f;
            if (moved < 0.6f && wantsToMove && Time.time >= nextUnstick)
            {
                DashPressed = true;
                Move = new Vector2(-Move.y, Move.x).normalized;
                nextUnstick = Time.time + 3f;
                UnstickDashes++;
                Debug.Log($"SHATTERSPIRE Autopilot: haengt bei ({transform.position.x:0.0} / "
                          + $"{transform.position.z:0.0}) fest, dasht quer heraus.");
            }
            stuckAnchor = transform.position;
            stuckSince = Time.time;
        }

        private Transform FallenCompanionNearby()
        {
            var members = PartyMember.Active;
            for (var i = 0; i < members.Count; i++)
            {
                var other = members[i];
                if (!other || other.IsLocal || other.IsAlive) continue;
                var down = other.GetComponent<FallenHero>();
                if (!down || !down.IsDown) continue;
                if (CombatBrain.FlatDistance(transform.position, other.transform.position) <= 12f) return other.transform;
            }
            return null;
        }

        // ── Zielen und Druecken ─────────────────────────────────────────────

        private void Aim(Health threat)
        {
            ManualAim = false;
            if (threat)
            {
                AutoAim = true;
                AimPoint = threat.transform.position;
                return;
            }
            AutoAim = false;
            var heading = new Vector3(Move.x, 0f, Move.y);
            if (heading.sqrMagnitude > 0.02f) aimDirection = heading.normalized;
            AimPoint = transform.position + aimDirection * 8f;
        }

        private void Press(in CombatIntent intent)
        {
            AttackHeld |= intent.Attack;
            SkillReleased |= intent.SkillRelease;
            HeavyPressed |= intent.HeavyPress;
            HeavyHeld |= intent.HeavyHold;
            HeavyReleased |= intent.HeavyRelease;
            UltimateReleased |= intent.UltimateRelease;
            if (!intent.Dash) return;
            DashPressed = true;
            Move = intent.DashMove;
        }

        private void ReleaseAllButtons()
        {
            Move = Vector2.zero;
            AttackHeld = false;
            HeavyHeld = HeavyPressed = HeavyReleased = false;
            SkillHeld = SkillReleased = false;
            UltimateHeld = UltimateReleased = false;
            DashPressed = false;
        }
    }
}
