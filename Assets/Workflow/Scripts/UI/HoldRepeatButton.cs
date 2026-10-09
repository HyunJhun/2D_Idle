using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdlePrototype
{
    // One shared onClick event for press, repeats, keyboard submit and programmatic use.
    // Pointer release never invokes a second click after the initial press.
    public sealed class HoldRepeatButton : Button, IBeginDragHandler, IDragHandler
    {
        [SerializeField, Min(0)] float holdDelay = 0.15f;
        [SerializeField, Min(.01f)] float repeatInterval = 0.1f;
        public float HoldDelay => holdDelay;
        public float RepeatInterval => repeatInterval;
        public bool IsHolding { get; private set; }
        int holdingPointer;
        double nextRepeat;

        public override void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || IsHolding ||
                !IsActive() || !IsInteractable())
            {
                return;
            }

            base.OnPointerDown(data);
            IsHolding = true; 
            holdingPointer = data.pointerId;
            nextRepeat = Time.unscaledTimeAsDouble + holdDelay;
            onClick.Invoke();
            if (!IsActive() || !IsInteractable()) CancelHold();
        }
        public override void OnPointerUp(PointerEventData data)
        {
            if (IsHolding && data.pointerId != holdingPointer) return;
            base.OnPointerUp(data);
            if (data.button == PointerEventData.InputButton.Left) CancelHold();
        }
        public override void OnPointerClick(PointerEventData data) { /* Already invoked on down. */ }
        public override void OnPointerExit(PointerEventData data)
        {
            base.OnPointerExit(data);
            if (IsHolding && data.pointerId == holdingPointer) CancelHold();
        }
        public void OnBeginDrag(PointerEventData data) { if (data.pointerId == holdingPointer) CancelHold(); }
        public void OnDrag(PointerEventData data) { }
        public void CancelHold() { IsHolding = false; }
        protected override void OnDisable() { CancelHold(); base.OnDisable(); }
        protected override void OnCanvasGroupChanged()
        {
            base.OnCanvasGroupChanged();
            if (!IsInteractable()) CancelHold();
        }
        void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }
        void OnApplicationPause(bool paused) { if (paused) CancelHold(); }
        void Update()
        {
            if (!IsHolding) return;
            if (!IsActive() || !IsInteractable()) { CancelHold(); return; }
            double now = Time.unscaledTimeAsDouble;
            if (now < nextRepeat) return;
            // Keep the quarter-second cadence, but don't burst old repeats after a stall.
            nextRepeat += repeatInterval;
            if (nextRepeat <= now) nextRepeat = now + repeatInterval;
            onClick.Invoke();
        }
    }
}
