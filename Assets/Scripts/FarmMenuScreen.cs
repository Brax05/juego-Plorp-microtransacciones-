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
        overlay = ui.Panel(parent, "Secondary screen", 0, 108, 570, 840, PixelUIFactory.Hex("#A75F3B"), true);
        overlay.gameObject.SetActive(false);
        state.Changed += Refresh;
    }

    public void Show(int tab)
    {
        selectedTab = tab;
        overlay.gameObject.SetActive(tab != 0);
        if (tab != 0) Draw();
    }

    void Refresh()
    {
        if (selectedTab != 0) Draw();
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
