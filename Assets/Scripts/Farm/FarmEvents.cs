using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Random things that happen on the farm every couple of minutes (only while the player is looking at it):
///  - CUERVO: lands on a field and eats ripe crops unless you tap it (tapping gives coins).
///  - LLUVIA: one minute of rain, crops and animals produce twice as fast.
///  - ZORRO: sneaks to the chicken yard and steals eggs from the barn unless you tap it (tapping gives a gem).
///  - CAMIÓN DE FERIA: parks for a while and buys your spare food at double price.
/// Pepito announces each one and the view glides over to where it happens.
/// Coordinates are pixels of the farm design (1024x765, top-left), like FarmArtLayout.
/// </summary>
public sealed class FarmEvents : MonoBehaviour
{
    enum Kind { None, Crow, Rain, Fox, Truck }

    const float FirstDelay = 60f;
    const float MinGap = 100f, MaxGap = 180f;
    const float RainSeconds = 60f;
    const float TruckStay = 45f;
    const float CrowPeckSeconds = 8f, FoxSeconds = 9f;

    /// <summary>×2 growth while it rains (read by FarmEconomy).</summary>
    public static float GrowthMultiplier { get; private set; } = 1f;

    /// <summary>Set every frame by FarmUIBootstrap: farm tab visible and nothing else on top.</summary>
    public bool Running;

    FarmGameState state;
    FarmWorldController world;
    PepitoController pepito;
    Action<string> message;
    Transform portrait;
    PixelUIFactory ui;
    RectTransform layer, map, viewport, rain;
    ScrollRect scroll;

    float nextEvent = FirstDelay;
    Kind current = Kind.None, last = Kind.None;
    float eventTime;          // seconds the current event has been running (only counts while Running)

    // Actor of the current event (crow / fox / truck).
    RectTransform actor;
    RawImage actorArt;
    Text marker;
    Vector2 from, to;
    int phase;                // event-specific: 0 arriving, 1 working, 2 leaving
    float phaseTime;
    CropPlot targetPlot;
    bool tapped;

    // Rain
    readonly List<RectTransform> drops = new List<RectTransform>();
    Image rainTint;

    // Camera glide
    Vector2 panFrom, panTo;
    float panT = 1f;

    // Fair truck sale window
    RectTransform salePanel;

    public static FarmEvents Create(FarmWorldController world, FarmGameState state, PepitoController pepito,
        Transform portrait, Action<string> message)
    {
        var events = world.gameObject.GetOrAdd<FarmEvents>();
        events.world = world;
        events.state = state;
        events.pepito = pepito;
        events.portrait = portrait;
        events.message = message;
        events.Build();
        return events;
    }

    void Build()
    {
        ui = new PixelUIFactory();
        layer = ui.Rect(transform, "Farm events", 0, 0, FarmArtLayout.Width, FarmArtLayout.Height);
        layer.SetAsLastSibling();
        map = transform.parent as RectTransform;
        viewport = map != null ? map.parent as RectTransform : null;
        scroll = viewport != null ? viewport.GetComponent<ScrollRect>() : null;

        if (viewport != null)
        {
            // Authored at 700: ScreenLayout grows it with the farm view.
            rain = ui.Rect(viewport, "Rain", 0, 0, 570, 700);
            rainTint = ui.Block(rain, "Tint", 0, 0, 570, 700, new Color(.2f, .3f, .55f, .22f));
            for (int i = 0; i < 46; i++)
            {
                var drop = ui.Texture(rain, "Drop", "fx_raindrop", 0, 0, 2, 16).rectTransform; // Art/Effects/fx_raindrop.png
                drop.localEulerAngles = new Vector3(0, 0, -12f);
                drops.Add(drop);
            }
            rain.gameObject.SetActive(false);
        }
    }

    // ---------- Loop ----------

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdatePan(dt);
        if (current == Kind.Rain || (current == Kind.None && rain != null && rain.gameObject.activeSelf)) UpdateRainDrops(dt);
        if (!Running) return; // paused on other tabs and while any window (like the truck sale) is open

        if (current == Kind.None)
        {
            nextEvent -= dt;
            if (nextEvent <= 0f) StartRandom();
            return;
        }

        eventTime += dt;
        phaseTime += dt;
        switch (current)
        {
            case Kind.Crow: UpdateCrow(); break;
            case Kind.Rain: UpdateRain(); break;
            case Kind.Fox: UpdateFox(); break;
            case Kind.Truck: UpdateTruck(); break;
        }
        UpdateMarker();
    }

    void StartRandom()
    {
        // Weighted pick, never the same event twice in a row.
        var options = new List<Kind>();
        void Add(Kind k, int w) { if (k != last) for (int i = 0; i < w; i++) options.Add(k); }
        Add(Kind.Crow, 35);
        Add(Kind.Rain, 20);
        if (FarmEconomy.AnimalCount(state, 0) > 0) Add(Kind.Fox, 25);
        Add(Kind.Truck, 20);
        var kind = options[UnityEngine.Random.Range(0, options.Count)];
        current = last = kind;
        eventTime = phaseTime = 0f;
        phase = 0;
        tapped = false;
        switch (kind)
        {
            case Kind.Crow: StartCrow(); break;
            case Kind.Rain: StartRain(); break;
            case Kind.Fox: StartFox(); break;
            case Kind.Truck: StartTruck(); break;
        }
    }

    void Finish()
    {
        if (actor != null) UiUtil.Kill(actor.gameObject);
        actor = null;
        actorArt = null;
        marker = null;
        targetPlot = null;
        current = Kind.None;
        nextEvent = UnityEngine.Random.Range(MinGap, MaxGap);
    }

    void Say(string text, float seconds = 7f)
    {
        if (pepito != null) pepito.Say(text, seconds);
    }

    // ---------- Crow ----------

    void StartCrow()
    {
        // The field with the most ripe crops (any active one if none is ripe).
        targetPlot = null;
        int best = -1;
        foreach (var plot in world.Plots)
        {
            if (!plot.gameObject.activeInHierarchy) continue;
            int ready = plot.ReadyCount;
            if (ready > best) { best = ready; targetPlot = plot; }
        }
        if (targetPlot == null) { Finish(); return; }
        int index = 0;
        for (int i = 0; i < world.Plots.Count; i++) if (world.Plots[i] == targetPlot) index = i;
        var field = FarmArtLayout.Fields[Mathf.Clamp(index, 0, FarmArtLayout.Fields.Length - 1)];
        to = (field[0] + field[1] + field[2] + field[3]) / 4f + new Vector2(0, -8);
        from = to + new Vector2(-320f, -220f);
        Spawn("Crow", "crow_0", 46, 36, from, () =>
        {
            if (tapped) return;
            tapped = true;
            phase = 2;
            phaseTime = 0f;
            from = Pos();
            to = from + new Vector2(260f, -260f);
            long coins = Math.Max(20, (long)(FarmEconomy.IncomePerSecond(state) * 15));
            state.BuyGold(0, coins);
            RewardFx.Collect(actor.position, null, 0, coins);
            GameAudio.Play("sfx_crow");
            message("¡Fuera, cuervo! +" + FarmGameState.Format(coins) + " monedas");
        });
        PanTo(to);
        string crop = FarmItems.ForCrop(targetPlot.Kind).Name.ToLowerInvariant();
        Say("¡Miau! Un cuervo viene por tus " + crop + ". ¡Tócalo para espantarlo!");
        GameAudio.Play("sfx_crow", .6f);
        CurvedPopup.Show(portrait as RectTransform, "¡UN CUERVO!", new Vector2(285f, 300f), PixelUIFactory.Gold, 26);
    }

    void UpdateCrow()
    {
        float flap = Mathf.Repeat(Time.unscaledTime * 8f, 2f);
        switch (phase)
        {
            case 0: // flying in
            {
                float t = Mathf.Clamp01(phaseTime / 2.4f);
                var p = Vector2.Lerp(from, to, 1f - Mathf.Pow(1f - t, 2f)) + new Vector2(0f, -Mathf.Sin(t * Mathf.PI) * 30f);
                Place(p);
                SetFrame(flap < 1f ? "crow_0" : "crow_1");
                if (t >= 1f) { phase = 1; phaseTime = 0f; SetFrame("crow_1"); }
                break;
            }
            case 1: // pecking
            {
                float peck = Mathf.Abs(Mathf.Sin(phaseTime * 9f)) * 4f;
                Place(to + new Vector2(0f, peck));
                if (phaseTime >= CrowPeckSeconds)
                {
                    int eaten = targetPlot != null ? targetPlot.StealReady(4) : 0;
                    var item = targetPlot != null ? FarmItems.ForCrop(targetPlot.Kind) : null;
                    if (eaten > 0 && item != null)
                        Say("Miau... el cuervo se comió " + eaten + " " + (eaten == 1 ? item.Single : item.Name.ToLowerInvariant()) +
                            ". La próxima vez, tócalo rápido.", 6f);
                    phase = 2;
                    phaseTime = 0f;
                    from = Pos();
                    to = from + new Vector2(300f, -240f);
                }
                break;
            }
            default: // leaving
            {
                float t = Mathf.Clamp01(phaseTime / 1.4f);
                Place(Vector2.Lerp(from, to, t * t));
                SetFrame(flap < 1f ? "crow_0" : "crow_1");
                if (t >= 1f) Finish();
                break;
            }
        }
    }

    // ---------- Rain ----------

    void StartRain()
    {
        if (rain == null) { Finish(); return; }
        rain.gameObject.SetActive(true);
        rain.SetAsLastSibling();
        float rainH = rain.rect.height;
        foreach (var d in drops) d.anchoredPosition = new Vector2(UnityEngine.Random.Range(0f, 600f), -UnityEngine.Random.Range(0f, rainH));
        GrowthMultiplier = 2f;
        Say("Miau... ¡está lloviendo! Por un minuto todo crece el doble de rápido. Yo me quedo bajo techo.");
        CurvedPopup.Show(portrait as RectTransform, "¡LLUVIA! x2", new Vector2(285f, 300f), PixelUIFactory.Hex("#9FD3F0"), 26);
    }

    void UpdateRain()
    {
        if (eventTime < RainSeconds) return;
        GrowthMultiplier = 1f;
        Say("Miau. Paró de llover. Los cultivos quedaron felices.", 4f);
        Finish();
    }

    void UpdateRainDrops(float dt)
    {
        if (rain == null) return;
        bool raining = current == Kind.Rain;
        // Fade in/out with the event.
        float a = rainTint.color.a;
        a = Mathf.MoveTowards(a, raining ? .22f : 0f, dt * .3f);
        rainTint.color = new Color(.2f, .3f, .55f, a);
        float rainH = rain.rect.height;
        float alpha = a / .22f;
        foreach (var d in drops)
        {
            var p = d.anchoredPosition + new Vector2(110f, -620f) * dt;
            if (p.y < -rainH) p = new Vector2(UnityEngine.Random.Range(-80f, 570f), 10f);
            d.anchoredPosition = p;
            var img = d.GetComponent<RawImage>();
            if (img != null) img.color = new Color(1f, 1f, 1f, alpha);
        }
        if (!raining && a <= 0f) rain.gameObject.SetActive(false);
    }

    // ---------- Fox ----------

    static readonly Vector2 FoxStart = new Vector2(700f, 236f), FoxEnd = new Vector2(560f, 236f);

    void StartFox()
    {
        from = FoxStart;
        to = FoxEnd;
        Spawn("Fox", "fox_0", 52, 38, from, () =>
        {
            if (tapped || phase == 2) return;
            tapped = true;
            phase = 2;
            phaseTime = 0f;
            from = Pos();
            to = from + new Vector2(260f, 30f);
            if (actorArt != null) actorArt.uvRect = new Rect(1, 0, -1, 1); // face right to run away
            state.AddGems(1);
            RewardFx.Popup(actor.position, "+1 gema", PixelUIFactory.Hex("#9FE8FF"));
            GameAudio.Play("sfx_fox");
            message("¡El zorro huyó! Las gallinas te regalan 1 gema");
        });
        PanTo((FoxStart + FoxEnd) / 2f);
        Say("¡Miau! ¡Un zorro ronda el gallinero! Tócalo antes de que se lleve los huevos.");
        CurvedPopup.Show(portrait as RectTransform, "¡UN ZORRO!", new Vector2(285f, 300f), PixelUIFactory.Hex("#FF9A4A"), 26);
    }

    void UpdateFox()
    {
        float step = Mathf.Repeat(Time.unscaledTime * 6f, 2f);
        SetFrame(step < 1f ? "fox_0" : "fox_1");
        switch (phase)
        {
            case 0: // sneaking in
            {
                float t = Mathf.Clamp01(phaseTime / FoxSeconds);
                Place(Vector2.Lerp(from, to, t));
                if (t >= 1f)
                {
                    int eggs = state.TakeItems("egg", 3);
                    Say(eggs > 0
                        ? "Miau... el zorro se llevó " + eggs + (eggs == 1 ? " huevo" : " huevos") + " del granero. ¡Qué descarado!"
                        : "Miau... el zorro asustó a las gallinas, pero no encontró huevos. Ja.", 6f);
                    phase = 2;
                    phaseTime = 0f;
                    from = Pos();
                    to = from + new Vector2(300f, 20f);
                    if (actorArt != null) actorArt.uvRect = new Rect(1, 0, -1, 1);
                }
                break;
            }
            default: // running away
            {
                float t = Mathf.Clamp01(phaseTime / 1.2f);
                Place(Vector2.Lerp(from, to, t));
                if (t >= 1f) Finish();
                break;
            }
        }
    }

    // ---------- Fair truck ----------

    static readonly Vector2 TruckPark = new Vector2(160f, 640f);

    void StartTruck()
    {
        from = TruckPark + new Vector2(-320f, 0f);
        to = TruckPark;
        Spawn("Truck", "truck", 92, 62, from, OpenSale);
        PanTo(TruckPark);
        Say("¡Miau! Llegó el CAMIÓN DE FERIA. Compra comida a precio doble. Tócalo para venderle.");
        GameAudio.Play("sfx_truck_honk");
        CurvedPopup.Show(portrait as RectTransform, "¡CAMIÓN DE FERIA!", new Vector2(285f, 300f), PixelUIFactory.Gold, 24);
    }

    void UpdateTruck()
    {
        float bump = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f)) * 1.5f;
        switch (phase)
        {
            case 0:
            {
                float t = Mathf.Clamp01(phaseTime / 3f);
                Place(Vector2.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)) + new Vector2(0, bump));
                if (t >= 1f) { phase = 1; phaseTime = 0f; }
                break;
            }
            case 1:
                Place(to);
                if (phaseTime >= TruckStay) LeaveTruck(false);
                break;
            default:
            {
                float t = Mathf.Clamp01(phaseTime / 2.5f);
                Place(Vector2.Lerp(from, to, t * t) + new Vector2(0, bump));
                if (t >= 1f) Finish();
                break;
            }
        }
    }

    void LeaveTruck(bool sold)
    {
        if (phase == 2) return;
        if (!sold) Say("Miau. El camión de feria se fue. Volverá otro día.", 4f);
        phase = 2;
        phaseTime = 0f;
        from = Pos();
        to = from + new Vector2(420f, 0f);
    }

    /// <summary>What the truck buys: the food you have the most spare of (Plorp's order is kept aside).</summary>
    FarmItem BestSale(out int amount, out long price)
    {
        FarmItem best = null;
        amount = 0;
        price = 0;
        var goal = state.CurrentGoal;
        foreach (var item in FarmItems.All)
        {
            long have = state.ItemCount(item.Id);
            long needed = 0;
            if (goal != null)
                foreach (var line in goal.Lines) if (line.Item.Id == item.Id) needed += line.Amount;
            int spare = (int)Math.Min(999, have - needed);
            if (spare <= 0) continue;
            long value = 2 * FarmEconomy.UnitValue(state, item) * spare;
            if (value > price) { best = item; amount = spare; price = value; }
        }
        return best;
    }

    void OpenSale()
    {
        if (phase != 1 || salePanel != null) return;
        var item = BestSale(out int amount, out long price);
        var root = Popup.OpenBlank(portrait, "Fair truck sale");
        salePanel = root;
        ui.Backdrop(root, "Backdrop", Popup.W, Popup.H, new Color(0f, 0f, .05f, .65f));
        var card = ui.Panel(root, "Card", 40, 300 + ScreenLayout.Extra * .5f, 490, 380,
            PixelUIFactory.Hex("#CE8854"), true);
        ui.Label(card, "Title", "CAMIÓN DE FERIA", 0, 16, 490, 40, 28, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.Texture(card, "Truck", "truck", 24, 70, 138, 93);
        if (item == null)
        {
            ui.Paragraph(card, "Text", "Miau... no tienes comida de sobra para venderle. Lo que hay en el granero " +
                "es para el pedido de Plorp.", 176, 70, 290, 110, 17, Color.white);
            ui.ActionButton(card, "Close", "OK", 145, 290, 200, 64, () => CloseSale(false));
            return;
        }
        ui.Paragraph(card, "Text", "Miau, el camionero compra tus " + item.Name.ToLowerInvariant() +
            " a precio doble. Lo del pedido de Plorp no se toca.", 176, 70, 290, 110, 17, Color.white);
        ui.Block(card, "Offer shade", 20, 180, 450, 86, new Color(.15f, .07f, .03f, .45f));
        ui.Texture(card, "Item", item.Icon, 40, 196, 52, 52);
        ui.Label(card, "Amount", "x" + FarmGameState.Format(amount), 100, 196, 120, 52, 26, Color.white);
        ui.Texture(card, "Coin", "icon_coin", 236, 202, 40, 40);
        ui.Label(card, "Price", "+" + FarmGameState.Format(price), 282, 196, 180, 52, 30, PixelUIFactory.Gold);
        ui.ActionButton(card, "No", "NO, GRACIAS", 24, 290, 200, 64, () => CloseSale(false));
        var no = card.Find("No");
        if (no != null) no.Find("Face").GetComponent<Image>().color = PixelUIFactory.Hex("#8A6A55");
        ui.ActionButton(card, "Sell", "¡VENDER!", 240, 290, 226, 64, () =>
        {
            int sold = state.TakeItems(item.Id, amount);
            long earned = sold == amount ? price : 2 * FarmEconomy.UnitValue(state, item) * sold;
            if (sold > 0)
            {
                state.BuyGold(0, earned);
                if (actor != null) RewardFx.Collect(actor.position, null, 0, earned);
                message("Vendiste " + sold + " " + item.Name.ToLowerInvariant() + ": +" + FarmGameState.Format(earned) + " monedas");
            }
            CloseSale(sold > 0);
        });
    }

    void CloseSale(bool sold)
    {
        if (salePanel != null) Popup.Close(salePanel);
        salePanel = null;
        if (sold)
        {
            Say("Miau. El camionero dice gracias. Plorp dice \"plorp\". Yo digo: ¿y mi atún?", 5f);
            LeaveTruck(true);
        }
    }

    // ---------- Actors ----------

    void Spawn(string name, string sprite, float w, float h, Vector2 at, Action onTap)
    {
        if (actor != null) UiUtil.Kill(actor.gameObject);
        actor = ui.Rect(layer, name, 0, 0, w, h);
        actor.pivot = new Vector2(.5f, 0f); // feet
        var hit = actor.gameObject.GetOrAdd<Image>();
        hit.color = new Color(1, 1, 1, 0);
        actorArt = ui.Texture(actor, "Art", "" + sprite, 0, 0, w, h);
        var button = actor.gameObject.GetOrAdd<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onTap());
        // Bigger tap area than the drawing.
        actor.sizeDelta = new Vector2(w + 20f, h + 16f);
        actorArt.rectTransform.anchoredPosition = new Vector2(10f, -8f);
        // Bouncing "!" so the player notices it.
        marker = ui.Label(actor, "Alert", "!", (w + 20f) / 2f - 12f, -30f, 24f, 30f, 28, PixelUIFactory.Gold,
            TextAnchor.MiddleCenter);
        Place(at);
    }

    void UpdateMarker()
    {
        if (marker == null) return;
        bool show = (current == Kind.Crow || current == Kind.Fox) ? phase < 2 && !tapped : current == Kind.Truck && phase == 1;
        marker.gameObject.SetActive(show);
        if (show) marker.rectTransform.anchoredPosition = new Vector2(marker.rectTransform.anchoredPosition.x,
            -30f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 8f);
    }

    void Place(Vector2 designPoint)
    {
        if (actor != null) actor.anchoredPosition = new Vector2(designPoint.x, -designPoint.y);
    }

    Vector2 Pos() => actor != null ? new Vector2(actor.anchoredPosition.x, -actor.anchoredPosition.y) : Vector2.zero;

    void SetFrame(string sprite)
    {
        if (actorArt == null) return;
        var tex = Art.Tex("" + sprite);
        if (tex == null || actorArt.texture == tex) return;
        tex.filterMode = FilterMode.Point;
        actorArt.texture = tex;
    }

    // ---------- Camera ----------

    /// <summary>Glides the farm view so the event is in the middle of the screen.</summary>
    void PanTo(Vector2 designPoint)
    {
        if (map == null || viewport == null) return;
        if (scroll != null) scroll.StopMovement();
        float zoom = map.localScale.x;
        float w = viewport.rect.width, h = viewport.rect.height;
        float x = w / 2f - (FarmArtLayout.MarginX + designPoint.x) * zoom;
        float y = (FarmArtLayout.MarginY + designPoint.y) * zoom - h / 2f;
        x = Mathf.Clamp(x, Mathf.Min(0f, w - FarmArtLayout.MapWidth * zoom), 0f);
        y = Mathf.Clamp(y, -FarmArtLayout.CoveredTop, Mathf.Max(0f, FarmArtLayout.MapHeight * zoom - h));
        panFrom = map.anchoredPosition;
        panTo = new Vector2(x, y);
        panT = 0f;
    }

    void UpdatePan(float dt)
    {
        if (panT >= 1f || map == null) return;
        panT = Mathf.Min(1f, panT + dt / .8f);
        float e = 1f - Mathf.Pow(1f - panT, 3f);
        map.anchoredPosition = Vector2.Lerp(panFrom, panTo, e);
    }

    void OnDestroy()
    {
        GrowthMultiplier = 1f;
    }
}
