using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Arched tip text ("¡Toca las hojas para limpiarlas!"): each letter sits on an arc, the word pops in
/// with a bounce, waves gently, then floats up and fades.
/// </summary>
public sealed class CurvedPopup : MonoBehaviour
{
    const float Life = 3.4f;

    RectTransform[] letters;
    Text[] texts;
    float[] baseAngle;
    float radius, age;

    /// <summary>Dismisses any active CurvedPopup under the given parent.</summary>
    public static void Dismiss(Transform parent)
    {
        if (parent == null) return;
        var existing = parent.Find("Tip popup");
        if (existing != null) Destroy(existing.gameObject);
    }

    /// <summary>Shows <paramref name="text"/> arched above <paramref name="center"/> (local position in <paramref name="parent"/>).</summary>
    public static CurvedPopup Show(RectTransform parent, string text, Vector2 center, Color color, int size = 24)
    {
        Dismiss(parent);
        var ui = new PixelUIFactory();
        var root = ui.Rect(parent, "Tip popup", 0, 0, 10, 10);
        root.anchoredPosition = new Vector2(center.x, -center.y);
        root.SetAsLastSibling();
        var popup = root.gameObject.AddComponent<CurvedPopup>();
        popup.Build(ui, text, color, size);
        return popup;
    }

    void Build(PixelUIFactory ui, string text, Color color, int size)
    {
        int n = text.Length;
        letters = new RectTransform[n];
        texts = new Text[n];
        baseAngle = new float[n];
        float spacing = size * .62f;                  // approximate letter width
        radius = Mathf.Max(140f, n * spacing / 1.6f);  // longer tips get a flatter arc
        float total = (n - 1) * spacing / radius;      // arc length in radians
        for (int i = 0; i < n; i++)
        {
            var label = ui.Label(transform, "Letter", text[i].ToString(), 0, 0, size * 1.2f, size * 1.4f, size, color,
                TextAnchor.MiddleCenter);
            label.rectTransform.pivot = new Vector2(.5f, .5f);
            letters[i] = label.rectTransform;
            texts[i] = label;
            baseAngle[i] = -total / 2f + i * (n > 1 ? total / (n - 1) : 0f);
        }
        Update();
    }

    void Update()
    {
        if (letters == null || texts == null) return;
        age += Time.unscaledDeltaTime;
        float t = age / Life;
        float pop = age < .35f ? EaseOutBack(age / .35f) : 1f;
        float rise = t > .75f ? (t - .75f) / .25f : 0f;
        float alpha = 1f - rise;
        for (int i = 0; i < letters.Length; i++)
        {
            if (letters[i] == null || texts[i] == null) continue;
            float a = baseAngle[i];
            float wave = Mathf.Sin(age * 6f + i * .5f) * 3f;
            // Points on a circle whose centre is below the text → letters form an arch.
            var pos = new Vector2(Mathf.Sin(a) * radius, (Mathf.Cos(a) - 1f) * radius + wave + rise * 40f);
            letters[i].anchoredPosition = pos;
            letters[i].localEulerAngles = new Vector3(0f, 0f, -a * Mathf.Rad2Deg);
            // Letters pop one after another.
            float delay = i * .02f;
            float s = age < delay ? 0f : Mathf.Min(pop, EaseOutBack(Mathf.Clamp01((age - delay) / .3f)));
            letters[i].localScale = Vector3.one * s;
            var c = texts[i].color;
            c.a = alpha;
            texts[i].color = c;
        }
        if (age >= Life) Destroy(gameObject);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
