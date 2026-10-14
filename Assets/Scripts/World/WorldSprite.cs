using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>One independent, clickable pixel sprite in the farm world.</summary>
public sealed class WorldSprite : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    RectTransform rect;
    RawImage art;
    RectTransform artRect;
    string currentTexture;
    bool facingRight = true;
    bool dragged;
    ScrollRect parentScroll;
    static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

    public RectTransform Rect => rect;

    public void Initialize(PixelUIFactory ui, string textureName, UnityEngine.Events.UnityAction onClick)
    {
        rect = GetComponent<RectTransform>();
        parentScroll = GetComponentInParent<ScrollRect>();
        var hitImage = gameObject.AddComponent<Image>();
        hitImage.color = new Color(1, 1, 1, 0);
        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = hitImage;
        button.onClick.AddListener(() =>
        {
            if (dragged) return;
            onClick();
        });
        art = ui.Texture(transform, "Sprite art", "World/Sprites/" + textureName,
            0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
        artRect = art.rectTransform;
        artRect.pivot = new Vector2(.5f, .5f);
        artRect.anchoredPosition = new Vector2(rect.sizeDelta.x * .5f, -rect.sizeDelta.y * .5f);
        SetTexture(textureName);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragged = true;
        if (parentScroll == null) parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll != null) parentScroll.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentScroll == null) parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll != null) parentScroll.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (parentScroll == null) parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll != null) parentScroll.OnEndDrag(eventData);
        StartCoroutine(ResetDrag());
    }

    IEnumerator ResetDrag()
    {
        yield return null;
        dragged = false;
    }

    public void SetPose(bool facingRight, float heightScale, float lift, float lean)
    {
        if (this.facingRight != facingRight)
        {
            art.uvRect = facingRight ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
            this.facingRight = facingRight;
        }
        artRect.localScale = new Vector3(1f, heightScale, 1f);
        artRect.localRotation = Quaternion.Euler(0f, 0f, lean);
        artRect.anchoredPosition = new Vector2(rect.sizeDelta.x * .5f,
            -rect.sizeDelta.y * .5f + lift);
    }

    public void SetTexture(string textureName)
    {
        if (currentTexture == textureName) return;
        if (!TextureCache.TryGetValue(textureName, out var texture))
        {
            texture = Resources.Load<Texture2D>("World/Sprites/" + textureName);
            if (texture != null) TextureCache[textureName] = texture;
        }
        if (texture == null)
        {
            Debug.LogError("Missing world sprite: " + textureName);
            return;
        }
        texture.filterMode = FilterMode.Point;
        art.texture = texture;
        currentTexture = textureName;
    }

    public void SetReady(bool ready)
    {
        art.color = ready ? Color.white : new Color(.82f, .82f, .82f, .88f);
    }
}
