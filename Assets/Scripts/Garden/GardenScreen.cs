using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Jardinería" tab: a plant on the window sill (Resources/UI/Garden/garden_bg.png, 570x840).
///  - Drag the tools from the bottom cards onto the plant: the watering can pours water drops,
///    the fertilizer bag pours sand, the lamp shines, and the scissors cut dry/yellow leaves.
///  - Tap dusty leaves to clean them, squash bugs for gems, tap the flower to collect monedas de flor.
///  - The window shows the weather (sun beams / clouds); Pepito pops in at the top with alerts.
///  - Tap the sign to buy or switch plants.
/// </summary>
public sealed class GardenScreen : MonoBehaviour
{
    const float PageW = 570f, PageH = 840f;
    static readonly Vector2 PotTop = new Vector2(283f, 484f);
    static readonly Vector2[] Slots =
    {
        new Vector2(-14f, 1.0f), new Vector2(15f, 1.05f),
        new Vector2(-38f, .92f), new Vector2(40f, .92f),
        new Vector2(-62f, .8f), new Vector2(64f, .8f),
    };
    static readonly float[] CardX = { 37f, 165f, 294f, 423f };
    const float CardY = 679f, CardW = 110f, CardH = 153f;
    static readonly string[] ToolSprites = { "tool_water", "tool_scissors", "tool_fertilizer", "tool_lamp" };
    static readonly string[] ToolLabels = { "REGAR", "CORTAR", "ABONO", "LUZ" };
    static readonly string[] ToolHints =
    {
        "Arrastra la regadera sobre la planta",
        "Arrastra las tijeras sobre una hoja seca o quemada",
        "Arrastra el abono sobre la planta (cuesta 2 monedas de flor)",
        "Arrastra la lámpara sobre la planta",
    };

    static readonly Color Warn = PixelUIFactory.Hex("#FFB347");
    static readonly Color Good = PixelUIFactory.Hex("#B8F28A");
    const float BarX = 132f, BarW = 236f;

    FarmGameState state;
    GardenState garden;
    Action<string> message;
    PixelUIFactory ui;
    RectTransform page;

    Text signText, weatherText, careValue, flowerCoinText;
    RectTransform waterFill, lightFill, careFill, waterBand, lightBand;
    Text waterValue, lightValue, statusText, bonusText;
    RectTransform sunLayer, cloudLayer, curtainLeft, curtainRight;
    Text curtainButtonText;
    float curtainOpen = 1f; // 1 open, 0 closed (animated)
    CanvasGroup toastGroup;
    readonly RawImage[] leaves = new RawImage[GardenState.LeafSlots];
    readonly float[] wiggle = new float[GardenState.LeafSlots];
    RectTransform plantRoot, plantZone, flowerStem, flowerRoot, coinBubble, fxLayer;
    RawImage flower;
    Text coinBubbleText;
    readonly Dictionary<GardenBug, RawImage> bugViews = new Dictionary<GardenBug, RawImage>();
    readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    RectTransform shop;
    RectTransform shopPulse;

    // Dragging a tool
    int dragTool = -1;
    RawImage ghost;
    Vector2 dragScreen;
    Camera dragCam;
    float pourTimer, particleTimer;
    bool fertilizedThisDrag;

    // Pepito toast
    RectTransform toast;
    Text toastText;
    SpriteStripAnimator toastFace;
    float toastUntil, toastSlide;

    public static GardenScreen Create(Transform secondary, FarmGameState state, Action<string> message)
    {
        var ui = new PixelUIFactory();
        var page = ui.Rect(secondary, "Page - Jardineria", 0, 0, PageW, PageH);
        var screen = page.gameObject.AddComponent<GardenScreen>();
        screen.Build(ui, page, state, message);
        page.gameObject.SetActive(false);
        return screen;
    }

    void Build(PixelUIFactory factory, RectTransform pageRect, FarmGameState farm, Action<string> showMessage)
    {
        ui = factory;
        page = pageRect;
        state = farm;
        garden = farm.Garden;
        message = showMessage;

        var bg = ui.Texture(transform, "Background", "UI/Garden/garden_bg", 0, 0, PageW, PageH);
        bg.raycastTarget = true;

        // Weather seen through the window (lower panes).
        sunLayer = ui.Rect(transform, "Sunny window", 92, 204, 389, 264);
        sunLayer.gameObject.AddComponent<RectMask2D>(); // keep the beams inside the glass
        ui.Block(sunLayer, "Warm light", 0, 0, 389, 283, new Color(1f, .93f, .55f, .16f));
        for (int i = 0; i < 3; i++)
        {
            var beam = ui.Block(sunLayer, "Beam " + i, 40 + i * 120, -60, 46, 420, new Color(1f, .97f, .7f, .2f));
            beam.rectTransform.localEulerAngles = new Vector3(0f, 0f, -28f);
        }
        cloudLayer = ui.Rect(transform, "Cloudy window", 92, 204, 389, 264);
        cloudLayer.gameObject.AddComponent<RectMask2D>();
        ui.Block(cloudLayer, "Grey", 0, 0, 389, 283, new Color(.55f, .6f, .7f, .38f));
        var rng = new System.Random(4);
        for (int i = 0; i < 26; i++)
            ui.Block(cloudLayer, "Drop", rng.Next(8, 380), rng.Next(8, 275), 3, 4, new Color(.85f, .93f, 1f, .7f));

        // Curtains over the window (closed = no sun gets in).
        curtainLeft = BuildCurtain("Curtain left", 86);
        curtainRight = BuildCurtain("Curtain right", 290);

        // Sign with the species name: tap to open the plant shop. Weather line under it.
        signText = ui.Label(transform, "Species", "", 128, 120, 312, 40, 24, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Open plants", 110, 112, 352, 56, OpenShop);
        // Clear button for the plant shop (the sign alone wasn't obvious).
        var shopButton = ui.Panel(transform, "Plant shop button", 165, 164, 240, 30, PixelUIFactory.Green);
        ui.Texture(shopButton, "Icon", "UI/Icons/semillas", 8, 1, 28, 28);
        ui.Label(shopButton, "Caption", "TIENDA DE PLANTAS", 38, 0, 196, 30, 14, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Open plant shop", 165, 164, 240, 30, OpenShop);
        shopButton.pivot = new Vector2(.5f, .5f);
        shopButton.anchoredPosition += new Vector2(120f, -15f); // keep the same place with a centred pivot
        shopPulse = shopButton;

        // Monedas de flor counter (top-left of the window frame).
        var pill = ui.Panel(transform, "Flower coins", 6, 96, 92, 34, PixelUIFactory.Hex("#7A3F5E"));
        ui.Texture(pill, "Icon", "UI/Garden/icon_flowercoin", 6, 5, 24, 24);
        flowerCoinText = ui.Label(pill, "Count", "", 32, 2, 56, 30, 16, Color.white);

        // Curtain button (top-right of the window frame).
        var curtainButton = ui.Panel(transform, "Curtains button", 472, 96, 92, 34, PixelUIFactory.Hex("#8C3A3A"));
        curtainButtonText = ui.Label(curtainButton, "Caption", "", 2, 0, 88, 34, 12, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Toggle curtains", 472, 96, 92, 34, () =>
        {
            garden.ToggleCurtains();
            message(garden.CurtainsClosed ? "Cortinas cerradas: no entra el sol" : "Cortinas abiertas");
        });

        // Water / light / care bars.
        ui.Block(transform, "Bars shade", 96, 198, 380, 106, new Color(.04f, .06f, .1f, .55f));
        weatherText = ui.Label(transform, "Weather", "", 100, 282, 372, 18, 12, Color.white, TextAnchor.MiddleCenter);
        BuildBar(206, "UI/Garden/icon_water", PixelUIFactory.Hex("#5AB0F0"), out waterFill, out waterBand, out waterValue);
        BuildBar(232, "UI/Garden/icon_sun", PixelUIFactory.Hex("#FFD44F"), out lightFill, out lightBand, out lightValue);
        BuildBar(258, "UI/Garden/icon_sprout", PixelUIFactory.Hex("#FF8CC6"), out careFill, out _, out careValue);

        // Plant: flower on a centre stem (behind), then leaves (centre ones first = behind).
        plantRoot = ui.Rect(transform, "Plant", 0, 0, PageW, PageH);
        plantZone = ui.Rect(transform, "Plant zone", 130, 280, 310, 230);
        flowerStem = ui.Block(plantRoot, "Flower stem", PotTop.x - 2, PotTop.y - 170, 5, 170, PixelUIFactory.Hex("#3E8A3A")).rectTransform;
        flowerRoot = ui.Rect(plantRoot, "Flower", PotTop.x - 26, PotTop.y - 210, 52, 52);
        flower = ui.Texture(flowerRoot, "Art", "UI/Garden/flower_especial", 0, 0, 52, 52);
        flower.raycastTarget = true;
        var flowerButton = flower.gameObject.AddComponent<Button>();
        flowerButton.targetGraphic = flower;
        flowerButton.transition = Selectable.Transition.None;
        flowerButton.onClick.AddListener(CollectFlower);
        coinBubble = ui.Panel(flowerRoot, "Coins", 40, -10, 52, 26, PixelUIFactory.Hex("#7A3F5E"));
        ui.Texture(coinBubble, "Icon", "UI/Garden/icon_flowercoin", 4, 3, 20, 20);
        coinBubbleText = ui.Label(coinBubble, "Count", "", 24, 0, 28, 26, 13, Color.white);

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

        // Tool cards: drag them onto the plant.
        for (int i = 0; i < 4; i++)
        {
            float x = CardX[i];
            ui.Label(transform, "Tool name " + i, ToolLabels[i], x, 770, CardW, 26, 17, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
            if (i == 2)
            {
                ui.Texture(transform, "Abono cost icon", "UI/Garden/icon_flowercoin", x + 30, 800, 20, 20);
                ui.Label(transform, "Abono cost", GardenState.FertilizerCost.ToString(), x + 54, 797, 40, 24, 15, Color.white);
            }
            else ui.Label(transform, "Tool hint " + i, "arrastra", x, 798, CardW, 24, 13, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
            var hit = ui.Hit(transform, "Drag tool " + i, x, CardY, CardW, CardH, () => { });
            hit.gameObject.AddComponent<GardenToolDrag>().Setup(this, i);
        }

        fxLayer = ui.Rect(transform, "Effects", 0, 0, PageW, PageH);

        // Pepito toast at the top (alerts while the player is here).
        toast = ui.Rect(transform, "Pepito toast", 0, 0, PageW, 96);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        ui.Panel(toast, "Frame", 6, 6, 82, 82, PixelUIFactory.Hex("#4B5160"));
        toastFace = SpriteStripAnimator.Create(toast, "Pepito", "UI/Characters/pepito_talk", 12, 12, 70);
        var bubble = ui.Panel(toast, "Bubble", 92, 10, 470, 74, PixelUIFactory.Hex("#F6E7C8"));
        toastText = ui.Paragraph(bubble, "Text", "", 12, 6, 446, 62, 16, PixelUIFactory.Ink);
        var outline = toastText.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        toast.anchoredPosition = new Vector2(0f, 40f);

        garden.Changed += Refresh;
        garden.Alert += ShowToast;
        Refresh();
    }

    /// <summary>A fabric curtain made of stripes; its width is animated (gathered when open).</summary>
    RectTransform BuildCurtain(string name, float x)
    {
        // Ends just above the pot rim (the pot is painted in the background, curtains cannot go behind it).
        var curtain = ui.Rect(transform, name, x, 186, 34, 280);
        curtain.gameObject.AddComponent<RectMask2D>();
        ui.Block(curtain, "Fabric", 0, 0, 200, 280, PixelUIFactory.Hex("#B5443F"));
        for (int i = 0; i < 10; i++)
            ui.Block(curtain, "Fold", 6 + i * 20, 0, 6, 280, PixelUIFactory.Hex(i % 2 == 0 ? "#8E2F2C" : "#CF5B54"));
        ui.Block(curtain, "Hem", 0, 270, 200, 10, PixelUIFactory.Hex("#E7C46A"));
        ui.Block(curtain, "Rod", 0, 0, 200, 6, PixelUIFactory.Hex("#5A3420"));
        return curtain;
    }

    void BuildBar(float y, string icon, Color color, out RectTransform fill, out RectTransform band, out Text value)
    {
        ui.Texture(transform, "Icon", icon, 104, y - 2, 22, 22);
        ui.Block(transform, "Bar back", BarX, y + 3, BarW, 14, PixelUIFactory.Hex("#2A1A14"));
        band = ui.Block(transform, "Comfort zone", BarX, y + 1, 0, 18, new Color(.5f, 1f, .4f, .35f)).rectTransform;
        fill = ui.Block(transform, "Bar fill", BarX, y + 5, 0, 10, color).rectTransform;
        value = ui.Label(transform, "Value", "", BarX + BarW + 8, y - 2, 104, 22, 13, Color.white);
    }

    static void SetBar(RectTransform fill, float fraction) =>
        fill.sizeDelta = new Vector2(BarW * Mathf.Clamp01(fraction), fill.sizeDelta.y);

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
        weatherText.text = garden.WeatherText;
        sunLayer.gameObject.SetActive(garden.Weather == GardenWeather.Sunny && !garden.CurtainsClosed);
        curtainButtonText.text = garden.CurtainsClosed ? "ABRIR\nCORTINAS" : "CERRAR\nCORTINAS";
        cloudLayer.gameObject.SetActive(garden.Weather == GardenWeather.Cloudy);
        flowerCoinText.text = FarmGameState.Format(garden.FlowerCoins);

        SetBar(waterFill, garden.Water / 100f);
        SetBar(lightFill, garden.Light / 100f);
        SetBar(careFill, garden.Bloomed ? 1f : (garden.CurrentCare % 25f) / 25f);
        SetBand(waterBand, plant.WaterMin, plant.WaterMax);
        SetBand(lightBand, plant.LightMin, plant.LightMax);
        waterValue.text = Mathf.RoundToInt(garden.Water) + "%";
        waterValue.color = garden.TooMuchWater || garden.TooLittleWater ? Warn : Color.white;
        lightValue.text = Mathf.RoundToInt(garden.Light) + "%";
        lightValue.color = garden.TooMuchLight || garden.TooLittleLight ? Warn : Color.white;
        // The bar shows progress inside the current level (each level = 25% of care).
        careValue.text = "Nv." + garden.Level + "/" + GardenState.MaxLevel + " " + GardenState.LevelNames[garden.Level];

        statusText.text = garden.StatusText;
        statusText.color = garden.Comfortable && garden.Bugs.Count == 0 ? Color.white : Warn;
        bonusText.text = garden.BonusSummary;

        bool bloom = garden.Bloomed;
        flowerRoot.gameObject.SetActive(bloom);
        flowerStem.gameObject.SetActive(bloom);
        if (bloom) flower.texture = Tex("UI/Garden/flower_" + plant.Id);
        coinBubble.gameObject.SetActive(bloom && garden.StoredCoins > 0);
        coinBubbleText.text = "x" + garden.StoredCoins;

        for (int i = 0; i < leaves.Length; i++)
        {
            var leafState = garden.Leaves[i];
            bool show = i < garden.VisibleLeaves && leafState != LeafState.Empty;
            leaves[i].gameObject.SetActive(show);
            if (show) leaves[i].texture = LeafTexture(plant.LeafShape, leafState);
        }
        SyncBugs();
    }

    void SyncBugs()
    {
        var gone = new List<GardenBug>();
        foreach (var pair in bugViews) if (!garden.Bugs.Contains(pair.Key)) gone.Add(pair.Key);
        foreach (var bug in gone)
        {
            if (bugViews[bug] != null) Destroy(bugViews[bug].gameObject);
            bugViews.Remove(bug);
        }
        foreach (var bug in garden.Bugs)
        {
            if (bugViews.ContainsKey(bug) || bug.Leaf >= leaves.Length) continue;
            var leaf = leaves[bug.Leaf].rectTransform;
            var view = ui.Texture(leaf, "Bug", "UI/Garden/bug_0", 0, 0, 22, 22);
            view.rectTransform.anchorMin = view.rectTransform.anchorMax = new Vector2(.5f, 0f);
            view.rectTransform.pivot = new Vector2(.5f, .5f);
            view.rectTransform.anchoredPosition = new Vector2(0f, 68f + bug.Along * 80f);
            view.raycastTarget = true;
            var target = bug;
            var button = view.gameObject.AddComponent<Button>();
            button.targetGraphic = view;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Squash(target));
            bugViews[bug] = view;
        }
    }

    Texture2D Tex(string path)
    {
        if (!textures.TryGetValue(path, out var tex))
        {
            tex = Resources.Load<Texture2D>(path);
            if (tex != null) tex.filterMode = FilterMode.Point;
            textures[path] = tex;
        }
        return tex;
    }

    Texture2D LeafTexture(string shape, LeafState leafState)
    {
        string suffix = leafState == LeafState.Strong ? "strong" : leafState == LeafState.Dusty ? "dusty"
            : leafState == LeafState.Withered ? "withered" : leafState == LeafState.Burnt ? "burnt" : "healthy";
        return Tex("UI/Garden/leaf_" + shape + "_" + suffix);
    }

    void Update()
    {
        if (garden == null) return;
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        // Plant size follows care; leaves sway, dry leaves droop.
        float grow = .8f + .3f * Mathf.Clamp01(garden.CurrentCare / 100f);
        for (int i = 0; i < leaves.Length; i++)
        {
            if (!leaves[i].gameObject.activeSelf) continue;
            var leafState = garden.Leaves[i];
            bool dry = leafState == LeafState.Withered || leafState == LeafState.Burnt;
            float angle = Slots[i].x + (dry ? Mathf.Sign(Slots[i].x) * 22f : 0f);
            wiggle[i] = Mathf.MoveTowards(wiggle[i], 0f, dt * 2f);
            float sway = Mathf.Sin(t * 1.3f + i * 1.7f) * 1.6f + Mathf.Sin(t * 22f) * 9f * wiggle[i];
            leaves[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -(angle + sway));
            leaves[i].rectTransform.localScale = new Vector3(grow, Slots[i].y * grow * (dry ? .85f : 1f), 1f);
        }
        if (flowerRoot.gameObject.activeSelf)
        {
            flowerRoot.localScale = Vector3.one * (1f + .05f * Mathf.Sin(t * 2.2f));
            flowerRoot.localEulerAngles = new Vector3(0f, 0f, 4f * Mathf.Sin(t * 1.1f));
        }

        // Bugs crawl a little and flap.
        foreach (var pair in bugViews)
        {
            if (pair.Value == null) continue;
            pair.Value.texture = Tex(((int)(t * 6f) & 1) == 0 ? "UI/Garden/bug_0" : "UI/Garden/bug_1");
            pair.Value.rectTransform.anchoredPosition =
                new Vector2(Mathf.Sin(t * 3f + pair.Key.Along * 9f) * 4f, 68f + pair.Key.Along * 80f);
        }

        // The plant shop button breathes while there is something new to buy.
        bool canBuy = false;
        for (int i = 0; i < GardenState.Species.Length; i++)
            if (!garden.Owned[i] && garden.CanUnlock(i) && garden.FlowerCoins >= GardenState.Species[i].CostFlowerCoins) canBuy = true;
        shopPulse.localScale = Vector3.one * (canBuy ? 1f + .05f * Mathf.Sin(t * 5f) : 1f);

        // Curtains slide open/closed.
        curtainOpen = Mathf.MoveTowards(curtainOpen, garden.CurtainsClosed ? 0f : 1f, dt / .35f);
        float closedW = 200f, openW = 34f, w = Mathf.Lerp(closedW, openW, curtainOpen);
        curtainLeft.sizeDelta = new Vector2(w, curtainLeft.sizeDelta.y);
        curtainRight.sizeDelta = new Vector2(w, curtainRight.sizeDelta.y);
        curtainRight.anchoredPosition = new Vector2(487f - w, curtainRight.anchoredPosition.y);

        UpdateTool(dt);
        UpdateToast(dt);
    }

    // ---------- Taps ----------

    void TapLeaf(int index)
    {
        wiggle[index] = 1f;
        if (garden.CleanLeaf(index))
        {
            Burst(leaves[index].rectTransform.TransformPoint(new Vector3(0f, 110f, 0f)), Color.white, 8);
            message("Hoja limpia: ¡más fuerte!");
        }
        else if (garden.IsDry(index)) message("Hoja seca: arrástrale las tijeras");
    }

    void Squash(GardenBug bug)
    {
        if (!bugViews.TryGetValue(bug, out var view) || view == null) return;
        Vector3 at = view.rectTransform.position;
        int gems = garden.SquashBug(bug);
        if (gems <= 0) return;
        Burst(at, PixelUIFactory.Hex("#7CF2E4"), 10);
        RewardFx.Popup(at, "+" + gems + (gems == 1 ? " gema" : " gemas"), PixelUIFactory.Hex("#7CF2E4"));
        message("¡Plaf! +" + gems + (gems == 1 ? " gema" : " gemas"));
    }

    void CollectFlower()
    {
        Vector3 from = flower.rectTransform.position;
        int coins = garden.CollectFlowerCoins();
        if (coins <= 0)
        {
            message("La flor está juntando monedas de flor");
            return;
        }
        for (int i = 0; i < Mathf.Min(coins, 6); i++)
            Particle(from, "UI/Garden/icon_flowercoin", 22f, Vector3.zero, 0f, .8f, flowerCoinText.transform, i * .07f);
        message("+" + coins + " monedas de flor");
    }

    // ---------- Dragging tools ----------

    public void ToolTapped(int tool) => message(ToolHints[tool]);

    public void BeginTool(int tool, PointerEventData e)
    {
        dragTool = tool;
        fertilizedThisDrag = false;
        pourTimer = 0f;
        if (ghost != null) Destroy(ghost.gameObject);
        var tex = Tex("UI/Garden/" + ToolSprites[tool]);
        float h = 78f, w = tex != null ? h * tex.width / tex.height : h;
        ghost = ui.Texture(fxLayer, "Held tool", "UI/Garden/" + ToolSprites[tool], 0, 0, w, h);
        ghost.rectTransform.pivot = new Vector2(.5f, .5f);
        fxLayer.SetAsLastSibling();
        toast.SetAsLastSibling();
        MoveTool(e);
    }

    public void MoveTool(PointerEventData e)
    {
        dragScreen = e.position;
        dragCam = e.pressEventCamera;
        if (ghost == null) return;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(page, dragScreen, dragCam, out var world))
            // Hold the tool a bit above the finger so it stays visible.
            ghost.rectTransform.position = world + Vector3.up * 40f * page.lossyScale.y;
    }

    public void EndTool()
    {
        dragTool = -1;
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }

    bool OverPlant => RectTransformUtility.RectangleContainsScreenPoint(plantZone, dragScreen, dragCam);

    void UpdateTool(float dt)
    {
        if (dragTool < 0 || ghost == null) return;
        float t = Time.unscaledTime;
        var g = ghost.rectTransform;
        bool over = OverPlant;
        float s = page.lossyScale.x;
        particleTimer -= dt;

        switch (dragTool)
        {
            case 0: // watering can: tilt and pour drops
                g.localEulerAngles = new Vector3(0f, 0f, over ? -30f : 0f);
                if (over)
                {
                    garden.PourWater(dt);
                    if (particleTimer <= 0f)
                    {
                        particleTimer = .035f;
                        Vector3 spout = g.TransformPoint(new Vector3(g.rect.width * .45f, g.rect.height * .1f, 0f));
                        Particle(spout, null, 4f, new Vector3(UnityEngine.Random.Range(-20f, 20f), -60f, 0f) * s, 900f * s, .5f,
                            null, 0f, PixelUIFactory.Hex("#7CC8FF"));
                    }
                }
                break;
            case 1: // scissors: snip any dry leaf under them
                g.localScale = new Vector3(1f, 1f + .08f * Mathf.Sin(t * 20f), 1f);
                for (int i = 0; i < leaves.Length; i++)
                {
                    if (!leaves[i].gameObject.activeSelf || !garden.IsDry(i)) continue;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(leaves[i].rectTransform, dragScreen, dragCam)) continue;
                    DropLeaf(i);
                    garden.CutLeaf(i);
                    RewardFx.Popup(g.position, "¡Snip!", Color.white);
                    break;
                }
                break;
            case 2: // fertilizer: pour sand; after a moment of pouring it's applied once per drag
                g.localEulerAngles = new Vector3(0f, 0f, over ? 35f : 0f);
                if (over)
                {
                    pourTimer += dt;
                    if (particleTimer <= 0f)
                    {
                        particleTimer = .03f;
                        Vector3 mouth = g.TransformPoint(new Vector3(-g.rect.width * .35f, g.rect.height * .2f, 0f));
                        Color sand = UnityEngine.Random.value < .5f ? PixelUIFactory.Hex("#E8C46A") : PixelUIFactory.Hex("#B08040");
                        Particle(mouth, null, 3f, new Vector3(UnityEngine.Random.Range(-25f, 10f), -40f, 0f) * s, 700f * s, .55f,
                            null, 0f, sand);
                    }
                    if (!fertilizedThisDrag && pourTimer >= .6f)
                    {
                        fertilizedThisDrag = true;
                        message(garden.ApplyFertilizer());
                    }
                }
                break;
            default: // lamp: glow over the plant
                if (over)
                {
                    garden.PourLight(dt);
                    if (particleTimer <= 0f)
                    {
                        particleTimer = .06f;
                        Vector3 bulb = g.TransformPoint(new Vector3(0f, -g.rect.height * .25f, 0f));
                        Particle(bulb, null, 5f, new Vector3(UnityEngine.Random.Range(-70f, 70f), -90f, 0f) * s, 0f, .5f,
                            null, 0f, new Color(1f, .95f, .55f, .9f));
                    }
                }
                break;
        }
    }

    void DropLeaf(int index)
    {
        var src = leaves[index];
        var copy = ui.Texture(fxLayer, "Cut leaf", "UI/Garden/leaf_especial_withered", 0, 0, 64, 160);
        copy.texture = src.texture;
        var rt = copy.rectTransform;
        rt.pivot = new Vector2(.5f, 0f);
        rt.position = src.rectTransform.position;
        rt.rotation = src.rectTransform.rotation;
        rt.localScale = src.rectTransform.localScale;
        copy.gameObject.AddComponent<GardenParticle>().Launch(rt.position,
            new Vector3(UnityEngine.Random.Range(-60f, 60f), 40f, 0f) * page.lossyScale.x, 500f * page.lossyScale.x, 1.1f,
            null, 0f, UnityEngine.Random.Range(-200f, 200f));
    }

    void Particle(Vector3 at, string texture, float size, Vector3 velocity, float gravity, float life,
        Transform target, float delay, Color? color = null)
    {
        Graphic g;
        if (texture != null) g = ui.Texture(fxLayer, "Particle", texture, 0, 0, size, size);
        else g = ui.Block(fxLayer, "Particle", 0, 0, size, size, color ?? Color.white);
        g.raycastTarget = false;
        g.rectTransform.pivot = new Vector2(.5f, .5f);
        g.gameObject.AddComponent<GardenParticle>().Launch(at, velocity, gravity, life, target, delay, 0f);
    }

    void Burst(Vector3 at, Color color, int count)
    {
        float s = page.lossyScale.x;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            Particle(at, null, 5f, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 140f * s, 0f, .5f, null, 0f, color);
        }
    }

    // ---------- Pepito toast ----------

    void ShowToast(string text)
    {
        if (!gameObject.activeInHierarchy) return;
        toastText.text = text;
        toastUntil = Time.unscaledTime + 5f;
        toastFace.Talk(2);
        toast.SetAsLastSibling();
    }

    void UpdateToast(float dt)
    {
        float target = Time.unscaledTime < toastUntil ? 1f : 0f;
        toastSlide = Mathf.MoveTowards(toastSlide, target, dt / .25f);
        float eased = 1f - Mathf.Pow(1f - toastSlide, 3f);
        // Slides down a little and fades; fully invisible (and not blocking taps) when hidden.
        toast.anchoredPosition = new Vector2(0f, Mathf.Lerp(40f, 0f, eased));
        toastGroup.alpha = eased;
        toastGroup.blocksRaycasts = false;
    }

    // ---------- Plant shop ----------

    void OpenShop()
    {
        if (shop != null) Destroy(shop.gameObject);
        shop = ui.Rect(transform, "Plant shop", 0, 0, PageW, PageH);
        ui.Backdrop(shop, "Backdrop", PageW, PageH, new Color(0f, 0f, .05f, .7f));
        var card = ui.Panel(shop, "Card", 14, 20, 542, 800, PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", "PLANTAS", 20, 14, 300, 40, 28, PixelUIFactory.Gold);
        ui.Texture(card, "Coins icon", "UI/Garden/icon_flowercoin", 370, 20, 26, 26);
        ui.Label(card, "Coins", FarmGameState.Format(garden.FlowerCoins) + " de flor", 400, 16, 130, 34, 16, Color.white);
        for (int i = 0; i < GardenState.Species.Length; i++)
        {
            int index = i;
            var sp = GardenState.Species[i];
            float y = 62 + i * 112;
            bool owned = garden.Owned[i], current = garden.Current == i, unlockable = garden.CanUnlock(i);
            var row = ui.Panel(card, "Plant " + i, 12, y, 518, 106, PixelUIFactory.Hex(owned || unlockable ? "#9A5A33" : "#6E4A36"));
            ui.Texture(row, "Leaf", "UI/Garden/leaf_" + sp.LeafShape + "_healthy", 10, 4, 34, 86);
            ui.Texture(row, "Flower", "UI/Garden/flower_" + sp.Id, 26, 4, 30, 30);
            ui.Label(row, "Name", sp.Name.ToUpperInvariant() + (garden.EverBloomed[i] ? "  (florecida)" : ""), 64, 8, 280, 24, 17, Color.white);
            ui.Label(row, "Likes", "Agua " + sp.WaterMin + "-" + sp.WaterMax + "%  ·  Sol " + sp.LightMin + "-" + sp.LightMax + "%",
                64, 34, 280, 20, 12, PixelUIFactory.Cream);
            ui.Paragraph(row, "Bonus", sp.BonusText + (owned ? "  (cuidado " + Mathf.FloorToInt(garden.Care[i]) + "%)" : ""),
                64, 56, 276, 40, 13, Good);
            string action = current ? "EN MACETA" : owned ? "PLANTAR"
                : !unlockable ? "BLOQUEADA" : sp.CostFlowerCoins + " DE FLOR";
            ui.ActionButton(row, "Action", action, 350, 22, 156, 50, () =>
            {
                message(garden.PlantSpecies(index));
                OpenShop();
            }, !current && (owned || unlockable));
            if (!owned && !unlockable)
                ui.Paragraph(row, "Need", "Antes haz florecer: " + GardenState.Species[i - 1].Name, 344, 74, 168, 26, 11, PixelUIFactory.Cream);
        }
        ui.ActionButton(card, "Close", "CERRAR", 171, 738, 200, 50, () =>
        {
            Destroy(shop.gameObject);
            shop = null;
        });
    }

    void OnEnable() => Refresh();

    void OnDisable() => EndTool();

    void OnDestroy()
    {
        if (garden == null) return;
        garden.Changed -= Refresh;
        garden.Alert -= ShowToast;
    }
}

/// <summary>Forwards drag/tap on a tool card to the garden screen.</summary>
public sealed class GardenToolDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    GardenScreen screen;
    int tool;
    bool dragging;

    public void Setup(GardenScreen owner, int index) { screen = owner; tool = index; }
    public void OnBeginDrag(PointerEventData e) { dragging = true; screen.BeginTool(tool, e); }
    public void OnDrag(PointerEventData e) => screen.MoveTool(e);
    public void OnEndDrag(PointerEventData e) { dragging = false; screen.EndTool(); }
    public void OnPointerClick(PointerEventData e) { if (!dragging && !e.dragging) screen.ToolTapped(tool); }
}

/// <summary>Small effect particle: drops, sand, sparkles, falling leaves, or a coin flying to a target.</summary>
public sealed class GardenParticle : MonoBehaviour
{
    Vector3 velocity;
    float gravity, life, age, delay, spin;
    Transform target;
    Vector3 start;
    Graphic graphic;

    public void Launch(Vector3 at, Vector3 vel, float grav, float lifetime, Transform flyTo, float wait, float spinSpeed)
    {
        transform.position = start = at;
        velocity = vel;
        gravity = grav;
        life = lifetime;
        target = flyTo;
        delay = wait;
        spin = spinSpeed;
        graphic = GetComponent<Graphic>();
        if (delay > 0f && graphic != null) graphic.canvasRenderer.SetAlpha(0f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        if (age < delay) return;
        float t = (age - delay) / life;
        if (graphic != null) graphic.canvasRenderer.SetAlpha(target != null ? 1f : Mathf.Clamp01(1.4f - t * 1.4f));
        if (target != null)
        {
            float e = t * t * (3f - 2f * t);
            Vector3 mid = Vector3.Lerp(start, target.position, .4f) + Vector3.up * 80f * transform.lossyScale.y;
            transform.position = Vector3.Lerp(Vector3.Lerp(start, mid, e), Vector3.Lerp(mid, target.position, e), e);
        }
        else
        {
            velocity += Vector3.down * gravity * dt;
            transform.position += velocity * dt;
            if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        }
        if (t >= 1f) Destroy(gameObject);
    }
}
