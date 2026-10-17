using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The reference-style farm art, HUD, upgrade controls and bottom navigation.</summary>
public sealed class FarmScreen
{
    readonly FarmGameState state;
    readonly Action<int> navigate;
    readonly Action<string> message;
    readonly Text[] prices = new Text[3];
    readonly Text[] levels = new Text[3];
    readonly Button[] upgradeButtons = new Button[3];
    readonly Text gold;
    readonly Text gems;
    readonly Text farmLevel;
    readonly RectTransform levelProgress;
    readonly float maxProgressWidth;
    readonly RectTransform activeTab;
    readonly RectTransform[] tabButtons = new RectTransform[5];
    readonly Vector2 selectedTabStart;
    readonly FarmWorldController world;

    public FarmScreen(Transform parent, FarmGameState state,
        Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.navigate = navigate;
        this.message = message;

        var worldTransform = Require<RectTransform>(parent, "Modular farm world");
        world = worldTransform.GetComponent<FarmWorldController>();
        if (world == null)
            throw new InvalidOperationException("Modular farm world needs FarmWorldController on the canvas.");
        var viewport = worldTransform.parent as RectTransform;
        if (viewport != null)
        {
            var scroll = viewport.GetComponent<ScrollRect>();
            if (scroll != null && scroll.content == null)
            {
                scroll.content = worldTransform;
                scroll.viewport = viewport;
            }
        }
        world.Initialize(state, message);

        farmLevel = Require<Text>(parent, "Farm level");
        levelProgress = Require<RectTransform>(parent, "Level progress");
        maxProgressWidth = levelProgress.sizeDelta.x > 0f ? levelProgress.sizeDelta.x : 192f;
        gold = Require<Text>(parent, "Gold");
        gems = Require<Text>(parent, "Gems");

        BindHit(parent, "Harvest", Harvest);
        BindHit(parent, "Open coin store", () => navigate(1));
        BindHit(parent, "Open gem store", () => navigate(1));

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            prices[i] = Require<Text>(parent, "Price " + i);
            levels[i] = Require<Text>(parent, "Upgrade level " + i);
            upgradeButtons[i] = BindHit(parent, "Buy upgrade " + i, () => Buy(index));
        }

        string[] tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabButtons[i] = BindHit(parent, tabs[i],
                () => navigate(index)).GetComponent<RectTransform>();
        }

        activeTab = Require<RectTransform>(parent, "Selected tab");
        selectedTabStart = activeTab.anchoredPosition;

        state.Changed += Refresh;
        Refresh();
    }

    static T Require<T>(Transform parent, string name) where T : Component
    {
        var t = FarmUIBootstrap.FindChildRecursive(parent, name);
        var component = t != null ? t.GetComponent<T>() : null;
        if (component == null)
            throw new InvalidOperationException("PixelFarmCanvas is missing " + name + " (" + typeof(T).Name + ").");
        return component;
    }

    static Button BindHit(Transform parent, string name, UnityEngine.Events.UnityAction action)
    {
        var btn = Require<Button>(parent, name);
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
        return btn;
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabButtons.Length || tabButtons[index] == null || tabButtons[0] == null)
            return;
        Vector3 offset = activeTab.parent.InverseTransformVector(
            tabButtons[index].position - tabButtons[0].position);
        activeTab.anchoredPosition = selectedTabStart + new Vector2(offset.x, offset.y);
    }

    void Harvest()
    {
        long earned = world.HarvestAll();
        message(earned > 0
            ? "+" + FarmGameState.Format(earned) + " monedas · +" + world.LastHarvestFood + " alimento"
            : "Nada listo para cosechar");
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
        levelProgress.sizeDelta = new Vector2(maxProgressWidth * state.FarmLevelProgress,
            levelProgress.sizeDelta.y);
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
