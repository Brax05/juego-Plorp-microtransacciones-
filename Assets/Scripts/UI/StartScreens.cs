using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen (JUGAR / AJUSTES) and the loading screen shown before the farm.
/// Both use full-screen art from Art/Screens (title.png, loading.png); buttons are invisible hit areas over the art.
/// </summary>
public sealed class StartScreens : MonoBehaviour
{
    const float Width = 570f, CanvasH = 1018f;          // portrait canvas
    const float ArtWidth = 572f, ArtHeight = 1024f;    // source art
    const float LoadSeconds = 2.8f;

    static float Height => ScreenLayout.Height;
    /// <summary>Dark band above/below the 570x1018 art on a taller phone.</summary>
    static float Band => Mathf.Max(0f, (Height - CanvasH) * .5f);

    static readonly string[] Tips =
    {
        "¿Sabías que a Plorp le encantan las cosechas?",
        "Pepito es un gato. Plorp lo subió a la nave para que le traduzca.",
        "Plorp solo dice \"plorp\". Pepito jura que lo entiende.",
        "Los cultivos siguen creciendo aunque cierres el juego.",
        "Junta alimento y animales hasta que Plorp quede conforme.",
        "Si Pepito dice que Plorp tiene hambre, cómprale algo. Hay un logro.",
        "\"Zep zep zep\" significa hambre. O eso cree Pepito.",
    };

    static readonly Color DotOn = PixelUIFactory.Hex("#F2D04B");
    static readonly Color DotOff = PixelUIFactory.Hex("#7A4A2E");

    FarmGameState state;
    Action onFinished;
    PixelUIFactory ui;
    RectTransform title, loading, settings;
    Image playGlow;
    RectTransform barFill;
    float barWidth;
    Text tip;
    Image[][] dots;
    Text musicCaption, soundCaption;
    RectTransform musicFill, soundFill, musicShine, soundShine;
    Image musicFillImg, soundFillImg, musicShineImg, soundShineImg;
    Text musicPercent, soundPercent;
    float loadStarted = -1f;
    int tipIndex;
    float nextTip;

    public static StartScreens Create(Transform portrait, FarmGameState state, Action onFinished)
    {
        var root = new PixelUIFactory().Rect(portrait, "Start screens", 0, 0, Width, Height);
        root.sizeDelta = new Vector2(Width, Height); // reused from the canvas: follow the screen height
        root.gameObject.SetActive(true); // saved hidden in the canvas
        root.SetAsLastSibling();
        var screens = root.gameObject.GetOrAdd<StartScreens>();
        screens.Build(state, onFinished);
        return screens;
    }

    // Converts coordinates measured on the 572x1024 art into the 570x1018 canvas (centred on a taller phone).
    static float X(float v) => v * Width / ArtWidth;
    static float Y(float v) => Band + v * CanvasH / ArtHeight;

    /// <summary>Geometry is applied explicitly: the canvas may already contain a baked copy of this screen.</summary>
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    void Build(FarmGameState gameState, Action finished)
    {
        state = gameState;
        onFinished = finished;
        ui = new PixelUIFactory();

        // ---------- Title ----------
        title = ui.Rect(transform, "Title screen", 0, 0, Width, Height);
        Place(title, 0, 0, Width, Height);
        if (Band > 0f)
        {
            var titleTop = ui.Block(title, "Top band", 0, 0, Width, Band, PixelUIFactory.Hex("#0E0D2C"));
            var titleBottom = ui.Block(title, "Bottom band", 0, Band + CanvasH, Width, Band, PixelUIFactory.Hex("#0E2230"));
            Place(titleTop.rectTransform, 0, 0, Width, Band);
            Place(titleBottom.rectTransform, 0, Band + CanvasH, Width, Band);
            titleTop.raycastTarget = titleBottom.raycastTarget = true;
        }
        var titleArt = ui.Texture(title, "Art", "title", 0, Band, Width, CanvasH);
        Place(titleArt.rectTransform, 0, Band, Width, CanvasH);
        titleArt.raycastTarget = true; // blocks the farm underneath
        var version = ui.Label(title, "Version", "v0.4", 10, Band + CanvasH - 26, 120, 20, 12,
            new Color(1, 1, 1, .45f), TextAnchor.MiddleLeft);
        Place(version.rectTransform, 10, Band + CanvasH - 26, 120, 20);

        playGlow = ui.Block(title, "Play glow", X(140), Y(850), X(292), Y(56), new Color(1f, .95f, .6f, 0f));
        Place(playGlow.rectTransform, X(140), Y(850), X(292), Y(56));
        PressArea(title, "Jugar", X(133), Y(844), X(307), Y(69), StartLoading);
        PressArea(title, "Ajustes", X(188), Y(940), X(198), Y(52), OpenSettings);

        // ---------- Settings popup (from the title) ----------
        settings = ui.Rect(transform, "Title settings", 0, 0, Width, Height);
        Place(settings, 0, 0, Width, Height);
        var settingsShade = ui.Backdrop(settings, "Backdrop", Width, Height, new Color(0f, 0f, .05f, .65f));
        Place(settingsShade.rectTransform, 0, 0, Width, Height);
        // 260 in the reference portrait; centred when the screen is taller.
        float cardY = 260f + ScreenLayout.Extra * .5f;
        var card = ui.Panel(settings, "Card", 45, cardY, 480, 460, PixelUIFactory.Hex("#CE8854"), true);
        Place(card, 45, cardY, 480, 460);
        ui.Label(card, "Title", "AJUSTES", 0, 20, 480, 44, 30, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        musicCaption = Toggle(card, "Música", 74, () => { state.ToggleMusic(); RefreshSettings(); });
        BuildVolumeRow(card, "MusicaVol", 136, () => state.MusicVolume, () => state.MusicEnabled,
            v => state.SetMusicVolume(v, false), delta => state.StepMusicVolume(delta),
            out musicFill, out musicShine, out musicFillImg, out musicShineImg, out musicPercent);

        soundCaption = Toggle(card, "Sonidos", 188, () => { state.ToggleSound(); RefreshSettings(); });
        BuildVolumeRow(card, "SonidosVol", 250, () => state.SoundVolume, () => state.SoundEnabled,
            v => { state.SetSoundVolume(v, false); GameAudio.Play("sfx_ui_click", .7f, 0f, .06f); },
            delta => { state.StepSoundVolume(delta); GameAudio.Play("sfx_ui_click", .7f, 0f, .06f); },
            out soundFill, out soundShine, out soundFillImg, out soundShineImg, out soundPercent);

        ui.ActionButton(card, "Volver", "VOLVER", 140, 380, 200, 54, () => settings.gameObject.SetActive(false));
        settings.gameObject.SetActive(false);

        // ---------- Loading ----------
        loading = ui.Rect(transform, "Loading screen", 0, 0, Width, Height);
        Place(loading, 0, 0, Width, Height);
        if (Band > 0f)
        {
            var loadTop = ui.Block(loading, "Top band", 0, 0, Width, Band, PixelUIFactory.Hex("#0E0F2E"));
            var loadBottom = ui.Block(loading, "Bottom band", 0, Band + CanvasH, Width, Band, PixelUIFactory.Hex("#0D2533"));
            Place(loadTop.rectTransform, 0, 0, Width, Band);
            Place(loadBottom.rectTransform, 0, Band + CanvasH, Width, Band);
            loadTop.raycastTarget = loadBottom.raycastTarget = true;
        }
        var loadingArt = ui.Texture(loading, "Art", "loading", 0, Band, Width, CanvasH);
        Place(loadingArt.rectTransform, 0, Band, Width, CanvasH);
        loadingArt.raycastTarget = true;

        dots = new Image[3][];
        float[] dotCenters = { 252f, 287f, 320f };
        for (int i = 0; i < 3; i++) dots[i] = Dot(loading, X(dotCenters[i]), Y(327f));

        var tipShade = ui.Block(loading, "Tip shade", X(70), Y(778), X(432), Y(80),
            new Color(.03f, .05f, .1f, .55f));
        Place(tipShade.rectTransform, X(70), Y(778), X(432), Y(80));
        tip = ui.Paragraph(loading, "Tip", "", X(80), Y(780), X(412), Y(76), 22, Color.white,
            TextAnchor.MiddleCenter);
        Place(tip.rectTransform, X(80), Y(780), X(412), Y(76));

        barWidth = X(464);
        barFill = ui.Rect(loading, "Bar fill", X(52), Y(869), 0, Y(36));
        Place(barFill, X(52), Y(869), 0, Y(36));
        var fill = barFill.gameObject.GetOrAdd<Image>();
        fill.color = PixelUIFactory.Hex("#4FC63F");
        fill.raycastTarget = false;
        var shine = ui.Block(barFill, "Shine", 0, 4, 0, 8, PixelUIFactory.Hex("#8FE86A"));
        shine.rectTransform.anchorMax = new Vector2(1, 1); // stretch with the fill
        shine.rectTransform.sizeDelta = new Vector2(0, 8);
        loading.gameObject.SetActive(false);

        RefreshSettings();
    }

    void PressArea(Transform parent, string name, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
    {
        var area = ui.Rect(parent, name, x, y, w, h);
        Place(area, x, y, w, h);
        var image = area.gameObject.GetOrAdd<Image>();
        image.color = Color.white;
        var button = image.gameObject.GetOrAdd<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(1, 1, 1, 0f);
        colors.highlightedColor = new Color(1, 1, 1, .08f);
        colors.pressedColor = new Color(1, 1, 1, .28f);
        colors.selectedColor = new Color(1, 1, 1, 0f);
        colors.fadeDuration = .06f;
        button.colors = colors;
        button.onClick.AddListener(action);
    }

    Text Toggle(Transform card, string label, float y, UnityEngine.Events.UnityAction action)
    {
        ui.Label(card, label + " name", label.ToUpperInvariant(), 34, y, 200, 56, 24, Color.white);
        var panel = ui.Panel(card, label + " toggle", 240, y, 186, 56, PixelUIFactory.Green);
        var caption = ui.Label(panel, "Caption", "", 4, 2, 178, 52, 20, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(card, label + " hit", 240, y, 186, 56, action);
        return caption;
    }

    Image[] Dot(Transform parent, float cx, float cy)
    {
        // Rounded pixel dot made of two crossed blocks; covers the dot painted in the art.
        var h = ui.Block(parent, "Dot h", cx - 9, cy - 6, 18, 12, DotOff);
        var v = ui.Block(parent, "Dot v", cx - 6, cy - 9, 12, 18, DotOff);
        Place(h.rectTransform, cx - 9, cy - 6, 18, 12);
        Place(v.rectTransform, cx - 6, cy - 9, 12, 18);
        return new[] { h, v };
    }

    void RefreshSettings()
    {
        musicCaption.text = state.MusicEnabled ? "ACTIVADO" : "APAGADO";
        soundCaption.text = state.SoundEnabled ? "ACTIVADO" : "APAGADO";

        RefreshVolumeDisplay(musicFill, musicShine, musicFillImg, musicShineImg, musicPercent,
            state.MusicEnabled, state.MusicVolume);
        RefreshVolumeDisplay(soundFill, soundShine, soundFillImg, soundShineImg, soundPercent,
            state.SoundEnabled, state.SoundVolume);
    }

    static void RefreshVolumeDisplay(RectTransform fill, RectTransform shine, Image fillImg, Image shineImg,
        Text percent, bool on, float volume)
    {
        float effective = on ? volume : 0f;
        float w = Mathf.Clamp01(effective) * 192f;
        if (fill != null) fill.sizeDelta = new Vector2(w, 22f);
        if (shine != null) shine.sizeDelta = new Vector2(w, 3f);
        if (fillImg != null) fillImg.color = on && volume > 0.001f ? PixelUIFactory.Green : PixelUIFactory.Disabled;
        if (shineImg != null) shineImg.color = on && volume > 0.001f ? PixelUIFactory.Hex("#8FE86A") : PixelUIFactory.Hex("#8A7A70");
        if (percent != null) percent.text = on ? (Mathf.RoundToInt(volume * 100f) + "%") : "APAGADO";
    }

    void BuildVolumeRow(Transform card, string name, float y, Func<float> getVolume, Func<bool> isEnabled,
        Action<float> setVolume, Action<float> stepVolume,
        out RectTransform fillOut, out RectTransform shineOut, out Image fillImgOut, out Image shineImgOut, out Text percentOut)
    {
        var row = ui.Rect(card, name, 24, y, 432, 34);
        ui.Label(row, "Label", "VOLUMEN", 0, 4, 80, 24, 13, PixelUIFactory.Gold, TextAnchor.MiddleLeft);

        ui.ActionButton(row, "Minus", "-", 84, 1, 36, 30, () =>
        {
            stepVolume(-0.1f);
            RefreshSettings();
        });
        UiUtil.CaptionSize(row, "Minus", 20);

        var track = ui.Panel(row, "Track", 126, 3, 196, 26, PixelUIFactory.Hex("#221316"), true);
        var trackImg = track.GetComponent<Image>();
        if (trackImg != null) trackImg.raycastTarget = true;

        var groove = ui.Block(track, "Groove", 2, 2, 192, 22, PixelUIFactory.Hex("#140A0C"));
        groove.raycastTarget = false;

        float vol = isEnabled() ? getVolume() : 0f;
        var fill = ui.Block(groove.transform, "Fill", 0, 0, 192f * Mathf.Clamp01(vol), 22,
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

        ui.ActionButton(row, "Plus", "+", 328, 1, 36, 30, () =>
        {
            stepVolume(0.1f);
            RefreshSettings();
        });
        UiUtil.CaptionSize(row, "Plus", 20);

        var percent = ui.Label(row, "Percent", (Mathf.RoundToInt((isEnabled() ? getVolume() : 0f) * 100f)) + "%",
            368, 4, 60, 24, 14, Color.white, TextAnchor.MiddleCenter);

        fillOut = fill.rectTransform;
        shineOut = shine.rectTransform;
        fillImgOut = fill;
        shineImgOut = shine;
        percentOut = percent;
    }

    void OpenSettings()
    {
        RefreshSettings();
        settings.gameObject.SetActive(true);
        settings.SetAsLastSibling();
    }

    void StartLoading()
    {
        title.gameObject.SetActive(false);
        settings.gameObject.SetActive(false);
        loading.gameObject.SetActive(true);
        loadStarted = Time.unscaledTime;
        tipIndex = UnityEngine.Random.Range(0, Tips.Length);
        ShowTip();
    }

    void ShowTip()
    {
        tip.text = Tips[tipIndex % Tips.Length].ToUpperInvariant();
        tipIndex++;
        nextTip = Time.unscaledTime + 1.5f;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (title != null && title.gameObject.activeSelf)
        {
            if (playGlow != null)
            {
                var c = playGlow.color;
                c.a = .05f + .07f * (.5f + .5f * Mathf.Sin(now * 3f));
                playGlow.color = c;
            }
        }

        if (loadStarted < 0f) return;

        float t = Mathf.Clamp01((now - loadStarted) / LoadSeconds);
        // Fast start, a short "thinking" pause, then a sprint to the end.
        float eased = t < .55f ? Mathf.SmoothStep(0f, .7f, t / .55f)
            : t < .75f ? .7f + .05f * ((t - .55f) / .2f)
            : Mathf.SmoothStep(.75f, 1f, (t - .75f) / .25f);
        if (barFill != null)
            barFill.sizeDelta = new Vector2(barWidth * eased, barFill.sizeDelta.y);

        int active = (int)(now / .3f) % 3;
        if (dots != null)
        {
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                foreach (var part in dots[i]) if (part != null) part.color = i == active ? DotOn : DotOff;
            }
        }

        if (now >= nextTip && t < .9f) ShowTip();

        if (t >= 1f)
        {
            loadStarted = -1f;
            gameObject.SetActive(false);
            onFinished?.Invoke();
        }
    }
}
