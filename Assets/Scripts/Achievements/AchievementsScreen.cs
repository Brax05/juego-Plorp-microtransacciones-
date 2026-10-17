using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Logros" tab: scrolling list of every achievement with its progress bar and gem reward.
/// Ready ones go first (with a COBRAR button), then in-progress, then claimed.
/// </summary>
public sealed class AchievementsScreen : MonoBehaviour
{
    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    Text subtitle;
    RectTransform content;
    float rebuildAt = -1f;

    public static AchievementsScreen Create(Transform secondary, FarmGameState state, Action<string> message)
    {
        var page = MenuPage.Create(secondary, "Page - Logros (nueva)", "LOGROS", "", out var subtitle);
        var screen = page.gameObject.AddComponent<AchievementsScreen>();
        screen.state = state;
        screen.message = message;
        screen.subtitle = subtitle;
        screen.ui = new PixelUIFactory();
        screen.content = MenuPage.ScrollList(page, 116);
        state.Changed += screen.OnStateChanged;
        page.gameObject.SetActive(false);
        return screen;
    }

    void OnEnable()
    {
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
    }

    void Rebuild()
    {
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
        ui.Texture(card, "Gem", "UI/Shop/icon_gem", 328, 62, 26, 22);
        ui.Label(card, "Reward", "+" + a.Gems, 356, 60, 50, 26, 15, PixelUIFactory.Gold);
        var def = a;
        ui.ActionButton(card, "Claim", claimed ? "LISTO" : ready ? "COBRAR" : "EN CURSO", 410, 26, 110, 52, () =>
        {
            if (AchievementCatalog.Claim(state, def)) message("+" + def.Gems + " gemas: " + def.Name);
        }, ready);
        return y + 112;
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= OnStateChanged;
    }
}
