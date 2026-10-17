using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays the interactive farm (crop cells, animals, workers) on top of the painted farm
/// (Resources/World/farm_background.png, 570x530): the four soil fields, the chicken yard and the
/// sheep/cow pasture. All coordinates are pixels of that painting, measured from its top-left corner.
/// The older modular terrain, buildings and fences in the prefab are hidden, because the painting has them.
/// Around the farm there is a cloud-covered map with locked zones for future areas.
/// </summary>
public static class FarmArtLayout
{
    public const float Width = 570f, Height = 530f;
    /// <summary>The painting is shown bigger than the screen so the player pans around the farm.</summary>
    public const float Zoom = 1.4f;
    /// <summary>The farm view now reaches down to the upgrade rows (the old harvest bar is gone).</summary>
    public const float ViewportHeight = 634f;
    static readonly Vector2 StartView = new Vector2(-56f, 25f); // farm offset at start: chicken yard just below the header
    public const int Rows = 4, Columns = 4;
    public const int CellsPerPlot = Rows * Columns;
    const float CropSize = 30f;
    const float Inset = .14f; // keep crops off the field's wooden/grass rim

    /// <summary>Diamond-shaped soil fields: top, right, bottom, left corners. Index = plot (carrot, tomato, pumpkin, cabbage).</summary>
    static readonly Vector2[][] Fields =
    {
        new[] { new Vector2(156, 185), new Vector2(247, 240), new Vector2(135, 300), new Vector2(41, 243) },
        new[] { new Vector2(292, 250), new Vector2(407, 320), new Vector2(294, 380), new Vector2(181, 309) },
        new[] { new Vector2(154, 325), new Vector2(248, 388), new Vector2(137, 448), new Vector2(45, 385) },
        new[] { new Vector2(299, 397), new Vector2(409, 469), new Vector2(295, 529), new Vector2(188, 460) },
    };

    // Chicken yard (top middle) and pasture (right), as rectangles in painting pixels.
    static readonly Rect ChickenYard = new Rect(190, 125, 180, 55);
    static readonly Rect Pasture = new Rect(415, 252, 145, 80);

    static readonly Vector2[] ChickenSpots =
    {
        new Vector2(.30f, .45f), new Vector2(.55f, .30f), new Vector2(.75f, .60f),
        new Vector2(.15f, .80f), new Vector2(.45f, .85f), new Vector2(.90f, .25f),
    };
    static readonly Vector2[] SheepSpots =
    {
        new Vector2(.35f, .20f), new Vector2(.70f, .35f), new Vector2(.20f, .55f),
        new Vector2(.55f, .65f), new Vector2(.85f, .70f), new Vector2(.40f, .90f),
    };
    static readonly Vector2[] CowSpots =
    {
        new Vector2(.10f, .25f), new Vector2(.60f, .10f), new Vector2(.90f, .45f),
        new Vector2(.05f, .85f), new Vector2(.70f, .90f), new Vector2(.30f, .40f),
    };
    // Workers stand on the dirt paths between the fields.
    static readonly Vector2[] WorkerSpots =
    {
        new Vector2(60, 205), new Vector2(165, 330), new Vector2(395, 265), new Vector2(200, 435),
    };

    /// <summary>Called when the player taps a padlock in the clouds (set by FarmUIBootstrap).</summary>
    public static System.Action LockedZoneTapped;

    // The farm sits in the middle of a bigger map; everything around it is covered by clouds
    // with padlocks ("Más adelante...") for areas that will open later.
    public const float MarginX = 200f, MarginY = 170f;
    public const float MapWidth = Width + 2 * MarginX, MapHeight = Height + 2 * MarginY;
    /// <summary>Grass/trees/fence border drawn around the painting in farm_extended.png.</summary>
    public const float Border = 56f;
    const float CloudBand = MarginX - Border; // cloud thickness outside the fence

    static readonly Vector2[] LockedZones =
    {
        new Vector2(CloudBand / 2f, MarginY + Height / 2f),                                   // west
        new Vector2(MarginX + Width + Border + CloudBand / 2f, MarginY + Height / 2f),        // east
        new Vector2(MarginX + Width / 2f, (MarginY - Border) / 2f + 12f),                     // north
        new Vector2(MarginX + Width / 2f, MarginY + Height + Border + (MarginY - Border) / 2f), // south
    };

    public static void Apply(RectTransform world)
    {
        var ui = new PixelUIFactory();
        var map = EnsureMap(world, ui);

        world.sizeDelta = new Vector2(Width, Height);
        world.localScale = Vector3.one;
        world.anchoredPosition = new Vector2(MarginX, -MarginY);

        map.localScale = new Vector3(Zoom, Zoom, 1f);
        map.anchoredPosition = new Vector2(StartView.x - MarginX * Zoom, StartView.y + MarginY * Zoom);

        var viewport = map.parent as RectTransform;
        if (viewport != null)
        {
            viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, ViewportHeight);
            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
        }
        var scroll = viewport != null ? viewport.GetComponent<ScrollRect>() : null;
        if (scroll != null)
        {
            // Drag to walk around the map in any direction; it stops at the clouds' outer edge.
            scroll.content = map;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = .12f;
        }

        foreach (string hidden in new[] { "Terrain", "Buildings" })
        {
            var t = world.Find(hidden);
            if (t != null) t.gameObject.SetActive(false);
        }

        // farm_extended.png = the painting plus a border of grass, trees and a fence all around,
        // so nothing looks cut at the edges. It sticks out Border px on every side of the painting.
        var old = world.Find("Painted farm");
        if (old != null) old.name = "Painted farm (old)"; // keep the name free for the new one
        if (old != null) old.gameObject.SetActive(false);
        var art = ui.Texture(world, "Painted farm", "World/farm_extended", -Border, -Border,
            Width + 2 * Border, Height + 2 * Border);
        art.raycastTarget = true; // so dragging on the grass pans the farm
        art.transform.SetAsFirstSibling();

        LayoutPlots(world.Find("Crop Plots"));
        LayoutPens(world.Find("Animal Pens"));
        for (int i = 0; i < WorkerSpots.Length; i++)
            Place(world.Find("worker-" + (i + 1)) as RectTransform, WorkerSpots[i], new Vector2(30, 37));
    }

    /// <summary>
    /// Wraps the farm world in a bigger "Farm map": sky-colored base, the farm in the middle,
    /// a cloud frame over the edges and padlock signs in the clouds. Safe to call twice.
    /// </summary>
    static RectTransform EnsureMap(RectTransform world, PixelUIFactory ui)
    {
        var existing = world.parent as RectTransform;
        if (existing != null && existing.name == "Farm map") return existing;

        var viewport = world.parent;
        var map = ui.Rect(viewport, "Farm map", 0, 0, MapWidth, MapHeight);
        map.SetSiblingIndex(world.GetSiblingIndex());
        var sky = ui.Block(map, "Sky", 0, 0, MapWidth, MapHeight, PixelUIFactory.Hex("#E6EDF9"));
        sky.raycastTarget = true; // drag anywhere to pan
        world.SetParent(map, false);

        // Not a raycast target: the cloud image covers the whole map (its hole is only
        // transparent), so it would swallow every tap on the farm. The sky below handles drags.
        var clouds = ui.Texture(map, "Clouds", "World/clouds_frame", 0, 0, MapWidth, MapHeight);
        clouds.raycastTarget = false;

        foreach (var zone in LockedZones) LockSign(map, ui, zone);
        return map;
    }

    static void LockSign(Transform map, PixelUIFactory ui, Vector2 center)
    {
        const float w = 132f, h = 48f;
        var sign = ui.Panel(map, "Locked zone", center.x - w / 2f, center.y - h / 2f, w, h,
            PixelUIFactory.Hex("#CE8854"));
        ui.Texture(sign, "Padlock", "UI/Icons/candado", 8, 8, 32, 32);
        ui.Paragraph(sign, "Text", "Más adelante...", 42, 6, w - 48, h - 12, 13, Color.white);
        ui.Hit(sign, "Tap", 0, 0, w, h, () => LockedZoneTapped?.Invoke());
    }

    /// <summary>Where a field's golden ticket points: the field center, in the plot's local space.</summary>
    public static Vector2 PlotTicketPoint(int plot)
    {
        var field = Fields[Mathf.Clamp(plot, 0, Fields.Length - 1)];
        float minX = Mathf.Min(field[3].x, field[0].x), minY = field[0].y;
        Vector2 center = (field[0] + field[1] + field[2] + field[3]) / 4f;
        return new Vector2(center.x - minX, -(center.y - minY) + 6f);
    }

    static void LayoutPlots(Transform root)
    {
        if (root == null) return;
        var plots = root.GetComponentsInChildren<CropPlot>(true);
        for (int p = 0; p < plots.Length && p < Fields.Length; p++)
        {
            var plot = (RectTransform)plots[p].transform;
            var field = Fields[p];
            float minX = Mathf.Min(field[3].x, field[0].x), minY = field[0].y;
            float maxX = field[1].x, maxY = field[2].y;
            plot.anchoredPosition = new Vector2(minX, -minY);
            plot.sizeDelta = new Vector2(maxX - minX, maxY - minY);
            // The soil is painted in the background now.
            var soil = plot.GetComponent<RawImage>();
            if (soil != null) soil.enabled = false;

            EnsureCells(plot, p);
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    var cell = plot.Find("Crop " + p + "." + (row * Columns + col)) as RectTransform;
                    if (cell == null) continue;
                    // u runs top→right, v runs top→left across the diamond.
                    float u = Inset + (1f - 2f * Inset) * (col + .5f) / Columns;
                    float v = Inset + (1f - 2f * Inset) * (row + .5f) / Rows;
                    Vector2 point = field[0] + u * (field[1] - field[0]) + v * (field[3] - field[0]);
                    // Sprite bottom-center sits on the planting point.
                    cell.anchoredPosition = new Vector2(point.x - minX - CropSize / 2f, -(point.y - minY - CropSize + 3f));
                    SetSize(cell, new Vector2(CropSize, CropSize));
                }
            }
            SortByDepth(plot);
        }
    }

    /// <summary>The prefab authors 12 cells per plot; clone the first one up to 16.</summary>
    static void EnsureCells(Transform plot, int plotIndex)
    {
        var template = plot.Find("Crop " + plotIndex + ".0");
        if (template == null) return;
        for (int i = 0; i < CellsPerPlot; i++)
        {
            string name = "Crop " + plotIndex + "." + i;
            if (plot.Find(name) != null) continue;
            var clone = Object.Instantiate(template.gameObject, plot);
            clone.name = name;
        }
    }

    static void LayoutPens(Transform pens)
    {
        if (pens == null) return;
        var chickens = pens.Find("Chicken Pen") as RectTransform;
        var pasture = pens.Find("Pasture Pen") as RectTransform;
        SetupPen(chickens, ChickenYard);
        SetupPen(pasture, Pasture);
        for (int i = 0; i < 6; i++)
        {
            int n = i + 1;
            PlaceInPen(chickens, "chicken-" + n, ChickenYard, ChickenSpots[i], new Vector2(24, 24));
            PlaceInPen(pasture, "sheep-" + n, Pasture, SheepSpots[i], new Vector2(32, 29));
            PlaceInPen(pasture, "cow-" + n, Pasture, CowSpots[i], new Vector2(36, 33));
        }
    }

    static void SetupPen(RectTransform pen, Rect area)
    {
        if (pen == null) return;
        pen.anchoredPosition = new Vector2(area.x, -area.y);
        pen.sizeDelta = area.size;
        var fence = pen.Find("Fence");
        if (fence != null) fence.gameObject.SetActive(false);
    }

    static void PlaceInPen(RectTransform pen, string id, Rect area, Vector2 spot, Vector2 size)
    {
        if (pen == null) return;
        var actor = pen.Find(id) as RectTransform;
        if (actor == null) return;
        // Spot is the animal's feet, as a fraction of the pen.
        Vector2 feet = new Vector2(spot.x * area.width, spot.y * area.height);
        actor.anchoredPosition = new Vector2(feet.x - size.x / 2f, -(feet.y - size.y));
        SetSize(actor, size);
    }

    static void Place(RectTransform actor, Vector2 feet, Vector2 size)
    {
        if (actor == null) return;
        actor.anchoredPosition = new Vector2(feet.x - size.x / 2f, -(feet.y - size.y));
        SetSize(actor, size);
    }

    static void SetSize(RectTransform rect, Vector2 size)
    {
        rect.sizeDelta = size;
        var art = rect.Find("Sprite art") as RectTransform;
        if (art != null)
        {
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = size;
        }
    }

    /// <summary>Back-to-front drawing order so lower crops overlap the ones behind them.</summary>
    static void SortByDepth(Transform parent)
    {
        var children = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in parent)
            if (child is RectTransform rt && child.GetComponent<CropCell>() != null) children.Add(rt);
        children.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        foreach (var child in children) child.SetAsLastSibling();
    }
}
