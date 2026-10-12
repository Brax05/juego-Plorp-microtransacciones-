using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>One independent, clickable pixel sprite in the farm world.</summary>
public sealed class WorldSprite : MonoBehaviour
{
    RectTransform rect;
    RawImage art;
    string currentTexture;
    static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

    public RectTransform Rect => rect;

    public void Initialize(PixelUIFactory ui, string textureName, UnityEngine.Events.UnityAction onClick)
    {
        rect = GetComponent<RectTransform>();
        var hitImage = gameObject.AddComponent<Image>();
        hitImage.color = new Color(1, 1, 1, 0);
        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = hitImage;
        button.onClick.AddListener(onClick);
        art = ui.Texture(transform, "Sprite art", "World/Sprites/" + textureName,
            0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
        SetTexture(textureName);
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
