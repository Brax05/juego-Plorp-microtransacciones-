using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fits the 570x1018 portrait to the real phone screen. The width is filled and the content grows in
/// height; the extra space goes to the farm (the middle), and the upgrade rows and the bottom
/// navigation move down with it. The reference layout (editor and bake) is never changed.
///
/// The canvas is authored top-left, so the top HUD stays where it is and only the bottom block is
/// re-anchored to the bottom edge. That makes the layout idempotent: rotating or resizing recalculates
/// the same values instead of accumulating offsets.
/// </summary>
public static class ScreenLayout
{
    public const float RefWidth = 570f, RefHeight = 1018f;
    public const float MenuTop = 108f, BottomGap = 70f;
    public const float FarmHeight = 634f, ScrollTop = 244f;
    public const float MenuHeight = 840f;

    public static float Width => RefWidth;
    public static float Height { get; private set; } = RefHeight;
    public static float Extra => Mathf.Max(0f, Height - RefHeight);
    public static float PageHeight => Mathf.Max(MenuHeight, Height - MenuTop - BottomGap);
    public static float FarmViewHeight => FarmHeight + Extra;
    public static float FitScale { get; private set; } = 1f;

    static RectTransform root;
    static RectTransform backdrop;
    static readonly Dictionary<RectTransform, Vector2> basePos = new Dictionary<RectTransform, Vector2>();
    static readonly Dictionary<RectTransform, Vector2> baseSize = new Dictionary<RectTransform, Vector2>();

    static readonly string[] Tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };

    /// <summary>Computes the fit and lays out the fixed canvas pieces. Call before building the runtime UI.</summary>
    public static void Prepare(RectTransform content, Vector3 authoredScale, bool bake, Vector2? screen = null)
    {
        if (content == null) return;
        if (root != content)
        {
            root = content;
            backdrop = null;
            basePos.Clear();
            baseSize.Clear();
        }

        if (bake)
        {
            Height = RefHeight;
            FitScale = 1f;
        }
        else
        {
            Vector2 size = screen ?? new Vector2(Screen.width, Screen.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(size.x)), h = Mathf.Max(1, Mathf.RoundToInt(size.y));
            float byWidth = w / RefWidth, byHeight = h / RefHeight;
            // Phones are taller than the reference: fill the width and grow the content.
            // Wider screens (tablets) keep the reference height and are centred with side bars.
            bool tall = h * RefWidth >= w * RefHeight;
            FitScale = tall ? byWidth : byHeight;
            Height = tall ? Mathf.Max(RefHeight, h / Mathf.Max(.0001f, FitScale)) : RefHeight;
        }

        content.sizeDelta = new Vector2(RefWidth, Height);
        content.localScale = authoredScale * FitScale;
        // Baking keeps the authored reference layout so the prefab stays editable in Unity.
        if (bake) return;
        EnsureBackdrop(content);
        ApplyStatic();
    }

    /// <summary>Lays out the menu pages once the runtime UI exists (their lists depend on the height).</summary>
    public static void Fit(RectTransform content)
    {
        if (content == null || content != root) return;
        ApplyStatic(); // catches pieces created after Prepare (upgrade labels, badge, Pepito...)
        ApplyPages();
    }

    /// <summary>Full pass used when the screen size changes.</summary>
    public static void Apply(RectTransform content, Vector3 authoredScale)
    {
        Prepare(content, authoredScale, false);
        Fit(content);
    }

    // ---------- Fixed pieces ----------

    static void EnsureBackdrop(RectTransform content)
    {
        var parent = content.parent as RectTransform;
        if (parent == null) return;
        if (backdrop == null || backdrop.parent != parent)
        {
            backdrop = Child(parent, "Screen backdrop");
            if (backdrop == null)
            {
                var go = new GameObject("Screen backdrop", typeof(RectTransform), typeof(Image));
                backdrop = (RectTransform)go.transform;
                backdrop.SetParent(parent, false);
            }
        }
        backdrop.SetSiblingIndex(0);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.pivot = new Vector2(.5f, .5f);
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        var image = backdrop.GetComponent<Image>();
        if (image == null) image = backdrop.gameObject.AddComponent<Image>();
        image.color = new Color(.035f, .04f, .10f, 1f);
        image.raycastTarget = false;
    }

    static void ApplyStatic()
    {
        // Containers that used to be 1018 tall now follow the content.
        SetSize(Child(root, "HUD"), RefWidth, Height);
        SetSize(Child(root, "Modular chrome"), RefWidth, Height);
        SetSize(Child(root, "Navigation"), RefWidth, Height);
        SetSize(Child(root, "Upgrades"), RefWidth, Height);
        SetSize(Child(root, "Reward effects"), RefWidth, Height);
        SetSize(Child(root, "Pepito guide"), RefWidth, Height);
        SetSize(Child(root, "Start screens"), RefWidth, Height);

        // The farm grows down into the space left by the old harvest bar.
        var farm = Child(root, "Farm Viewport");
        SetSize(farm, RefWidth, FarmViewHeight);
        if (farm != null)
        {
            SetSize(Child(farm, "Scroll bounds"), RefWidth, FarmViewHeight - ScrollTop);
            var ambience = Child(farm, "Ambience");
            GrowHeight(ambience, Extra);
            if (ambience != null) GrowHeight(Child(ambience, "Sky tint"), Extra);
            var rain = Child(farm, "Rain");
            GrowHeight(rain, Extra);
            if (rain != null) GrowHeight(Child(rain, "Tint"), Extra);
        }

        // Bottom block: anchor to the bottom so it stays on the bottom edge.
        AnchorBottom(Child(root, "Harvest"));
        AnchorBottom(Child(root, "Pepito"));
        AnchorBottom(Child(root, "Achievements badge"));
        for (int i = 0; i < Tabs.Length; i++)
        {
            AnchorBottom(Child(root, "Navigation icon " + Tabs[i]));
            AnchorBottom(Child(root, "Navigation label " + Tabs[i]));
        }

        var chrome = Child(root, "Modular chrome");
        if (chrome != null)
        {
            AnchorBottom(Child(chrome, "Harvest bar"));
            for (int i = 0; i < 3; i++) AnchorBottom(Child(chrome, "Upgrade row " + i));
            for (int i = 0; i < 5; i++) AnchorBottom(Child(chrome, "Navigation tile " + i));
        }

        var navigation = Child(root, "Navigation");
        if (navigation != null)
            foreach (RectTransform child in navigation) AnchorBottom(child);

        // Upgrade labels/buttons live in their own full-screen rows.
        var upgrades = Child(root, "Upgrades");
        if (upgrades != null)
            foreach (RectTransform row in upgrades)
            {
                SetSize(row, RefWidth, Height);
                foreach (RectTransform child in row) AnchorBottom(child);
            }

        // Menu overlay: pages end just above the navigation.
        var secondary = Child(root, "Secondary screen");
        SetSize(secondary, RefWidth, PageHeight);
        if (secondary != null)
        {
            SetSize(Child(secondary, "Edge"), RefWidth - 6f, PageHeight - 6f);
            SetSize(Child(secondary, "Face"), RefWidth - 14f, PageHeight - 14f);
            SetSize(Child(secondary, "Top shine"), RefWidth - 18f, 3f);
        }
    }

    // ---------- Menu pages ----------

    static void ApplyPages()
    {
        var secondary = Child(root, "Secondary screen");
        if (secondary == null) return;
        foreach (RectTransform page in secondary)
        {
            if (!page.name.StartsWith("Page - ")) continue;
            ApplyPage(page);
        }
    }

    static void ApplyPage(RectTransform page)
    {
        if (page.name == "Page - Jardineria")
        {
            // The garden is a single painted room: scale it vertically so the tools stay on the
            // bottom shelf and the window/plant keep their places.
            SetSize(page, RefWidth, MenuHeight);
            page.localScale = new Vector3(1f, PageHeight / MenuHeight, 1f);
            return;
        }

        SetSize(page, RefWidth, PageHeight);

        if (page.name == "Page - Casa")
        {
            SetSize(Child(page, "Room"), RefWidth, MenuHeight);
            SetSize(Child(page, "Cats"), RefWidth, PageHeight);
            EnsureFloor(page);
            return;
        }

        SetSize(Child(page, "Background"), RefWidth, PageHeight);
        FitPlanks(page, PageHeight);

        if (page.name == "Page - Ajustes")
        {
            // Spread the settings sections proportionally instead of leaving a hole at the bottom.
            float f = PageHeight / MenuHeight;
            foreach (RectTransform child in page)
            {
                Remember(child);
                Vector2 b = basePos[child];
                child.anchoredPosition = new Vector2(b.x, b.y * f);
            }
            return;
        }

        foreach (RectTransform child in page)
        {
            if (child.name != "List viewport") continue;
            Remember(child);
            float top = -basePos[child].y;
            child.sizeDelta = new Vector2(baseSize[child].x, Mathf.Max(80f, PageHeight - top - 8f));
        }
    }

    /// <summary>Repeats the wooden planks down the taller pages (and hides the ones that don't fit).</summary>
    static void FitPlanks(RectTransform page, float pageHeight)
    {
        var planks = new List<RectTransform>();
        foreach (RectTransform child in page)
            if (child.name == "Plank") planks.Add(child);
        if (planks.Count == 0) return;

        int need = Mathf.CeilToInt(pageHeight / 60f);
        var template = planks[0];
        int last = planks[planks.Count - 1].GetSiblingIndex();
        while (planks.Count < need)
        {
            var clone = Object.Instantiate(template.gameObject, page);
            var rect = clone.GetComponent<RectTransform>();
            rect.name = "Plank";
            rect.SetSiblingIndex(++last);
            planks.Add(rect);
        }
        for (int i = 0; i < planks.Count; i++)
        {
            var plank = planks[i];
            Remember(plank);
            plank.anchoredPosition = new Vector2(0f, -i * 60f);
            plank.sizeDelta = new Vector2(RefWidth, 2f);
            plank.gameObject.SetActive(i * 60f < pageHeight);
        }
    }

    /// <summary>Plorp's house keeps its painted room and gets more wooden floor below it.</summary>
    static void EnsureFloor(RectTransform page)
    {
        float extra = Extra;
        var floor = Child(page, "Floor extension");
        if (floor == null)
        {
            if (extra <= .5f) return;
            var go = new GameObject("Floor extension", typeof(RectTransform), typeof(Image));
            floor = (RectTransform)go.transform;
            floor.SetParent(page, false);
            floor.SetSiblingIndex(1); // right above "Room"
        }
        floor.gameObject.SetActive(extra > .5f);
        if (extra <= .5f) return;

        floor.anchorMin = floor.anchorMax = floor.pivot = new Vector2(0f, 1f);
        floor.anchoredPosition = new Vector2(0f, -MenuHeight);
        floor.sizeDelta = new Vector2(RefWidth, extra);
        var image = floor.GetComponent<Image>();
        image.color = PixelUIFactory.Hex("#C8894D");
        image.raycastTarget = false;

        int lines = Mathf.FloorToInt(extra / 28f);
        for (int i = 0; i < lines; i++)
        {
            string name = "Floor line " + i;
            var line = Child(floor, name);
            if (line == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                line = (RectTransform)go.transform;
                line.SetParent(floor, false);
                var lineImage = line.gameObject.GetComponent<Image>();
                lineImage.color = PixelUIFactory.Hex("#A86A34");
                lineImage.raycastTarget = false;
            }
            line.anchorMin = line.anchorMax = line.pivot = new Vector2(0f, 1f);
            line.anchoredPosition = new Vector2(0f, -14f - i * 28f);
            line.sizeDelta = new Vector2(RefWidth, 2f);
            line.gameObject.SetActive(true);
        }
        // Hide lines left over from a previous, taller layout.
        for (int i = lines; i < floor.childCount; i++)
            if (floor.GetChild(i).name.StartsWith("Floor line ")) floor.GetChild(i).gameObject.SetActive(false);
    }

    // ---------- Pepito's tour ----------

    /// <summary>Moves a tour highlight from the 570x1018 reference to the current layout.</summary>
    public static Rect GuideArea(int section, Rect area)
    {
        if (area.width <= 0f) return area;
        float extra = Extra;
        switch (section)
        {
            case 0: // Granja
                if (area.y >= 630f) return new Rect(area.x, area.y + extra, area.width, area.height);
                if (area.y >= 240f && area.height > 300f)
                    return new Rect(area.x, area.y, area.width, area.height + extra);
                return area;
            case 1: // Tienda
                if (area.y >= 270f && area.height > 500f)
                    return new Rect(area.x, area.y, area.width, area.height + extra);
                return area;
            case 2: // Logros
                if (area.y >= 460f && area.height > 400f)
                    return new Rect(area.x, area.y, area.width, area.height + extra);
                if (area.y >= 940f) return new Rect(area.x, area.y + extra, area.width, area.height);
                return area;
            case 3: // Jardinería (page scaled vertically)
            case 4: // Ajustes (sections spread)
            {
                float f = PageHeight / MenuHeight;
                return new Rect(area.x, MenuTop + (area.y - MenuTop) * f, area.width, area.height * f);
            }
            case 5: // Casa (cats have a taller floor)
                if (area.y >= 630f) return new Rect(area.x, area.y, area.width, area.height + extra);
                return area;
            case 6: // Cocina
                if (area.y >= 430f && area.height > 400f)
                    return new Rect(area.x, area.y, area.width, area.height + extra);
                return area;
        }
        return area;
    }

    /// <summary>Keeps an element at the bottom edge (used by runtime-created bottom pieces).</summary>
    public static void PinBottom(RectTransform rect) => AnchorBottom(rect);

    // ---------- Helpers ----------

    static RectTransform Child(Transform parent, string name)
    {
        if (parent == null) return null;
        return parent.Find(name) as RectTransform;
    }

    static void Remember(RectTransform rect)
    {
        if (rect == null || basePos.ContainsKey(rect)) return;
        basePos[rect] = rect.anchoredPosition;
        baseSize[rect] = rect.sizeDelta;
    }

    static void SetSize(RectTransform rect, float width, float height)
    {
        if (rect == null) return;
        Remember(rect);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Keeps the authored size and adds the extra screen height below it (farm overlays).</summary>
    static void GrowHeight(RectTransform rect, float extra)
    {
        if (rect == null) return;
        Remember(rect);
        rect.sizeDelta = new Vector2(baseSize[rect].x, baseSize[rect].y + extra);
    }

    /// <summary>
    /// Turns a top-anchored element into a bottom-anchored one, keeping its distance to the bottom of
    /// the 1018 reference. Safe to call again: already anchored elements are left alone.
    /// </summary>
    static void AnchorBottom(RectTransform rect)
    {
        if (rect == null) return;
        Remember(rect);
        if (rect.anchorMin.y == 0f && rect.anchorMax.y == 0f) return;
        float down = -rect.anchoredPosition.y;
        float height = rect.sizeDelta.y;
        rect.anchorMin = new Vector2(rect.anchorMin.x, 0f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0f);
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
            RefHeight - down - (1f - rect.pivot.y) * height);
    }
}
