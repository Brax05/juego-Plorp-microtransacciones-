using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Logros" tab with two sub-tabs:
///  - MISIONES: the daily streak and today's three missions (+ bonus chest when all are done).
///  - LOGROS: scrolling list of every achievement with its progress bar and gem reward.
///    Ready ones go first (with a COBRAR button), then in-progress, then claimed.
/// </summary>
public sealed class AchievementsScreen : MonoBehaviour
{
    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    Text subtitle;
    RectTransform content;
    float rebuildAt = -1f;
    readonly Image[] tabFaces = new Image[2];
    readonly Text[] tabCaptions = new Text[2];
    int tab;
    float nextClock;
    Text resetClock;

    /// <summary>Tab to open next time (0 misiones, 1 logros); -1 keeps the last one.</summary>
    public static int PendingTab = -1;

    public static AchievementsScreen Create(Transform secondary, FarmGameState state, Action<string> message)
    {
        var page = MenuPage.Create(secondary, "Page - Logros (nueva)", "LOGROS", "", out var subtitle);
        var screen = page.gameObject.GetOrAdd<AchievementsScreen>();
        screen.state = state;
        screen.message = message;
        screen.subtitle = subtitle;
        screen.ui = new PixelUIFactory();
        var ui = screen.ui;
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            var tabPanel = ui.Panel(page, "Tab " + i, 30 + i * 260, 112, 250, 46, PixelUIFactory.Green);
            screen.tabFaces[i] = tabPanel.Find("Face").GetComponent<Image>();
            screen.tabCaptions[i] = ui.Label(tabPanel, "Caption", i == 0 ? "MISIONES" : "LOGROS", 0, 0, 250, 46, 20,
                Color.white, TextAnchor.MiddleCenter);
            ui.Hit(page, "Tab hit " + i, 30 + i * 260, 112, 250, 46, () => { screen.tab = index; screen.Rebuild(); });
        }
        screen.content = MenuPage.ScrollList(page, 166);
        state.Changed += screen.OnStateChanged;
        DailyMissions.Changed += screen.OnStateChanged;
        page.gameObject.SetActive(false);
        return screen;
    }


    void OnEnable()
    {
        if (PendingTab >= 0) { tab = PendingTab; PendingTab = -1; }
        else if (state != null && PepitoGuide.Seen(2) && DailyMissions.ReadyCount(state) == 0 && AchievementCatalog.ReadyCount(state) > 0) tab = 1;
        if (content != null) Rebuild();
    }

    // Progress changes often (every harvest): rebuild at most twice per second while visible.
    void OnStateChanged()
    {
        if (gameObject.activeInHierarchy && rebuildAt < 0f) rebuildAt = Time.unscaledTime + .5f;
    }

    void Update()
    {
        if (rebuildAt >= 0f && Time.unscaledTime >= rebuildAt && !MenuPage.IsScrolling(content))
        {
            rebuildAt = -1f;
            Rebuild();
        }
        if (resetClock != null && Time.unscaledTime >= nextClock)
        {
            nextClock = Time.unscaledTime + 1f;
            resetClock.text = "Nuevas misiones en " + Clock(DailyMissions.TimeToReset);
        }
    }

    void Rebuild()
    {
        int missionsReady = DailyMissions.ReadyCount(state);
        int achievementsReady = AchievementCatalog.ReadyCount(state);
        for (int i = 0; i < 2; i++)
        {
            tabFaces[i].color = i == tab ? PixelUIFactory.Green : PixelUIFactory.Hex("#6E4A36");
            int n = i == 0 ? missionsReady : achievementsReady;
            tabCaptions[i].text = (i == 0 ? "MISIONES" : "LOGROS") + (n > 0 ? " (" + n + ")" : "");
        }
        resetClock = null;
        if (tab == 0) { RebuildMissions(); return; }

        int done = 0;
        foreach (var a in AchievementCatalog.All) if (AchievementCatalog.Claimed(a)) done++;
        int ready = AchievementCatalog.ReadyCount(state);
        subtitle.text = done + "/" + AchievementCatalog.All.Length + " completados" +
                        (ready > 0 ? " · ¡" + ready + " por cobrar!" : " · Cumple metas y gana gemas");

        var ordered = new List<AchievementDef>(AchievementCatalog.All);
        ordered.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

        MenuPage.Clear(content);
        float y = 6;
        foreach (var a in ordered) y = Card(a, y);
        MenuPage.SetHeight(content, y + 10);
    }

    int Rank(AchievementDef a) =>
        AchievementCatalog.Ready(state, a) ? 0 : AchievementCatalog.Claimed(a) ? 2 : 1;

    float Card(AchievementDef a, float y)
    {
        bool claimed = AchievementCatalog.Claimed(a);
        bool ready = AchievementCatalog.Ready(state, a);
        long progress = Math.Min(a.Progress(state), a.Target);
        var card = ui.Panel(content, a.Id, 6, y, 530, 104,
            PixelUIFactory.Hex(ready ? "#B07A2E" : claimed ? "#6E5040" : "#9A5A33"));
        ui.Block(card, "Icon back", 12, 16, 64, 64, PixelUIFactory.Hex("#5C3020"));
        ui.Texture(card, "Icon", a.Icon, 20, 24, 48, 48);
        ui.Label(card, "Name", a.Name.ToUpperInvariant(), 86, 10, 290, 24, 17, claimed ? PixelUIFactory.Cream : Color.white);
        ui.Paragraph(card, "Desc", a.Description, 86, 36, 290, 22, 13, PixelUIFactory.Cream);

        // Progress bar
        ui.Block(card, "Bar back", 86, 66, 232, 16, PixelUIFactory.Hex("#2A1A14"));
        float fraction = a.Target <= 0 ? 1f : progress / (float)a.Target;
        ui.Block(card, "Bar fill", 86, 66, 232 * Mathf.Clamp01(fraction), 16,
            PixelUIFactory.Hex(claimed ? "#8FA07A" : "#7ED957"));
        ui.Label(card, "Progress", FarmGameState.Format(progress) + "/" + FarmGameState.Format(a.Target),
            86, 64, 232, 20, 12, Color.white, TextAnchor.MiddleCenter);

        // Reward + button
        ui.Texture(card, "Gem", "icon_gem", 328, 62, 26, 22);
        ui.Label(card, "Reward", "+" + a.Gems, 356, 60, 50, 26, 15, PixelUIFactory.Gold);
        var def = a;
        ui.ActionButton(card, "Claim", claimed ? "LISTO" : ready ? "COBRAR" : "EN CURSO", 410, 26, 110, 52, () =>
        {
            if (AchievementCatalog.Claim(state, def)) message("+" + def.Gems + " gemas: " + def.Name);
        }, ready);
        return y + 112;
    }

    // ---------- Misiones ----------

    void RebuildMissions()
    {
        var list = DailyMissions.Missions(state);
        int done = 0;
        foreach (var m in list) if (m.Claimed) done++;
        subtitle.text = "Racha: " + DailyMissions.Streak + (DailyMissions.Streak == 1 ? " día" : " días seguidos") +
                        " · Misiones de hoy " + done + "/" + list.Length;

        MenuPage.Clear(content);
        float y = 6;
        y = StreakCard(y);
        y = MenuPage.Section(ui, content, "MISIONES DE HOY", y + 4);
        resetClock = ui.Label(content, "Reset clock", "", 250, y - 32, 280, 30, 13, PixelUIFactory.Cream, TextAnchor.MiddleRight);
        nextClock = 0f;
        foreach (var m in list) y = MissionCard(m, y);
        y = BonusCard(y);
        MenuPage.SetHeight(content, y + 10);
    }

    float StreakCard(float y)
    {
        bool ready = DailyMissions.StreakReady;
        int today = DailyMissions.StreakDay;
        var card = ui.Panel(content, "Streak", 6, y, 530, 176, PixelUIFactory.Hex(ready ? "#B07A2E" : "#7A3F5E"));
        ui.Label(card, "Title", "RACHA DIARIA", 18, 10, 300, 28, 20, PixelUIFactory.Gold);
        ui.Label(card, "Days", "Día " + (today + 1) + " de 7 · llevas " + DailyMissions.Streak +
            (DailyMissions.Streak == 1 ? " día" : " días"), 210, 10, 300, 28, 13, PixelUIFactory.Cream, TextAnchor.MiddleRight);
        for (int d = 0; d < 7; d++)
        {
            bool past = d < today || (d == today && !ready);
            bool current = d == today && ready;
            float x = 16 + d * 72;
            var box = ui.Panel(card, "Day " + d, x, 44, 66, 82,
                PixelUIFactory.Hex(current ? "#FFD44F" : past ? "#5E4A3E" : d == 6 ? "#2F5E7A" : "#9A5A33"));
            Color ink = current ? PixelUIFactory.Ink : Color.white;
            ui.Label(box, "Name", "DÍA " + (d + 1), 0, 6, 66, 18, 12, ink, TextAnchor.MiddleCenter);
            ui.Texture(box, "Gem", "icon_gem", 20, 28, 26, 22);
            ui.Label(box, "Reward", past ? "OK" : "+" + DailyMissions.StreakGems[d], 0, 52, 66, 22, 14,
                past ? PixelUIFactory.Hex("#8FE86A") : ink, TextAnchor.MiddleCenter);
        }
        if (ready)
        {
            long coins = DailyMissions.StreakCoins(state, today);
            ui.ActionButton(card, "Claim streak", "COBRAR DÍA " + (today + 1) + ": +" + DailyMissions.StreakGems[today] +
                " GEMAS Y " + FarmGameState.Format(coins), 16, 132, 498, 36, () =>
            {
                int g = DailyMissions.StreakGems[DailyMissions.StreakDay];
                if (DailyMissions.ClaimStreak(state)) message("¡Racha cobrada! +" + g + " gemas");
            });
            var caption = FarmUIBootstrap.FindChildRecursive(card, "Caption");
            if (caption != null) caption.GetComponent<Text>().fontSize = 15;
        }
        else
        {
            ui.Paragraph(card, "Hint", "Vuelve mañana para el día " + ((today + 1) % 7 + 1) +
                ". Si faltas un día, la racha parte de nuevo.", 16, 132, 498, 36, 13, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
        }
        return y + 184;
    }

    float MissionCard(DailyMission m, float y)
    {
        bool ready = m.Done && !m.Claimed;
        string[] levels = { "FÁCIL", "MEDIA", "DIFÍCIL" };
        var card = ui.Panel(content, "Mission " + m.Slot, 6, y, 530, 104,
            PixelUIFactory.Hex(ready ? "#B07A2E" : m.Claimed ? "#6E5040" : "#9A5A33"));
        ui.Block(card, "Icon back", 12, 16, 64, 64, PixelUIFactory.Hex("#5C3020"));
        ui.Texture(card, "Icon", DailyMissions.Icon(m.Kind), 20, 24, 48, 48);
        ui.Label(card, "Level", levels[m.Slot], 86, 8, 120, 20, 12, PixelUIFactory.Gold);
        ui.Paragraph(card, "Name", DailyMissions.Describe(m.Kind, m.Target), 86, 26, 300, 36, 16,
            m.Claimed ? PixelUIFactory.Cream : Color.white);

        ui.Block(card, "Bar back", 86, 66, 232, 16, PixelUIFactory.Hex("#2A1A14"));
        float fraction = m.Target <= 0 ? 1f : m.Progress / (float)m.Target;
        ui.Block(card, "Bar fill", 86, 66, 232 * Mathf.Clamp01(fraction), 16,
            PixelUIFactory.Hex(m.Claimed ? "#8FA07A" : "#7ED957"));
        ui.Label(card, "Progress", FarmGameState.Format(m.Progress) + "/" + FarmGameState.Format(m.Target),
            86, 64, 232, 20, 12, Color.white, TextAnchor.MiddleCenter);

        ui.Texture(card, "Gem", "icon_gem", 330, 40, 22, 19);
        ui.Label(card, "Reward gems", "+" + m.Gems, 354, 36, 50, 24, 14, PixelUIFactory.Gold);
        ui.Texture(card, "Coin", "icon_coin", 330, 64, 20, 20);
        ui.Label(card, "Reward coins", FarmGameState.Format(DailyMissions.MissionCoins(state, m)), 352, 61, 56, 24, 12, Color.white);
        var mission = m;
        ui.ActionButton(card, "Claim", m.Claimed ? "LISTO" : ready ? "COBRAR" : "EN CURSO", 410, 26, 110, 52, () =>
        {
            long coins = DailyMissions.MissionCoins(state, mission);
            if (DailyMissions.Claim(state, mission))
                message("+" + mission.Gems + " gemas y +" + FarmGameState.Format(coins) + " monedas");
        }, ready);
        return y + 112;
    }

    float BonusCard(float y)
    {
        bool ready = DailyMissions.BonusReady(state);
        bool claimed = DailyMissions.BonusClaimed;
        var card = ui.Panel(content, "Bonus", 6, y, 530, 84,
            PixelUIFactory.Hex(ready ? "#B07A2E" : claimed ? "#6E5040" : "#2F5E7A"));
        ui.Texture(card, "Chest", "starter_chest", 14, 10, 64, 64);
        ui.Label(card, "Name", "COFRE DEL DÍA", 88, 10, 300, 26, 17, Color.white);
        ui.Paragraph(card, "Desc", "Cobra las 3 misiones de hoy y ábrelo.", 88, 38, 240, 36, 13, PixelUIFactory.Cream);
        ui.Texture(card, "Gem", "icon_gem", 330, 30, 26, 22);
        ui.Label(card, "Reward", "+" + DailyMissions.BonusGems, 358, 28, 50, 26, 15, PixelUIFactory.Gold);
        ui.ActionButton(card, "Claim", claimed ? "LISTO" : ready ? "ABRIR" : "CERRADO", 410, 16, 110, 52, () =>
        {
            if (DailyMissions.ClaimBonus(state)) message("¡Cofre del día! +" + DailyMissions.BonusGems + " gemas");
        }, ready);
        return y + 92;
    }

    static string Clock(System.TimeSpan t)
    {
        int h = (int)t.TotalHours, m = t.Minutes;
        return h > 0 ? h + " h " + m + " min" : Mathf.Max(1, m) + " min";
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= OnStateChanged;
        DailyMissions.Changed -= OnStateChanged;
    }
}
