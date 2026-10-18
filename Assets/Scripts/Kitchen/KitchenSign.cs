using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "COCINA" sign in front of the yellow barn on the farm. Shows how many pots are cooking and,
/// when a dish is done, a pulsing golden "¡LISTO!". Tapping it opens the kitchen page.
/// Position in pixels of the farm design (1024x765), like FarmArtLayout.
/// </summary>
public sealed class KitchenSign : MonoBehaviour
{
    static readonly Vector2 Spot = new Vector2(640f, 262f);
    const float W = 150f, H = 52f;

    Text info;
    RectTransform ready;
    Image glow;
    float nextRefresh;

    public static KitchenSign Create(FarmWorldController world, Action open)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(world.transform, "Kitchen sign", Spot.x, Spot.y, W, H + 34);
        var sign = rect.gameObject.AddComponent<KitchenSign>();
        sign.Build(ui, open);
        return sign;
    }

    void Build(PixelUIFactory ui, Action open)
    {
        // Ready glow + "¡LISTO!" above the sign.
        ready = ui.Rect(transform, "Ready", 0, 0, W, 34);
        glow = ui.Block(ready, "Glow", 20, 4, W - 40, 26, new Color(1f, .85f, .2f, .35f));
        ui.Label(ready, "Text", "¡LISTO!", 0, 2, W, 30, 16, PixelUIFactory.Gold, TextAnchor.MiddleCenter);

        var board = ui.Panel(transform, "Board", 0, 34, W, H, PixelUIFactory.Hex("#B86F3F"));
        ui.Texture(board, "Pot", "World/Sprites/pot_cooking", 8, 6, 46, 40);
        ui.Label(board, "Title", "COCINA", 56, 6, 90, 22, 16, PixelUIFactory.Gold);
        info = ui.Label(board, "Info", "", 56, 26, 90, 18, 11, PixelUIFactory.Cream);
        ui.Hit(transform, "Tap", 0, 0, W, H + 34, () => open?.Invoke());
        Refresh();
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextRefresh)
        {
            nextRefresh = now + .5f;
            Refresh();
        }
        if (ready != null && ready.gameObject.activeSelf)
        {
            float pulse = .5f + .5f * Mathf.Sin(now * 5f);
            ready.anchoredPosition = new Vector2(0f, pulse * 4f);
            glow.color = new Color(1f, .85f + .1f * pulse, .2f, .2f + .3f * pulse);
        }
    }

    void Refresh()
    {
        int readyCount = Kitchen.ReadyCount();
        int cooking = 0;
        for (int i = 0; i < Kitchen.Slots; i++) if (!Kitchen.SlotEmpty(i) && !Kitchen.SlotReady(i)) cooking++;
        ready.gameObject.SetActive(readyCount > 0);
        info.text = readyCount > 0 ? readyCount + (readyCount == 1 ? " plato listo" : " platos listos")
            : cooking > 0 ? "cocinando " + cooking : "toca para cocinar";
    }
}
