using UnityEngine;

namespace Shatterspire
{
    /// <summary>Welche Knoepfe ein Kopf in diesem Bild drueckt, der nicht der Mensch am Geraet ist.</summary>
    public struct CombatIntent
    {
        public bool Attack;
        public bool SkillRelease;
        public bool HeavyPress;
        public bool HeavyHold;
        public bool HeavyRelease;
        public bool UltimateRelease;
        public bool Dash;

        /// <summary>Wohin der Dash geht, als Stick in der Weltebene. Null, wenn nicht gedasht wird.</summary>
        public Vector2 DashMove;
    }

    /// <summary>
    /// Die Kampfentscheidungen eines Helden, den kein Mensch steuert.
    ///
    /// Lag vorher allein in <see cref="BotInput"/>. Jetzt braucht sie auch der Autopilot, der den
    /// eigenen Helden im Selbsttest spielt - und er soll nicht anders kaempfen als die Begleiter, nur
    /// weniger zurueckhaltend. Deshalb eine Stelle, zwei Spielweisen: <see cref="Companion"/> haelt
    /// sich mit dem schweren Angriff zurueck, weil er auf dem Bildschirm des Spielers laut ist;
    /// <see cref="Player"/> setzt ihn ein, sobald er bereit ist, wie jemand, der am Knopf sitzt.
    /// </summary>
    public sealed class CombatBrain
    {
        /// <summary>Wie ein Kopf kaempft.</summary>
        public readonly struct Style
        {
            /// <summary>Mindestabstand zwischen zwei schweren Angriffen.</summary>
            public readonly float HeavyPause;

            /// <summary>Nur gegen Gruppen oder starke Gegner - sonst sofort, wenn bereit.</summary>
            public readonly bool HeavyOnlyWhenWorthIt;

            /// <summary>Wie viele Gegner in 8 Einheiten die Ultimate wert sind (Bosse immer).</summary>
            public readonly int UltimateCrowd;

            /// <summary>Spanne, aus der der Loslasspunkt des schweren Angriffs gewuerfelt wird.</summary>
            public readonly float ReleaseFrom;
            public readonly float ReleaseTo;

            /// <summary>Wie oft einem angekuendigten Schlag ausgewichen wird, von 0 bis 1.</summary>
            public readonly float DodgeChance;

            public Style(float heavyPause, bool heavyOnlyWhenWorthIt, int ultimateCrowd, float releaseFrom, float releaseTo,
                float dodgeChance)
            {
                HeavyPause = heavyPause;
                HeavyOnlyWhenWorthIt = heavyOnlyWhenWorthIt;
                UltimateCrowd = ultimateCrowd;
                ReleaseFrom = releaseFrom;
                ReleaseTo = releaseTo;
                DodgeChance = dodgeChance;
            }
        }

        /// <summary>
        /// Begleiter. Der schwere Angriff hoechstens alle sechs Sekunden und nur, wo er etwas
        /// ausrichtet - vorher fiel XIROs Urteil alle zwei Sekunden, auch wenn man selbst einen
        /// anderen Helden spielte. Der Loslasspunkt liegt meistens, aber nicht immer, im Fenster:
        /// ein Bot, der ihn jedes Mal trifft, waere besser als jeder Spieler.
        ///
        /// Weicht etwa jedem dritten angekuendigten Schlag aus. Vorher gar keinem - im ersten
        /// Selbsttest fielen die Begleiter in einem einzigen Aufstieg dreizehnmal.
        /// </summary>
        public static readonly Style Companion = new(6f, true, 3, 0.42f, 0.86f, 0.35f);

        /// <summary>
        /// Der eigene Held im Selbsttest. Setzt den schweren Angriff ein, sobald er bereit ist, und
        /// trifft das perfekte Fenster etwa drei von vier Malen - wie jemand, der das Spiel kennt.
        ///
        /// Weicht zwei von drei angekuendigten Schlaegen aus. Ohne das stand der Autopilot jeden
        /// Schlag aus, und die Nahkaempfer nahmen im ersten Selbsttest auf Etage 1 das 1,4- bis
        /// 1,8-fache ihres Lebens an Schaden - mehr, als es ein Mensch getan haette.
        /// </summary>
        public static readonly Style Player = new(0.3f, false, 2, 0.4f, 0.84f, 0.65f);

        /// <summary>Wie nah ein Ziel sein muss, damit es ueberhaupt beachtet wird.</summary>
        public const float ThreatRange = 14f;

        private readonly Style style;
        private float nextSkill;
        private float nextHeavy;
        private float nextUltimate;
        private float nextDash;
        private float heavyReleaseAt = 0.6f;

        public CombatBrain(Style style) => this.style = style;

        /// <summary>Wie viele schwere Angriffe dieser Kopf losgelassen hat. Der Selbsttest vergleicht.</summary>
        public int HeavyReleasesIssued { get; private set; }

        /// <summary>
        /// Haelt eine angefangene Ladung und laesst sie am gewuerfelten Punkt los - auch wenn das
        /// Ziel inzwischen gefallen ist. Volle Ladung loest nicht mehr von selbst aus; ohne das
        /// stuende der Held mit geladenem Schlag da und koennte nichts anderes mehr tun.
        /// </summary>
        public bool FinishHeavy(WeaponSystem weapon, ref CombatIntent intent)
        {
            if (!weapon || !weapon.ChargingHeavy) return false;
            if (weapon.HeavyChargeNormalized >= heavyReleaseAt)
            {
                intent.HeavyRelease = true;
                HeavyReleasesIssued++;
            }
            else
            {
                intent.HeavyHold = true;
            }
            return true;
        }

        /// <summary>
        /// Welchen Knopf der Held drueckt. Die Reihenfolge ist die Reihenfolge des Wertes: eine
        /// Ultimate ist das Teuerste, was er hat, dann die Faehigkeit, dann der schwere Schlag, und
        /// der einfache Angriff laeuft die ganze Zeit mit.
        /// </summary>
        public void Fight(Transform self, Health health, WeaponSystem weapon, HeroClassId hero, Health threat,
            float distance, ref CombatIntent intent)
        {
            if (!threat || !weapon || !self) return;
            var inRange = distance <= HeroCatalog.EngageRange(hero);

            if (weapon.UltimateActive) return;
            if (Dodge(self, ref intent)) return;
            if (Escape(self, health, threat, distance, ref intent)) return;

            // Die Ultimate nur, wenn sie sich lohnt. Wer sie am ersten Laeufer verbraucht, hat sie im
            // Bosskampf nicht - und genau dort braucht die Gruppe sie.
            if (weapon.UltimateReady && Time.time >= nextUltimate && inRange
                && (CountEnemiesWithin(self.position, 8f) >= style.UltimateCrowd || BossWithin(self.position, 13f)))
            {
                intent.UltimateRelease = true;
                nextUltimate = Time.time + 8f;
                return;
            }

            if (weapon.SkillCooldownRemaining <= 0f && inRange && Time.time >= nextSkill)
            {
                intent.SkillRelease = true;
                nextSkill = Time.time + 0.75f;
                return;
            }

            if (weapon.HeavyReady && inRange && Time.time >= nextHeavy
                && (!style.HeavyOnlyWhenWorthIt || WorthAHeavy(threat)))
            {
                intent.HeavyPress = true;
                intent.HeavyHold = true;
                heavyReleaseAt = Random.Range(style.ReleaseFrom, style.ReleaseTo);
                nextHeavy = Time.time + style.HeavyPause;
                return;
            }

            intent.Attack = inRange;
        }

        private readonly System.Collections.Generic.Dictionary<int, float> dodgeDecided = new();

        /// <summary>
        /// Einem angekuendigten Schlag ausweichen, der diesem Helden gilt. Je Ankuendigung wird
        /// einmal entschieden, nicht jedes Bild neu - sonst waere die Chance eine Gewissheit.
        /// Der Dash geht vom Gegner weg und ein Stueck zur Seite, aus der Linie heraus.
        /// </summary>
        public bool Dodge(Transform self, ref CombatIntent intent)
        {
            if (style.DodgeChance <= 0f || Time.time < nextDash) return false;
            var enemies = EnemyAgent.Active;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy || !enemy.IsTelegraphing || enemy.Target != self) continue;
                if (FlatDistance(self.position, enemy.transform.position) > 5f) continue;
                var id = enemy.GetInstanceID();
                if (dodgeDecided.TryGetValue(id, out var decidedAt) && Time.time - decidedAt < 1.5f) continue;
                dodgeDecided[id] = Time.time;
                if (Random.value > style.DodgeChance) continue;
                var away = self.position - enemy.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -self.forward;
                away = (away.normalized + Vector3.Cross(Vector3.up, away.normalized) * 0.6f).normalized;
                intent.Dash = true;
                intent.DashMove = new Vector2(away.x, away.z);
                nextDash = Time.time + 1.2f;
                Dodges++;
                LastDodgeAt = Time.time;
                return true;
            }
            return false;
        }

        /// <summary>Wie oft dieser Kopf ausgewichen ist. Der Selbsttest schreibt es mit.</summary>
        public int Dodges { get; private set; }

        /// <summary>Wann zuletzt ausgewichen wurde. Ein Treffer kurz danach heisst: der Dash hat nicht gereicht.</summary>
        public float LastDodgeAt { get; private set; } = -99f;

        /// <summary>
        /// Ausweichen, wenn es eng wird: wenig Leben und ein Gegner auf der Haut. Der Dash geht vom
        /// Gegner weg - eine Rolle in die Umklammerung hinein waere schlimmer als keine.
        /// </summary>
        private bool Escape(Transform self, Health health, Health threat, float distance, ref CombatIntent intent)
        {
            if (Time.time < nextDash || !health) return false;
            if (health.Normalized > 0.35f || distance > 3.2f) return false;
            var away = self.position - threat.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) return false;
            intent.Dash = true;
            intent.DashMove = Vector2.ClampMagnitude(new Vector2(away.normalized.x, away.normalized.z), 1f);
            nextDash = Time.time + 4.5f;
            return true;
        }

        /// <summary>
        /// Das naechste Ziel. Dasselbe Selbstzielen wie beim Spieler, mit einer Ausnahme: Gegner, die
        /// noch ruhen, bleiben ruhen. Sonst zoege ein Kopf ein Lager aus dem Nachbarraum, das noch
        /// niemand gesehen hat.
        /// </summary>
        public static Health ChooseTarget(Transform self, float range = ThreatRange)
        {
            var best = Targeting.FindBestAutoAim(self.position, self.forward, range, TeamId.Enemy);
            if (best && best.GetComponent<EnemyAgent>() is { IsIdle: true }) return null;
            return best;
        }

        /// <summary>
        /// Lohnt sich der schwere Angriff? Gegen einen starken Gegner immer, sonst nur, wenn er mehr
        /// als einen trifft.
        /// </summary>
        public static bool WorthAHeavy(Health threat)
        {
            if (!threat) return false;
            var agent = threat.GetComponent<EnemyAgent>();
            if (agent && (EnemyKinds.IsBoss(agent.Kind) || agent.Kind == EnemyKind.Elite)) return true;
            return CountEnemiesWithin(threat.transform.position, 3.5f) >= 2;
        }

        public static int CountEnemiesWithin(Vector3 point, float radius)
        {
            var count = 0;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!Targeting.IsTargetable(candidate, TeamId.Enemy)) continue;
                if (FlatDistance(point, candidate.transform.position) <= radius) count++;
            }
            return count;
        }

        public static bool BossWithin(Vector3 point, float radius)
        {
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!Targeting.IsTargetable(candidate, TeamId.Enemy)) continue;
                var agent = candidate.GetComponent<EnemyAgent>();
                if (!agent || !EnemyKinds.IsBoss(agent.Kind)) continue;
                if (FlatDistance(point, candidate.transform.position) <= radius) return true;
            }
            return false;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
