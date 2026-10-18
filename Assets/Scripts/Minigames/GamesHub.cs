using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "SALA DE JUEGOS" (Módulos 7 and 8): the JUEGOS sign on the farm opens a window with the minigames
/// (Pesca, Abducción, Atrapa huevos), their tickets / bait and best scores.
/// </summary>
public static class GamesHub
{
    public static void Open(Transform portrait, FarmGameState state, Action<string> message)
    {
        var ui = new PixelUIFactory();
        var root = Popup.Open(portrait, "Games hub", "SALA DE JUEGOS", 120, 780, out var card);
        Popup.Pepito(ui, card, 22, 72, 400,
            "¡Miau! Minijuegos de la granja. Cada uno usa boletos (o cebos para pescar) que vuelven solos con el tiempo.");

        float y = 216;
        // Fishing
        bool pond = FarmZones.IsOpen(ZoneId.Pond);
        Card(ui, card, y, "World/Sprites/fish_gold", "PESCA",
            pond ? "Pesca en el estanque. Pescados para Plorp, monedas y peces dorados con gemas."
                 : "Primero abre el ESTANQUE: está en las nubes del oeste de la granja.",
            pond ? "Cebos " + FishingGame.Bait() + "/" + FishingGame.MaxBait + " · pescados: " + FishingGame.TotalCaught : "Bloqueado",
            pond ? "PESCAR" : "BLOQUEADO", pond, () =>
            {
                Popup.Close(root);
                FishingGame.Open(portrait, state, message);
            });
        y += 180;
        Card(ui, card, y, "UI/Garden/ufo_plorp", "ABDUCCIÓN",
            "Maneja la nave de Plorp y sube con el rayo a las vacas y ovejas que se escaparon.",
            Tickets("abduction"), "JUGAR", true, () =>
            {
                if (MinigameTickets.Count("abduction") <= 0) { message("Sin boletos: llega 1 cada 20 minutos"); return; }
                Popup.Close(root);
                ArcadeGame.Open<AbductionGame>(portrait, state, message);
            });
        y += 180;
        Card(ui, card, y, "World/Sprites/egg_gold", "ATRAPA HUEVOS",
            "Las gallinas tiran huevos: atrápalos con la canasta. ¡Cuidado con las piedras!",
            Tickets("eggs"), "JUGAR", true, () =>
            {
                if (MinigameTickets.Count("eggs") <= 0) { message("Sin boletos: llega 1 cada 20 minutos"); return; }
                Popup.Close(root);
                ArcadeGame.Open<EggCatchGame>(portrait, state, message);
            });
    }

    static string Tickets(string game)
    {
        int n = MinigameTickets.Count(game);
        string next = n < MinigameTickets.Max ? " · +1 en " + Kitchen.Time(MinigameTickets.SecondsToNext(game)) : "";
        return "Boletos " + n + "/" + MinigameTickets.Max + next + " · récord " + MinigameTickets.Best(game);
    }

    static void Card(PixelUIFactory ui, Transform card, float y, string icon, string title, string desc, string info,
        string button, bool enabled, UnityEngine.Events.UnityAction action)
    {
        var row = ui.Panel(card, title, 18, y, 506, 168, PixelUIFactory.Hex(enabled ? "#9A5A33" : "#6E4A36"));
        ui.Block(row, "Icon back", 14, 18, 84, 84, PixelUIFactory.Hex("#5C3020"));
        ui.Texture(row, "Icon", icon, 20, 26, 72, 66);
        ui.Label(row, "Name", title, 110, 12, 380, 28, 20, PixelUIFactory.Gold);
        ui.Paragraph(row, "Desc", desc, 110, 40, 380, 58, 14, Color.white);
        ui.Paragraph(row, "Info", info, 14, 114, 300, 40, 13, PixelUIFactory.Cream);
        ui.ActionButton(row, "Play", button, 330, 106, 162, 50, action, enabled);
        Popup.CaptionSize(row, "Play", 17);
    }
}

/// <summary>The JUEGOS sign on the farm (opens the games room). Shows a "!" when a game has full tickets.</summary>
public sealed class GamesSign : MonoBehaviour
{
    static readonly Vector2 Spot = new Vector2(470f, 278f); // farm design pixels, on the path by the chicken yard
    const float W = 140f, H = 52f;
    Text info;
    float next;

    public static GamesSign Create(FarmWorldController world, Action open)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(world.transform, "Games sign", Spot.x, Spot.y, W, H);
        var sign = rect.gameObject.AddComponent<GamesSign>();
        var board = ui.Panel(rect, "Board", 0, 0, W, H, PixelUIFactory.Hex("#2F5E7A"));
        ui.Texture(board, "Icon", "UI/Garden/ufo_plorp", 6, 8, 40, 36);
        ui.Label(board, "Title", "JUEGOS", 50, 6, 86, 22, 16, PixelUIFactory.Gold);
        sign.info = ui.Label(board, "Info", "", 50, 26, 86, 18, 11, Color.white);
        ui.Hit(rect, "Tap", 0, 0, W, H, () => open?.Invoke());
        return sign;
    }

    void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 1f;
        int tickets = MinigameTickets.Count("abduction") + MinigameTickets.Count("eggs");
        info.text = tickets + " boletos";
    }
}
