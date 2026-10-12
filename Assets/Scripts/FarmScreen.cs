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

    public FarmScreen(Transform parent, FarmGameState state, PixelUIFactory ui,
        Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.ui = ui;
        this.navigate = navigate;
        this.message = message;

        ui.Texture(parent, "Farm illustration", "UI/farm", 0, 0, 570, 530);
        ui.Texture(parent, "Pixel UI frame", "UI/chrome", 0, 0, 570, 1018);
        farmLevel = ui.Label(parent, "Farm level", "", 158, 13, 58, 42, 27, Color.white);
        gold = ui.Label(parent, "Gold", "", 424, 20, 98, 35, 25, PixelUIFactory.Gold, TextAnchor.MiddleRight);
        gems = ui.Label(parent, "Gems", "", 434, 61, 87, 34, 25, Color.white, TextAnchor.MiddleRight);

        ui.Hit(parent, "Harvest", 17, 552, 536, 64, Harvest);
        ui.Hit(parent, "Open coin store", 383, 18, 38, 35, () => navigate(1));
        ui.Hit(parent, "Open gem store", 383, 57, 38, 35, () => navigate(1));
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            float y = 628 + 80 * i;
            prices[i] = ui.Label(parent, "Price " + i, "", 122, y + 34, 95, 32, 23, PixelUIFactory.Gold);
            levels[i] = ui.Label(parent, "Upgrade level " + i, "", 410, y + 30, 120, 28, 21,
                Color.white, TextAnchor.MiddleCenter);
            ui.Hit(parent, "Buy upgrade " + i, 400, y + 7, 140, 56, () => Buy(index));
        }

        string[] tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            ui.Hit(parent, tabs[i], 2 + i * 114, 950, 110, 68, () => navigate(index));
        }
        activeTab = ui.Rect(parent, "Selected tab", 11, 953, 92, 4);
        var bar = activeTab.gameObject.AddComponent<Image>();
        bar.color = PixelUIFactory.Gold;
        bar.raycastTarget = false;
        state.Changed += Refresh;
        Refresh();
    }

    public void SelectTab(int index)
    {
        activeTab.anchoredPosition = new Vector2(11 + index * 114, -953);
    }

    void Harvest()
    {
        long earned = state.Harvest();
        message("+" + FarmGameState.Format(earned) + " monedas");
    }

    void Buy(int index)
    {
        message(state.BuyUpgrade(index) ? "Mejora comprada" : "Faltan monedas");
    }

    void Refresh()
    {
        farmLevel.text = state.FarmLevel.ToString();
        gold.text = FarmGameState.Format(state.Gold);
        gems.text = state.Gems.ToString();
        for (int i = 0; i < 4; i++)
        {
            prices[i].text = FarmGameState.Format(state.Prices[i]);
            levels[i].text = "Nv." + state.Levels[i];
        }
    }
}
