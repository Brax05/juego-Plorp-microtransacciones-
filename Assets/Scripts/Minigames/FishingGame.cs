using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fishing at the pond (Módulo 6).
///  1. LANZAR uses one bait and throws the float into the water.
///  2. Wait without touching: when the float sinks and "¡PICA!" appears, tap quickly (short window).
///  3. Reel: a marker runs along a bar; tap while it is on the green part to land the fish.
/// Fish: common (60%), trout (30%) and golden (10%, harder, gives gems). Fish go to the barn (Plorp
/// orders) and give coins. Bait: up to 5, one comes back every 8 minutes.
/// </summary>
public sealed class FishingGame : MonoBehaviour
{
    public const int MaxBait = 5;
    const int BaitSeconds = 8 * 60;
    const string Key = "GlorpFarm.fish.";
    const float WaterX = 34f, WaterY = 120f, WaterW = 474f, WaterH = 330f; // in the card
    const float BarX = 46f, BarY = 520f, BarW = 450f, BarH = 34f;

    enum Phase { Idle, Waiting, Bite, Reel, Done }

    struct FishKind
    {
        public string Name, Sprite;
        public int Items, Gems;
        public float Zone, Speed;
    }

    static readonly FishKind[] Kinds =
    {
        new FishKind { Name = "PEZ",        Sprite = "fish_common", Items = 1, Gems = 0, Zone = .30f, Speed = .8f },
        new FishKind { Name = "TRUCHA",     Sprite = "fish_trout",  Items = 2, Gems = 0, Zone = .20f, Speed = 1.15f },
        new FishKind { Name = "PEZ DORADO", Sprite = "fish_gold",   Items = 3, Gems = 2, Zone = .12f, Speed = 1.6f },
    };

    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    RectTransform root, card, floatRect, bar, zone, marker, ripple;
    Image rippleImage;
    Text status, baitText, resultText;
    RawImage resultIcon;
    GameObject castButton;
    Phase phase;
    float phaseTime, biteAt, zoneStart, markerPos;
    int kind;
    Vector2 floatPos;

    // ---------- Bait ----------

    static readonly Recharge BaitStock = new Recharge(Key + "bait", Key + "baitSince", MaxBait, BaitSeconds);

    public static int Bait() => BaitStock.Count;
    static int SecondsToNextBait() => BaitStock.SecondsToNext;
    static bool UseBait() => BaitStock.Use();

    public static int TotalCaught => SaveStore.GetInt(Key + "total", 0);

    // ---------- Window ----------

    public static void Open(Transform portrait, FarmGameState state, Action<string> message)
    {
        GameAudio.PushMusic("music_pond");
        var root = Popup.Open(portrait, "Fishing", "ESTANQUE", 120, 790, out var card);
        var game = root.gameObject.GetOrAdd<FishingGame>();
        game.root = root;
        game.card = card;
        game.state = state;
        game.message = message;
        game.ui = new PixelUIFactory();
        game.Build();
    }

    void Build()
    {
        // Water with reeds and a little dock.
        ui.Texture(card, "Pond", "fishing_pond", WaterX - 12, WaterY - 12, WaterW + 24, WaterH + 24); // Art/Minigames/fishing_pond.png

        ripple = ui.Rect(card, "Ripple", 0, 0, 40, 14);
        rippleImage = ripple.gameObject.GetOrAdd<Image>();
        rippleImage.color = new Color(1, 1, 1, 0);
        rippleImage.raycastTarget = false;

        floatRect = ui.Texture(card, "Float", "fishing_float", 0, 0, 14, 28).rectTransform;
        floatRect.gameObject.SetActive(false);

        status = ui.Paragraph(card, "Status", "", 20, WaterY + WaterH + 18, 502, 40, 20, Color.white, TextAnchor.MiddleCenter);

        // Reel bar.
        bar = ui.Rect(card, "Reel bar", BarX, BarY, BarW, BarH);
        ui.Block(bar, "Back", 0, 0, BarW, BarH, PixelUIFactory.Hex("#2A1A14"));
        zone = ui.Block(bar, "Zone", 0, 3, 50, BarH - 6, PixelUIFactory.Hex("#7ED957")).rectTransform;
        marker = ui.Block(bar, "Marker", 0, -6, 8, BarH + 12, PixelUIFactory.Gold).rectTransform;
        bar.gameObject.SetActive(false);

        // Tap area over the water and the bar (bite / reel).
        ui.Hit(card, "Water hit", WaterX - 12, WaterY - 12, WaterW + 24, BarY + BarH + 10 - (WaterY - 12), OnTap);

        baitText = ui.Label(card, "Bait", "", 20, 572, 502, 26, 15, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
        var cast = ui.ActionButton(card, "Cast", "LANZAR", 156, 604, 230, 66, Cast);
        castButton = cast.gameObject;

        ui.Block(card, "Result shade", 20, 688, 502, 84, new Color(.15f, .07f, .03f, .45f));
        resultIcon = ui.Texture(card, "Result icon", "fish_common", 36, 700, 96, 54);
        resultText = ui.Paragraph(card, "Result", "Pescados en total: " + TotalCaught, 142, 694, 370, 72, 16, Color.white);
        resultIcon.gameObject.SetActive(false);

        SetPhase(Phase.Idle);
    }

    void SetPhase(Phase p)
    {
        phase = p;
        phaseTime = 0f;
        bool reel = p == Phase.Reel;
        bar.gameObject.SetActive(reel);
        floatRect.gameObject.SetActive(p == Phase.Waiting || p == Phase.Bite);
        var castPanel = card.Find("Cast");
        bool canCast = p == Phase.Idle || p == Phase.Done;
        castButton.SetActive(canCast);
        if (castPanel != null) castPanel.gameObject.SetActive(canCast);
        switch (p)
        {
            case Phase.Idle:
            case Phase.Done:
                if (p == Phase.Idle) status.text = Bait() > 0 ? "Toca LANZAR para tirar el anzuelo." : "Sin cebos. Vuelve más tarde.";
                status.color = Color.white;
                break;
            case Phase.Waiting:
                status.text = "Espera... no toques todavía.";
                status.color = PixelUIFactory.Cream;
                break;
            case Phase.Bite:
                status.text = "¡PICA! ¡TOCA YA!";
                status.color = PixelUIFactory.Gold;
                break;
            case Phase.Reel:
                status.text = "¡Tira! Toca cuando la barra amarilla esté en lo verde.";
                status.color = Color.white;
                var k = Kinds[kind];
                zoneStart = UnityEngine.Random.Range(.1f, .9f - k.Zone);
                zone.anchoredPosition = new Vector2(zoneStart * BarW, -3f);
                zone.sizeDelta = new Vector2(k.Zone * BarW, BarH - 6);
                markerPos = 0f;
                break;
        }
    }

    void Cast()
    {
        if (phase != Phase.Idle && phase != Phase.Done) return;
        if (!UseBait())
        {
            message("Sin cebos: llega 1 cada 8 minutos");
            return;
        }
        GameAudio.Play("sfx_fish_cast");
        // Which fish will bite.
        float roll = UnityEngine.Random.value;
        kind = roll < .10f ? 2 : roll < .40f ? 1 : 0;
        floatPos = new Vector2(UnityEngine.Random.Range(WaterX + 60, WaterX + WaterW - 70), UnityEngine.Random.Range(WaterY + 50, WaterY + WaterH - 90));
        biteAt = UnityEngine.Random.Range(1.6f, 4.5f);
        SetPhase(Phase.Waiting);
    }

    void OnTap()
    {
        switch (phase)
        {
            case Phase.Waiting:
                Escaped("¡Muy pronto! El pez se asustó.");
                break;
            case Phase.Bite:
                SetPhase(Phase.Reel);
                break;
            case Phase.Reel:
                float m = markerPos;
                var k = Kinds[kind];
                if (m >= zoneStart && m <= zoneStart + k.Zone) Caught();
                else Escaped("¡Se soltó! Toca cuando la barra esté en lo verde.");
                break;
        }
    }

    void Caught()
    {
        var k = Kinds[kind];
        long coins = FarmZones.ItemValue(state, "fish") * k.Items;
        state.Collect(coins, new Dictionary<string, int> { { "fish", k.Items } });
        if (k.Gems > 0) state.AddGems(k.Gems);
        SaveStore.SetInt(Key + "total", TotalCaught + k.Items);
        RewardFx.Collect(floatRect.position, FarmItems.Fish, k.Items, coins, kind == 2);
        resultIcon.gameObject.SetActive(true);
        var tex = Art.Tex(k.Sprite);
        if (tex != null) { tex.filterMode = FilterMode.Point; resultIcon.texture = tex; }
        resultText.text = "¡" + k.Name + "! +" + k.Items + (k.Items == 1 ? " pescado" : " pescados") + " y +" +
                          FarmGameState.Format(coins) + " monedas" + (k.Gems > 0 ? " y +" + k.Gems + " gemas" : "") +
                          "\nPescados en total: " + TotalCaught;
        status.text = kind == 2 ? "¡¡UN PEZ DORADO!! Plorp está llorando de emoción." : "¡Lo atrapaste! Miau.";
        status.color = PixelUIFactory.Gold;
        SetPhaseKeepStatus(Phase.Done);
    }

    void Escaped(string why)
    {
        GameAudio.Play("sfx_ui_error", .6f);
        status.text = why;
        status.color = PixelUIFactory.Hex("#FFB39A");
        SetPhaseKeepStatus(Phase.Done);
    }

    void SetPhaseKeepStatus(Phase p)
    {
        string text = status.text;
        var color = status.color;
        SetPhase(p);
        status.text = text;
        status.color = color;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        phaseTime += dt;
        int bait = Bait();
        int next = SecondsToNextBait();
        baitText.text = "Cebos: " + bait + "/" + MaxBait + (bait < MaxBait ? "  ·  +1 en " + UiUtil.Duration(next) : "");

        switch (phase)
        {
            case Phase.Waiting:
            {
                float bob = Mathf.Sin(phaseTime * 3f) * 2f;
                PlaceFloat(floatPos + new Vector2(0, bob));
                // A couple of nibbles before the real bite.
                if (phaseTime > biteAt - .8f) PlaceFloat(floatPos + new Vector2(Mathf.Sin(phaseTime * 40f) * 2f, bob));
                Ripple(phaseTime * .6f % 1f, .35f);
                if (phaseTime >= biteAt) { SetPhase(Phase.Bite); GameAudio.Play("sfx_fish_bite"); }
                break;
            }
            case Phase.Bite:
            {
                PlaceFloat(floatPos + new Vector2(0, 10f)); // sunk
                Ripple(phaseTime * 2.5f % 1f, .8f);
                float window = kind == 2 ? .6f : .85f;
                if (phaseTime >= window) Escaped("¡Muy tarde! Se comió el cebo y se fue.");
                break;
            }
            case Phase.Reel:
            {
                var k = Kinds[kind];
                markerPos = Mathf.PingPong(phaseTime * k.Speed, 1f);
                marker.anchoredPosition = new Vector2(markerPos * BarW - 4f, 6f);
                if (phaseTime > 8f) Escaped("¡Se cansó de esperar y se fue!");
                break;
            }
            default:
                rippleImage.color = new Color(1, 1, 1, 0);
                break;
        }
    }

    void PlaceFloat(Vector2 p) => floatRect.anchoredPosition = new Vector2(p.x, -p.y);

    void Ripple(float t, float strength)
    {
        float w = 20f + 40f * t;
        ripple.sizeDelta = new Vector2(w, w * .35f);
        ripple.anchoredPosition = new Vector2(floatPos.x + 7f - w / 2f, -(floatPos.y + 16f - w * .35f / 2f));
        rippleImage.color = new Color(1f, 1f, 1f, (1f - t) * .5f * strength);
    }

    void OnDestroy() => GameAudio.PopMusic("music_pond");
}
