using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable panel layout for the four secondary tabs.</summary>
public sealed class FarmMenuScreen
{
    readonly FarmGameState state;
    readonly Action<int> navigate;
    readonly Action<string> message;
    readonly RectTransform overlay;
    int selectedTab;

    public FarmMenuScreen(Transform parent, FarmGameState state,
        Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.navigate = navigate;
        this.message = message;

        var existingOverlay = FarmUIBootstrap.FindChildRecursive(parent, "Secondary screen") as RectTransform;
        if (existingOverlay == null)
            throw new InvalidOperationException("PixelFarmCanvas is missing Secondary screen.");
        overlay = existingOverlay;
        if (!HasPrebuiltPages)
            throw new InvalidOperationException("PixelFarmCanvas is missing its menu pages.");
        overlay.gameObject.SetActive(false);
        state.Changed += Refresh;
    }

    bool HasPrebuiltPages => overlay != null &&
        (overlay.Find("Page - Tienda") != null ||
         overlay.Find("Page - Logros") != null ||
         overlay.Find("Page - Mejoras") != null ||
         overlay.Find("Page - Ajustes") != null);

    public void Show(int tab)
    {
        selectedTab = tab;
        overlay.gameObject.SetActive(tab != 0);
        if (tab != 0)
        {
            ShowPrebuilt(tab);
        }
    }

    void Refresh()
    {
        if (selectedTab != 0)
        {
            RefreshPrebuilt();
        }
    }

    void ShowPrebuilt(int tab)
    {
        var pStore = overlay.Find("Page - Tienda");
        var pAchievements = overlay.Find("Page - Logros");
        var pUpgrades = overlay.Find("Page - Mejoras");
        var pSettings = overlay.Find("Page - Ajustes");

        if (pStore != null) pStore.gameObject.SetActive(tab == 1);
        if (pAchievements != null) pAchievements.gameObject.SetActive(tab == 2);
        if (pUpgrades != null) pUpgrades.gameObject.SetActive(tab == 3);
        if (pSettings != null) pSettings.gameObject.SetActive(tab == 4);

        RefreshPrebuilt();
    }

    void RefreshPrebuilt()
    {
        switch (selectedTab)
        {
            case 1: RefreshPrebuiltStore(); break;
            case 2: RefreshPrebuiltAchievements(); break;
            case 3: RefreshPrebuiltUpgrades(); break;
            case 4: RefreshPrebuiltSettings(); break;
        }
    }

    void RefreshPrebuiltStore()
    {
        var p = overlay.Find("Page - Tienda");
        if (p == null) return;
        var bolsa = p.Find("Bolsa de monedas");
        if (bolsa != null)
        {
            BindActionButton(bolsa, () => message(state.BuyGold(10, 1500) ? "+1.5K monedas" : "Faltan gemas"));
        }
        var cofre = p.Find("Cofre de monedas");
        if (cofre != null)
        {
            BindActionButton(cofre, () => message(state.BuyGold(45, 10000) ? "+10K monedas" : "Faltan gemas"));
        }
        var gems = p.Find("More gems");
        if (gems != null)
        {
            BindActionButton(gems, () => navigate(2));
        }
    }

    void RefreshPrebuiltAchievements()
    {
        var p = overlay.Find("Page - Logros");
        if (p == null) return;
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var card = p.Find(FarmGameState.AchievementNames[i]);
            if (card == null) continue;
            var progress = card.Find("Progress")?.GetComponent<Text>();
            if (progress != null) progress.text = state.AchievementProgress(i);
            string caption = state.Claimed[i] ? "COBRADO" : state.AchievementReady(i) ? "COBRAR" : "PENDIENTE";
            var btn = BindActionButton(card, () => { if (state.ClaimAchievement(index)) message("Gemas recibidas"); });
            if (btn != null)
            {
                btn.interactable = state.AchievementReady(i);
                var captionText = FarmUIBootstrap.FindChildRecursive(card, "Caption")?.GetComponent<Text>();
                if (captionText != null) captionText.text = caption;
            }
        }
    }

    void RefreshPrebuiltUpgrades()
    {
        var p = overlay.Find("Page - Mejoras");
        if (p == null) return;
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var card = p.Find(FarmGameState.UpgradeNames[i]);
            if (card == null) continue;
            var level = card.Find("Level")?.GetComponent<Text>();
            if (level != null)
            {
                level.text = "Nivel " + state.Levels[i];
            }
            var price = card.Find("Price")?.GetComponent<Text>();
            if (price != null)
            {
                price.text = state.IsMaxed(i) ? "Nivel maximo" : FarmGameState.Format(state.Prices[i]) + " monedas";
            }
            var button = BindActionButton(card, () =>
                message(state.BuyUpgrade(index)
                    ? (index == 1 ? "Nuevo animal desbloqueado" : "Mejora comprada")
                    : "Faltan monedas"));
            if (button != null) button.interactable = !state.IsMaxed(i) && state.Gold >= state.Prices[i];
        }
    }

    void RefreshPrebuiltSettings()
    {
        var p = overlay.Find("Page - Ajustes");
        if (p == null) return;

        var music = p.Find("Musica");
        if (music != null)
        {
            BindActionButton(music, () =>
            {
                state.ToggleMusic();
                message("Musica " + (state.MusicEnabled ? "activada" : "apagada"));
                RefreshPrebuiltSettings();
            });
            var cap = FarmUIBootstrap.FindChildRecursive(music, "Caption")?.GetComponent<Text>();
            if (cap != null) cap.text = state.MusicEnabled ? "ACTIVADO" : "APAGADO";
        }

        var sound = p.Find("Sonidos");
        if (sound != null)
        {
            BindActionButton(sound, () =>
            {
                state.ToggleSound();
                message("Sonidos " + (state.SoundEnabled ? "activada" : "apagada"));
                RefreshPrebuiltSettings();
            });
            var cap = FarmUIBootstrap.FindChildRecursive(sound, "Caption")?.GetComponent<Text>();
            if (cap != null) cap.text = state.SoundEnabled ? "ACTIVADO" : "APAGADO";
        }
    }

    static Button BindActionButton(Transform container, UnityEngine.Events.UnityAction action)
    {
        var btn = container.GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
        return btn;
    }

}
