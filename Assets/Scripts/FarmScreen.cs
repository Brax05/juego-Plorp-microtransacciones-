using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The reference-style farm art, HUD, upgrade controls and bottom navigation.</summary>
public sealed class FarmScreen
{
    readonly FarmGameState state;
    readonly PixelUIFactory ui;
    readonly Action<int> navigate;
    readonly Action<string> message;
    readonly Text[] prices = new Text[3];
    readonly Text[] levels = new Text[3];
    readonly Button[] upgradeButtons = new Button[3];
    readonly Text gold;
    readonly Text gems;
    readonly Text farmLevel;
    readonly RectTransform levelProgress;
    readonly RectTransform activeTab;
    readonly FarmWorldController world;

    public FarmScreen(Transform parent, FarmGameState state, PixelUIFactory ui,
        Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.ui = ui;
        this.navigate = navigate;
        this.message = message;

        var worldTransform = FarmUIBootstrap.FindChildRecursive(parent, "Modular farm world");
        if (worldTransform != null)
        {
            world = worldTransform.GetComponent<FarmWorldController>() ?? worldTransform.gameObject.AddComponent<FarmWorldController>();
            var viewport = worldTransform.parent as RectTransform;
            if (viewport != null)
            {
                var scroll = viewport.GetComponent<ScrollRect>();
                if (scroll != null && scroll.content == null)
                {
                    scroll.content = worldTransform as RectTransform;
                    scroll.viewport = viewport;
                }
            }
        }
        else
        {
            var viewport = ui.Rect(parent, "Farm Viewport", 0, 0, 570, 546);
            viewport.SetAsFirstSibling();
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 25f;

            var worldRoot = ui.Rect(viewport, "Modular farm world", 0, 0, FarmWorldController.MapWidth, FarmWorldController.MapHeight);
            scroll.content = worldRoot;
            scroll.viewport = viewport;
            world = worldRoot.gameObject.AddComponent<FarmWorldController>();
        }
        world.Initialize(state, ui, message);

        if (FarmUIBootstrap.FindChildRecursive(parent, "Pixel UI frame") == null)
        {
            ui.Texture(parent, "Pixel UI frame", "UI/chrome", 0, 0, 570, 1018);
        }

        // Kept separate from chrome.png so it can be positioned from the prefab Inspector.
        if (FarmUIBootstrap.FindChildRecursive(parent, "Level emblem") == null)
        {
            ui.Texture(parent, "Level emblem", "UI/Icons/nivel", 15, 22, 72, 72);
        }

        var farmLevelObj = FarmUIBootstrap.FindChildRecursive(parent, "Farm level");
        farmLevel = farmLevelObj != null ? farmLevelObj.GetComponent<Text>() : ui.Label(parent, "Farm level", "", 248, 18, 44, 36, 20, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        SetRect(farmLevel.rectTransform, 248, 18, 44, 36);
        farmLevel.fontSize = 20;
        farmLevel.color = PixelUIFactory.Gold;
        farmLevel.alignment = TextAnchor.MiddleCenter;

        levelProgress = ui.Rect(parent, "Level progress", 98, 66, 0, 22);
        var levelProgressImage = levelProgress.gameObject.AddComponent<Image>();
        levelProgressImage.color = PixelUIFactory.Gold;
        levelProgressImage.raycastTarget = false;

        var goldObj = FarmUIBootstrap.FindChildRecursive(parent, "Gold");
        gold = goldObj != null ? goldObj.GetComponent<Text>() : ui.Label(parent, "Gold", "", 480, 16, 70, 40, 25, PixelUIFactory.Gold, TextAnchor.MiddleLeft);
        SetRect(gold.rectTransform, 480, 16, 70, 40);
        gold.fontSize = 25;
        gold.alignment = TextAnchor.MiddleLeft;
        gold.resizeTextForBestFit = true;
        gold.resizeTextMinSize = 12;
        gold.resizeTextMaxSize = 25;

        var gemsObj = FarmUIBootstrap.FindChildRecursive(parent, "Gems");
        gems = gemsObj != null ? gemsObj.GetComponent<Text>() : ui.Label(parent, "Gems", "", 480, 58, 70, 40, 25, Color.white, TextAnchor.MiddleLeft);
        SetRect(gems.rectTransform, 480, 58, 70, 40);
        gems.fontSize = 25;
        gems.alignment = TextAnchor.MiddleLeft;
        gems.resizeTextForBestFit = true;
        gems.resizeTextMinSize = 12;
        gems.resizeTextMaxSize = 25;

        BindHit(parent, ui, "Harvest", 17, 552, 536, 64, Harvest);
        BindHit(parent, ui, "Open coin store", 370, 20, 42, 34, () => navigate(1));
        BindHit(parent, ui, "Open gem store", 370, 62, 42, 34, () => navigate(1));

        var obsoleteMachineryRow = FarmUIBootstrap.FindChildRecursive(parent, "Upgrade Row 3");
        if (obsoleteMachineryRow != null)
        {
            obsoleteMachineryRow.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(obsoleteMachineryRow.gameObject);
        }

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float y = 636 + 104 * i;
            var pObj = FarmUIBootstrap.FindChildRecursive(parent, "Price " + i);
            prices[i] = pObj != null ? pObj.GetComponent<Text>() : ui.Label(parent, "Price " + i, "", 150, y + 44, 100, 30, 20, PixelUIFactory.Gold);
            SetRect(prices[i].rectTransform, 150, y + 44, 100, 30);
            prices[i].fontSize = 20;
            prices[i].alignment = TextAnchor.MiddleLeft;

            var lObj = FarmUIBootstrap.FindChildRecursive(parent, "Upgrade level " + i);
            levels[i] = lObj != null ? lObj.GetComponent<Text>() : ui.Label(parent, "Upgrade level " + i, "", 410, y + 45, 120, 27, 16,
                Color.white, TextAnchor.MiddleCenter);
            SetRect(levels[i].rectTransform, 410, y + 45, 120, 27);
            levels[i].fontSize = 16;
            levels[i].color = Color.white;
            levels[i].alignment = TextAnchor.MiddleCenter;

            upgradeButtons[i] = BindHit(parent, ui, "Buy upgrade " + i, 400, y + 12, 140, 66, () => Buy(index));
            SetRect(upgradeButtons[i].GetComponent<RectTransform>(), 400, y + 12, 140, 66);
        }

        string[] tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };
        string[] tabIcons = { "granja", "tienda", "trofeos", "mejora", "ajustes" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            BindHit(parent, ui, tabs[i], 2 + i * 114, 950, 110, 68, () => navigate(index));
            ui.Texture(parent, "Navigation icon " + tabs[i], "UI/Icons/" + tabIcons[i],
                30 + i * 114, 956, 54, 54);
            ui.Label(parent, "Navigation label " + tabs[i], tabs[i].ToUpperInvariant(),
                5 + i * 114, 987, 104, 27, 15, Color.white, TextAnchor.MiddleCenter);
        }

        var tabObj = FarmUIBootstrap.FindChildRecursive(parent, "Selected tab") as RectTransform;
        if (tabObj != null)
        {
            activeTab = tabObj;
        }
        else
        {
            activeTab = ui.Rect(parent, "Selected tab", 11, 953, 92, 4);
            var bar = activeTab.gameObject.AddComponent<Image>();
            bar.color = PixelUIFactory.Gold;
            bar.raycastTarget = false;
        }

        state.Changed += Refresh;
        Refresh();
    }

    static Button BindHit(Transform parent, PixelUIFactory ui, string name, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        var t = FarmUIBootstrap.FindChildRecursive(parent, name);
        Button btn;
        if (t != null)
        {
            btn = t.GetComponent<Button>() ?? t.gameObject.AddComponent<Button>();
        }
        else
        {
            btn = ui.Hit(parent, name, x, y, w, h, action);
            return btn;
        }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
        return btn;
    }

    static void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public void SelectTab(int index)
    {
        activeTab.anchoredPosition = new Vector2(11 + index * 114, -953);
    }

    void Harvest()
    {
        long earned = world.HarvestAll();
        message(earned > 0 ? "+" + FarmGameState.Format(earned) + " monedas" : "Nada listo para cosechar");
    }

    void Buy(int index)
    {
        if (state.IsMaxed(index))
        {
            message("Mejora al maximo");
            return;
        }
        message(state.BuyUpgrade(index)
            ? (index == 1 ? "Nuevo animal desbloqueado" : "Mejora comprada")
            : "Faltan monedas");
    }

    void Refresh()
    {
        farmLevel.text = state.FarmLevel.ToString();
        levelProgress.sizeDelta = new Vector2(192f * state.FarmLevelProgress, 22f);
        gold.text = FarmGameState.Format(state.Gold);
        gems.text = FarmGameState.Format(state.Gems);
        for (int i = 0; i < 3; i++)
        {
            prices[i].text = state.IsMaxed(i) ? "MAX" : FarmGameState.Format(state.Prices[i]);
            levels[i].text = "Nv." + state.Levels[i];
            upgradeButtons[i].interactable = !state.IsMaxed(i) && state.Gold >= state.Prices[i];
        }
    }
}
