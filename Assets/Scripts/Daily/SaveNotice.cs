using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "TU PARTIDA" notice: where the game is saved and what is lost without Google Play Juegos. It shows the
/// first time the game opens (before Pepito's tour) and again from AJUSTES ▸ PARTIDA GUARDADA, whose card
/// also shows the cloud save status and a CONECTAR button.
/// </summary>
public static class SaveNotice
{
    const string SeenKey = "GlorpFarm.notice.save";
    const float CardY = 120f, CardH = 700f;

    static Text statusText;
    static Text connectCaption;

    public static string Body
    {
        get
        {
            string text =
                "• Tu granja, tus gemas y tus compras se guardan en este teléfono.\n\n" +
                "• Si inicias sesión en Google Play Juegos, también se guardan en tu cuenta de Google y vuelven " +
                "si reinstalas el juego o cambias de teléfono.\n\n" +
                "• Sin sesión iniciada, si desinstalas el juego o borras sus datos, se pierden la granja y las gemas " +
                "compradas, y no se pueden recuperar.";
#if PLORP_REFUNDS
            text += "\n\n• Si una compra se reembolsa, sus gemas se descuentan de tu cuenta.";
#endif
            return text + "\n\nPuedes volver a leer esto en AJUSTES ▸ PARTIDA GUARDADA.";
        }
    }

    /// <summary>Shows the notice the first time. <paramref name="closed"/> always runs.</summary>
    public static void ShowIfNew(Transform portrait, Action closed)
    {
        if (SaveStore.GetInt(SeenKey, 0) == 1)
        {
            closed?.Invoke();
            return;
        }
        Show(portrait, () =>
        {
            SaveStore.SetInt(SeenKey, 1);
            SaveStore.Save();
            closed?.Invoke();
        });
    }

    public static void Show(Transform portrait, Action closed = null)
    {
        var ui = new PixelUIFactory();
        RectTransform card;
        var root = Popup.Open(portrait, "Save notice", "TU PARTIDA", CardY, CardH, out card, closed, false);
        float w = Popup.W - 28f;
        Popup.Pepito(ui, card, 18, 70, 396, "Miau. Antes de empezar, te cuento dónde guardo tu granja para que no se pierda.");
        ui.Paragraph(card, "Body", Body, 26, 196, w - 52, 330, 17, Color.white, TextAnchor.UpperLeft);

        float y = 540f;
        if (CloudSave.Available && !CloudSave.SignedIn)
        {
            ui.ActionButton(card, "Connect", "CONECTAR GOOGLE PLAY JUEGOS", 40, y, w - 80, 58, () =>
            {
                CloudSave.Connect(null);
                Popup.Close(root);
            });
            UiUtil.CaptionSize(card, "Connect", 18);
            y += 70f;
        }
        ui.ActionButton(card, "Ok", "ENTENDIDO", 120, y, w - 240, 58, () => Popup.Close(root));
    }

    // ---------- AJUSTES ▸ PARTIDA GUARDADA ----------

    /// <summary>
    /// Takes the "Saved game" card of the Ajustes page (authored in the canvas): its description shows the
    /// cloud status, and two buttons are added: CONECTAR and VER AVISO.
    /// </summary>
    public static void BindSettings(Transform settingsPage, Transform portrait, Action<string> message)
    {
        var card = settingsPage != null ? settingsPage.Find("Saved game") : null;
        if (card == null) return;
        var description = card.Find("Description");
        statusText = description != null ? description.GetComponent<Text>() : null;
        if (statusText != null)
        {
            var rect = statusText.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 66f); // room for the buttons under it
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.resizeTextForBestFit = true;
            statusText.resizeTextMinSize = 11;
            statusText.resizeTextMaxSize = Mathf.Max(12, statusText.fontSize);
        }

        var ui = new PixelUIFactory();
        ui.ActionButton(card, "Cloud connect", "CONECTAR", 18, 138, 250, 38, () =>
            CloudSave.Connect(message));
        UiUtil.CaptionSize(card, "Cloud connect", 16);
        var connectPanel = card.Find("Cloud connect");
        connectCaption = connectPanel != null ? connectPanel.Find("Caption")?.GetComponent<Text>() : null;
        ui.ActionButton(card, "Notice", "VER AVISO", 280, 138, 232, 38, () => Show(portrait));
        UiUtil.CaptionSize(card, "Notice", 16);

        CloudSave.StatusChanged -= Refresh;
        CloudSave.StatusChanged += Refresh;
        Refresh();
    }

    public static void Refresh()
    {
        if (statusText != null) statusText.text = CloudSave.StatusLine;
        if (connectCaption != null)
            connectCaption.text = !CloudSave.Available ? "SOLO TELÉFONO" : CloudSave.SignedIn ? "CONECTADO" : "CONECTAR";
    }
}
