using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown over a field or an animal when something is ready to collect: an arched golden word
/// ("¡COSECHA!", "¡HUEVO!"...) whose letters wave, with a pulsing golden glow and sparkles around it.
/// Tapping it collects. Lives in the world's "Harvest tickets" layer (above everything) and follows its target.
/// </summary>
public sealed class HarvestTicket : MonoBehaviour
{
    const float HitW = 96f, HitH = 44f;
    const int FontSize = 15;
    static readonly Color Gold = PixelUIFactory.Hex("#FFD44F");

    RectTransform rect;
    RectTransform target;
    Vector2 targetPoint;           // point in the target's local space the word floats over
    Func<int> readyCount;
    Func<FarmItem> item;
    Action collect;
    bool forAnimal;

    PixelUIFactory ui;
    CanvasGroup group;
    RectTransform body;
    Image glow;
    Text count;
    RectTransform[] letters = new RectTransform[0];
    float[] angles = new float[0];
    float radius;
    readonly RectTransform[] sparkles = new RectTransform[5];
    string word;
    float phase, nextCheck, shownAt;
    bool shown;

    public static HarvestTicket Create(Transform layer, string name, RectTransform target, Vector2 targetPoint,
        Func<int> readyCount, Func<FarmItem> item, Action collect)
    {
        var ui = new PixelUIFactory();
        var rect = ui.Rect(layer, name, 0, 0, HitW, HitH);
        rect.pivot = new Vector2(.5f, 0f); // bottom centre sits on the target point
        var ticket = rect.gameObject.AddComponent<HarvestTicket>();
        ticket.ui = ui;
        ticket.rect = rect;
        ticket.target = target;
        ticket.targetPoint = targetPoint;
        ticket.readyCount = readyCount;
        ticket.item = item;
        ticket.collect = collect;
        ticket.forAnimal = name.StartsWith("Ticket ") && !name.StartsWith("Ticket plot");
        ticket.Build();
        return ticket;
    }

    void Build()
    {
        group = gameObject.AddComponent<CanvasGroup>();
        phase = UnityEngine.Random.value * 6f;

        // Soft golden halo (also the tap area).
        glow = ui.Block(transform, "Glow", 0, 0, HitW, HitH, new Color(1f, .85f, .2f, .3f));
        glow.raycastTarget = true;

        body = ui.Rect(transform, "Word", 0, 0, HitW, HitH);
        body.anchorMin = body.anchorMax = body.pivot = new Vector2(.5f, 0f);
        body.anchoredPosition = Vector2.zero;
        count = ui.Label(body, "Count", "", -10, 0, HitW + 20, 16, 12, Color.white, TextAnchor.MiddleCenter);
        count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(.5f, 0f);
        count.rectTransform.pivot = new Vector2(.5f, 0f);
        count.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        for (int i = 0; i < sparkles.Length; i++)
            sparkles[i] = ui.Block(transform, "Sparkle " + i, 0, 0, 3, 3, Color.white).rectTransform;

        var button = gameObject.AddComponent<Button>();
        button.targetGraphic = glow;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { if (shown && collect != null) collect(); });

        Hide();
    }

    string WordFor(FarmItem it)
    {
        if (forAnimal && it != null) return "¡" + it.Single.ToUpperInvariant() + "!";
        return "¡COSECHA!";
    }

    /// <summary>Rebuilds the letters on an arch (only when the word changes).</summary>
    void SetWord(string text)
    {
        if (text == word) return;
        word = text;
        foreach (var l in letters) if (l != null) Destroy(l.gameObject);
        int n = text.Length;
        letters = new RectTransform[n];
        angles = new float[n];
        float spacing = FontSize * .66f;
        radius = 70f;
        float total = (n - 1) * spacing / radius;
        for (int i = 0; i < n; i++)
        {
            var label = ui.Label(body, "Letter", text[i].ToString(), 0, 0, FontSize * 1.3f, FontSize * 1.5f,
                FontSize, Gold, TextAnchor.MiddleCenter);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            letters[i] = rt;
            angles[i] = -total / 2f + i * (n > 1 ? total / (n - 1) : 0f);
        }
    }

    void Update()
    {
        if (readyCount == null || body == null || glow == null || rect == null) return;
        float now = Time.unscaledTime;
        if (now >= nextCheck)
        {
            nextCheck = now + .25f;
            int ready = target != null && target.gameObject.activeInHierarchy ? readyCount() : 0;
            if (ready > 0 && !shown) Show();
            else if (ready <= 0 && shown) Hide();
            if (shown)
            {
                if (item != null) SetWord(WordFor(item()));
                if (count != null) count.text = ready > 1 ? "x" + ready : "";
            }
        }
        if (!shown || target == null) return;

        // Follow the target (animals walk around).
        rect.position = target.TransformPoint(targetPoint);

        // Pop in with a bounce, then each letter waves on the arch.
        float age = now - shownAt;
        if (letters != null)
        {
            for (int i = 0; i < letters.Length; i++)
            {
                if (letters[i] == null) continue;
                float a = angles[i];
                float wave = Mathf.Sin(now * 6f + i * .6f + phase) * 2.5f;
                letters[i].anchoredPosition = new Vector2(Mathf.Sin(a) * radius,
                    (Mathf.Cos(a) - 1f) * radius + 26f + wave);
                letters[i].localEulerAngles = new Vector3(0f, 0f, -a * Mathf.Rad2Deg);
                float delay = i * .03f;
                letters[i].localScale = Vector3.one * (age < delay ? 0f : EaseOutBack(Mathf.Clamp01((age - delay) / .3f)));
            }
        }

        float pulse = .5f + .5f * Mathf.Sin(now * 5f + phase);
        glow.color = new Color(1f, .82f + .1f * pulse, .2f, .12f + .25f * pulse);
        glow.rectTransform.localScale = new Vector3(1f + .08f * pulse, 1f + .15f * pulse, 1f);

        for (int i = 0; i < sparkles.Length; i++)
        {
            if (sparkles[i] == null) continue;
            float a = now * 2f + phase + i * Mathf.PI * 2f / sparkles.Length;
            sparkles[i].anchoredPosition = new Vector2(HitW / 2f + Mathf.Cos(a) * HitW * .55f,
                -(HitH / 2f + Mathf.Sin(a) * HitH * .6f));
            float twinkle = .5f + .5f * Mathf.Sin(now * 9f + i * 1.7f);
            sparkles[i].localScale = Vector3.one * (.6f + twinkle);
            var img = sparkles[i].GetComponent<Image>();
            if (img != null) img.color = Color.Lerp(Gold, Color.white, twinkle);
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void Show()
    {
        shown = true;
        shownAt = Time.unscaledTime;
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
        }
    }

    void Hide()
    {
        shown = false;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }
}
