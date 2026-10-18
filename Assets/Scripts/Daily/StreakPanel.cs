using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "RACHA DIARIA" popup: the first time the game opens each day, Pepito shows the 7-day streak and
/// today's reward. COBRAR takes it; "Después" leaves it for later (it waits in LOGROS ▸ MISIONES).
/// </summary>
public sealed class StreakPanel : MonoBehaviour
{
    const float W = 570f, H = 1018f;
    const float CardX = 20f, CardY = 230f, CardW = 530f, CardH = 470f;
    const string ShownKey = "GlorpFarm.daily.popupShown";


    public static bool Active { get; private set; }

    FarmGameState state;
    Action<string> message;
    Action onClosed;
    RectTransform card;
    float shownAt;

    /// <summary>Shows the popup once a day while today's streak reward is waiting. <paramref name="closed"/> always runs.</summary>
    public static void ShowIfReady(Transform portrait, FarmGameState state, Action<string> message, Action closed)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (Active || !DailyMissions.StreakReady || PlayerPrefs.GetString(ShownKey, "") == today)
        {
            closed?.Invoke();
            return;
        }
        PlayerPrefs.SetString(ShownKey, today);
        var ui = new PixelUIFactory();
        var root = Popup.OpenBlank(portrait, "Daily streak");
        var panel = root.gameObject.GetOrAdd<StreakPanel>();
        panel.state = state;
        panel.message = message;
        panel.onClosed = closed;
        panel.Build(ui);
        Active = true;
    }

    void Build(PixelUIFactory ui)
    {
        int today = DailyMissions.StreakDay;
        int streak = DailyMissions.Streak;
        ui.Backdrop(transform, "Backdrop", W, H, new Color(0f, 0f, .05f, .7f));
        card = ui.Panel(transform, "Card", CardX, CardY, CardW, CardH, PixelUIFactory.Hex("#CE8854"), true);
        card.pivot = new Vector2(.5f, .5f);
        card.anchoredPosition = new Vector2(CardX + CardW / 2f, -(CardY + CardH / 2f));
        ui.Label(card, "Title", "RACHA DIARIA", 0, 16, CardW, 40, 30, PixelUIFactory.Gold, TextAnchor.MiddleCenter);

        // Pepito and his bubble.
        string line = streak <= 1
            ? "¡Miau! Si vuelves mañana, Plorp te guarda un regalo más grande. Así cada día hasta el 7."
            : "¡Miau! Llevas " + streak + " días seguidos. Plorp está orgulloso. Dijo \"plorp\" dos veces.";
        Popup.Pepito(ui, card, 18, 64, 396, line);

        // The seven days.
        for (int d = 0; d < 7; d++)
        {
            bool past = d < today;
            bool current = d == today;
            float x = 20 + d * 70;
            var box = ui.Panel(card, "Day " + d, x, 200, 66, 96,
                PixelUIFactory.Hex(current ? "#FFD44F" : past ? "#5E4A3E" : d == 6 ? "#2F5E7A" : "#9A5A33"));
            Color ink = current ? PixelUIFactory.Ink : Color.white;
            ui.Label(box, "Name", "DÍA " + (d + 1), 0, 8, 66, 18, 12, ink, TextAnchor.MiddleCenter);
            ui.Texture(box, "Gem", "icon_gem", 18, 32, 30, 26);
            ui.Label(box, "Reward", past ? "OK" : "+" + DailyMissions.StreakGems[d], 0, 62, 66, 24, 15,
                past ? PixelUIFactory.Hex("#8FE86A") : ink, TextAnchor.MiddleCenter);
        }

        long coins = DailyMissions.StreakCoins(state, today);
        ui.Label(card, "Today", "HOY: +" + DailyMissions.StreakGems[today] + " gemas y +" + FarmGameState.Format(coins) +
            " monedas", 0, 308, CardW, 30, 18, Color.white, TextAnchor.MiddleCenter);
        ui.Paragraph(card, "Rule", "Si faltas un día, la racha vuelve al día 1.", 20, 338, CardW - 40, 24, 13,
            PixelUIFactory.Cream, TextAnchor.MiddleCenter);

        ui.ActionButton(card, "Later", "DESPUÉS", 24, 380, 170, 66, Close);
        var later = card.Find("Later");
        if (later != null) later.Find("Face").GetComponent<Image>().color = PixelUIFactory.Hex("#8A6A55");
        ui.ActionButton(card, "Claim", "¡COBRAR!", 206, 380, 300, 66, Claim);

        shownAt = Time.unscaledTime;
        Update();
    }

    void Claim()
    {
        int gems = DailyMissions.StreakGems[DailyMissions.StreakDay];
        long coins = DailyMissions.StreakCoins(state, DailyMissions.StreakDay);
        if (DailyMissions.ClaimStreak(state))
        {
            GameAudio.Play("sfx_streak_reward");
            RewardFx.Collect(card.position, null, 0, coins);
            message?.Invoke("¡Racha cobrada! +" + gems + " gemas y +" + FarmGameState.Format(coins) + " monedas");
        }
        Close();
    }

    void Close()
    {
        if (!Active) return;
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
        float s = age < .35f ? UiUtil.EaseOutBack(age / .35f) : 1f;
        card.localScale = new Vector3(s, s, 1f);
    }

    void OnDestroy()
    {
        if (onClosed != null) Active = false;
    }
}
