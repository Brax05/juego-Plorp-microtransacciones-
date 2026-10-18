using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared layout for the runtime-built menu pages (Tienda, Logros): wooden background,
/// header with title/subtitle and a vertical scrolling list.
/// </summary>
public static class MenuPage
{
    public const float Width = 570f, Height = 840f;
    public const float ListTop = 118f, ListWidth = 542f;

    public static RectTransform Create(Transform parent, string name, string title, string subtitle,
        out Text subtitleText)
    {
        var ui = new PixelUIFactory();
        var page = ui.Rect(parent, name, 0, 0, Width, Height);
        var bg = ui.Block(page, "Background", 0, 0, Width, Height, PixelUIFactory.Hex("#4A2A1C"));
        bg.raycastTarget = true;
        for (int i = 0; i < 14; i++)
            ui.Block(page, "Plank", 0, i * 60, Width, 2, PixelUIFactory.Hex("#3A2016"));
        var header = ui.Panel(page, "Header", 16, 12, 538, 96, PixelUIFactory.Hex("#CE8854"));
        ui.Label(header, "Title", title, 20, 10, 498, 42, 30, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        subtitleText = ui.Paragraph(header, "Subtitle", subtitle, 20, 52, 498, 34, 15, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
        return page;
    }

    /// <summary>Scroll area from <paramref name="top"/> to the bottom of the page. Returns the content rect.</summary>
    public static RectTransform ScrollList(Transform page, float top)
    {
        var ui = new PixelUIFactory();
        var viewport = ui.Rect(page, "List viewport", 14, top, ListWidth, Height - top - 8);
        viewport.gameObject.GetOrAdd<RectMask2D>();
        var hit = viewport.gameObject.GetOrAdd<Image>();
        hit.color = new Color(0, 0, 0, 0); // receives drags anywhere in the list
        var content = ui.Rect(viewport, "List content", 0, 0, ListWidth, 10);
        var scroll = viewport.gameObject.GetOrAdd<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.elasticity = .1f;
        scroll.inertia = true;
        scroll.scrollSensitivity = 30f;
        scroll.decelerationRate = .135f;
        viewport.gameObject.GetOrAdd<ScrollActivity>();
        return content;
    }

    /// <summary>
    /// True while the player is dragging the list or it is still gliding. Pages must not rebuild their
    /// cards then: destroying the card under the finger cancels the drag and makes scrolling jump.
    /// </summary>
    public static bool IsScrolling(RectTransform content)
    {
        var activity = content != null ? content.GetComponentInParent<ScrollActivity>() : null;
        return activity != null && activity.Busy;
    }

    sealed class ScrollActivity : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        bool dragging;
        ScrollRect scroll;
        public bool Busy
        {
            get
            {
                if (scroll == null) scroll = GetComponent<ScrollRect>();
                return dragging || (scroll != null && scroll.velocity.sqrMagnitude > 25f);
            }
        }
        public void OnBeginDrag(PointerEventData e) => dragging = true;
        public void OnEndDrag(PointerEventData e) => dragging = false;
        void OnDisable() => dragging = false;
    }

    public static void Clear(Transform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            UiUtil.Kill(content.GetChild(i).gameObject);
        }
    }

    public static void SetHeight(RectTransform content, float height) =>
        content.sizeDelta = new Vector2(content.sizeDelta.x, height);

    /// <summary>Small section title inside a list.</summary>
    public static float Section(PixelUIFactory ui, Transform content, string text, float y)
    {
        ui.Label(content, "Section", text, 8, y, 520, 30, 18, PixelUIFactory.Gold);
        return y + 34;
    }
}
