using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Creates the common pixel-style panels, labels and hit areas.</summary>
public sealed class PixelUIFactory
{
    public static readonly Color Ink = Hex("#301B1E");
    public static readonly Color Gold = Hex("#FFD44F");
    public static readonly Color Cream = Hex("#F6D8A6");
    public static readonly Color Green = Hex("#2DA447");
    readonly Font font;

    public PixelUIFactory()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    public Image Block(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public RectTransform Panel(Transform parent, string name, float x, float y, float width, float height,
        Color fill, bool blockInput = false)
    {
        var panel = Rect(parent, name, x, y, width, height);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = Ink;
        background.raycastTarget = blockInput;
        Block(panel, "Edge", 3, 3, width - 6, height - 6, Hex("#7A3F2E"));
        Block(panel, "Face", 7, 7, width - 14, height - 14, fill);
        Block(panel, "Top shine", 9, 9, width - 18, 3, Hex("#D7945E"));
        return panel;
    }

    public RawImage Texture(Transform parent, string name, string resource, float x, float y, float width, float height)
    {
        var raw = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null) Debug.LogError("Missing UI texture: Resources/" + resource);
        else texture.filterMode = FilterMode.Point;
        raw.texture = texture;
        raw.raycastTarget = false;
        return raw;
    }

    public Text Label(Transform parent, string name, string value, float x, float y, float width, float height,
        int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Ink;
        outline.effectDistance = new Vector2(2, -2);
        return text;
    }

    public Button Hit(Transform parent, string name, float x, float y, float width, float height, UnityAction action)
    {
        var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = new Color(1, 1, 1, 0);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        return button;
    }

    public Button ActionButton(Transform parent, string name, string title, float x, float y, float width,
        float height, UnityAction action, bool enabled = true)
    {
        var panel = Panel(parent, name, x, y, width, height, enabled ? Green : Hex("#67594F"));
        Label(panel, "Caption", title, 4, 2, width - 8, height - 4, 20, Color.white, TextAnchor.MiddleCenter);
        var hit = Hit(parent, name + " hit", x, y, width, height, action);
        hit.interactable = enabled;
        return hit;
    }

    public static Color Hex(string code)
    {
        ColorUtility.TryParseHtmlString(code, out Color color);
        return color;
    }
}
