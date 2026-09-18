using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Was mit einem Helden passiert, der faellt - und was, wenn er zurueckkommt.
    ///
    /// Gebraucht wird das, seit die zwei Mitglieder der Gruppe echte Helden sind. Ein Gegner wird
    /// beim Tod geloescht; ein Held darf das nicht, er kommt auf der naechsten Etage wieder. Ohne
    /// diese Klasse bliebe er stehen wie festgefroren, mitten im Weg, und Gegner liefen weiter gegen
    /// ihn.
    ///
    /// Die Figur bleibt liegen, wo sie gefallen ist. Das ist Absicht: man soll sehen, wo die Gruppe
    /// auseinandergebrochen ist.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class FallenHero : MonoBehaviour
    {
        private Health health;
        private PartyMember member;
        private CharacterController motor;
        private StylizedCharacterMotion motion;
        private bool down;

        public bool IsDown => down;

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
            // Zurueckgeholt wird ueber Health.Revive - das meldet keinen eigenen Vorgang, also fragt
            // diese Stelle nach. Billiger als ein weiteres Ereignis, das jeder mitpflegen muesste.
            if (down && health && health.IsAlive) Rise();
        }

        private void Fall()
        {
            if (down) return;
            down = true;
            // Der Koerper steht niemandem mehr im Weg, weder dem Spieler noch den Gegnern.
            if (motor) motor.enabled = false;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            motion?.PlayPresence(PresenceMotion.Death, 1.3f, 0.9f);
            if (member) member.Status = "FALLEN";
            if (member)
                Debug.Log($"SHATTERSPIRE Gruppe: {member.DisplayName} ist gefallen.");
        }

        private void Rise()
        {
            down = false;
            if (motor) motor.enabled = true;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = true;
            motion?.PlayPresence(PresenceMotion.AwakenFloor, 1.2f, 1.1f);
            if (member) member.Status = "FOLLOWING";
        }
    }
}
