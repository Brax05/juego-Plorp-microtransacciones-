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
    readonly Text[] prices = new Text[4];
    readonly Text[] levels = new Text[4];
    readonly Text gold;
    readonly Text gems;
    readonly Text farmLevel;
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
        }
        else
        {
            var worldRoot = ui.Rect(parent, "Modular farm world", 0, 0, 570, 530);
            world = worldRoot.gameObject.AddComponent<FarmWorldController>();
        }
        world.Initialize(state, ui, message);

        if (FarmUIBootstrap.FindChildRecursive(parent, "Pixel UI frame") == null)
        {
            ui.Texture(parent, "Pixel UI frame", "UI/chrome", 0, 0, 570, 1018);
        }

        var farmLevelObj = FarmUIBootstrap.FindChildRecursive(parent, "Farm level");
        farmLevel = farmLevelObj != null ? farmLevelObj.GetComponent<Text>() : ui.Label(parent, "Farm level", "", 158, 13, 58, 42, 27, Color.white);

        var goldObj = FarmUIBootstrap.FindChildRecursive(parent, "Gold");
        gold = goldObj != null ? goldObj.GetComponent<Text>() : ui.Label(parent, "Gold", "", 424, 20, 98, 35, 25, PixelUIFactory.Gold, TextAnchor.MiddleRight);

        var gemsObj = FarmUIBootstrap.FindChildRecursive(parent, "Gems");
        gems = gemsObj != null ? gemsObj.GetComponent<Text>() : ui.Label(parent, "Gems", "", 434, 61, 87, 34, 25, Color.white, TextAnchor.MiddleRight);

        BindHit(parent, ui, "Harvest", 17, 552, 536, 64, Harvest);
        BindHit(parent, ui, "Open coin store", 383, 18, 38, 35, () => navigate(1));
        BindHit(parent, ui, "Open gem store", 383, 57, 38, 35, () => navigate(1));

        for (int i = 0; i < 4; i++)
        {
            int index = i;
            float y = 628 + 80 * i;
            var pObj = FarmUIBootstrap.FindChildRecursive(parent, "Price " + i);
            prices[i] = pObj != null ? pObj.GetComponent<Text>() : ui.Label(parent, "Price " + i, "", 122, y + 34, 95, 32, 23, PixelUIFactory.Gold);

            var lObj = FarmUIBootstrap.FindChildRecursive(parent, "Upgrade level " + i);
            levels[i] = lObj != null ? lObj.GetComponent<Text>() : ui.Label(parent, "Upgrade level " + i, "", 410, y + 30, 120, 28, 21,
                Color.white, TextAnchor.MiddleCenter);

            BindHit(parent, ui, "Buy upgrade " + i, 400, y + 7, 140, 56, () => Buy(index));
        }

        string[] tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            BindHit(parent, ui, tabs[i], 2 + i * 114, 950, 110, 68, () => navigate(index));
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
        message(state.BuyUpgrade(index)
            ? (index == 1 ? "Gallina, oveja y vaca nuevas" : "Mejora comprada")
            : "Faltan monedas");
    }

    void Refresh()
    {
        farmLevel.text = state.FarmLevel.ToString();
        gold.text = FarmGameState.Format(state.Gold);
        gems.text = state.Gems.ToString();
        for (int i = 0; i < 4; i++)
        {
            prices[i].text = FarmGameState.Format(state.Prices[i]);
            levels[i].text = i == 1 ? state.AnimalGroups + " c/u" : "Nv." + state.Levels[i];
        }
    }
}
