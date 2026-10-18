using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pages that open over the farm inside "Secondary screen":
/// 1 Tienda, 2 Logros, 3 Jardinería, 4 Ajustes (authored in the canvas), 5 Casa de Plorp, 6 Cocina.
/// </summary>
public sealed class FarmMenuScreen
{
    // Pages of the first canvas version, replaced by the runtime-built ones (kept hidden; the bake removes them).
    public static readonly string[] LegacyPages = { "Page - Tienda", "Page - Logros", "Page - Mejoras" };

    readonly FarmGameState state;
    readonly Action<string> message;
    readonly RectTransform overlay;
    readonly Transform[] pages = new Transform[7];
    int selectedTab;

    public FarmMenuScreen(Transform portrait, FarmGameState state, Action<int> navigate, Action<string> message)
    {
        this.state = state;
        this.message = message;
        overlay = FarmUIBootstrap.FindChildRecursive(portrait, "Secondary screen") as RectTransform;
        if (overlay == null) throw new InvalidOperationException("Falta 'Secondary screen' en PixelFarmCanvas.");
        overlay.gameObject.SetActive(false);
        foreach (string legacy in LegacyPages)
        {
            var old = overlay.Find(legacy);
            if (old != null) old.gameObject.SetActive(false);
        }

        pages[1] = StoreScreen.Create(overlay, state, message).transform;
        pages[2] = AchievementsScreen.Create(overlay, state, message).transform;
        pages[3] = GardenScreen.Create(overlay, state, message).transform;
        pages[4] = overlay.Find("Page - Ajustes");
        pages[5] = HouseScreen.Create(overlay, portrait, state, message, navigate).transform;
        pages[6] = KitchenScreen.Create(overlay, state, message, () => navigate(5)).transform;
        BindSettings();
        state.Changed += () => { if (selectedTab == 4) RefreshSettings(); };
    }

    public void Show(int tab)
    {
        selectedTab = tab;
        overlay.gameObject.SetActive(tab != 0);
        for (int i = 1; i < pages.Length; i++)
        {
            if (pages[i] == null) continue;
            pages[i].gameObject.SetActive(i == tab);
            if (i == tab) pages[i].SetAsLastSibling();
        }
        if (tab == 4) RefreshSettings();
    }

    // ---------- Ajustes (authored in the canvas) ----------

    void BindSettings()
    {
        var page = pages[4];
        if (page == null) return;
        Bind(page.Find("Musica"), () =>
        {
            state.ToggleMusic();
            message("Música " + (state.MusicEnabled ? "activada" : "apagada"));
            RefreshSettings();
        });
        Bind(page.Find("Sonidos"), () =>
        {
            state.ToggleSound();
            message("Sonidos " + (state.SoundEnabled ? "activados" : "apagados"));
            RefreshSettings();
        });
    }

    void RefreshSettings()
    {
        var page = pages[4];
        if (page == null) return;
        SetCaption(page.Find("Musica"), state.MusicEnabled);
        SetCaption(page.Find("Sonidos"), state.SoundEnabled);
    }

    static void Bind(Transform card, UnityEngine.Events.UnityAction action)
    {
        var button = card != null ? card.GetComponentInChildren<Button>(true) : null;
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    static void SetCaption(Transform card, bool on)
    {
        var caption = card != null ? FarmUIBootstrap.FindChildRecursive(card, "Caption")?.GetComponent<Text>() : null;
        if (caption != null) caption.text = on ? "ACTIVADO" : "APAGADO";
    }
}
