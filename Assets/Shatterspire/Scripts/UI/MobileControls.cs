using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shatterspire
{
    public sealed class MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private RectTransform knob;
        public void Configure(RectTransform value) { rect = (RectTransform)transform; knob = value; }
        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);
        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return;
            var radius = rect.rect.width * 0.38f;
            var value = Vector2.ClampMagnitude(local / radius, 1f);
            MobileInput.Move = value;
            knob.anchoredPosition = value * radius;
        }
        public void OnPointerUp(PointerEventData eventData) { MobileInput.Move = Vector2.zero; knob.anchoredPosition = Vector2.zero; }
    }

    public enum MobileAction { Attack, Heavy, Skill, Dash }

    public sealed class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private MobileAction action;
        public void Configure(MobileAction value) => action = value;
        public void OnPointerDown(PointerEventData eventData)
        {
            switch (action)
            {
                case MobileAction.Attack: MobileInput.Attack = true; break;
                case MobileAction.Heavy: MobileInput.SetHeavy(true); break;
                case MobileAction.Skill: MobileInput.PressSkill(); break;
                case MobileAction.Dash: MobileInput.PressDash(); break;
            }
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (action == MobileAction.Attack) MobileInput.Attack = false;
            if (action == MobileAction.Heavy) MobileInput.SetHeavy(false);
        }
    }
}
