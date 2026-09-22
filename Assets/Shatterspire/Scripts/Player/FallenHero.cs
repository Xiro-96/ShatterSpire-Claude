using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Was mit einem Helden passiert, der faellt - und wie man ihn wieder auf die Beine bekommt.
    ///
    /// Gebraucht wird das, seit die zwei Mitglieder der Gruppe echte Helden sind. Ein Gegner wird
    /// beim Tod geloescht; ein Held darf das nicht. Ohne diese Klasse bliebe er stehen wie
    /// festgefroren, mitten im Weg, und Gegner liefen weiter gegen ihn.
    ///
    /// Aufheben ist die Stelle, an der aus drei Helden eine Gruppe wird: es kostet Zeit, es geht nur
    /// dicht daneben, und wer dort steht, kaempft nicht. Genau deshalb faellt die Entscheidung
    /// mitten im Gefecht und nicht danach.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class FallenHero : MonoBehaviour
    {
        /// <summary>Wie nah man stehen muss. Etwa eine Armlaenge ueber dem eigenen Radius.</summary>
        public const float ReviveRadius = 2.4f;

        /// <summary>Wie lange es dauert. Lang genug, dass man sich den Moment aussuchen muss.</summary>
        public const float ReviveSeconds = 3f;

        /// <summary>Mit so viel Leben steht er wieder auf. Genug zum Weiterlaufen, nicht mehr.</summary>
        public const float ReviveHealth = 0.45f;

        private Health health;
        private PartyMember member;
        private CharacterController motor;
        private StylizedCharacterMotion motion;
        private Transform ring;
        private Renderer ringRenderer;
        private bool down;
        private float progress;

        public bool IsDown => down;

        /// <summary>Wie weit das Aufheben gediehen ist, von 0 bis 1. Fuer das HUD.</summary>
        public float ReviveProgress => progress / ReviveSeconds;

        /// <summary>Hebt gerade jemand auf? Das HUD zeigt den Balken nur dann.</summary>
        public bool BeingRevived { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            member = GetComponent<PartyMember>();
            motor = GetComponent<CharacterController>();
            motion = GetComponent<StylizedCharacterMotion>();
            health.Died += Fall;
        }

        private void OnDestroy()
        {
            if (health) health.Died -= Fall;
        }

        private void Update()
        {
            if (!down) return;
            // Zurueckgeholt wird auch ueber Health.Revive beim Etagenwechsel. Das meldet keinen
            // eigenen Vorgang, also fragt diese Stelle jedes Bild nach.
            if (health && health.IsAlive)
            {
                Rise();
                return;
            }

            var helper = Helper();
            BeingRevived = helper;
            progress = Advance(progress, helper, Time.deltaTime);
            UpdateRing();
            if (member) member.Status = helper ? "BEING REVIVED" : "FALLEN";
            if (progress < ReviveSeconds || !health) return;
            health.Revive(ReviveHealth);
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.6f, 2f,
                member ? member.Accent : Color.white);
            Sfx.Play(Sound.CoreActivated, transform.position, 0.9f);
        }

        /// <summary>
        /// Wie weit das Aufheben nach diesem Bild ist.
        ///
        /// Steht als reine Rechnung da, damit sie sich ohne laufendes Spiel nachpruefen laesst -
        /// wegzulaufen darf nicht alles zunichte machen, aber es muss etwas kosten. Deshalb faellt
        /// der Fortschritt nur halb so schnell zurueck, wie er gestiegen ist.
        /// </summary>
        public static float Advance(float progress, bool helper, float deltaTime)
            => helper
                ? Mathf.Clamp(progress + deltaTime, 0f, ReviveSeconds)
                : Mathf.Clamp(progress - deltaTime * DecayFactor, 0f, ReviveSeconds);

        /// <summary>Anteil, mit dem der Fortschritt ohne Helfer zurueckfaellt.</summary>
        public const float DecayFactor = 0.5f;

        /// <summary>
        /// Wer gerade aufhebt: irgendein lebendes Mitglied der Gruppe dicht daneben, das nicht selbst
        /// am Boden liegt. Zwei Helfer gehen nicht schneller - sonst waere die Antwort auf einen
        /// gefallenen Mitstreiter, geschlossen stehen zu bleiben.
        /// </summary>
        private PartyMember Helper()
        {
            var members = PartyMember.Active;
            for (var i = 0; i < members.Count; i++)
            {
                var other = members[i];
                if (!other || other == member || !other.IsAlive) continue;
                var offset = other.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= ReviveRadius * ReviveRadius) return other;
            }
            return null;
        }

        private void Fall()
        {
            if (down) return;
            down = true;
            progress = 0f;
            // Der Koerper steht niemandem mehr im Weg, weder dem Spieler noch den Gegnern.
            if (motor) motor.enabled = false;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            motion?.PlayPresence(PresenceMotion.Death, 1.3f, 0.9f);
            if (member) member.Status = "FALLEN";
            CreateRing();
            if (member) Debug.Log($"SHATTERSPIRE Gruppe: {member.DisplayName} ist gefallen.");
        }

        private void Rise()
        {
            down = false;
            BeingRevived = false;
            progress = 0f;
            if (motor) motor.enabled = true;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = true;
            motion?.PlayPresence(PresenceMotion.AwakenFloor, 1.2f, 1.1f);
            // Wie beim eigenen Helden: aufstehen verschafft Luft, sonst liegt er gleich wieder.
            EnemyAgent.ClearRoomAround(transform.position);
            if (member) member.Status = "FOLLOWING";
            if (ring) Destroy(ring.gameObject);
        }

        /// <summary>
        /// Der Ring am Boden. Er liegt dort, wo der Held liegt, und fuellt sich beim Aufheben - damit
        /// die Entscheidung im Raum steht und nicht nur in einer Ecke des Bildschirms.
        /// </summary>
        private void CreateRing()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "Fallen Marker";
            PrototypeFactory.RemoveCollider(marker.GetComponent<Collider>());
            var renderer = marker.GetComponent<Renderer>();
            ringRenderer = renderer;
            renderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(FallenColor, 0.58f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ring = marker.transform;
            ring.SetParent(transform, false);
            ring.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.localScale = new Vector3(ReviveRadius * 2f, ReviveRadius * 2f, 1f);
        }

        private static readonly Color FallenColor = new(0.85f, 0.2f, 0.22f, 0.85f);

        private void UpdateRing()
        {
            if (!ring) return;
            // Der Ring zieht sich beim Aufheben zusammen: aus "hier liegt jemand" wird "gleich steht
            // er wieder". Eine Farbe allein liest sich auf dem Telefon zu langsam.
            // Weltlage jedes Bild neu: der Ring haengt am Helden, und dessen Drehung soll ihn nicht
            // mitkippen. Genau diese Falle hat schon einmal die Zielanzeige doppelt gedreht.
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            var shrink = Mathf.Lerp(1f, 0.45f, ReviveProgress);
            var pulse = BeingRevived ? 1f : 0.94f + Mathf.Sin(Time.time * 4f) * 0.06f;
            var size = ReviveRadius * 2f * shrink * pulse;
            ring.localScale = new Vector3(size, size, 1f);
            // Genau zwei Materialien, nicht eines je Bild: die Fabrik legt sie nach Farbe ab, und
            // ein Farbverlauf ueber drei Sekunden haette hundert Stueck im Speicher hinterlassen.
            if (!ringRenderer) return;
            var wanted = ReviveProgress >= 0.5f && member ? member.Accent : FallenColor;
            ringRenderer.sharedMaterial = PrototypeFactory.CreateRadialDecal(wanted, 0.58f);
        }
    }
}
