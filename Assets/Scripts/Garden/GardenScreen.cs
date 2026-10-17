using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Jardinería" tab (replaces the old Mejoras page): a plant on the window sill to look after.
/// Bars show water, light and growth with the plant's comfort zone; tools at the bottom
/// (regadera, tijeras, abono, lámpara); tap dusty leaves to clean them; tap the sign to buy
/// or switch seeds. Built at runtime on top of Resources/UI/Garden/garden_bg.png (570x840).
/// </summary>
public sealed class GardenScreen : MonoBehaviour
{
    const float PageW = 570f, PageH = 840f;
    static readonly Vector2 PotTop = new Vector2(283f, 484f);
    // Leaf slots: angle (deg, + = right) and length. Slots unlock 0,1 first (centre), then outwards.
    static readonly Vector2[] Slots =
    {
        new Vector2(-14f, 1.0f), new Vector2(15f, 1.05f),
        new Vector2(-38f, .92f), new Vector2(40f, .92f),
        new Vector2(-62f, .8f), new Vector2(64f, .8f),
    };
    static readonly float[] CardX = { 37f, 165f, 294f, 423f };
    const float CardY = 679f, CardW = 110f, CardH = 153f;

    static readonly Color Warn = PixelUIFactory.Hex("#FFB347");
    static readonly Color Good = PixelUIFactory.Hex("#B8F28A");

    FarmGameState state;
    GardenState garden;
    Action<string> message;
    PixelUIFactory ui;

    Text signText;
    RectTransform waterFill, lightFill, xpFill, waterBand, lightBand;
    Text waterValue, lightValue, levelValue;
    Text statusText, bonusText;
    readonly RawImage[] leaves = new RawImage[GardenState.LeafSlots];
    readonly float[] wiggle = new float[GardenState.LeafSlots];
    RectTransform plantRoot;
    RawImage flower;
    RectTransform flowerStem;
    readonly Text[] costs = new Text[4];
    readonly Text[] toolLevels = new Text[4];
    readonly RectTransform[] cards = new RectTransform[4];
    RectTransform shop;
    readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    const float BarX = 132f, BarW = 236f;

    public static GardenScreen Create(Transform secondary, FarmGameState state, Action<string> message)
    {
        var ui = new PixelUIFactory();
        var page = ui.Rect(secondary, "Page - Jardineria", 0, 0, PageW, PageH);
        var screen = page.gameObject.AddComponent<GardenScreen>();
        screen.Build(ui, state, message);
        page.gameObject.SetActive(false);
        return screen;
    }

    void Build(PixelUIFactory factory, FarmGameState farm, Action<string> showMessage)
    {
        ui = factory;
        state = farm;
        garden = farm.Garden;
        message = showMessage;

        var bg = ui.Texture(transform, "Background", "UI/Garden/garden_bg", 0, 0, PageW, PageH);
        bg.raycastTarget = true;

        // Sign with the species name: tap to open the seed shop.
        signText = ui.Label(transform, "Species", "", 128, 120, 312, 40, 24, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Open seeds", 110, 112, 352, 56, OpenShop);
        ui.Label(transform, "Seeds hint", "toca el cartel para cambiar de semilla", 110, 168, 352, 16, 11,
            new Color(1f, 1f, 1f, .9f), TextAnchor.MiddleCenter);

        // Water / light / growth bars over the window.
        ui.Block(transform, "Bars shade", 96, 196, 380, 84, new Color(.04f, .06f, .1f, .55f));
        BuildBar(208, "UI/Garden/icon_water", PixelUIFactory.Hex("#5AB0F0"), out waterFill, out waterBand, out waterValue);
        BuildBar(234, "UI/Garden/icon_sun", PixelUIFactory.Hex("#FFD44F"), out lightFill, out lightBand, out lightValue);
        BuildBar(260, "UI/Garden/icon_sprout", PixelUIFactory.Hex("#7ED957"), out xpFill, out _, out levelValue);

        // The plant: an optional sunflower head, then the leaves (centre ones behind).
        plantRoot = ui.Rect(transform, "Plant", 0, 0, PageW, PageH);
        flowerStem = ui.Block(plantRoot, "Flower stem", PotTop.x - 2, PotTop.y - 150, 5, 150, PixelUIFactory.Hex("#3E8A3A")).rectTransform;
        flower = ui.Texture(plantRoot, "Flower", "UI/Garden/sunflower_head", PotTop.x - 24, PotTop.y - 186, 48, 48);
        for (int i = 0; i < leaves.Length; i++)
        {
            int index = i;
            var leaf = ui.Texture(plantRoot, "Leaf " + i, "UI/Garden/leaf_especial_healthy", 0, 0, 64, 160);
            var rt = leaf.rectTransform;
            rt.pivot = new Vector2(.5f, 0f);
            rt.anchoredPosition = new Vector2(PotTop.x, -PotTop.y);
            leaf.raycastTarget = true;
            var button = leaf.gameObject.AddComponent<Button>();
            button.targetGraphic = leaf;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => TapLeaf(index));
            leaves[i] = leaf;
        }

        // Status and bonus on the table.
        ui.Block(transform, "Status shade", 30, 612, 510, 58, new Color(.15f, .07f, .03f, .6f));
        statusText = ui.Paragraph(transform, "Status", "", 40, 614, 490, 28, 16, Color.white, TextAnchor.MiddleCenter);
        bonusText = ui.Paragraph(transform, "Bonus", "", 40, 641, 490, 26, 13, Good, TextAnchor.MiddleCenter);

        // Tool cards (icons are painted in the background).
        for (int i = 0; i < 4; i++)
        {
            int tool = i;
            float x = CardX[i];
            cards[i] = ui.Rect(transform, "Tool " + i, x, CardY, CardW, CardH);
            ui.Texture(transform, "Coin " + i, "UI/Garden/icon_coin", x + 12, 774, 20, 20);
            costs[i] = ui.Label(transform, "Cost " + i, "", x + 34, 771, CardW - 38, 26, 17, PixelUIFactory.Gold);
            toolLevels[i] = ui.Label(transform, "Tool level " + i, "", x, 799, CardW, 24, 15, Color.white, TextAnchor.MiddleCenter);
            ui.Hit(transform, "Use tool " + i, x, CardY, CardW, CardH, () => UseTool(tool));
        }

        garden.Changed += Refresh;
        garden.LeveledUp += OnLevelUp;
        garden.LeafLost += OnLeafLost;
        Refresh();
    }

    void BuildBar(float y, string icon, Color color, out RectTransform fill, out RectTransform band, out Text value)
    {
        ui.Texture(transform, "Icon", icon, 104, y - 2, 22, 22);
        ui.Block(transform, "Bar back", BarX, y + 3, BarW, 14, PixelUIFactory.Hex("#2A1A14"));
        band = ui.Block(transform, "Comfort zone", BarX, y + 1, 0, 18, new Color(.5f, 1f, .4f, .35f)).rectTransform;
        fill = ui.Block(transform, "Bar fill", BarX, y + 5, 0, 10, color).rectTransform;
        value = ui.Label(transform, "Value", "", BarX + BarW + 8, y - 2, 90, 22, 14, Color.white);
    }

    static void SetBar(RectTransform fill, float fraction)
    {
        fill.sizeDelta = new Vector2(BarW * Mathf.Clamp01(fraction), fill.sizeDelta.y);
    }

    static void SetBand(RectTransform band, float min, float max)
    {
        band.anchoredPosition = new Vector2(BarX + BarW * min / 100f, band.anchoredPosition.y);
        band.sizeDelta = new Vector2(BarW * (max - min) / 100f, band.sizeDelta.y);
    }

    void Refresh()
    {
        if (garden == null || !gameObject.activeInHierarchy) return;
        var plant = garden.Plant;
        signText.text = plant.Name.ToUpperInvariant();

        SetBar(waterFill, garden.Water / 100f);
        SetBar(lightFill, garden.Light / 100f);
        SetBar(xpFill, garden.XpProgress);
        SetBand(waterBand, plant.WaterMin, plant.WaterMax);
        SetBand(lightBand, plant.LightMin, plant.LightMax);
        waterValue.text = Mathf.RoundToInt(garden.Water) + "%";
        waterValue.color = garden.TooMuchWater || garden.TooLittleWater ? Warn : Color.white;
        lightValue.text = Mathf.RoundToInt(garden.Light) + "%";
        lightValue.color = garden.TooMuchLight || garden.TooLittleLight ? Warn : Color.white;
        levelValue.text = "Nv." + garden.Level;

        statusText.text = garden.StatusText;
        statusText.color = garden.Comfortable ? Color.white : Warn;
        bonusText.text = garden.BonusSummary;

        for (int i = 0; i < 4; i++)
        {
            costs[i].text = FarmGameState.Format(garden.ToolCost(i));
            costs[i].color = state.Gold >= garden.ToolCost(i) ? PixelUIFactory.Gold : Warn;
            toolLevels[i].text = i == 2 && garden.Fertilized
                ? "x2 · " + Mathf.CeilToInt((float)(garden.FertilizerUntil - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0)) + "s"
                : "Niv. " + garden.ToolLevel(i);
        }

        bool sunflower = plant.Shape == "girasol" && garden.Level >= 2;
        flower.gameObject.SetActive(sunflower);
        flowerStem.gameObject.SetActive(sunflower);

        for (int i = 0; i < leaves.Length; i++)
        {
            var leafState = garden.Leaves[i];
            bool show = i < garden.VisibleLeaves && leafState != LeafState.Empty;
            leaves[i].gameObject.SetActive(show);
            if (show) leaves[i].texture = LeafTexture(plant.Shape, leafState);
        }
    }

    Texture2D LeafTexture(string shape, LeafState leafState)
    {
        string suffix;
        switch (leafState)
        {
            case LeafState.Strong: suffix = "strong"; break;
            case LeafState.Dusty: suffix = "dusty"; break;
            case LeafState.Withered: suffix = "withered"; break;
            case LeafState.Burnt: suffix = "burnt"; break;
            default: suffix = "healthy"; break;
        }
        string path = "UI/Garden/leaf_" + shape + "_" + suffix;
        if (!textures.TryGetValue(path, out var tex))
        {
            tex = Resources.Load<Texture2D>(path);
            if (tex != null) tex.filterMode = FilterMode.Point;
            textures[path] = tex;
        }
        return tex;
    }

    void Update()
    {
        if (garden == null) return;
        float t = Time.unscaledTime;
        float grow = .86f + .035f * Mathf.Min(garden.Level, 8);
        for (int i = 0; i < leaves.Length; i++)
        {
            if (!leaves[i].gameObject.activeSelf) continue;
            var leafState = garden.Leaves[i];
            bool dry = leafState == LeafState.Withered || leafState == LeafState.Burnt;
            float angle = Slots[i].x + (dry ? Mathf.Sign(Slots[i].x == 0 ? 1 : Slots[i].x) * 22f : 0f);
            wiggle[i] = Mathf.MoveTowards(wiggle[i], 0f, Time.unscaledDeltaTime * 2f);
            float sway = Mathf.Sin(t * 1.3f + i * 1.7f) * 1.6f + Mathf.Sin(t * 22f) * 9f * wiggle[i];
            leaves[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -(angle + sway));
            float length = Slots[i].y * grow * (dry ? .85f : 1f);
            leaves[i].rectTransform.localScale = new Vector3(grow, length, 1f);
        }
    }

    void TapLeaf(int index)
    {
        wiggle[index] = 1f;
        if (garden.CleanLeaf(index))
        {
            Sparkle(leaves[index].rectTransform.TransformPoint(new Vector3(0f, 90f, 0f)), Color.white);
            message("Hoja limpia: ¡más fuerte!");
        }
        else if (garden.Leaves[index] == LeafState.Withered || garden.Leaves[index] == LeafState.Burnt)
        {
            message("Hoja seca: córtala con las tijeras");
        }
    }

    void UseTool(int tool)
    {
        message(garden.UseTool(tool));
        Sparkle(cards[tool].TransformPoint(new Vector3(CardW / 2f, -CardH / 2f, 0f)), PixelUIFactory.Gold);
    }

    void OnLevelUp(int level)
    {
        message("¡" + garden.Plant.Name + " Nv." + level + "! +" + (1 + level / 3) + " gemas");
        Sparkle(plantRoot.TransformPoint(new Vector3(PotTop.x, -PotTop.y + 120f, 0f)), PixelUIFactory.Gold, 14);
    }

    void OnLeafLost(string why)
    {
        if (gameObject.activeInHierarchy) message(why);
    }

    void Sparkle(Vector3 worldPos, Color color, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            var dot = ui.Block(transform, "Sparkle", 0, 0, 5, 5, color);
            dot.raycastTarget = false;
            dot.gameObject.AddComponent<SparkleDot>().Launch(worldPos, i * Mathf.PI * 2f / count);
        }
    }

    // ---------- Seed shop ----------

    void OpenShop()
    {
        if (shop != null) Destroy(shop.gameObject);
        shop = ui.Rect(transform, "Seed shop", 0, 0, PageW, PageH);
        ui.Backdrop(shop, "Backdrop", PageW, PageH, new Color(0f, 0f, .05f, .7f));
        var card = ui.Panel(shop, "Card", 20, 70, 530, 640, PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", "SEMILLAS", 0, 16, 530, 40, 28, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        for (int i = 0; i < GardenState.Species.Length; i++)
        {
            int index = i;
            var sp = GardenState.Species[i];
            float y = 66 + i * 128;
            var row = ui.Panel(card, "Seed " + i, 16, y, 498, 120, PixelUIFactory.Hex("#9A5A33"));
            var icon = ui.Texture(row, "Leaf", "UI/Garden/leaf_" + sp.Shape + "_healthy", 14, 6, 36, 108);
            icon.raycastTarget = false;
            ui.Label(row, "Name", sp.Name.ToUpperInvariant() + (garden.Owned[i] ? "  Nv." + garden.Levels[i] : ""),
                62, 10, 280, 26, 18, Color.white);
            ui.Label(row, "Likes", "Agua " + sp.WaterMin + "-" + sp.WaterMax + "%  ·  Sol " + sp.LightMin + "-" + sp.LightMax + "%",
                62, 40, 290, 22, 13, PixelUIFactory.Cream);
            ui.Paragraph(row, "Bonus", sp.BonusText, 62, 64, 270, 44, 14, Good);
            string action = garden.Current == i ? "EN MACETA"
                : garden.Owned[i] ? "PLANTAR"
                : sp.CostGems > 0 ? sp.CostGems + " GEMAS" : FarmGameState.Format(sp.CostGold) + " MONEDAS";
            ui.ActionButton(row, "Plant " + i, action, 340, 33, 146, 54, () =>
            {
                message(garden.PlantSpecies(index));
                OpenShop(); // redraw with new states
            }, garden.Current != i);
        }
        ui.ActionButton(card, "Close", "CERRAR", 165, 578, 200, 50, () =>
        {
            Destroy(shop.gameObject);
            shop = null;
        });
    }

    void OnEnable() => Refresh();

    void OnDestroy()
    {
        if (garden == null) return;
        garden.Changed -= Refresh;
        garden.LeveledUp -= OnLevelUp;
        garden.LeafLost -= OnLeafLost;
    }
}

/// <summary>Tiny particle that flies out and fades (used for sparkles).</summary>
public sealed class SparkleDot : MonoBehaviour
{
    Vector3 velocity;
    float life = .6f, age;
    Image image;

    public void Launch(Vector3 worldPos, float angle)
    {
        image = GetComponent<Image>();
        transform.position = worldPos;
        float scale = transform.lossyScale.x;
        velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (90f + UnityEngine.Random.value * 60f) * scale;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        transform.position += velocity * Time.unscaledDeltaTime;
        velocity *= .92f;
        var c = image.color;
        c.a = 1f - age / life;
        image.color = c;
        if (age >= life) Destroy(gameObject);
    }
}
