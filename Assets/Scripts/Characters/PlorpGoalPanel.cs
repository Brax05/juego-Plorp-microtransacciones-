using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact card in the top-left of the farm with Plorp's current order: up to three foods
/// (icon + have/need) and an ENTREGAR button. Also shows the celebration when Plorp is satisfied.
/// It sits top-left, over the trees, so the chicken yard and the fields stay visible.
/// </summary>
public sealed class PlorpGoalPanel : MonoBehaviour
{
    const float Left = 8f, Top = 112f, Width = 176f, Height = 128f;
    const int Slots = 3;

    static readonly Color Wood = PixelUIFactory.Hex("#B86F3F");
    static readonly Color Disabled = PixelUIFactory.Hex("#67594F");
    static readonly Color Ok = PixelUIFactory.Hex("#8FE86A");

    FarmGameState state;
    Action<string> message;
    PepitoController pepito;
    PixelUIFactory ui;
    Transform portrait;

    PlorpFace face;
    Text titleText;
    Text orderText;
    readonly RawImage[] icons = new RawImage[Slots];
    readonly Text[] counts = new Text[Slots];
    readonly string[] iconPaths = new string[Slots];
    Image buttonFace;
    Text buttonCaption;
    Image readyGlow;
    RectTransform celebration;

    public static PlorpGoalPanel Create(Transform portrait, int siblingIndex, FarmGameState state,
        Action<string> message, PepitoController pepito)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Panel(portrait, "Plorp goal", Left, Top, Width, Height, Wood);
        rect.SetSiblingIndex(siblingIndex);
        var panel = rect.gameObject.GetOrAdd<PlorpGoalPanel>();
        panel.Build(ui, portrait, state, message, pepito);
        return panel;
    }

    void Build(PixelUIFactory factory, Transform portraitRoot, FarmGameState gameState,
        Action<string> showMessage, PepitoController pepitoController)
    {
        ui = factory;
        portrait = portraitRoot;
        state = gameState;
        message = showMessage;
        pepito = pepitoController;

        ui.Block(transform, "Face frame", 9, 9, 42, 42, PixelUIFactory.Hex("#1E1838"));
        face = PlorpFace.Create(transform, "Plorp face", 11, 12, 38);
        ui.Hit(transform, "Plorp tap", 9, 9, 42, 42, () =>
        {
            GameAudio.Play("sfx_plorp_voice", .8f, .1f);
            face.Bump();
            pepito.Say("Miau... Plorp dijo \"" + state.CurrentGoal.Quote + "\". Creo que quiere " +
                       Describe(state.CurrentGoal) + ".", 7f);
        });

        titleText = ui.Label(transform, "Title", "", 56, 9, 112, 14, 11, PixelUIFactory.Gold);
        orderText = ui.Paragraph(transform, "Order", "", 56, 24, 112, 26, 14, Color.white);

        for (int i = 0; i < Slots; i++)
        {
            float x = 10 + i * 52;
            icons[i] = ui.Texture(transform, "Item icon " + i, FarmItems.Carrot.Icon, x + 14, 52, 24, 24);
            counts[i] = ui.Label(transform, "Item count " + i, "", x, 75, 52, 15, 12, Color.white,
                TextAnchor.MiddleCenter);
        }

        readyGlow = ui.Block(transform, "Ready glow", 6, 90, 164, 34, new Color(1f, .95f, .5f, 0f));
        var button = ui.Panel(transform, "Deliver", 9, 92, 158, 30, Disabled);
        buttonFace = button.Find("Face").GetComponent<Image>();
        buttonCaption = ui.Label(button, "Caption", "ENTREGAR", 2, 0, 154, 30, 14, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Deliver hit", 9, 92, 158, 30, Deliver);

        state.Changed += Refresh;
        Refresh();
    }

    /// <summary>"10 zanahorias y 2 huevos" — used by Pepito.</summary>
    public static string Describe(PlorpGoal goal)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var line in goal.Lines) parts.Add(line.Amount + " " + line.Item.Name.ToLowerInvariant());
        if (parts.Count == 1) return parts[0];
        return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + " y " + parts[parts.Count - 1];
    }

    void Deliver()
    {
        bool wasSatisfied = state.PlorpSatisfied;
        if (!state.DeliverToPlorp(out var goal))
        {
            foreach (var line in state.CurrentGoal.Lines)
            {
                long missing = line.Amount - state.ItemCount(line.Item.Id);
                if (missing <= 0) continue;
                GameAudio.Play("sfx_ui_error");
                message("Faltan " + missing + " " + line.Item.Name.ToLowerInvariant());
                return;
            }
            return;
        }

        face.Bump();
        message("Plorp: \"" + goal.Quote + "\"  +" + goal.RewardGems + " gemas");
        if (!wasSatisfied && state.PlorpSatisfied)
        {
            ShowCelebration(goal);
            return;
        }
        pepito.Say(state.PlorpSatisfied
            ? "Miau... Plorp se comió todo. Dice que era un antojo. Yo no juzgo."
            : "Miau. Plorp dijo \"" + goal.Quote + "\". Le gustó. Ahora quiere " +
              Describe(state.CurrentGoal) + ".", 8f);
    }

    void Refresh()
    {
        if (state == null || titleText == null || orderText == null || buttonFace == null || buttonCaption == null) return;
        var goal = state.CurrentGoal;
        if (goal == null) return;
        titleText.text = state.PlorpSatisfied ? "PLORP CONFORME" : "PLORP QUIERE";
        orderText.text = goal.Title != null ? goal.Title.ToUpperInvariant() : "";

        for (int i = 0; i < Slots; i++)
        {
            if (icons[i] == null || counts[i] == null) continue;
            bool used = goal.Lines != null && i < goal.Lines.Count;
            icons[i].gameObject.SetActive(used);
            counts[i].gameObject.SetActive(used);
            if (!used) continue;
            var line = goal.Lines[i];
            if (line.Item != null)
            {
                SetIcon(i, line.Item.Icon);
                long have = state.ItemCount(line.Item.Id);
                counts[i].text = FarmGameState.Format(Math.Min(have, line.Amount)) + "/" + FarmGameState.Format(line.Amount);
                counts[i].color = have >= line.Amount ? Ok : Color.white;
            }
        }

        bool ready = state.GoalReady;
        buttonFace.color = ready ? PixelUIFactory.Green : Disabled;
        buttonCaption.text = ready ? "¡ENTREGAR!" : "ENTREGAR";
    }

    void SetIcon(int slot, string path)
    {
        if (iconPaths[slot] == path) return;
        iconPaths[slot] = path;
        var texture = Art.Tex(path);
        if (texture != null) texture.filterMode = FilterMode.Point;
        icons[slot].texture = texture;
    }

    void Update()
    {
        if (state == null || readyGlow == null) return;
        var c = readyGlow.color;
        c.a = state.GoalReady ? .25f + .25f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
        readyGlow.color = c;
    }

    void ShowCelebration(PlorpGoal last)
    {
        if (celebration != null) UiUtil.Kill(celebration.gameObject);
        celebration = ui.Rect(portrait, "Plorp satisfied", 0, 0, 570, 1018);
        celebration.SetAsLastSibling();
        ui.Backdrop(celebration, "Backdrop", 570, 1018, new Color(0f, 0f, .06f, .72f));

        var card = ui.Panel(celebration, "Card", 35, 250, 500, 470, PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", "¡PLORP ESTÁ CONFORME!", 0, 22, 500, 46, 30, PixelUIFactory.Gold,
            TextAnchor.MiddleCenter);
        ui.Block(card, "Face frame", 176, 80, 148, 148, PixelUIFactory.Hex("#1E1838"));
        var bigFace = PlorpFace.Create(card, "Plorp", 180, 84, 140);
        bigFace.Bump();
        ui.Paragraph(card, "Story",
            "Le diste " + FarmGameState.Format(state.TotalFood) +
            " comidas entre verduras, huevos, quesos y leche. Pepito traduce: \"Miau... dice gracias. Creo. Y que volverá con antojos.\"",
            30, 240, 440, 96, 18, Color.white, TextAnchor.MiddleCenter);
        ui.Label(card, "Reward", "+" + last.RewardGems + " gemas  ·  +" + FarmGameState.Format(last.RewardGold) +
            " monedas", 0, 340, 500, 30, 18, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.ActionButton(card, "Continue", "SEGUIR GRANJEANDO", 90, 386, 320, 60, () =>
        {
            UiUtil.Kill(celebration.gameObject);
            celebration = null;
            pepito.Say("Miau. Plorp está conforme. Hace años que no lo veía así de tranquilo.", 8f);
        });
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= Refresh;
    }
}
