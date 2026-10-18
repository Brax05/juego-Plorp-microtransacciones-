using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pepito, the gray street cat who speaks for Plorp. Slides in from the left every so often to
/// translate Plorp's advice or to ask for a snack FOR PLORP with a random price (coins or gems).
/// Buying the first snack unlocks an achievement.
/// Tap Pepito for another tip; tap the bubble to send him away.
/// </summary>
public sealed class PepitoController : MonoBehaviour
{
    const float Top = 512f;            // bottom of the farm view, just above the upgrades
    const float AdviceSeconds = 9f;
    const float RequestSeconds = 20f;
    const float RequestCooldown = 90f;

    static readonly string[] Snacks = { "una lata de atún", "un platito de leche", "tres sardinas",
        "croquetas", "un huevo frito", "un pescado entero" };
    static readonly Color Bubble = PixelUIFactory.Hex("#F6E7C8");
    static readonly Color Slate = PixelUIFactory.Hex("#4B5160");

    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    RectTransform root;
    CanvasGroup group;
    SpriteStripAnimator face;
    Text bubbleText;
    RectTransform buttonsRow;
    Text payCaption;
    Image payFace;

    bool started;
    bool visible;
    float slide;                     // 0 hidden, 1 shown
    float hideAt;
    float nextVisit;
    float lastRequest = -999f;
    string lastAdvice;
    bool asking;
    long askGold;
    int askGems;

    public static PepitoController Create(Transform portrait, int siblingIndex, FarmGameState state,
        Action<string> message)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(portrait, "Pepito", 0, Top, 570, 118);
        float authoredY = rect.anchoredPosition.y; // the height edited in the canvas is kept
        rect.SetSiblingIndex(siblingIndex);
        var pepito = rect.gameObject.GetOrAdd<PepitoController>();
        pepito.Build(ui, rect, state, message);
        pepito.baseY = authoredY;
        return pepito;
    }

    void Build(PixelUIFactory factory, RectTransform rect, FarmGameState gameState, Action<string> showMessage)
    {
        ui = factory;
        root = rect;
        state = gameState;
        message = showMessage;

        // Portrait with a dark frame, and the name plate underneath.
        ui.Panel(root, "Portrait frame", 4, 0, 90, 88, Slate);
        face = SpriteStripAnimator.Create(root, "Pepito face", "pepito_talk", 11, 7, 76);
        ui.Hit(root, "Pepito tap", 4, 0, 90, 88, NextAdvice);
        var plate = ui.Panel(root, "Name plate", 2, 88, 94, 28, PixelUIFactory.Hex("#8A8F9C"));
        ui.Label(plate, "Name", "PEPITO", 0, 0, 94, 28, 15, Color.white, TextAnchor.MiddleCenter);

        // Speech bubble with a small tail pointing at Pepito.
        ui.Block(root, "Bubble tail", 92, 34, 10, 16, PixelUIFactory.Ink);
        ui.Block(root, "Bubble tail fill", 95, 37, 10, 10, Bubble);
        var bubble = ui.Rect(root, "Bubble", 100, 2, 462, 112);
        var bg = bubble.gameObject.GetOrAdd<Image>();
        bg.color = PixelUIFactory.Ink;
        ui.Block(bubble, "Fill", 3, 3, 456, 106, Bubble);
        var tap = bubble.gameObject.GetOrAdd<Button>();
        tap.targetGraphic = bg;
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(() => { if (!asking) Hide(); });

        bubbleText = ui.Paragraph(bubble, "Text", "", 14, 8, 436, 60, 18, PixelUIFactory.Ink);
        var outline = bubbleText.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        buttonsRow = ui.Rect(bubble, "Request buttons", 12, 68, 440, 38);
        var pay = ui.Panel(buttonsRow, "Pay", 0, 0, 260, 38, PixelUIFactory.Green);
        payFace = pay.Find("Face").GetComponent<Image>();
        payCaption = ui.Label(pay, "Caption", "", 4, 0, 252, 38, 15, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(buttonsRow, "Pay hit", 0, 0, 260, 38, Feed);
        var no = ui.Panel(buttonsRow, "No", 270, 0, 168, 38, PixelUIFactory.Hex("#67594F"));
        ui.Label(no, "Caption", "AHORA NO", 4, 0, 160, 38, 15, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(buttonsRow, "No hit", 270, 0, 168, 38, Decline);

        // Stays active (so Update keeps the visit timer running); hidden through the CanvasGroup.
        group = root.gameObject.GetOrAdd<CanvasGroup>();
        state.Changed += RefreshPayButton;
        ApplySlide();
    }

    /// <summary>Starts Pepito's visits (after the loading screen).</summary>
    public void Begin(bool sayIntro = true)
    {
        if (started) return;
        started = true;
        lastAdvice = "intro";
        if (!sayIntro)
        {
            // He just gave the welcome tour: first visit a bit later.
            nextVisit = Time.unscaledTime + 25f;
            return;
        }
        nextVisit = Time.unscaledTime + 1.2f;
        Say(PepitoAdvice.Intro, AdviceSeconds + 3f);
    }

    bool suppressed;
    float baseY = -Top;
    public bool Started => started;

    /// <summary>
    /// True while another tab (Jardinería, Tienda, Logros...) is open: the farm Pepito hides and stays
    /// quiet so he doesn't pop up over those screens. The garden has its own Pepito toast.
    /// </summary>
    public bool Suppressed
    {
        get => suppressed;
        set
        {
            if (suppressed == value) return;
            suppressed = value;
            if (value)
            {
                if (visible) Hide();
            }
            else if (started)
            {
                nextVisit = Mathf.Max(nextVisit, Time.unscaledTime + 8f);
            }
        }
    }

    /// <summary>Makes Pepito pop up and say something right now (used by Plorp's orders).</summary>
    public void Say(string text, float seconds = AdviceSeconds)
    {
        if (asking || suppressed) return; // never interrupt a food request; quiet on other tabs
        Show();
        if (buttonsRow != null) buttonsRow.gameObject.SetActive(false);
        if (bubbleText != null)
        {
            bubbleText.rectTransform.sizeDelta = new Vector2(436, 96);
            bubbleText.text = text;
        }
        if (face != null) face.Talk(2);
        hideAt = Time.unscaledTime + seconds;
    }

    void NextAdvice()
    {
        if (asking) return;
        var advice = PepitoAdvice.Pick(state, lastAdvice);
        if (advice == null) return;
        lastAdvice = advice.Id;
        Say(advice.Text(state));
    }

    void AskForFood()
    {
        Show();
        asking = true;
        lastRequest = Time.unscaledTime;
        string snack = Snacks[UnityEngine.Random.Range(0, Snacks.Length)];
        bool useGems = UnityEngine.Random.value < .4f;
        if (useGems)
        {
            askGold = 0;
            askGems = UnityEngine.Random.Range(2, 9);
        }
        else
        {
            askGems = 0;
            float basePrice = 80f + 60f * state.FarmLevel;
            askGold = Mathf.Max(10, Mathf.RoundToInt(basePrice * UnityEngine.Random.Range(.5f, 1.8f) / 10f) * 10);
        }
        if (bubbleText != null)
        {
            bubbleText.rectTransform.sizeDelta = new Vector2(436, 60);
            bubbleText.text = "Miau... creo que Plorp tiene hambre. ¿Le compras " + snack + "?";
        }
        if (payCaption != null)
            payCaption.text = askGems > 0 ? "DAR " + askGems + " GEMAS" : "DAR " + FarmGameState.Format(askGold) + " MONEDAS";
        if (buttonsRow != null) buttonsRow.gameObject.SetActive(true);
        RefreshPayButton();
        if (face != null) face.Talk(1);
        hideAt = Time.unscaledTime + RequestSeconds;
    }

    void RefreshPayButton()
    {
        if (!asking || payFace == null || state == null) return;
        bool affordable = state.Gold >= askGold && state.Gems >= askGems;
        payFace.color = affordable ? PixelUIFactory.Green : PixelUIFactory.Hex("#67594F");
    }

    void Feed()
    {
        if (!asking) return;
        bool first = state.PepitoMeals == 0;
        if (!state.FeedPepito(askGold, askGems))
        {
            message(askGems > 0 ? "Faltan gemas" : "Faltan monedas");
            return;
        }
        asking = false;
        message(first ? "¡Logro listo: Alimenta a Plorp!" : "Plorp: \"¡Plorp!\"");
        Say(first
            ? "Miau. Plorp dijo \"plorp plorp\". Eso es gracias. Revisa LOGROS, te dejó algo."
            : "Miau. Plorp se lo comió de un bocado. Dijo \"zep\". Creo que es un cumplido.", 6f);
    }

    void Decline()
    {
        if (!asking) return;
        asking = false;
        Say("Miau... está bien. Plorp me está mirando raro, pero se le pasará.", 4f);
    }

    void Show()
    {
        visible = true;
    }

    void Hide()
    {
        visible = false;
        asking = false;
        nextVisit = Time.unscaledTime + UnityEngine.Random.Range(30f, 60f);
    }

    void Update()
    {
        if (!started || state == null) return;
        float now = Time.unscaledTime;

        if (visible && now >= hideAt)
        {
            if (asking) Decline();
            else Hide();
        }

        if (!suppressed && !visible && slide <= 0f && now >= nextVisit)
        {
            bool canAsk = state.Harvests >= 2 && now - lastRequest >= RequestCooldown;
            if (canAsk && UnityEngine.Random.value < .45f) AskForFood();
            else NextAdvice();
        }

        float target = visible ? 1f : 0f;
        if (!Mathf.Approximately(slide, target))
        {
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / .3f);
            ApplySlide();
        }
    }

    void ApplySlide()
    {
        if (root == null || group == null) return;
        // Ease-out slide from the left edge.
        float eased = 1f - Mathf.Pow(1f - slide, 3f);
        root.anchoredPosition = new Vector2(Mathf.Lerp(-580f, 0f, eased), baseY);
        group.alpha = slide > 0f ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable = visible;
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= RefreshPayButton;
    }
}
