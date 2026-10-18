using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Creates the pixel-style UI pieces (rects, blocks, panels, labels, pictures, buttons).
///
/// <b>The canvas is the source of truth.</b> Every method first looks for a child with that name under
/// the parent (one that this factory hasn't used yet). If it exists — because the UI was baked into
/// PixelFarmCanvas with "Plorp ▸ Hornear UI en el canvas" — it is reused <b>as it is</b>: position, size,
/// colour, text and picture edited in Unity are kept. Only when it doesn't exist is it created with the
/// values given in code. Buttons always get their action from code.
/// </summary>
public sealed class PixelUIFactory
{
    public static readonly Color Ink = Hex("#301B1E");
    public static readonly Color Gold = Hex("#FFD44F");
    public static readonly Color Cream = Hex("#F6D8A6");
    public static readonly Color Green = Hex("#2DA447");
    public static readonly Color Disabled = Hex("#67594F");
    readonly Font font;
    readonly HashSet<Transform> used = new HashSet<Transform>();

    public PixelUIFactory()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>True when the last call reused an object that already was in the canvas.</summary>
    public bool Reused { get; private set; }

    /// <summary>Unused child of <paramref name="parent"/> called <paramref name="name"/>, or null.</summary>
    Transform Existing(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name && !used.Contains(child)) return child;
        }
        return null;
    }

    public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
    {
        var existing = Existing(parent, name) as RectTransform;
        if (existing != null)
        {
            used.Add(existing);
            Reused = true;
            return existing;
        }
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        used.Add(rect);
        Reused = false;
        return rect;
    }

    public Image Block(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var rect = Rect(parent, name, x, y, width, height);
        bool reused = Reused;
        var image = rect.gameObject.GetOrAdd<Image>();
        if (!reused)
        {
            image.color = color;
            image.raycastTarget = false;
        }
        Reused = reused;
        return image;
    }

    public RectTransform Panel(Transform parent, string name, float x, float y, float width, float height,
        Color fill, bool blockInput = false)
    {
        var panel = Rect(parent, name, x, y, width, height);
        bool reused = Reused;
        var background = panel.gameObject.GetOrAdd<Image>();
        background.raycastTarget = blockInput;
        if (!reused)
        {
            background.color = Ink;
            Block(panel, "Edge", 3, 3, width - 6, height - 6, Hex("#7A3F2E"));
            Block(panel, "Face", 7, 7, width - 14, height - 14, fill);
            Block(panel, "Top shine", 9, 9, width - 18, 3, Hex("#D7945E"));
        }
        else
        {
            // Mark the parts as used so later children with the same names are created fresh.
            Block(panel, "Edge", 0, 0, 0, 0, Color.clear);
            Block(panel, "Face", 0, 0, 0, 0, Color.clear);
            Block(panel, "Top shine", 0, 0, 0, 0, Color.clear);
        }
        Reused = reused;
        return panel;
    }

    public RawImage Texture(Transform parent, string name, string picture, float x, float y, float width, float height)
    {
        var rect = Rect(parent, name, x, y, width, height);
        bool reused = Reused;
        var raw = rect.gameObject.GetOrAdd<RawImage>();
        // A picture chosen in Unity wins over the one in code.
        if (!reused || raw.texture == null)
        {
            raw.texture = Art.Tex(picture);
            raw.raycastTarget = false;
        }
        Reused = reused;
        return raw;
    }

    public Text Label(Transform parent, string name, string value, float x, float y, float width, float height,
        int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var rect = Rect(parent, name, x, y, width, height);
        bool reused = Reused;
        var text = rect.gameObject.GetOrAdd<Text>();
        if (!reused)
        {
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.GetOrAdd<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(2, -2);
        }
        if (text.font == null) text.font = font;
        Reused = reused;
        return text;
    }

    /// <summary>Multi-line label that wraps inside its box and shrinks text that does not fit.</summary>
    public Text Paragraph(Transform parent, string name, string value, float x, float y, float width, float height,
        int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var text = Label(parent, name, value, x, y, width, height, size, color, alignment);
        if (!Reused)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size - 6);
            text.resizeTextMaxSize = size;
            text.lineSpacing = .95f;
        }
        return text;
    }

    /// <summary>Full-rect translucent layer that swallows taps (modal backdrop).</summary>
    public Image Backdrop(Transform parent, string name, float width, float height, Color color)
    {
        var image = Block(parent, name, 0, 0, width, height, color);
        image.raycastTarget = true;
        return image;
    }

    /// <summary>Invisible tap area. The action always comes from code.</summary>
    public Button Hit(Transform parent, string name, float x, float y, float width, float height, UnityAction action)
    {
        var rect = Rect(parent, name, x, y, width, height);
        bool reused = Reused;
        var image = rect.gameObject.GetOrAdd<Image>();
        if (!reused) image.color = new Color(1, 1, 1, 0);
        image.raycastTarget = true;
        var button = rect.gameObject.GetOrAdd<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(action);
        Reused = reused;
        return button;
    }

    /// <summary>Wooden panel with a caption and a tap area ("Name" + "Name hit").</summary>
    public Button ActionButton(Transform parent, string name, string title, float x, float y, float width,
        float height, UnityAction action, bool enabled = true)
    {
        var panel = Panel(parent, name, x, y, width, height, enabled ? Green : Disabled);
        bool reused = Reused;
        Label(panel, "Caption", title, 4, 2, width - 8, height - 4, 20, Color.white, TextAnchor.MiddleCenter);
        var hit = Hit(parent, name + " hit", x, y, width, height, () =>
        {
            GameAudio.Play("sfx_ui_click", .6f);
            action?.Invoke();
        });
        hit.interactable = enabled;
        Reused = reused;
        return hit;
    }

    public static Color Hex(string code)
    {
        ColorUtility.TryParseHtmlString(code, out Color color);
        return color;
    }
}
