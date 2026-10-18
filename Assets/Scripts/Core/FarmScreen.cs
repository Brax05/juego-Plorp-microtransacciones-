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
    readonly Color[] priceColors = new Color[3];
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

        // The "TOCAR PARA COSECHAR" bar is replaced by golden tickets over each field/animal,
        // and its space now belongs to the farm view.
        var harvestButton = FarmUIBootstrap.FindChildRecursive(parent, "Harvest");
        if (harvestButton != null) harvestButton.gameObject.SetActive(false);
        var harvestBar = FarmUIBootstrap.FindChildRecursive(parent, "Harvest bar");
        if (harvestBar != null) harvestBar.gameObject.SetActive(false);
        BindHit(parent, "Open coin store", () => { StoreScreen.PendingTab = 0; navigate(1); });
        BindHit(parent, "Open gem store", () => { StoreScreen.PendingTab = 1; navigate(1); });

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            prices[i] = Require<Text>(parent, "Price " + i);
            priceColors[i] = prices[i].color;
            levels[i] = Require<Text>(parent, "Upgrade level " + i);
            upgradeButtons[i] = BindHit(parent, "Buy upgrade " + i, () => Buy(index));
            BuildTierInfo(parent, i);
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

    void Buy(int index)
    {
        if (state.IsMaxed(index))
        {
            message("Mejora al maximo");
            return;
        }
        message(UpgradeMessage(state, index, state.BuyUpgrade(index)));
    }

    /// <summary>Feedback after a purchase, celebrating every 10th one (new crop / new animal / x2).</summary>
    public static string UpgradeMessage(FarmGameState state, int index, bool bought)
    {
        if (!bought)
            return "Faltan " + FarmGameState.Format(Math.Max(0, state.Prices[index] - state.Gold)) + " monedas para " +
                   FarmGameState.UpgradeNames[index].ToLowerInvariant();
        int level = state.Levels[index];
        bool tierUp = index != FarmEconomy.Workers &&
                      (index == FarmEconomy.Animals ? level % FarmEconomy.LevelsPerTier == 1
                                                     : FarmEconomy.TierProgress(level) == 0);
        if (index == FarmEconomy.Workers)
            return "Trabajadores Nv." + level + ": +" + (level * 5) + "% ganancias";
        if (!tierUp) return FarmGameState.UpgradeNames[index] + " Nv." + level;
        if (index == FarmEconomy.Seeds)
        {
            int tier = FarmEconomy.Tier(level);
            return tier < FarmEconomy.CropTiers.Length
                ? "¡Nuevo cultivo: " + FarmEconomy.CropName(FarmEconomy.CropTiers[tier]) + "!"
                : "¡Cultivos x2!";
        }
        int species = FarmEconomy.Tier(level);
        return species < FarmEconomy.Species.Length
            ? "¡Nuevo animal: " + FarmEconomy.SpeciesNames[species] + "!"
            : "¡Animales x2!";
    }

    // Under each upgrade row: what it gives now and a 10-step bar to the next tier.
    readonly Text[] tierLabels = new Text[3];
    readonly Image[][] tierSteps = new Image[3][];
    static readonly Color StepOn = PixelUIFactory.Hex("#FFD44F");
    static readonly Color StepOff = PixelUIFactory.Hex("#4A2A22");

    void BuildTierInfo(Transform parent, int index)
    {
        var ui = new PixelUIFactory();
        var row = FarmUIBootstrap.FindChildRecursive(parent, "Upgrade Row " + index) ?? parent;
        float top = 634f + index * 104f;
        // "TRABAJADORES" is a long name, so its line goes lower, where the bar would be.
        float labelY = index == FarmEconomy.Workers ? top + 48 : top + 28;
        tierLabels[index] = ui.Paragraph(row, "Tier label " + index, "", 262, labelY, 136, 22, 12, PixelUIFactory.Cream);
        if (index == FarmEconomy.Workers) return;
        tierSteps[index] = new Image[FarmEconomy.LevelsPerTier];
        for (int s = 0; s < FarmEconomy.LevelsPerTier; s++)
            tierSteps[index][s] = ui.Block(row, "Tier step " + s, 262 + s * 13, top + 54, 11, 9, StepOff);
    }

    void RefreshTierInfo(int index)
    {
        string next = FarmEconomy.NextTierReward(state, index);
        tierLabels[index].text = FarmEconomy.CurrentLabel(state, index);
        if (tierSteps[index] == null) return;
        int level = state.Levels[index];
        // Animals start at 0, so their first tier fills with purchases 1..10 like the others.
        // Seeds start at level 1 (bar fills with the next 10 purchases); animals start at 0
        // and the first purchase of a tier brings the new species, so it counts as a step.
        int done = index == FarmEconomy.Animals
            ? (level == 0 ? 0 : (level - 1) % FarmEconomy.LevelsPerTier + 1)
            : FarmEconomy.TierProgress(level);
        for (int s = 0; s < tierSteps[index].Length; s++)
            tierSteps[index][s].color = s < done ? StepOn : StepOff;
        if (!string.IsNullOrEmpty(next)) tierLabels[index].text += "  → " + next;
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
            // Red price when you can't afford it yet (the button still answers with how much is missing).
            prices[i].color = state.IsMaxed(i) || state.Gold >= state.Prices[i] ? priceColors[i] : PixelUIFactory.Hex("#FF7B6B");
            levels[i].text = "Nv." + state.Levels[i];
            RefreshTierInfo(i);
            // Always tappable: a disabled button felt broken ("no se puede comprar") when coins were short.
            upgradeButtons[i].interactable = !state.IsMaxed(i);
        }
    }
}
