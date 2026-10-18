using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Harvest juice: when something is collected, its icons fly in an arc to Plorp's order card,
/// coins fly to the gold counter (which bounces when they land), and a "+120" floats up.
/// Golden harvests get a bigger golden popup.
/// </summary>
public sealed class RewardFx : MonoBehaviour
{
    public static RewardFx Instance { get; private set; }

    PixelUIFactory ui;
    RectTransform layer;
    Transform goldTarget;
    Transform foodTarget;
    float goldPunch;
    Vector3 goldBaseScale = Vector3.one;

    public static void Create(Transform portrait, Transform goldLabel)
    {
        var ui = new PixelUIFactory();
        var layer = ui.Rect(portrait, "Reward effects", 0, 0, 570, 1018);
        var fx = layer.gameObject.AddComponent<RewardFx>();
        fx.ui = ui;
        fx.layer = layer;
        fx.goldTarget = goldLabel;
        if (goldLabel != null) fx.goldBaseScale = goldLabel.localScale;
        Instance = fx;
    }

    /// <summary>Where collected food flies to (Plorp's order card).</summary>
    public static void SetFoodTarget(Transform target)
    {
        if (Instance != null) Instance.foodTarget = target;
    }

    /// <summary>Full effect for a player collection at a world position.</summary>
    public static void Collect(Vector3 from, FarmItem item, int count, long gold, bool golden = false)
    {
        if (Instance == null) return;
        Instance.Play(from, item, count, gold, golden);
    }

    /// <summary>Just the floating "+X" (used for the workers' automatic harvest).</summary>
    public static void Popup(Vector3 from, string text, Color color)
    {
        if (Instance == null) return;
        Instance.Float(from, text, color, 14);
    }

    void Play(Vector3 from, FarmItem item, int count, long gold, bool golden)
    {
        layer.SetAsLastSibling();
        var startScreens = transform.parent.Find("Start screens");
        if (startScreens != null) startScreens.SetAsLastSibling();

        int icons = Mathf.Clamp(count, 1, 6);
        if (item != null && foodTarget != null)
            for (int i = 0; i < icons; i++)
                Fly(from, foodTarget, item.Icon, 26f, i * .06f, null);
        if (goldTarget != null && gold > 0)
            for (int i = 0; i < Mathf.Min(4, 1 + count / 3); i++)
                Fly(from, goldTarget, "UI/Garden/icon_coin", 22f, .1f + i * .07f, () => goldPunch = 1f);

        if (gold > 0)
            Float(from, "+" + FarmGameState.Format(gold), golden ? PixelUIFactory.Gold : Color.white, golden ? 22 : 18);
        if (golden) Float(from + Vector3.up * 40f * layer.lossyScale.y, "¡COSECHA DORADA!", PixelUIFactory.Gold, 20);
    }

    void Fly(Vector3 from, Transform target, string icon, float size, float delay, System.Action onArrive)
    {
        var raw = ui.Texture(layer, "Flying", icon, 0, 0, size, size);
        raw.rectTransform.pivot = new Vector2(.5f, .5f);
        raw.gameObject.AddComponent<Flyer>().Launch(from, target, delay, onArrive);
    }

    void Float(Vector3 from, string text, Color color, int size)
    {
        var label = ui.Label(layer, "Popup", text, 0, 0, 260, 30, size, color, TextAnchor.MiddleCenter);
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.gameObject.AddComponent<FloatingText>().Launch(from);
    }

    void Update()
    {
        if (goldTarget == null) return;
        goldPunch = Mathf.MoveTowards(goldPunch, 0f, Time.unscaledDeltaTime * 4f);
        goldTarget.localScale = goldBaseScale * (1f + .25f * goldPunch);
    }

    /// <summary>An icon that arcs from a point to a target, shrinking as it arrives.</summary>
    sealed class Flyer : MonoBehaviour
    {
        Vector3 from, side;
        Transform target;
        float delay, age;
        const float Duration = .7f;
        System.Action arrive;
        CanvasGroup group;

        public void Launch(Vector3 start, Transform to, float wait, System.Action onArrive)
        {
            from = start;
            target = to;
            delay = wait;
            arrive = onArrive;
            transform.position = start;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            float s = transform.lossyScale.x;
            side = new Vector3(Random.Range(-60f, 60f), Random.Range(40f, 90f), 0f) * s;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = (age - delay) / Duration;
            if (t < 0f) return;
            group.alpha = 1f;
            if (t >= 1f || target == null)
            {
                arrive?.Invoke();
                Destroy(gameObject);
                return;
            }
            float e = t * t * (3f - 2f * t);
            Vector3 to = target.position;
            Vector3 mid = Vector3.Lerp(from, to, .35f) + side;
            // Quadratic Bézier arc.
            transform.position = Vector3.Lerp(Vector3.Lerp(from, mid, e), Vector3.Lerp(mid, to, e), e);
            float pop = t < .15f ? Mathf.Lerp(.4f, 1.3f, t / .15f) : Mathf.Lerp(1.3f, .6f, (t - .15f) / .85f);
            transform.localScale = Vector3.one * pop;
        }
    }

    /// <summary>Text that rises and fades.</summary>
    sealed class FloatingText : MonoBehaviour
    {
        Vector3 start;
        float age;
        Text text;

        public void Launch(Vector3 from)
        {
            start = from;
            transform.position = from;
            text = GetComponent<Text>();
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float t = age / 1.1f;
            transform.position = start + Vector3.up * (60f * t) * transform.lossyScale.y;
            transform.localScale = Vector3.one * (t < .12f ? Mathf.Lerp(.5f, 1.2f, t / .12f) : 1f);
            var c = text.color;
            c.a = t < .6f ? 1f : 1f - (t - .6f) / .4f;
            text.color = c;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
