using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal window over the whole portrait: dark backdrop, wooden card, title and a close button.
/// Used by the zones in the clouds, the minigames and the star trip. While one is open Pepito and the
/// farm events wait (see <see cref="AnyOpen"/>).
/// </summary>
public static class Popup
{
    public const float W = 570f, H = 1018f;

    static int open;
    public static bool AnyOpen => open > 0;

    sealed class Marker : MonoBehaviour
    {
        public Action OnClosed;
        bool counted = true;
        public void Release()
        {
            if (!counted) return;
            counted = false;
            open = Mathf.Max(0, open - 1);
        }
        void OnDestroy()
        {
            Release();
            OnClosed?.Invoke();
        }
    }

    /// <summary>
    /// Opens a window. Returns its root (pass it to <see cref="Close"/>); <paramref name="card"/> is the panel
    /// to build on (its coordinates start at the card's top-left corner).
    /// </summary>
    public static RectTransform Open(Transform portrait, string name, string title, float y, float height,
        out RectTransform card, Action onClosed = null, bool closeButton = true)
    {
        var ui = new PixelUIFactory();
        var root = ui.Rect(portrait, name, 0, 0, W, H);
        root.SetAsLastSibling();
        var marker = root.gameObject.AddComponent<Marker>();
        marker.OnClosed = onClosed;
        open++;
        ui.Backdrop(root, "Backdrop", W, H, new Color(0f, 0f, .05f, .7f));
        card = ui.Panel(root, "Card", 14, y, W - 28, height, PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", title, 0, 14, W - 28, 40, 28, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        if (closeButton)
        {
            var r = root;
            ui.ActionButton(card, "Close", "X", W - 28 - 64, 12, 52, 46, () => Close(r));
            var face = card.Find("Close")?.Find("Face")?.GetComponent<Image>();
            if (face != null) face.color = PixelUIFactory.Hex("#B04A3A");
        }
        return root;
    }

    /// <summary>An empty full-screen window (counted like the others); the caller draws everything.</summary>
    public static RectTransform OpenBlank(Transform portrait, string name, Action onClosed = null)
    {
        var root = new PixelUIFactory().Rect(portrait, name, 0, 0, W, H);
        root.SetAsLastSibling();
        root.gameObject.AddComponent<Marker>().OnClosed = onClosed;
        open++;
        return root;
    }

    public static void Close(RectTransform root)
    {
        if (root == null) return;
        var marker = root.GetComponent<Marker>();
        if (marker != null) marker.Release();
        UnityEngine.Object.Destroy(root.gameObject);
    }

    /// <summary>Pepito's portrait with name plate and a speech bubble (shared look of every window).</summary>
    public static Text Pepito(PixelUIFactory ui, Transform card, float x, float y, float bubbleWidth, string text)
    {
        ui.Panel(card, "Portrait frame", x, y, 92, 90, PixelUIFactory.Hex("#4B5160"));
        var face = SpriteStripAnimator.Create(card, "Pepito face", "UI/Characters/pepito_talk", x + 8, y + 7, 76);
        face.Talk(2);
        var plate = ui.Panel(card, "Name plate", x - 2, y + 90, 96, 28, PixelUIFactory.Hex("#8A8F9C"));
        ui.Label(plate, "Name", "PEPITO", 0, 0, 96, 28, 15, Color.white, TextAnchor.MiddleCenter);
        ui.Block(card, "Bubble tail", x + 92, y + 34, 10, 16, PixelUIFactory.Ink);
        var bubble = ui.Rect(card, "Bubble", x + 98, y - 4, bubbleWidth, 110);
        bubble.gameObject.AddComponent<Image>().color = PixelUIFactory.Ink;
        ui.Block(bubble, "Fill", 3, 3, bubbleWidth - 6, 104, PixelUIFactory.Hex("#F6E7C8"));
        ui.Block(card, "Bubble tail fill", x + 95, y + 37, 10, 10, PixelUIFactory.Hex("#F6E7C8"));
        var label = ui.Paragraph(bubble, "Text", text, 12, 8, bubbleWidth - 24, 94, 16, PixelUIFactory.Ink);
        var outline = label.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        return label;
    }

    /// <summary>Changes the text size of an ActionButton's caption.</summary>
    public static void CaptionSize(Transform parent, string buttonName, int size)
    {
        var caption = parent.Find(buttonName)?.Find("Caption")?.GetComponent<Text>();
        if (caption != null) caption.fontSize = size;
    }

    /// <summary>Recolors an ActionButton.</summary>
    public static void ButtonColor(Transform parent, string buttonName, Color color)
    {
        var face = parent.Find(buttonName)?.Find("Face")?.GetComponent<Image>();
        if (face != null) face.color = color;
    }
}
