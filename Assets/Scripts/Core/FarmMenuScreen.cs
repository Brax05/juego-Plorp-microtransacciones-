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
        var musicaCard = page.Find("Musica");
        var sonidosCard = page.Find("Sonidos");

        Bind(musicaCard, "Toggle Musica hit", () =>
        {
            state.ToggleMusic();
            message("Música " + (state.MusicEnabled ? "activada" : "apagada"));
            RefreshSettings();
        });
        Bind(sonidosCard, "Toggle Sonidos hit", () =>
        {
            state.ToggleSound();
            message("Sonidos " + (state.SoundEnabled ? "activados" : "apagados"));
            RefreshSettings();
        });

        BuildVolumeRow(musicaCard,
            () => state.MusicVolume,
            () => state.MusicEnabled,
            v => state.SetMusicVolume(v, false),
            delta => state.StepMusicVolume(delta));

        BuildVolumeRow(sonidosCard,
            () => state.SoundVolume,
            () => state.SoundEnabled,
            v => { state.SetSoundVolume(v, false); GameAudio.Play("sfx_ui_click", .7f, 0f, .06f); },
            delta => { state.StepSoundVolume(delta); GameAudio.Play("sfx_ui_click", .7f, 0f, .06f); });
    }

    void BuildVolumeRow(Transform card, Func<float> getVolume, Func<bool> isEnabled,
        Action<float> setVolume, Action<float> stepVolume)
    {
        if (card == null) return;

        // Reuse the row if the canvas already has it (after baking), but always rewire the slider and
        // buttons: the bake removes the scripts, and the onClick listeners are not saved in the prefab.
        var ui = new PixelUIFactory();
        var row = ui.Rect(card, "Volume row", 18, 100, 494, 34);

        ui.Label(row, "Label", "VOLUMEN", 0, 4, 82, 24, 13, PixelUIFactory.Gold, TextAnchor.MiddleLeft);

        ui.ActionButton(row, "Minus", "-", 86, 1, 36, 30, () =>
        {
            stepVolume(-0.1f);
            RefreshSettings();
        });
        UiUtil.CaptionSize(row, "Minus", 20);

        var track = ui.Panel(row, "Track", 128, 3, 230, 26, PixelUIFactory.Hex("#221316"), true);
        var trackImg = track.GetComponent<Image>();
        if (trackImg != null) trackImg.raycastTarget = true;

        var groove = ui.Block(track, "Groove", 2, 2, 226, 22, PixelUIFactory.Hex("#140A0C"));
        groove.raycastTarget = false;

        float vol = isEnabled() ? getVolume() : 0f;
        var fill = ui.Block(groove.transform, "Fill", 0, 0, 226f * Mathf.Clamp01(vol), 22,
            isEnabled() ? PixelUIFactory.Green : PixelUIFactory.Disabled);
        fill.raycastTarget = false;

        var shine = ui.Block(fill.transform, "Shine", 0, 2, fill.rectTransform.sizeDelta.x, 3,
            isEnabled() ? PixelUIFactory.Hex("#8FE86A") : PixelUIFactory.Hex("#8A7A70"));
        shine.raycastTarget = false;

        var slider = track.gameObject.GetOrAdd<PixelSlider>();
        slider.ValueChanged = frac =>
        {
            setVolume(frac);
            RefreshSettings();
        };
        slider.ValueCommitted = _ => state.CommitVolumes();

        ui.ActionButton(row, "Plus", "+", 364, 1, 36, 30, () =>
        {
            stepVolume(0.1f);
            RefreshSettings();
        });
        UiUtil.CaptionSize(row, "Plus", 20);

        ui.Label(row, "Percent", (Mathf.RoundToInt((isEnabled() ? getVolume() : 0f) * 100f)) + "%",
            406, 4, 62, 24, 14, Color.white, TextAnchor.MiddleCenter);
    }

    void RefreshSettings()
    {
        var page = pages[4];
        if (page == null) return;
        var musicaCard = page.Find("Musica");
        var sonidosCard = page.Find("Sonidos");

        SetCaption(musicaCard, state.MusicEnabled);
        SetCaption(sonidosCard, state.SoundEnabled);

        RefreshVolumeRow(musicaCard, state.MusicEnabled, state.MusicVolume);
        RefreshVolumeRow(sonidosCard, state.SoundEnabled, state.SoundVolume);

        SaveNotice.Refresh();
    }

    static void RefreshVolumeRow(Transform card, bool on, float volume)
    {
        if (card == null) return;
        var row = card.Find("Volume row");
        if (row == null) return;

        var fill = row.Find("Track/Groove/Fill") as RectTransform;
        var fillImg = fill != null ? fill.GetComponent<Image>() : null;
        var shine = fill != null ? fill.Find("Shine") as RectTransform : null;
        var shineImg = shine != null ? shine.GetComponent<Image>() : null;
        var percent = row.Find("Percent")?.GetComponent<Text>();

        float effective = on ? volume : 0f;
        float w = Mathf.Clamp01(effective) * 226f;

        if (fill != null) fill.sizeDelta = new Vector2(w, 22f);
        if (shine != null) shine.sizeDelta = new Vector2(w, 3f);
        if (fillImg != null) fillImg.color = on && volume > 0.001f ? PixelUIFactory.Green : PixelUIFactory.Disabled;
        if (shineImg != null) shineImg.color = on && volume > 0.001f ? PixelUIFactory.Hex("#8FE86A") : PixelUIFactory.Hex("#8A7A70");
        if (percent != null) percent.text = on ? (Mathf.RoundToInt(volume * 100f) + "%") : "APAGADO";
    }

    static void Bind(Transform card, string buttonName, UnityEngine.Events.UnityAction action)
    {
        var button = card != null ? card.Find(buttonName)?.GetComponent<Button>() : null;
        if (button == null && card != null) button = card.GetComponentInChildren<Button>(true);
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
