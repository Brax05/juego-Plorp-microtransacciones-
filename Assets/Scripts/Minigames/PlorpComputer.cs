using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plorp's green computer (in his house): "PLORP-OS" with the minigames as apps (Pesca, Abducción,
/// Atrapa huevos). Opening it zooms the computer's screen up to fill the window; APAGAR shrinks it back
/// into the computer. Games open on top and, when they end, you are back at the computer.
/// </summary>
public sealed class PlorpComputer : MonoBehaviour
{
    const float CardX = 20f, CardY = 120f, CardW = 530f, CardH = 760f;
    const float ZoomSeconds = .35f;
    static readonly Color Bezel = PixelUIFactory.Hex("#3F9A3A");
    static readonly Color BezelLight = PixelUIFactory.Hex("#7ED957");
    static readonly Color ScreenDark = PixelUIFactory.Hex("#0E2A1A");
    static readonly Color Phosphor = PixelUIFactory.Hex("#8FF57A");

    FarmGameState state;
    Action<string> message;
    Transform portrait;
    PixelUIFactory ui;
    RectTransform root, card;
    Image backdrop;
    Vector2 fromPos, toPos;
    float fromScale, t, direction = 1f;
    Text[] infos = new Text[3];
    float nextInfo;

    public static void Open(Transform portrait, FarmGameState state, Action<string> message, RectTransform computerScreen)
    {
        var root = Popup.OpenBlank(portrait, "Plorp computer");
        var pc = root.gameObject.AddComponent<PlorpComputer>();
        pc.root = root;
        pc.state = state;
        pc.message = message;
        pc.portrait = portrait;
        pc.ui = new PixelUIFactory();
        pc.Build(computerScreen);
    }

    void Build(RectTransform computerScreen)
    {
        backdrop = ui.Backdrop(root, "Backdrop", Popup.W, Popup.H, new Color(0f, 0f, .05f, 0f));

        // Monitor: green bezel, dark screen with scanlines.
        card = ui.Rect(root, "Monitor", CardX, CardY, CardW, CardH);
        card.pivot = new Vector2(.5f, .5f);
        toPos = new Vector2(CardX + CardW / 2f, -(CardY + CardH / 2f));
        var frame = card.gameObject.AddComponent<Image>();
        frame.color = PixelUIFactory.Ink;
        frame.raycastTarget = true;
        ui.Block(card, "Bezel", 4, 4, CardW - 8, CardH - 8, Bezel);
        ui.Block(card, "Bezel light", 10, 10, CardW - 20, 6, BezelLight);
        ui.Block(card, "Screen edge", 24, 24, CardW - 48, CardH - 120, PixelUIFactory.Ink);
        ui.Block(card, "Screen", 28, 28, CardW - 56, CardH - 128, ScreenDark);
        for (float y = 32; y < CardH - 100; y += 6)
            ui.Block(card, "Scanline", 28, y, CardW - 56, 2, new Color(.2f, .5f, .3f, .18f));

        ui.Label(card, "OS", "PLORP-OS v1.0", 48, 40, 300, 30, 20, Phosphor);
        ui.Label(card, "Clock", FarmClock.IsNight ? "noche" : "día", CardW - 200, 40, 152, 30, 14, Phosphor, TextAnchor.MiddleRight);
        ui.Paragraph(card, "Hello", "> hola, terrícola. elige un juego.\n> plorp plorp.", 48, 74, CardW - 96, 48, 15,
            new Color(.6f, 1f, .6f, .85f));

        // Apps.
        float y0 = 136f;
        infos[0] = App(0, y0, "World/Sprites/fish_gold", "PESCA", "Pesca en el estanque.", () =>
        {
            if (!FarmZones.IsOpen(ZoneId.Pond)) { message("Primero abre el ESTANQUE (nubes del oeste de la granja)"); return; }
            FishingGame.Open(portrait, state, message);
        });
        infos[1] = App(1, y0 + 178f, "UI/Garden/ufo_plorp", "ABDUCCIÓN", "Sube vacas y ovejas con el rayo de la nave.", () =>
        {
            if (MinigameTickets.Count("abduction") <= 0) { message("Sin boletos: llega 1 cada 20 minutos"); return; }
            ArcadeGame.Open<AbductionGame>(portrait, state, message);
        });
        infos[2] = App(2, y0 + 356f, "World/Sprites/egg_gold", "ATRAPA HUEVOS", "Atrapa los huevos con la canasta.", () =>
        {
            if (MinigameTickets.Count("eggs") <= 0) { message("Sin boletos: llega 1 cada 20 minutos"); return; }
            ArcadeGame.Open<EggCatchGame>(portrait, state, message);
        });

        // Power button: shrinks back into the computer.
        ui.Block(card, "Power ring", CardW / 2f - 34, CardH - 84, 68, 68, PixelUIFactory.Ink);
        ui.Block(card, "Power", CardW / 2f - 30, CardH - 80, 60, 60, PixelUIFactory.Hex("#D23C3C"));
        ui.Label(card, "Power text", "APAGAR", CardW / 2f - 80, CardH - 22, 160, 18, 12, Color.white, TextAnchor.MiddleCenter);
        ui.Block(card, "Power mark", CardW / 2f - 3, CardH - 72, 6, 22, Color.white);
        ui.Hit(card, "Power hit", CardW / 2f - 40, CardH - 90, 80, 90, Close);

        // Zoom from the little screen on the desk.
        if (computerScreen != null)
        {
            Vector3 local = root.InverseTransformPoint(computerScreen.TransformPoint(computerScreen.rect.center));
            fromPos = new Vector2(local.x, local.y);
            fromScale = Mathf.Max(.08f, computerScreen.rect.width / (CardW - 56f));
        }
        else
        {
            fromPos = toPos;
            fromScale = .2f;
        }
        t = 0f;
        direction = 1f;
        Apply();
        Refresh();
    }

    Text App(int index, float y, string icon, string name, string desc, Action open)
    {
        var tile = ui.Rect(card, "App " + index, 44, y, CardW - 88, 164);
        var back = tile.gameObject.AddComponent<Image>();
        back.color = new Color(.25f, .6f, .3f, .22f);
        ui.Block(tile, "Border top", 0, 0, CardW - 88, 2, Phosphor);
        ui.Block(tile, "Border bottom", 0, 162, CardW - 88, 2, Phosphor);
        ui.Block(tile, "Icon back", 12, 18, 96, 96, new Color(.1f, .3f, .15f, 1f));
        ui.Texture(tile, "Icon", icon, 18, 28, 84, 76);
        ui.Label(tile, "Name", name, 124, 14, 300, 30, 22, Phosphor);
        ui.Paragraph(tile, "Desc", desc, 124, 46, 310, 44, 14, new Color(.75f, 1f, .75f, .9f));
        var info = ui.Paragraph(tile, "Info", "", 124, 92, 220, 60, 12, new Color(.6f, .9f, .6f, .9f));
        var playFace = ui.Panel(tile, "Play", CardW - 88 - 128, 100, 116, 52, PixelUIFactory.Green);
        ui.Label(playFace, "Caption", "JUGAR", 0, 0, 116, 52, 18, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(tile, "Play hit", 0, 0, CardW - 88, 164, () => { if (t >= 1f && direction > 0f) open(); });
        return info;
    }

    void Refresh()
    {
        bool pond = FarmZones.IsOpen(ZoneId.Pond);
        infos[0].text = pond ? "Cebos " + FishingGame.Bait() + "/" + FishingGame.MaxBait + " · pescados " + FishingGame.TotalCaught
            : "BLOQUEADO: abre el estanque en las nubes del oeste";
        infos[1].text = Tickets("abduction");
        infos[2].text = Tickets("eggs");
    }

    static string Tickets(string game)
    {
        int n = MinigameTickets.Count(game);
        string next = n < MinigameTickets.Max ? "\n+1 en " + Kitchen.Time(MinigameTickets.SecondsToNext(game)) : "";
        return "Boletos " + n + "/" + MinigameTickets.Max + " · récord " + MinigameTickets.Best(game) + next;
    }

    void Close()
    {
        if (direction < 0f) return;
        direction = -1f;
        t = Mathf.Min(t, 1f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (direction > 0f && t < 1f) t = Mathf.Min(1f, t + dt / ZoomSeconds);
        else if (direction < 0f)
        {
            t -= dt / ZoomSeconds;
            if (t <= 0f) { Popup.Close(root); return; }
        }
        Apply();
        if (Time.unscaledTime >= nextInfo)
        {
            nextInfo = Time.unscaledTime + 1f;
            Refresh();
        }
    }

    void Apply()
    {
        // Ease out when opening, ease in when closing.
        float e = direction > 0f ? 1f - Mathf.Pow(1f - t, 3f) : t * t;
        card.anchoredPosition = Vector2.Lerp(fromPos, toPos, e);
        float s = Mathf.Lerp(fromScale, 1f, e);
        card.localScale = new Vector3(s, s, 1f);
        backdrop.color = new Color(0f, 0f, .05f, .7f * e);
    }
}
