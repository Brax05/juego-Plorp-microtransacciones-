using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Golden ticket that floats over a field or an animal when something is ready to collect.
/// It glows and sparkles; tapping it collects. Replaces the old "TOCAR PARA COSECHAR" bar.
/// Lives in the world's "Harvest tickets" layer (above everything) and follows its target.
/// </summary>
public sealed class HarvestTicket : MonoBehaviour
{
    const float Size = 30f;
    static readonly Color Gold = PixelUIFactory.Hex("#FFD44F");
    static readonly Color Paper = PixelUIFactory.Hex("#FFF4D6");

    RectTransform rect;
    RectTransform target;
    Vector2 targetPoint;           // point in the target's local space the ticket points at
    Func<int> readyCount;
    Func<FarmItem> item;
    Action collect;

    CanvasGroup group;
    RawImage icon;
    Text count;
    Image glow;
    RectTransform body;
    readonly RectTransform[] sparkles = new RectTransform[4];
    string iconPath;
    float phase;
    float nextCheck;
    bool shown;
    float shownAt;

    public static HarvestTicket Create(Transform layer, string name, RectTransform target, Vector2 targetPoint,
        Func<int> readyCount, Func<FarmItem> item, Action collect)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(layer, name, 0, 0, Size, Size + 8);
        rect.pivot = new Vector2(.5f, 0f); // bottom tip sits on the target point
        var ticket = rect.gameObject.AddComponent<HarvestTicket>();
        ticket.rect = rect;
        ticket.target = target;
        ticket.targetPoint = targetPoint;
        ticket.readyCount = readyCount;
        ticket.item = item;
        ticket.collect = collect;
        ticket.Build(ui);
        return ticket;
    }

    void Build(PixelUIFactory ui)
    {
        group = gameObject.AddComponent<CanvasGroup>();
        phase = UnityEngine.Random.value * 6f;

        // Golden halo behind the ticket.
        glow = ui.Block(transform, "Glow", -6, -6, Size + 12, Size + 12, new Color(1f, .85f, .2f, .4f));
        CenterPivot(glow.rectTransform, new Vector2(.5f, .5f));

        body = ui.Rect(transform, "Body", 0, 0, Size, Size + 8);
        // Scale/bob from the bottom tip.
        body.anchorMin = body.anchorMax = body.pivot = new Vector2(.5f, 0f);
        body.anchoredPosition = Vector2.zero;
        ui.Block(body, "Tip", Size / 2f - 4, Size - 1, 8, 7, PixelUIFactory.Ink);
        ui.Block(body, "Tip fill", Size / 2f - 2, Size - 1, 4, 5, Gold);
        ui.Block(body, "Border", 0, 0, Size, Size, PixelUIFactory.Ink);
        ui.Block(body, "Gold edge", 2, 2, Size - 4, Size - 4, Gold);
        ui.Block(body, "Paper", 4, 4, Size - 8, Size - 8, Paper);
        icon = ui.Texture(body, "Icon", FarmItems.Carrot.Icon, 5, 3, Size - 10, Size - 10);
        count = ui.Label(body, "Count", "", 0, Size - 14, Size + 4, 12, 10, Color.white, TextAnchor.MiddleRight);

        for (int i = 0; i < sparkles.Length; i++)
            sparkles[i] = ui.Block(transform, "Sparkle " + i, 0, 0, 3, 3, Color.white).rectTransform;

        // Whole ticket is the button (the glow makes the hit area a bit bigger than the art).
        glow.raycastTarget = true;
        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = glow;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { if (shown) collect(); });

        Hide();
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextCheck)
        {
            nextCheck = now + .25f;
            int ready = target != null && target.gameObject.activeInHierarchy ? readyCount() : 0;
            if (ready > 0 && !shown) Show();
            else if (ready <= 0 && shown) Hide();
            if (shown)
            {
                count.text = ready > 1 ? "x" + ready : "";
                SetIcon(item()?.Icon);
            }
        }
        if (!shown || target == null) return;

        // Follow the target (animals walk around).
        rect.position = target.TransformPoint(targetPoint);

        // Pop in, then bob; the halo pulses and the sparkles circle around.
        float pop = Mathf.Clamp01((now - shownAt) / .2f);
        float scale = pop < 1f ? Mathf.Lerp(.3f, 1.15f, pop) : 1f + .04f * Mathf.Sin(now * 4f + phase);
        body.localScale = new Vector3(scale, scale, 1f);
        body.anchoredPosition = new Vector2(0f, 2f * Mathf.Sin(now * 3f + phase));

        float pulse = .5f + .5f * Mathf.Sin(now * 5f + phase);
        glow.color = new Color(1f, .82f + .1f * pulse, .2f, .2f + .35f * pulse);
        glow.rectTransform.localScale = Vector3.one * (1f + .12f * pulse);

        for (int i = 0; i < sparkles.Length; i++)
        {
            float a = now * 2.2f + phase + i * Mathf.PI / 2f;
            float r = Size * .72f;
            sparkles[i].anchoredPosition = new Vector2(Size / 2f + Mathf.Cos(a) * r - 1.5f,
                -(Size / 2f + Mathf.Sin(a) * r * .8f) + 1.5f);
            float twinkle = .5f + .5f * Mathf.Sin(now * 9f + i * 1.7f);
            sparkles[i].localScale = Vector3.one * (.6f + twinkle);
            sparkles[i].GetComponent<Image>().color = Color.Lerp(Gold, Color.white, twinkle);
        }
    }

    /// <summary>Changes the pivot without moving the rect on screen.</summary>
    static void CenterPivot(RectTransform r, Vector2 pivot)
    {
        Vector2 delta = Vector2.Scale(pivot - r.pivot, r.sizeDelta);
        r.pivot = pivot;
        r.anchoredPosition += delta;
    }

    void Show()
    {
        shown = true;
        shownAt = Time.unscaledTime;
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    void Hide()
    {
        shown = false;
        group.alpha = 0f;
        group.blocksRaycasts = false;
    }

    void SetIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || path == iconPath) return;
        iconPath = path;
        var texture = Resources.Load<Texture2D>(path);
        if (texture != null) texture.filterMode = FilterMode.Point;
        icon.texture = texture;
    }
}
