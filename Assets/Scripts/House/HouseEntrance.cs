using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The yellow barn on the farm is Plorp's house: tapping it opens the house (kitchen, computer, cats).
/// A plaque at the door names it, and a golden "¡LISTO!" floats over the roof when a dish is done.
/// Positions in pixels of the farm design (1024x765).
/// </summary>
public sealed class HouseEntrance : MonoBehaviour
{
    static readonly Rect Barn = new Rect(660f, 100f, 210f, 210f);
    static readonly Vector2 Plaque = new Vector2(650f, 298f), Badge = new Vector2(712f, 70f);

    RectTransform badge;
    Image glow;
    Text info;
    float next;

    public static HouseEntrance Create(FarmWorldController world, Action open)
    {
        var ui = new PixelUIFactory();
        var root = ui.Rect(world.transform, "Plorp house", 0, 0, FarmArtLayout.Width, FarmArtLayout.Height);
        var entrance = root.gameObject.AddComponent<HouseEntrance>();

        // Tap anywhere on the barn.
        ui.Hit(root, "Barn hit", Barn.x, Barn.y, Barn.width, Barn.height, () => open?.Invoke());

        var plaque = ui.Panel(root, "Plaque", Plaque.x, Plaque.y, 150, 40, PixelUIFactory.Hex("#B86F3F"));
        ui.Label(plaque, "Name", "CASA DE PLORP", 0, 3, 150, 18, 13, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        entrance.info = ui.Label(plaque, "Info", "", 0, 20, 150, 16, 10, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(root, "Plaque hit", Plaque.x, Plaque.y, 150, 40, () => open?.Invoke());

        entrance.badge = ui.Rect(root, "Ready", Badge.x, Badge.y, 110, 34);
        entrance.glow = ui.Block(entrance.badge, "Glow", 0, 0, 110, 34, new Color(1f, .85f, .2f, .4f));
        ui.Texture(entrance.badge, "Pot", "World/Sprites/pot_done", 4, 2, 34, 30);
        ui.Label(entrance.badge, "Text", "¡LISTO!", 38, 2, 70, 30, 16, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.Hit(entrance.badge, "Tap", 0, 0, 110, 34, () => open?.Invoke());
        // The roof is drawn in the farm art, so the house stays under the golden tickets and events.
        root.SetSiblingIndex(Mathf.Max(0, world.transform.Find("Harvest tickets")?.GetSiblingIndex() ?? root.GetSiblingIndex()));
        entrance.Refresh();
        return entrance;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (now >= next)
        {
            next = now + .5f;
            Refresh();
        }
        if (badge.gameObject.activeSelf)
        {
            float pulse = .5f + .5f * Mathf.Sin(now * 5f);
            badge.anchoredPosition = new Vector2(Badge.x, -(Badge.y - pulse * 5f));
            glow.color = new Color(1f, .85f + .1f * pulse, .2f, .2f + .3f * pulse);
        }
    }

    void Refresh()
    {
        int ready = Kitchen.ReadyCount();
        badge.gameObject.SetActive(ready > 0);
        int tickets = MinigameTickets.Count("abduction") + MinigameTickets.Count("eggs");
        info.text = ready > 0 ? ready + (ready == 1 ? " plato listo" : " platos listos") : "cocina · juegos · " + tickets + " boletos";
    }
}
