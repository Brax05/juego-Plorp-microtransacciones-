using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen (JUGAR / AJUSTES) and the loading screen shown before the farm.
/// Both use full-screen art from Resources/UI/Screens; buttons are invisible hit areas over the art.
/// </summary>
public sealed class StartScreens : MonoBehaviour
{
    const float Width = 570f, Height = 1018f;          // portrait canvas
    const float ArtWidth = 572f, ArtHeight = 1024f;    // source art
    const float LoadSeconds = 2.8f;

    static readonly string[] Tips =
    {
        "¿Sabías que a Plorp le encantan las cosechas?",
        "Pepito es un gato callejero. Plorp lo subió a la nave para que le traduzca.",
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
    float loadStarted = -1f;
    int tipIndex;
    float nextTip;

    public static StartScreens Create(Transform portrait, FarmGameState state, Action onFinished)
    {
        var root = new PixelUIFactory().Rect(portrait, "Start screens", 0, 0, Width, Height);
        root.SetAsLastSibling();
        var screens = root.gameObject.AddComponent<StartScreens>();
        screens.Build(state, onFinished);
        return screens;
    }

    // Converts coordinates measured on the 572x1024 art into the 570x1018 canvas.
    static float X(float v) => v * Width / ArtWidth;
    static float Y(float v) => v * Height / ArtHeight;

    void Build(FarmGameState gameState, Action finished)
    {
        state = gameState;
        onFinished = finished;
        ui = new PixelUIFactory();

        // ---------- Title ----------
        title = ui.Rect(transform, "Title screen", 0, 0, Width, Height);
        var titleArt = ui.Texture(title, "Art", "UI/Screens/title", 0, 0, Width, Height);
        titleArt.raycastTarget = true; // blocks the farm underneath
        ui.Label(title, "Version", "v0.4", 10, Height - 26, 120, 20, 12,
            new Color(1, 1, 1, .45f), TextAnchor.MiddleLeft);

        playGlow = ui.Block(title, "Play glow", X(140), Y(850), X(292), Y(56), new Color(1f, .95f, .6f, 0f));
        PressArea(title, "Jugar", X(133), Y(844), X(307), Y(69), StartLoading);
        PressArea(title, "Ajustes", X(188), Y(940), X(198), Y(52), OpenSettings);

        // ---------- Settings popup (from the title) ----------
        settings = ui.Rect(transform, "Title settings", 0, 0, Width, Height);
        ui.Backdrop(settings, "Backdrop", Width, Height, new Color(0f, 0f, .05f, .65f));
        var card = ui.Panel(settings, "Card", 55, 330, 460, 330, PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", "AJUSTES", 0, 22, 460, 44, 30, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        musicCaption = Toggle(card, "Música", 92, () => { state.ToggleMusic(); RefreshSettings(); });
        soundCaption = Toggle(card, "Sonidos", 162, () => { state.ToggleSound(); RefreshSettings(); });
        ui.ActionButton(card, "Volver", "VOLVER", 130, 244, 200, 56, () => settings.gameObject.SetActive(false));
        settings.gameObject.SetActive(false);

        // ---------- Loading ----------
        loading = ui.Rect(transform, "Loading screen", 0, 0, Width, Height);
        var loadingArt = ui.Texture(loading, "Art", "UI/Screens/loading", 0, 0, Width, Height);
        loadingArt.raycastTarget = true;

        dots = new Image[3][];
        float[] dotCenters = { 252f, 287f, 320f };
        for (int i = 0; i < 3; i++) dots[i] = Dot(loading, X(dotCenters[i]), Y(327f));

        ui.Block(loading, "Tip shade", X(70), Y(778), X(432), Y(80), new Color(.03f, .05f, .1f, .55f));
        tip = ui.Paragraph(loading, "Tip", "", X(80), Y(780), X(412), Y(76), 22, Color.white,
            TextAnchor.MiddleCenter);

        barWidth = X(464);
        barFill = ui.Rect(loading, "Bar fill", X(52), Y(869), 0, Y(36));
        var fill = barFill.gameObject.AddComponent<Image>();
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
        var image = ui.Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.color = Color.white;
        var button = image.gameObject.AddComponent<Button>();
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
        return new[]
        {
            ui.Block(parent, "Dot h", cx - 9, cy - 6, 18, 12, DotOff),
            ui.Block(parent, "Dot v", cx - 6, cy - 9, 12, 18, DotOff),
        };
    }

    void RefreshSettings()
    {
        musicCaption.text = state.MusicEnabled ? "ACTIVADO" : "APAGADO";
        soundCaption.text = state.SoundEnabled ? "ACTIVADO" : "APAGADO";
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
        if (title.gameObject.activeSelf)
        {
            var c = playGlow.color;
            c.a = .05f + .07f * (.5f + .5f * Mathf.Sin(now * 3f));
            playGlow.color = c;
        }

        if (loadStarted < 0f) return;

        float t = Mathf.Clamp01((now - loadStarted) / LoadSeconds);
        // Fast start, a short "thinking" pause, then a sprint to the end.
        float eased = t < .55f ? Mathf.SmoothStep(0f, .7f, t / .55f)
            : t < .75f ? .7f + .05f * ((t - .55f) / .2f)
            : Mathf.SmoothStep(.75f, 1f, (t - .75f) / .25f);
        barFill.sizeDelta = new Vector2(barWidth * eased, barFill.sizeDelta.y);

        int active = (int)(now / .3f) % 3;
        for (int i = 0; i < 3; i++)
            foreach (var part in dots[i]) part.color = i == active ? DotOn : DotOff;

        if (now >= nextTip && t < .9f) ShowTip();

        if (t >= 1f)
        {
            loadStarted = -1f;
            gameObject.SetActive(false);
            onFinished?.Invoke();
        }
    }
}
