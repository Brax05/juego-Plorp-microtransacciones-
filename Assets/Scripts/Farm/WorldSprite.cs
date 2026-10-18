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
    Vector2 artStart;
    Vector3 artScale;
    Quaternion artRotation;
    Color artColor;
    Rect artUV;
    string currentTexture;
    bool facingRight = true;
    bool dragged;
    bool keepArtTexture;
    ScrollRect parentScroll;
    static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

    public RectTransform Rect => rect;

    public void Initialize(string textureName, UnityEngine.Events.UnityAction onClick,
        bool keepCustomTexture = false, string authoredDefaultTextureName = null)
    {
        rect = GetComponent<RectTransform>();
        parentScroll = GetComponentInParent<ScrollRect>();
        var hitImage = GetComponent<Image>();
        if (hitImage == null)
        {
            hitImage = gameObject.GetOrAdd<Image>();
            hitImage.color = new Color(1, 1, 1, 0);
        }
        var button = GetComponent<Button>();
        if (button == null) button = gameObject.GetOrAdd<Button>();
        if (button.targetGraphic == null) button.targetGraphic = hitImage;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (dragged) return;
            onClick();
        });
        var existingArt = transform.Find("Sprite art");
        art = existingArt != null ? existingArt.GetComponent<RawImage>() : null;
        if (art == null)
            throw new System.InvalidOperationException(name + " needs a Sprite art RawImage in PixelFarmCanvas.");
        artRect = art.rectTransform;
        artStart = artRect.anchoredPosition;
        artScale = artRect.localScale;
        artRotation = artRect.localRotation;
        artColor = art.color;
        artUV = art.uvRect;
        var defaultTexture = Art.Tex("" +
            (authoredDefaultTextureName ?? textureName));
        keepArtTexture = keepCustomTexture && existingArt != null && art.texture != null &&
            art.texture != defaultTexture;
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
            art.uvRect = facingRight ? artUV :
                new Rect(artUV.x + artUV.width, artUV.y, -artUV.width, artUV.height);
            this.facingRight = facingRight;
        }
        artRect.localScale = new Vector3(artScale.x, artScale.y * heightScale, artScale.z);
        artRect.localRotation = artRotation * Quaternion.Euler(0f, 0f, lean);
        artRect.anchoredPosition = artStart + new Vector2(0f, lift);
    }

    public void SetTexture(string textureName)
    {
        if (keepArtTexture) return;
        if (currentTexture == textureName) return;
        if (!TextureCache.TryGetValue(textureName, out var texture))
        {
            texture = Art.Tex("" + textureName);
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
        art.color = ready ? artColor : artColor * new Color(.82f, .82f, .82f, .88f);
    }
}
