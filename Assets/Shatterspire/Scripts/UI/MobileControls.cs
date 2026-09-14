using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shatterspire
{
    /// <summary>Was ein Stick steuert: die Bewegung links, das Ziel rechts.</summary>
    public enum StickRole { Move, Aim }

    /// <summary>
    /// Schwebender Touch-Stick. Die ganze Bildschirmhaelfte ist Bedienflaeche: der Stick entsteht dort,
    /// wo der Daumen aufsetzt, und zaehlt von dort aus.
    ///
    /// Vorher sass er an einer festen Stelle und rechnete die Auslenkung ab seiner Mitte. Wer daneben
    /// tippte, bekam sofort Vollausschlag in eine Richtung, die er nicht gemeint hatte, und wer die
    /// Stelle verfehlte, bekam gar nichts. Genau das fuehlt sich schwerfaellig an, obwohl die Figur
    /// selbst schnell reagiert.
    /// </summary>
    public sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Weg bis Vollausschlag, als Anteil der Bildschirmhoehe. Zu klein wird zappelig.</summary>
        private const float TravelFraction = 0.14f;
        /// <summary>Unterhalb davon zaehlt die Auslenkung als Null - gegen Zittern im Daumen.</summary>
        private const float DeadZone = 0.14f;

        private RectTransform zone;
        private RectTransform ring;
        private RectTransform knob;
        private StickRole role;
        private Vector2 origin;
        private bool held;

        public void Configure(RectTransform ringRect, RectTransform knobRect, StickRole value)
        {
            zone = (RectTransform)transform;
            ring = ringRect;
            knob = knobRect;
            role = value;
            Show(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Local(eventData, out var local)) return;
            // Der Aufsetzpunkt ist die neue Mitte: der erste Moment erzeugt keine Bewegung.
            origin = local;
            held = true;
            Show(true);
            ring.anchoredPosition = origin;
            knob.anchoredPosition = origin;
            Publish(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!held || !Local(eventData, out var local)) return;
            var travel = Mathf.Max(40f, Screen.height * TravelFraction) / CanvasScale();
            var offset = Vector2.ClampMagnitude((local - origin) / travel, 1f);
            knob.anchoredPosition = origin + offset * travel;
            var magnitude = offset.magnitude;
            if (magnitude <= DeadZone)
            {
                Publish(Vector2.zero);
                return;
            }
            // Hinter der Totzone wieder auf 0 bis 1 streckne, damit kein Sprung entsteht.
            Publish(offset / magnitude * ((magnitude - DeadZone) / (1f - DeadZone)));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            held = false;
            Show(false);
            Publish(Vector2.zero);
        }

        private void OnDisable()
        {
            held = false;
            Publish(Vector2.zero);
        }

        private void Publish(Vector2 value)
        {
            if (role == StickRole.Move)
            {
                MobileInput.Move = value;
                return;
            }
            MobileInput.Aim = value;
            // Der Zielstick schiesst mit: ein Daumen fuer Richtung und Angriff, wie in
            // Zweistick-Spielen ueblich. Der Angriffsknopf bleibt zusaetzlich bestehen.
            MobileInput.AimFire = value.sqrMagnitude > 0.0001f;
        }

        private void Show(bool visible)
        {
            if (ring) ring.gameObject.SetActive(visible);
            if (knob) knob.gameObject.SetActive(visible);
        }

        private bool Local(PointerEventData eventData, out Vector2 local)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position,
                eventData.pressEventCamera, out local);

        private float CanvasScale()
        {
            var canvas = zone ? zone.GetComponentInParent<Canvas>() : null;
            return canvas && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
        }
    }

    public enum MobileAction { Attack, Heavy, Skill, Dash, Ultimate }

    /// <summary>
    /// Ein Aktionsknopf, den man ziehen kann.
    ///
    /// Tippen loest sofort aus, wie bisher. Zieht man ihn stattdessen auf, wird die Richtung zur
    /// Zielrichtung und die Zielanzeige erscheint auf dem Boden; erst das Loslassen loest aus. Das
    /// ist der Griff aus Brawl Stars: dieselbe Taste fuer "schnell" und fuer "genau".
    /// </summary>
    public sealed class MobileActionButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        /// <summary>Weg bis Vollausschlag beim Ziehen, als Anteil der Bildschirmhoehe.</summary>
        private const float TravelFraction = 0.12f;
        /// <summary>Darunter gilt der Zug als Tippen und die Zielrichtung bleibt unberuehrt.</summary>
        private const float DeadZone = 0.22f;

        private MobileAction action;
        private Vector2 origin;
        private bool dragging;
        public void Configure(MobileAction value) => action = value;

        private bool Aimable => action is MobileAction.Skill or MobileAction.Ultimate;

        public void OnDrag(PointerEventData eventData)
        {
            if (!Aimable) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)transform, eventData.position, eventData.pressEventCamera, out var local))
                return;
            if (!dragging)
            {
                origin = local;
                dragging = true;
            }
            var canvas = GetComponentInParent<Canvas>();
            var scale = canvas && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            var travel = Mathf.Max(40f, Screen.height * TravelFraction) / scale;
            var offset = Vector2.ClampMagnitude((local - origin) / travel, 1f);
            MobileInput.ActionAim = offset.magnitude <= DeadZone ? Vector2.zero : offset.normalized;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            switch (action)
            {
                case MobileAction.Attack: MobileInput.Attack = true; break;
                case MobileAction.Heavy: MobileInput.SetHeavy(true); break;
                case MobileAction.Skill: MobileInput.SetSkill(true); break;
                case MobileAction.Dash: MobileInput.PressDash(); break;
                case MobileAction.Ultimate: MobileInput.SetUltimate(true); break;
            }
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (action == MobileAction.Attack) MobileInput.Attack = false;
            if (action == MobileAction.Heavy) MobileInput.SetHeavy(false);
            if (action == MobileAction.Skill) MobileInput.SetSkill(false);
            if (action == MobileAction.Ultimate) MobileInput.SetUltimate(false);
            // Erst die Richtung loeschen, nachdem die Aktion sie gelesen hat: das Loslassen und der
            // Schuss liegen im selben Bild.
            if (dragging) MobileInput.ActionAim = Vector2.zero;
            dragging = false;
        }

        private void OnDisable()
        {
            if (action == MobileAction.Skill) MobileInput.SetSkill(false);
            if (action == MobileAction.Ultimate) MobileInput.SetUltimate(false);
            dragging = false;
        }
    }
}
