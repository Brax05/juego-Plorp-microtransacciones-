using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "MIENTRAS NO ESTABAS": when the player comes back, Pepito shows what the workers produced while the
/// game was closed (coins and food for Plorp's orders). RECOGER takes it; x2 doubles the coins for gems.
/// Only shown after at least a minute away. The time counted is capped (FarmEconomy.OfflineCapSeconds).
/// </summary>
public sealed class OfflinePanel : MonoBehaviour
{
    const float W = 570f, H = 1018f;
    const float CardX = 30f, CardY = 220f, CardW = 510f, CardH = 520f;
    public const float MinSeconds = 60f;
    const string LastSeenKey = "GlorpFarm.lastSeen";

    static readonly Color Purple = PixelUIFactory.Hex("#7A3F5E");

    static readonly string[] Lines =
    {
        "Miau... los trabajadores siguieron cosechando mientras no estabas. Plorp contó todo. Dos veces.",
        "¡Volviste! Plorp dijo \"plorp\". Creo que significa que te extrañó. O que tiene hambre.",
        "Miau. Plorp vigiló la granja desde su nave mientras no estabas. Dice que nadie robó nada... casi.",
        "Miau... yo dormí la siesta, pero los trabajadores no. Mira todo lo que juntaron.",
    };

    /// <summary>True while the panel is on screen (Pepito and tips wait).</summary>
    public static bool Active { get; private set; }

    // ---------- Last time the player was here ----------

    /// <summary>Remembers "now" as the last moment the player was in the game.</summary>
    public static void MarkSeen()
    {
        PlayerPrefs.SetString(LastSeenKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    }

    /// <summary>Seconds since the player was last in the game (0 the very first time).</summary>
    public static double SecondsAway()
    {
        if (!long.TryParse(PlayerPrefs.GetString(LastSeenKey, ""), out long last) || last <= 0) return 0;
        return Math.Max(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last);
    }

    // ---------- Panel ----------

    FarmGameState state;
    Action<string> message;
    Action onClosed;
    PixelUIFactory ui;
    RectTransform card;
    Text goldText, doubleCaption;
    Image doubleFace;
    long gold;
    int gemCost;
    float shownAt, shownGold;
    readonly Dictionary<string, int> items = new Dictionary<string, int>();

    /// <summary>
    /// Shows the panel if the player was away long enough and the farm made something.
    /// <paramref name="closed"/> always runs (right away when there is nothing to show).
    /// </summary>
    public static void ShowIfAway(Transform portrait, FarmGameState state, double secondsAway,
        Action<string> message, Action closed)
    {
        if (Active || secondsAway < MinSeconds)
        {
            closed?.Invoke();
            return;
        }
        double cap = FarmEconomy.OfflineCapSeconds(state);
        double counted = Math.Min(secondsAway, cap);
        var items = new Dictionary<string, int>();
        long gold = FarmEconomy.OfflineEarnings(state, counted, items);
        if (gold <= 0)
        {
            closed?.Invoke();
            return;
        }

        var ui = new PixelUIFactory();
        var root = Popup.OpenBlank(portrait, "Offline earnings");
        var panel = root.gameObject.GetOrAdd<OfflinePanel>();
        panel.state = state;
        panel.message = message;
        panel.onClosed = closed;
        panel.ui = ui;
        panel.gold = gold;
        foreach (var pair in items) panel.items[pair.Key] = pair.Value;
        // x2 price: 2 gems per half hour counted, between 2 and 20.
        panel.gemCost = Mathf.Clamp(2 * Mathf.CeilToInt((float)(counted / 1800.0)), 2, 20);
        panel.Build(secondsAway, counted, cap);
        Active = true;
    }

    void Build(double away, double counted, double cap)
    {
        ui.Backdrop(transform, "Backdrop", W, H, new Color(0f, 0f, .05f, .7f));
        card = ui.Panel(transform, "Card", CardX, CardY, CardW, CardH, PixelUIFactory.Hex("#CE8854"), true);
        card.pivot = new Vector2(.5f, .5f);
        card.anchoredPosition = new Vector2(CardX + CardW / 2f, -(CardY + CardH / 2f));

        ui.Label(card, "Title", "MIENTRAS NO ESTABAS", 0, 18, CardW, 40, 28, PixelUIFactory.Gold, TextAnchor.MiddleCenter);

        // Pepito with his name plate, and his speech bubble.
        string line = Lines[UnityEngine.Random.Range(0, Lines.Length)];
        Popup.Pepito(ui, card, 20, 70, 372, line + "\nEstuviste fuera " + UiUtil.Duration(Mathf.Max(60, (int)(away) / 60 * 60)) + ".");

        // Coins (they count up).
        ui.Block(card, "Coins shade", 20, 202, CardW - 40, 70, new Color(.15f, .07f, .03f, .45f));
        ui.Texture(card, "Coin", "icon_coin", 110, 213, 48, 48);
        goldText = ui.Label(card, "Gold", "+0", 168, 207, 260, 60, 40, PixelUIFactory.Gold);

        // Food for Plorp's orders.
        var list = new List<KeyValuePair<string, int>>(items);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        int shown = Mathf.Min(4, list.Count);
        float slotW = 112f;
        float startX = (CardW - shown * slotW) / 2f;
        for (int i = 0; i < shown; i++)
        {
            var item = FarmItems.Get(list[i].Key);
            if (item == null) continue;
            float x = startX + i * slotW;
            ui.Texture(card, "Item " + i, item.Icon, x + 8, 286, 40, 40);
            ui.Label(card, "Item count " + i, "x" + FarmGameState.Format(list[i].Value), x + 50, 290, 62, 32, 18, Color.white);
        }
        ui.Paragraph(card, "Food note", "Todo va al granero para los pedidos de Plorp.", 20, 330, CardW - 40, 24, 14,
            PixelUIFactory.Cream, TextAnchor.MiddleCenter);

        bool capped = away > cap;
        string note = (capped ? "Solo se cuentan " + UiUtil.Duration(Mathf.Max(60, (int)(cap) / 60 * 60)) + ". " : "Máximo " + UiUtil.Duration(Mathf.Max(60, (int)(cap) / 60 * 60)) + ". ") +
                      "Más trabajadores = más horas.";
        ui.Paragraph(card, "Cap note", note, 20, 356, CardW - 40, 24, 14,
            capped ? PixelUIFactory.Hex("#FFB39A") : PixelUIFactory.Cream, TextAnchor.MiddleCenter);

        // Buttons: take it, or pay gems for double coins.
        ui.ActionButton(card, "Collect", "RECOGER", 24, 418, 214, 72, () => Take(false));
        var doublePanel = ui.Panel(card, "Double", 252, 418, 234, 72, Purple);
        doubleFace = doublePanel.Find("Face").GetComponent<Image>();
        doubleCaption = ui.Label(doublePanel, "Caption", "x2", 14, 6, 90, 60, 30, Color.white, TextAnchor.MiddleCenter);
        ui.Texture(doublePanel, "Gem", "icon_gem", 108, 22, 34, 30);
        ui.Label(doublePanel, "Cost", gemCost.ToString(), 146, 6, 76, 60, 26, Color.white);
        ui.Hit(card, "Double hit", 252, 418, 234, 72, () => Take(true));

        shownAt = Time.unscaledTime;
        Refresh();
        Update();
    }

    void Refresh()
    {
        if (doubleFace != null)
            doubleFace.color = state.Gems >= gemCost ? Purple : PixelUIFactory.Hex("#67594F");
    }


    void Take(bool doubled)
    {
        if (!Active) return;
        long total = gold;
        if (doubled)
        {
            if (!state.Spend(0, gemCost))
            {
                message?.Invoke("Faltan " + (gemCost - state.Gems) + " gemas para el x2");
                return;
            }
            total *= 2;
        }
        // What the workers did alone doesn't count for today's missions.
        DailyMissions.Muted = true;
        try { state.Collect(total, items); }
        finally { DailyMissions.Muted = false; }
        // Coins fly to the counter, food to Plorp.
        FarmItem top = null;
        int topCount = 0;
        foreach (var pair in items)
            if (pair.Value > topCount) { topCount = pair.Value; top = FarmItems.Get(pair.Key); }
        Vector3 from = goldText != null ? goldText.transform.position : transform.position;
        RewardFx.Collect(from, top, Mathf.Min(6, topCount), total);
        message?.Invoke("+" + FarmGameState.Format(total) + " monedas" + (doubled ? " (x2)" : ""));
        Close();
    }

    void Close()
    {
        Active = false;
        var done = onClosed;
        onClosed = null;
        Popup.Close((RectTransform)transform);
        done?.Invoke();
    }

    void Update()
    {
        if (card == null) return;
        float age = Time.unscaledTime - shownAt;
        // Pop in.
        float s = age < .35f ? UiUtil.EaseOutBack(age / .35f) : 1f;
        card.localScale = new Vector3(s, s, 1f);
        // Coins count up over a second.
        float t = Mathf.Clamp01((age - .25f) / 1.1f);
        shownGold = gold * (1f - Mathf.Pow(1f - t, 3f));
        if (goldText != null) goldText.text = "+" + FarmGameState.Format((long)shownGold);
        if (Time.frameCount % 15 == 0) Refresh();
    }

    void OnDestroy()
    {
        if (onClosed != null) Active = false; // destroyed without closing (scene unloaded)
    }
}
