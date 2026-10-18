using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tickets for the arcade minigames: 3 each, one comes back every 20 minutes.</summary>
public static class MinigameTickets
{
    public const int Max = 3;
    const int Seconds = 20 * 60;
    const string Key = "GlorpFarm.games.";
    static Recharge Stock(string game) => new Recharge(Key + game + ".tickets", Key + game + ".since", Max, Seconds);

    public static int Count(string game) => Stock(game).Count;
    public static int SecondsToNext(string game) => Stock(game).SecondsToNext;
    public static bool Use(string game) => Stock(game).Use();
    /// <summary>Tickets of every arcade game together.</summary>
    public static int Total => Count("abduction") + Count("eggs");

    public static int Best(string game) => PlayerPrefs.GetInt(Key + game + ".best", 0);

    public static bool SaveBest(string game, int score)
    {
        if (score <= Best(game)) return false;
        PlayerPrefs.SetInt(Key + game + ".best", score);
        PlayerPrefs.Save();
        return true;
    }
}

/// <summary>Reports where the finger is inside the play area (local x/y from its top-left).</summary>
public sealed class PointerArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public bool Pressed { get; private set; }
    public Vector2 Position { get; private set; }
    RectTransform rect;

    void Awake() => rect = (RectTransform)transform;

    void Set(PointerEventData e)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local))
            Position = new Vector2(local.x + rect.rect.width * rect.pivot.x, rect.rect.height * (1f - rect.pivot.y) - local.y);
    }

    public void OnPointerDown(PointerEventData e) { Pressed = true; Set(e); }
    public void OnDrag(PointerEventData e) => Set(e);
    public void OnPointerUp(PointerEventData e) => Pressed = false;
    void OnDisable() => Pressed = false;
}

/// <summary>
/// Shared frame of the 30-second arcade minigames (Abducción, Atrapa huevos): window, play area,
/// timer and score, a 3-2-1 countdown and the results with rewards. Subclasses draw and play.
/// </summary>
public abstract class ArcadeGame : MonoBehaviour
{
    protected const float AreaX = 20f, AreaY = 74f, AreaW = 502f, AreaH = 560f;
    protected const float RoundSeconds = 30f;

    protected FarmGameState state;
    protected Action<string> message;
    protected PixelUIFactory ui;
    protected RectTransform card, area;
    protected PointerArea pointer;
    protected int score;
    protected bool playing;

    RectTransform root, results;
    Text timeText, scoreText, countdown;
    float timeLeft, countdownLeft;

    protected abstract string GameId { get; }
    protected abstract string Title { get; }
    protected abstract string HowTo { get; }
    protected abstract void BuildArea();
    protected abstract void ResetRound();
    protected abstract void Tick(float dt);
    /// <summary>Gives the rewards for the score and returns the text that describes them.</summary>
    protected abstract string GiveRewards(int finalScore);

    public static void Open<T>(Transform portrait, FarmGameState state, Action<string> message) where T : ArcadeGame
    {
        if (!MinigameTickets.Use(typeof(T) == typeof(AbductionGame) ? "abduction" : "eggs"))
        {
            message("Sin boletos: llega 1 cada 20 minutos");
            return;
        }
        var root = Popup.Open(portrait, "Minigame", "", 70, 880, out var card);
        var game = root.gameObject.GetOrAdd<T>();
        game.root = root;
        game.card = card;
        game.state = state;
        game.message = message;
        game.ui = new PixelUIFactory();
        game.Build();
    }

    void Build()
    {
        var title = card.Find("Title")?.GetComponent<Text>();
        if (title != null) title.text = Title;
        area = ui.Rect(card, "Play area", AreaX, AreaY, AreaW, AreaH);
        area.gameObject.GetOrAdd<RectMask2D>();
        BuildArea();
        // Touch layer on top of everything in the area.
        var touch = ui.Rect(area, "Touch", 0, 0, AreaW, AreaH);
        var img = touch.gameObject.GetOrAdd<Image>();
        img.color = new Color(0, 0, 0, 0);
        pointer = touch.gameObject.GetOrAdd<PointerArea>();

        timeText = ui.Label(card, "Time", "", 24, AreaY + AreaH + 10, 240, 34, 22, Color.white);
        scoreText = ui.Label(card, "Score", "", 278, AreaY + AreaH + 10, 240, 34, 22, PixelUIFactory.Gold, TextAnchor.MiddleRight);
        ui.Paragraph(card, "How to", HowTo, 24, AreaY + AreaH + 48, 494, 60, 15, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
        countdown = ui.Label(card, "Countdown", "", AreaX, AreaY + AreaH / 2f - 50, AreaW, 100, 80, PixelUIFactory.Gold,
            TextAnchor.MiddleCenter);
        StartRound();
    }

    void StartRound()
    {
        if (results != null) UiUtil.Kill(results.gameObject);
        results = null;
        score = 0;
        timeLeft = RoundSeconds;
        countdownLeft = 3f;
        GameAudio.Play("sfx_countdown", .7f, 0f);
        playing = false;
        ResetRound();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (countdownLeft > 0f)
        {
            int before = Mathf.CeilToInt(countdownLeft);
            countdownLeft -= dt;
            countdown.text = countdownLeft > 0f ? Mathf.CeilToInt(countdownLeft).ToString() : "";
            if (countdownLeft > 0f && Mathf.CeilToInt(countdownLeft) != before) GameAudio.Play("sfx_countdown", .7f, 0f);
            if (countdownLeft <= 0f) { playing = true; countdown.text = ""; GameAudio.Play("sfx_countdown_go", .8f, 0f); }
        }
        else if (playing)
        {
            timeLeft -= dt;
            Tick(dt);
            if (timeLeft <= 0f) Finish();
        }
        timeText.text = "Tiempo: " + Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        scoreText.text = "Puntos: " + score;
    }

    void Finish()
    {
        GameAudio.Play("sfx_minigame_end");
        playing = false;
        int final = Mathf.Max(0, score);
        bool record = MinigameTickets.SaveBest(GameId, final);
        string rewards = GiveRewards(final);

        results = ui.Rect(card, "Results", AreaX, AreaY, AreaW, AreaH);
        var back = results.gameObject.GetOrAdd<Image>();
        back.color = new Color(.05f, .03f, .08f, .82f);
        ui.Label(results, "Title", "¡TIEMPO!", 0, 40, AreaW, 60, 44, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.Label(results, "Score", final + " puntos" + (record ? "  · ¡RÉCORD!" : ""), 0, 110, AreaW, 40, 26, Color.white,
            TextAnchor.MiddleCenter);
        ui.Label(results, "Best", "Mejor: " + MinigameTickets.Best(GameId), 0, 152, AreaW, 28, 16, PixelUIFactory.Cream,
            TextAnchor.MiddleCenter);
        ui.Paragraph(results, "Rewards", rewards, 30, 196, AreaW - 60, 120, 19, PixelUIFactory.Gold, TextAnchor.MiddleCenter);

        int tickets = MinigameTickets.Count(GameId);
        ui.ActionButton(results, "Again", tickets > 0 ? "OTRA VEZ (" + tickets + " boletos)" : "SIN BOLETOS", 60, 350,
            AreaW - 120, 66, () =>
            {
                if (!MinigameTickets.Use(GameId)) { message("Sin boletos: llega 1 cada 20 minutos"); return; }
                StartRound();
            }, tickets > 0);
        UiUtil.CaptionSize(results, "Again", 17);
        ui.ActionButton(results, "Exit", "SALIR", 60, 430, AreaW - 120, 58, () => Popup.Close(root));
        UiUtil.ButtonColor(results, "Exit", PixelUIFactory.Hex("#8A6A55"));
    }

    // ---------- Helpers for subclasses ----------

    protected RawImage Sprite(Transform parent, string path, float w, float h)
    {
        var img = ui.Texture(parent, "Sprite", path, 0, 0, w, h);
        img.rectTransform.pivot = new Vector2(.5f, .5f);
        return img;
    }

    /// <summary>Places a centred sprite at area coordinates (x right, y down).</summary>
    protected static void Place(RectTransform r, float x, float y) => r.anchoredPosition = new Vector2(x, -y);

    protected void Float(Vector2 at, string text, Color color)
    {
        var label = ui.Label(area, "Points", text, 0, 0, 120, 30, 20, color, TextAnchor.MiddleCenter);
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        Place(label.rectTransform, at.x, at.y);
        RisingText.Attach(label);
    }


    /// <summary>Coins for minigames scale with what the farm makes.</summary>
    protected long CoinsPerPoint(double seconds) =>
        Math.Max(5, (long)Math.Ceiling(FarmEconomy.IncomePerSecond(state) * seconds));
}
