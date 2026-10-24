using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Simple drag-and-tap slider for pixel UI bars.
/// Calculates a normalized fraction (0..1) along the element's width.
/// <see cref="ValueChanged"/> fires while dragging; <see cref="ValueCommitted"/> fires once on release,
/// so callers can save the final value instead of writing it on every pointer move.
/// </summary>
public sealed class PixelSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Action<float> ValueChanged;
    public Action<float> ValueCommitted;
    RectTransform rectTransform;
    float lastValue;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData) => UpdateValue(eventData);
    public void OnDrag(PointerEventData eventData) => UpdateValue(eventData);
    public void OnPointerUp(PointerEventData eventData) => ValueCommitted?.Invoke(lastValue);

    void UpdateValue(PointerEventData eventData)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            float width = rectTransform.rect.width;
            if (width > 0f)
            {
                lastValue = Mathf.Clamp01((localPoint.x - rectTransform.rect.xMin) / width);
                ValueChanged?.Invoke(lastValue);
            }
        }
    }
}
