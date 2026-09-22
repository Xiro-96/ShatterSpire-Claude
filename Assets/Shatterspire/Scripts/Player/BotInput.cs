using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der Kopf hinter einem Mitglied der Gruppe, das niemand steuert.
    ///
    /// Er tut nichts selbst. Er drueckt Knoepfe - dieselben Knoepfe, die der Spieler drueckt, durch
    /// dieselbe Schnittstelle. Der Held daran ist ein ganz normaler Held: er hat seine Klasse, sein
    /// Leben, seine Aktionen, seine Ultimate, und er kann fallen.
    ///
    /// Das ist der Grund, warum diese Klasse so aussieht, wie sie aussieht. Sie koennte kuerzer
    /// sein, wenn sie Schaden direkt austeilen und die Figur direkt versetzen wuerde - so hat es der
    /// Vorgaenger gemacht, und dabei kam ein Begleiter heraus, der nicht sterben konnte und keine
    /// Faehigkeit hatte. Wer stattdessen nur Knoepfe drueckt, bekommt alles geschenkt, was der Held
    /// kann. Und wenn spaeter ein Mitspieler aus dem Netz an dieselbe Stelle tritt, aendert sich
    /// nichts weiter als die Herkunft der Knopfdruecke.
    ///
    /// Die Ausfuehrung laeuft vor allen anderen (<see cref="DefaultExecutionOrderAttribute"/>), damit
    /// ein Druck, der nur ein Bild lang gilt, sicher bei Waffe und Steuerung ankommt.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class BotInput : MonoBehaviour, IPlayerInputSource
    {
        /// <summary>Wie weit ein Mitglied hoechstens vom Spieler weg kaempft.</summary>
        private const float Leash = 12f;

        /// <summary>Daran gibt es kein Kaempfen mehr - erst zurueck zur Gruppe.</summary>
        private const float RegroupDistance = 16f;

        /// <summary>Ab hier gilt ein Mitglied als verloren und wird zum Spieler gesetzt.</summary>
        private const float TeleportDistance = 30f;

        /// <summary>Abstand, den die Gruppe untereinander haelt.</summary>
        private const float PartySpacing = 2.4f;

        /// <summary>Blase um den Spieler. Niemand soll ihm am Aermel kleben.</summary>
        private const float LeaderSpacing = 2.5f;

        /// <summary>Laenge und halbe Breite des Korridors, den die Gruppe vor dem Spieler frei haelt.</summary>
        private const float LaneLength = 3.4f;
        private const float LaneHalfWidth = 1.3f;

        /// <summary>Wie schnell der Bot reagiert. Ohne das trifft er auf das Bild genau - und wirkt tot.</summary>
        private const float ReactionSeconds = 0.18f;

        /// <summary>Wie weit ein Mitglied laeuft, um jemanden aufzuheben.</summary>
        private const float HelpRange = 16f;

        private Transform leader;
        private PartyMember member;
        private PlayerController controller;
        private WeaponSystem weapon;
        private Health health;
        private FloorNavigation navigation;
        private HeroClassId heroClass;
        private float side = 1f;
        private Vector3 leaderHeading = Vector3.forward;
        private Vector3 lastLeaderPosition;
        private Vector3 aimDirection = Vector3.forward;
        private Health target;
        private float nextDecision;

        /// <summary>Die Kampfentscheidungen - dieselben wie beim Autopiloten, in der Spielweise eines Begleiters.</summary>
        private readonly CombatBrain brain = new(CombatBrain.Companion);

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

        public void Follow(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) aimDirection = direction.normalized;
        }

        /// <summary>
        /// Wird erst aufgerufen, wenn der Held fertig ist. Die Verweise stehen deshalb hier und nicht
        /// in Awake: die Eingabe muss vor Steuerung und Waffe am Objekt haengen, damit die beiden sie
        /// in ihrem eigenen Awake finden - zu dem Zeitpunkt gibt es sie selbst also noch nicht.
        /// </summary>
        public void Configure(Transform player, HeroClassId hero, float formationSide)
        {
            leader = player;
            heroClass = hero;
            side = formationSide < 0f ? -1f : 1f;
            lastLeaderPosition = player ? player.position : transform.position;
            member = GetComponent<PartyMember>();
            controller = GetComponent<PlayerController>();
            weapon = GetComponent<WeaponSystem>();
            health = GetComponent<Health>();
        }

        public void SetNavigation(FloorNavigation value)
        {
            navigation = value;
            if (controller) controller.SetNavigation(value);
        }

        private void Update()
        {
            ReleaseAllButtons();
            if (!leader || !health || !health.IsAlive || Time.timeScale <= 0f)
            {
                if (member) member.Status = health && !health.IsAlive ? "FALLEN" : "FOLLOWING";
                return;
            }

            TrackLeaderHeading();
            RecoverIfLost();

            // Ziel nur ab und zu neu waehlen. Bild fuer Bild neu gesucht springt der Bot zwischen zwei
            // gleich weit entfernten Gegnern hin und her und trifft am Ende keinen.
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + ReactionSeconds;
                target = CombatBrain.ChooseTarget(transform);
            }
            if (target && !Targeting.IsTargetable(target, TeamId.Enemy)) target = null;

            var regrouping = CombatBrain.FlatDistance(transform.position, leader.position) > RegroupDistance;
            var threat = regrouping ? null : target;
            var distance = threat ? CombatBrain.FlatDistance(transform.position, threat.transform.position) : float.MaxValue;

            // Eine angefangene Ladung geht vor: wer mitten darin abbiegt, laesst den Schlag haengen.
            var holding = new CombatIntent();
            if (brain.FinishHeavy(weapon, ref holding))
            {
                var dodge = new CombatIntent();
                Press(brain.DodgeWhileCharging(transform, ref dodge) ? dodge : holding);
                Steer(DesiredPosition(threat));
                Aim(threat);
                return;
            }

            // Einen Gefallenen aufzuheben geht vor allem anderen - aber nur, wenn gerade nichts
            // unmittelbar auf einen selbst einschlaegt. Sonst liegen am Ende beide da.
            var fallen = FallenToHelp(distance);
            if (fallen)
            {
                Steer(fallen.transform.position);
                Aim(threat);
                if (member) member.Status = "REVIVING";
                return;
            }

            Steer(DesiredPosition(threat));
            Aim(threat);
            if (!regrouping)
            {
                var intent = new CombatIntent();
                brain.Fight(transform, health, weapon, heroClass, threat, distance, ref intent);
                Press(intent);
            }
            UpdateStatus(threat, regrouping);
        }

        // ── Bewegung ────────────────────────────────────────────────────────

        /// <summary>
        /// Wohin dieses Mitglied will. Ohne Gegner an seinen Platz in der Gruppe, mit Gegner auf die
        /// Entfernung, auf der sein Held kaempft: der Wachmann geht heran, der Schuetze bleibt weg.
        /// </summary>
        private Vector3 DesiredPosition(Health threat)
        {
            // Nach dem Ausweichen erst den Schlag abwarten, statt gleich wieder hineinzulaufen.
            if (brain.HoldOff(transform.position, out var clear)) return clear;
            if (!threat) return FormationSlot();

            var range = HeroCatalog.EngageRange(heroClass);
            // Der Nahkaempfer will heran, der Fernkaempfer will genau seinen Abstand halten - nicht
            // seine Reichweite ausreizen, sonst steht er bei jedem Schritt des Gegners ausserhalb.
            var keep = HeroCatalog.IsMelee(heroClass) ? range * 0.7f : range * 0.72f;
            var fromThreat = transform.position - threat.transform.position;
            fromThreat.y = 0f;
            if (fromThreat.sqrMagnitude < 0.04f) fromThreat = -leaderHeading;
            fromThreat.Normalize();

            var spot = threat.transform.position + fromThreat * keep;
            // Nicht in die Schusslinie des Spielers: ein Schritt zur eigenen Stammseite.
            spot += Vector3.Cross(Vector3.up, fromThreat) * (side * (HeroCatalog.IsMelee(heroClass) ? 1.1f : 1.8f));

            // Und nie weiter weg vom Spieler als die Leine. Ein Schuetze, der einem Laeufer quer
            // ueber die Etage nachstellt, laesst die Gruppe auseinanderfallen.
            var fromLeader = spot - leader.position;
            fromLeader.y = 0f;
            if (fromLeader.magnitude > Leash) spot = leader.position + fromLeader.normalized * Leash;
            return spot;
        }

        /// <summary>Ruhiger Platz in der Gruppe: hinter und neben dem Spieler, an seiner Laufrichtung.</summary>
        private Vector3 FormationSlot()
        {
            var right = Vector3.Cross(Vector3.up, leaderHeading);
            return member.Slot switch
            {
                PartySlot.Frontline => leader.position - leaderHeading * 1.4f + right * side * 2.3f,
                PartySlot.Rear => leader.position - leaderHeading * 3.4f + right * side * 1.2f,
                _ => leader.position - leaderHeading * 2.1f + right * side * 3.2f
            };
        }

        /// <summary>
        /// Aus dem Ziel wird ein Stick.
        ///
        /// Der Bot versetzt die Figur nicht mehr selbst - er lenkt sie, und Beschleunigung, Bremsweg,
        /// Waende und die begehbare Flaeche macht dieselbe Steuerung wie beim Spieler. Deshalb laeuft
        /// ein Bot jetzt auch nicht mehr durch eine Ecke, durch die der Spieler nicht kommt.
        /// </summary>
        private void Steer(Vector3 destination)
        {
            destination.y = transform.position.y;
            var waypoint = navigation != null
                ? navigation.NextWaypoint(transform.position, destination)
                : destination;
            var toWaypoint = waypoint - transform.position;
            toWaypoint.y = 0f;

            var steer = Vector3.zero;
            var distance = toWaypoint.magnitude;
            if (distance > 0.35f)
                // Weich ankommen: der letzte Meter wird langsamer gelaufen, sonst zappelt das
                // Mitglied um seinen Platz herum.
                steer = toWaypoint.normalized * Mathf.Clamp01(distance / 1.4f);

            steer += Separation();
            var flat = new Vector2(steer.x, steer.z);
            Move = Vector2.ClampMagnitude(flat, 1f);
        }

        /// <summary>
        /// Abstand halten - zum Spieler, zu den anderen, und vor allem aus dem Weg.
        ///
        /// Der Beitrag ist eine Richtung, kein Versatz. Vorher wurde hier direkt an der Position
        /// gedreht, und das lief bei 120 Bildern doppelt so schnell wie bei 60.
        /// </summary>
        private Vector3 Separation()
        {
            var correction = Vector3.zero;
            var fromLeader = transform.position - leader.position;
            fromLeader.y = 0f;
            if (fromLeader.sqrMagnitude < LeaderSpacing * LeaderSpacing)
                correction += (fromLeader.sqrMagnitude > 0.01f ? fromLeader.normalized : Vector3.right) * 1.1f;
            correction += StepOutOfLeadersLane();

            var members = PartyMember.Active;
            for (var i = 0; i < members.Count; i++)
            {
                var other = members[i];
                if (!other || other == member) continue;
                var away = transform.position - other.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f || away.sqrMagnitude >= PartySpacing * PartySpacing) continue;
                correction += away.normalized * 0.85f;
            }
            return correction;
        }

        /// <summary>
        /// Raeumt den Korridor vor dem Spieler. Wer in dem Streifen steht, in den der Spieler laeuft,
        /// tritt zur naeheren Seite heraus - umso entschlossener, je weiter er in der Mitte steht.
        /// Das ist der Unterschied zwischen "weicht irgendwann aus" und "man kommt vorbei".
        /// </summary>
        private Vector3 StepOutOfLeadersLane()
        {
            var heading = leaderHeading;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.01f) return Vector3.zero;
            heading.Normalize();
            var offset = transform.position - leader.position;
            offset.y = 0f;
            var ahead = Vector3.Dot(offset, heading);
            if (ahead < -0.6f || ahead > LaneLength) return Vector3.zero;
            var right = Vector3.Cross(Vector3.up, heading);
            var lateral = Vector3.Dot(offset, right);
            if (Mathf.Abs(lateral) >= LaneHalfWidth) return Vector3.zero;
            var away = Mathf.Abs(lateral) < 0.05f ? side : Mathf.Sign(lateral);
            var urgency = 1f - Mathf.Abs(lateral) / LaneHalfWidth;
            return right * (away * (0.4f + urgency * 0.9f));
        }

        // ── Zielen und Kaempfen ─────────────────────────────────────────────

        /// <summary>
        /// Zielen laeuft ueber dasselbe Selbstzielen wie beim Spieler auf dem Telefon: der Bot nennt
        /// die Richtung als Hinweis, die Waffe waehlt das Ziel. So gelten fuer ihn dieselben Regeln -
        /// Bosse zuerst, Armbruster vor Schildtraegern, und Vorhalten auf laufende Ziele.
        /// </summary>
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

        /// <summary>
        /// Der naechste gefallene Mitstreiter, zu dem es sich zu laufen lohnt. Nah genug, dass die
        /// Gruppe nicht auseinanderfaellt, und nicht mitten im eigenen Handgemenge - es sei denn, man
        /// steht ohnehin schon fast daneben.
        /// </summary>
        private FallenHero FallenToHelp(float threatDistance)
        {
            FallenHero best = null;
            var bestDistance = HelpRange;
            var members = PartyMember.Active;
            for (var i = 0; i < members.Count; i++)
            {
                var other = members[i];
                if (!other || other == member || other.IsAlive) continue;
                var down = other.GetComponent<FallenHero>();
                if (!down || !down.IsDown) continue;
                var offset = CombatBrain.FlatDistance(transform.position, other.transform.position);
                if (offset > bestDistance) continue;
                // Mitten im Kampf nur, wenn es fast ohne Umweg geht.
                if (threatDistance < 5f && offset > 4f) continue;
                bestDistance = offset;
                best = down;
            }
            return best;
        }

        /// <summary>Setzt die Absicht des Kopfes auf die Knoepfe dieses Bildes.</summary>
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

        // ── Kleinkram ───────────────────────────────────────────────────────

        private void ReleaseAllButtons()
        {
            Move = Vector2.zero;
            AttackHeld = false;
            HeavyHeld = HeavyPressed = HeavyReleased = false;
            SkillHeld = SkillReleased = false;
            UltimateHeld = UltimateReleased = false;
            DashPressed = false;
        }

        private void TrackLeaderHeading()
        {
            var moved = leader.position - lastLeaderPosition;
            moved.y = 0f;
            lastLeaderPosition = leader.position;
            if (moved.sqrMagnitude > 0.0004f)
                leaderHeading = Vector3.Slerp(leaderHeading, moved.normalized, 0.08f).normalized;
        }

        /// <summary>Zurueckgeblieben, etwa nach einem Etagenwechsel: nicht quer ueber die Etage laufen.</summary>
        private void RecoverIfLost()
        {
            if (CombatBrain.FlatDistance(transform.position, leader.position) <= TeleportDistance) return;
            var fallback = leader.position - leaderHeading * 2f
                           + Vector3.Cross(Vector3.up, leaderHeading) * side * 2f;
            Teleport(navigation != null ? navigation.ClampToWalkable(fallback, 0.5f) : fallback);
        }

        public void Teleport(Vector3 position)
        {
            if (controller) controller.Teleport(position);
            else transform.position = position;
            if (leader) lastLeaderPosition = leader.position;
        }

        private void UpdateStatus(Health threat, bool regrouping)
        {
            if (!member) return;
            member.Status = regrouping ? "REGROUPING"
                : !threat ? "FOLLOWING"
                : member.Slot == PartySlot.Frontline ? "FRONTLINE"
                : member.Slot == PartySlot.Rear ? "COVERING"
                : "FLANKING";
        }

    }
}
