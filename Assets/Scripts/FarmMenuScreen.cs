using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable panel layout for the four secondary tabs.</summary>
public sealed class FarmMenuScreen
{
    readonly FarmGameState state;
    readonly PixelUIFactory ui;
    readonly Action<int> navigate;
    readonly Action<string> message;
    readonly RectTransform overlay;
    RectTransform page;
    int selectedTab;

    public FarmMenuScreen(Transform parent, FarmGameState state, PixelUIFactory ui,
        Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.ui = ui;
        this.navigate = navigate;
        this.message = message;

        var existingOverlay = FarmUIBootstrap.FindChildRecursive(parent, "Secondary screen") as RectTransform;
        if (existingOverlay != null)
        {
            overlay = existingOverlay;
        }
        else
        {
            overlay = ui.Panel(parent, "Secondary screen", 0, 108, 570, 840, PixelUIFactory.Hex("#A75F3B"), true);
        }
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
            if (HasPrebuiltPages) ShowPrebuilt(tab);
            else Draw();
        }
    }

    void Refresh()
    {
        if (selectedTab != 0)
        {
            if (HasPrebuiltPages) RefreshPrebuilt();
            else Draw();
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
            BindActionButton(bolsa, () => message(state.BuyGold(10, 50000) ? "+50K monedas" : "Faltan gemas"));
        }
        var cofre = p.Find("Cofre de monedas");
        if (cofre != null)
        {
            BindActionButton(cofre, () => message(state.BuyGold(90, 500000) ? "+500K monedas" : "Faltan gemas"));
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
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            var card = p.Find(FarmGameState.UpgradeNames[i]);
            if (card == null) continue;
            var level = card.Find("Level")?.GetComponent<Text>();
            if (level != null)
            {
                level.text = i == 1 ? "Nivel " + state.Levels[i] + " · " + state.AnimalGroups + " c/u"
                                    : "Nivel " + state.Levels[i];
            }
            var price = card.Find("Price")?.GetComponent<Text>();
            if (price != null)
            {
                price.text = FarmGameState.Format(state.Prices[i]) + " monedas";
            }
            BindActionButton(card, () =>
                message(state.BuyUpgrade(index)
                    ? (index == 1 ? "Gallina, oveja y vaca nuevas" : "Mejora comprada")
                    : "Faltan monedas"));
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

    void Draw()
    {
        if (page != null)
        {
            page.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(page.gameObject);
        }
        page = ui.Rect(overlay, "Page contents", 0, 0, 570, 840);
        switch (selectedTab)
        {
            case 1: DrawStore(); break;
            case 2: DrawAchievements(); break;
            case 3: DrawUpgrades(); break;
            case 4: DrawSettings(); break;
        }
    }

    void Header(string title, string subtitle)
    {
        var header = ui.Panel(page, "Header", 16, 15, 538, 92, PixelUIFactory.Hex("#CE8854"));
        ui.Label(header, "Title", title.ToUpperInvariant(), 20, 10, 498, 43, 30, Color.white);
        ui.Label(header, "Subtitle", subtitle, 20, 53, 498, 26, 18, PixelUIFactory.Cream);
    }

    RectTransform Card(string name, float y, float height)
    {
        return ui.Panel(page, name, 20, y, 530, height, PixelUIFactory.Hex("#B77649"));
    }

    void DrawStore()
    {
        Header("Tienda", "Cambia gemas por monedas para la granja");
        StoreCard(122, "Bolsa de monedas", "+50K monedas", "10 gemas", 10, 50000);
        StoreCard(276, "Cofre de monedas", "+500K monedas", "90 gemas", 90, 500000);
        var card = Card("More gems", 436, 184);
        ui.Label(card, "Name", "MAS GEMAS", 20, 15, 400, 42, 26, Color.white);
        ui.Label(card, "Hint", "Completa logros para ganar gemas.", 20, 62, 488, 35, 20, PixelUIFactory.Cream);
        ui.ActionButton(card, "View achievements", "VER LOGROS", 20, 112, 220, 55, () => navigate(2));
        Footer("Las compras de esta demo se guardan automaticamente.");
    }

    void StoreCard(float y, string title, string reward, string cost, int gemCost, long goldAmount)
    {
        var card = Card(title, y, 138);
        ui.Label(card, "Name", title.ToUpperInvariant(), 18, 12, 310, 40, 24, Color.white);
        ui.Label(card, "Reward", reward, 18, 55, 270, 35, 22, PixelUIFactory.Gold);
        ui.Label(card, "Cost", cost, 18, 94, 240, 28, 18, PixelUIFactory.Cream);
        ui.ActionButton(card, "Buy " + title, "COMPRAR", 340, 43, 168, 57, () =>
            message(state.BuyGold(gemCost, goldAmount) ? reward : "Faltan gemas"));
    }

    void DrawAchievements()
    {
        Header("Logros", "Cumple objetivos y reclama gemas");
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float y = 122 + i * 176;
            var card = Card(FarmGameState.AchievementNames[i], y, 158);
            ui.Label(card, "Name", FarmGameState.AchievementNames[i].ToUpperInvariant(), 18, 12, 480, 42, 24, Color.white);
            ui.Label(card, "Progress", state.AchievementProgress(i), 18, 55, 250, 36, 21, PixelUIFactory.Cream);
            int reward = i == 0 ? 5 : i == 1 ? 20 : 30;
            ui.Label(card, "Reward", "+" + reward + " gemas", 18, 100, 210, 36, 22, PixelUIFactory.Gold);
            string caption = state.Claimed[i] ? "COBRADO" : state.AchievementReady(i) ? "COBRAR" : "PENDIENTE";
            ui.ActionButton(card, "Claim " + i, caption, 319, 87, 190, 54,
                () => { if (state.ClaimAchievement(index)) message("Gemas recibidas"); }, state.AchievementReady(i));
        }
        Footer("Los logros se actualizan al cosechar y mejorar.");
    }

    void DrawUpgrades()
    {
        Header("Mejoras", "Haz crecer tu produccion");
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            float y = 120 + i * 165;
            var card = Card(FarmGameState.UpgradeNames[i], y, 149);
            ui.Label(card, "Name", FarmGameState.UpgradeNames[i].ToUpperInvariant(), 18, 8, 480, 40, 24, Color.white);
            ui.Label(card, "Level", i == 1 ? "Nivel " + state.Levels[i] + " · " + state.AnimalGroups + " c/u"
                : "Nivel " + state.Levels[i], 18, 53, 220, 34, 21, PixelUIFactory.Cream);
            ui.Label(card, "Price", FarmGameState.Format(state.Prices[i]) + " monedas", 18, 96, 260, 35, 21, PixelUIFactory.Gold);
            ui.ActionButton(card, "Upgrade " + i, "MEJORAR", 329, 83, 180, 57, () =>
                message(state.BuyUpgrade(index)
                    ? (index == 1 ? "Gallina, oveja y vaca nuevas" : "Mejora comprada")
                    : "Faltan monedas"));
        }
    }

    void DrawSettings()
    {
        Header("Ajustes", "Preferencias de esta partida");
        SettingCard(124, "Musica", state.MusicEnabled, () => state.ToggleMusic());
        SettingCard(284, "Sonidos", state.SoundEnabled, () => state.ToggleSound());
        var card = Card("Saved game", 460, 190);
        ui.Label(card, "Title", "PARTIDA GUARDADA", 18, 15, 480, 42, 26, Color.white);
        ui.Label(card, "Description", "Monedas, gemas, mejoras y preferencias\nse guardan al cambiar.",
            18, 68, 490, 80, 20, PixelUIFactory.Cream);
        Footer("Estas opciones quedan listas para conectar audio real.");
    }

    void SettingCard(float y, string title, bool enabled, Action toggle)
    {
        var card = Card(title, y, 142);
        ui.Label(card, "Name", title.ToUpperInvariant(), 18, 17, 300, 43, 26, Color.white);
        ui.Label(card, "Description", "Preferencia de " + title.ToLowerInvariant(), 18, 66, 300, 32, 19,
            PixelUIFactory.Cream);
        ui.ActionButton(card, "Toggle " + title, enabled ? "ACTIVADO" : "APAGADO", 312, 43, 195, 57, () =>
        {
            toggle();
            message(title + (enabled ? " apagado" : " activado"));
        });
    }

    void Footer(string text)
    {
        ui.Label(page, "Footer", text, 30, 779, 510, 38, 16, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
    }
}
