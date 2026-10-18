using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays the interactive farm (crop cells, animals, workers) on top of the farm design
/// (Art/Farm/Map/farm_design.png, 1024x765): five soil fields, the chicken yard and the
/// sheep/cow pasture. All coordinates are pixels of that image, measured from its top-left corner.
/// The design already fades into clouds at its edges; more clouds surround it on a bigger map,
/// with locked zones for future areas. The old modular terrain, buildings and fences are hidden.
/// </summary>
public static class FarmArtLayout
{
    public const float Width = 1024f, Height = 765f;
    /// <summary>The farm is shown bigger than the screen so the player pans around it.</summary>
    public const float Zoom = 1.15f;
    /// <summary>The farm view reaches down to the upgrade rows (the old harvest bar is gone).</summary>
    public const float ViewportHeight = 634f;
    /// <summary>Top of the farm view covered by the level/coins/Plorp panels: the map may scroll this far
    /// past the top edge so what is up there (the hives) can be seen below them.</summary>
    public const float CoveredTop = 244f;
    /// <summary>Farm offset at start (screen px): chicken yard and the first field below the header.</summary>
    static readonly Vector2 StartView = new Vector2(-200f, 110f);
    public const int Rows = 4, Columns = 4;
    public const int CellsPerPlot = Rows * Columns;
    const float CropSize = 34f;
    const float Inset = .14f; // keep crops off the field's grass rim

    /// <summary>Soil fields: top, right, bottom, left corners. Index = plot, in unlock order.</summary>
    public static readonly Vector2[][] Fields =
    {
        new[] { new Vector2(351, 258), new Vector2(464, 326), new Vector2(325, 399), new Vector2(214, 329) }, // top middle
        new[] { new Vector2(521, 341), new Vector2(659, 425), new Vector2(524, 498), new Vector2(388, 413) }, // right
        new[] { new Vector2(175, 345), new Vector2(293, 423), new Vector2(191, 480), new Vector2(74, 403) },  // left
        new[] { new Vector2(351, 435), new Vector2(465, 509), new Vector2(332, 582), new Vector2(220, 507) }, // center
        new[] { new Vector2(528, 522), new Vector2(661, 609), new Vector2(525, 678), new Vector2(398, 598) }, // bottom right
    };

    // Chicken yard (in front of the shed) and pasture (right, by the barns).
    static readonly Rect ChickenYard = new Rect(410, 200, 210, 62);
    static readonly Rect Pasture = new Rect(690, 340, 190, 105);

    static readonly Vector2[] ChickenSpots =
    {
        new Vector2(.30f, .45f), new Vector2(.55f, .30f), new Vector2(.75f, .60f),
        new Vector2(.15f, .80f), new Vector2(.45f, .85f), new Vector2(.90f, .35f),
    };
    static readonly Vector2[] SheepSpots =
    {
        new Vector2(.30f, .25f), new Vector2(.62f, .35f), new Vector2(.20f, .60f),
        new Vector2(.50f, .65f), new Vector2(.80f, .70f), new Vector2(.40f, .92f),
    };
    static readonly Vector2[] CowSpots =
    {
        new Vector2(.10f, .30f), new Vector2(.48f, .12f), new Vector2(.85f, .45f),
        new Vector2(.08f, .88f), new Vector2(.68f, .95f), new Vector2(.30f, .45f),
    };
    // Workers stand on the dirt paths between the fields.
    static readonly Vector2[] WorkerSpots =
    {
        new Vector2(560, 322), new Vector2(375, 422), new Vector2(195, 497), new Vector2(150, 640),
    };

    /// <summary>
    /// Empty object saved in the canvas by the bake: from then on the farm keeps the positions edited in
    /// Unity (fields, crops, animals, workers, map zoom and start view) instead of the numbers in this file.
    /// </summary>
    public const string AuthoredMarker = "Layout from canvas";

    // The farm sits in the middle of a bigger map; everything around it is covered by clouds
    // with padlocks ("Más adelante...") for areas that will open later.
    public const float MarginX = 150f, MarginY = 150f;
    public const float MapWidth = Width + 2 * MarginX, MapHeight = Height + 2 * MarginY;

    static readonly Vector2[] LockedZones =
    {
        new Vector2(MarginX / 2f + 10f, MarginY + Height / 2f),          // west
        new Vector2(MarginX + Width + MarginX / 2f - 10f, MarginY + Height / 2f), // east
        new Vector2(MarginX + Width / 2f, MarginY / 2f + 20f),           // north
        new Vector2(MarginX + Width / 2f, MarginY + Height + MarginY / 2f - 10f), // south
    };

    public static void Apply(RectTransform world)
    {
        var ui = new PixelUIFactory();
        var map = EnsureMap(world, ui);
        var viewport = map.parent as RectTransform;
        bool authored = world.Find(AuthoredMarker) != null;

        if (!authored)
        {
            // First time only: place everything from the numbers above. After the bake, the canvas rules.
            world.sizeDelta = new Vector2(Width, Height);
            world.localScale = Vector3.one;
            world.anchoredPosition = new Vector2(MarginX, -MarginY);
            map.localScale = new Vector3(Zoom, Zoom, 1f);
            map.anchoredPosition = new Vector2(StartView.x - MarginX * Zoom, StartView.y + MarginY * Zoom);
            if (viewport != null) viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, ViewportHeight);
            LayoutPlots(world.Find("Crop Plots"));
            LayoutPens(world.Find("Animal Pens"));
            for (int i = 0; i < WorkerSpots.Length; i++)
                Place(world.Find("worker-" + (i + 1)) as RectTransform, WorkerSpots[i], new Vector2(36, 44));
            ui.Rect(world, AuthoredMarker, 0, 0, 0, 0);
        }

        if (viewport != null && viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
            viewport.gameObject.GetOrAdd<RectMask2D>();
        var scroll = viewport != null ? viewport.GetComponent<ScrollRect>() : null;
        if (scroll != null)
        {
            // Drag to walk around the map in any direction; it stops at the clouds' outer edge.
            scroll.content = map;
            // The scroll limits use a rect that starts under the HUD; the mask (drawing) stays full size.
            var bounds = ui.Rect(viewport, "Scroll bounds", 0, CoveredTop, viewport.rect.width, ViewportHeight - CoveredTop);
            if (!ui.Reused) bounds.sizeDelta = new Vector2(viewport.rect.width, ViewportHeight - CoveredTop);
            scroll.viewport = bounds;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = .12f;
        }

        // Pieces of the first modular farm, replaced by the painted design (the bake removes them).
        foreach (string hidden in new[] { "Terrain", "Buildings", "Painted farm (old)" })
        {
            var t = world.Find(hidden);
            if (t != null) t.gameObject.SetActive(false);
        }

        // The painted farm (Art/Farm/Map/farm_design.png). Change the picture in the canvas or replace the file.
        var art = ui.Texture(world, "Painted farm", "farm_design", 0, 0, Width, Height);
        if (art.texture != null && art.texture.name == "farm_background") art.texture = Art.Tex("farm_design");
        art.raycastTarget = true; // so dragging on the grass pans the farm
        art.transform.SetAsFirstSibling();
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
        var clouds = ui.Texture(map, "Clouds", "clouds_frame", 0, 0, MapWidth, MapHeight);
        clouds.raycastTarget = false;

        foreach (var zone in LockedZones) LockSign(map, ui, zone);
        return map;
    }

    static void LockSign(Transform map, PixelUIFactory ui, Vector2 center)
    {
        const float w = 132f, h = 48f;
        var sign = ui.Panel(map, "Locked zone", center.x - w / 2f, center.y - h / 2f, w, h,
            PixelUIFactory.Hex("#CE8854"));
        ui.Texture(sign, "Padlock", "candado", 8, 8, 32, 32);
        ui.Paragraph(sign, "Text", "Más adelante...", 42, 6, w - 48, h - 12, 13, Color.white);
        ui.Hit(sign, "Tap", 0, 0, w, h, null); // FarmZones gives every sign its action
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
        EnsurePlots(root);
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

    /// <summary>The prefab has 4 plots; the design has 5 fields, so the last plot is cloned.</summary>
    static void EnsurePlots(Transform root)
    {
        var plots = root.GetComponentsInChildren<CropPlot>(true);
        if (plots.Length == 0) return;
        for (int p = plots.Length; p < Fields.Length; p++)
        {
            var template = plots[plots.Length - 1].gameObject;
            int from = plots.Length - 1;
            var clone = Object.Instantiate(template, root);
            clone.name = "Plot " + p;
            foreach (Transform cell in clone.transform)
                if (cell.name.StartsWith("Crop " + from + "."))
                    cell.name = "Crop " + p + "." + cell.name.Substring(("Crop " + from + ".").Length);
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
            PlaceInPen(chickens, "chicken-" + n, ChickenYard, ChickenSpots[i], new Vector2(28, 28));
            PlaceInPen(pasture, "sheep-" + n, Pasture, SheepSpots[i], new Vector2(38, 34));
            PlaceInPen(pasture, "cow-" + n, Pasture, CowSpots[i], new Vector2(42, 38));
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
