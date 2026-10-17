using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-of-farm panel showing Plorp's current order (food + animals) with an ENTREGAR button,
/// plus the celebration popup when Plorp is finally satisfied ("conforme").
/// </summary>
public sealed class PlorpGoalPanel : MonoBehaviour
{
    const float Top = 116f;
    const float BarWidth = 196f;

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
    Text foodText;
    Text animalsText;
    RectTransform foodFill;
    Image buttonFace;
    Text buttonCaption;
    Image readyGlow;
    RectTransform celebration;

    public static PlorpGoalPanel Create(Transform portrait, int siblingIndex, FarmGameState state,
        Action<string> message, PepitoController pepito)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Panel(portrait, "Plorp goal", 8, Top, 554, 80, Wood);
        rect.SetSiblingIndex(siblingIndex);
        var panel = rect.gameObject.AddComponent<PlorpGoalPanel>();
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

        ui.Block(transform, "Face frame", 9, 9, 62, 62, PixelUIFactory.Hex("#1E1838"));
        face = PlorpFace.Create(transform, "Plorp face", 12, 12, 56);
        ui.Hit(transform, "Plorp tap", 9, 9, 62, 62, () =>
        {
            face.Bump();
            pepito.Say("Miau... Plorp dijo \"" + state.CurrentGoal.Quote + "\". Creo que quiere su " +
                       state.CurrentGoal.Title.ToLowerInvariant() + ".", 6f);
        });

        titleText = ui.Label(transform, "Title", "", 80, 8, 300, 22, 15, PixelUIFactory.Gold);

        ui.Block(transform, "Food bar back", 80, 33, BarWidth, 18, PixelUIFactory.Hex("#4A2A22"));
        foodFill = ui.Block(transform, "Food bar fill", 80, 33, 0, 18, PixelUIFactory.Hex("#4FC63F")).rectTransform;
        foodText = ui.Label(transform, "Food", "", 80, 31, BarWidth, 22, 13, Color.white, TextAnchor.MiddleCenter);
        animalsText = ui.Label(transform, "Animals", "", 80, 54, 300, 20, 13, Color.white);

        readyGlow = ui.Block(transform, "Ready glow", 386, 12, 158, 56, new Color(1f, .95f, .5f, 0f));
        var button = ui.Panel(transform, "Deliver", 390, 16, 150, 48, Disabled);
        buttonFace = button.Find("Face").GetComponent<Image>();
        buttonCaption = ui.Label(button, "Caption", "ENTREGAR", 2, 0, 146, 48, 17, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Deliver hit", 390, 16, 150, 48, Deliver);

        state.Changed += Refresh;
        Refresh();
    }

    void Deliver()
    {
        bool wasSatisfied = state.PlorpSatisfied;
        if (!state.DeliverToPlorp(out var goal))
        {
            var g = state.CurrentGoal;
            message(state.AnimalCount < g.Animals
                ? "Plorp quiere " + g.Animals + " animales"
                : "Falta alimento: " + FarmGameState.Format(g.Food - state.Food));
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
            : "Miau. Plorp dijo \"" + goal.Quote + "\". Eso significa que le gustó. Ahora pide su " +
              state.CurrentGoal.Title.ToLowerInvariant() + ".", 7f);
    }

    void Refresh()
    {
        var goal = state.CurrentGoal;
        string prefix = state.PlorpSatisfied ? "PLORP CONFORME · " : "PLORP QUIERE · ";
        titleText.text = prefix + goal.Title.ToUpperInvariant();

        float food = goal.Food <= 0 ? 1f : Mathf.Clamp01(state.Food / (float)goal.Food);
        foodFill.sizeDelta = new Vector2(BarWidth * food, foodFill.sizeDelta.y);
        foodText.text = "ALIMENTO " + FarmGameState.Format(Math.Min(state.Food, goal.Food)) + "/" +
                        FarmGameState.Format(goal.Food);

        bool animalsOk = state.AnimalCount >= goal.Animals;
        animalsText.text = goal.Animals > 0
            ? "ANIMALES " + Math.Min(state.AnimalCount, goal.Animals) + "/" + goal.Animals
            : "ANIMALES: ninguno por ahora";
        animalsText.color = animalsOk ? Ok : Color.white;

        bool ready = state.GoalReady;
        buttonFace.color = ready ? PixelUIFactory.Green : Disabled;
        buttonCaption.text = ready ? "¡ENTREGAR!" : "ENTREGAR";
    }

    void Update()
    {
        var c = readyGlow.color;
        c.a = state.GoalReady ? .25f + .25f * Mathf.Sin(Time.unscaledTime * 5f) : 0f;
        readyGlow.color = c;
    }

    void ShowCelebration(PlorpGoal last)
    {
        if (celebration != null) Destroy(celebration.gameObject);
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
            "Le diste " + FarmGameState.Format(state.TotalFood) + " de alimento y " + state.AnimalCount +
            " animales. Pepito traduce: \"Miau... dice gracias. Creo. Y que volverá con antojos.\"",
            30, 240, 440, 96, 18, Color.white, TextAnchor.MiddleCenter);
        ui.Label(card, "Reward", "+" + last.RewardGems + " gemas  ·  +" + FarmGameState.Format(last.RewardGold) +
            " monedas", 0, 340, 500, 30, 18, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.ActionButton(card, "Continue", "SEGUIR GRANJEANDO", 90, 386, 320, 60, () =>
        {
            Destroy(celebration.gameObject);
            celebration = null;
            pepito.Say("Miau. Plorp está conforme. Hace años que no lo veía así de tranquilo.", 8f);
        });
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= Refresh;
    }
}
