using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "CASA DE PLORP": the inside of the yellow barn (tap it on the farm). Everything Plorp has at home:
///  - the STOVE → Plorp's kitchen page (recipes and pots);
///  - Plorp's green COMPUTER → the minigames (the screen zooms in; closing shrinks it back);
///  - house cats walking around (tap them).
/// Coordinates are page pixels (570x840; the page starts at y = 108 of the portrait).
/// </summary>
public sealed class HouseScreen : MonoBehaviour
{
    public const float PageW = 570f, PageH = 840f;
    const float FloorTop = 540f, FloorBottom = 810f;

    // Objects (page pixels).
    static readonly Rect Stove = new Rect(40, 330, 152, 140);
    static readonly Rect Computer = new Rect(318, 300, 184, 180);
    // Screen of the computer inside its sprite (92x90 sprite: x 20..72, y 8..44), scaled ×2.
    static readonly Rect ScreenInSprite = new Rect(40, 16, 104, 72);

    static readonly string[] CatColors = { "gray", "orange", "black", "white" };
    static readonly string[] CatLines =
    {
        "¡Miau!", "Prrr...", "¡Miau miau!", "*ronronea*", "Mrrp?", "¡Fsss! (era broma)", "Miau... ¿atún?",
    };

    FarmGameState state;
    Action<string> message;
    Action<int> navigate;
    Transform portrait;
    PixelUIFactory ui;

    RawImage pot;
    Image potGlow, screenGlow;
    Text potReady, stoveInfo, computerInfo;
    Vector2 potReadyBase;
    RectTransform screen, catLayer;
    float nextInfo;

    sealed class Cat
    {
        public RectTransform rect;
        public RawImage art;
        public string color;
        public Vector2 pos, target;
        public float speed, idle, step;
    }
    readonly List<Cat> cats = new List<Cat>();

    public static HouseScreen Create(Transform secondary, Transform portrait, FarmGameState state,
        Action<string> message, Action<int> navigate)
    {
        var ui = new PixelUIFactory();
        var page = ui.Rect(secondary, "Page - Casa", 0, 0, PageW, PageH);
        var house = page.gameObject.GetOrAdd<HouseScreen>();
        house.state = state;
        house.message = message;
        house.navigate = navigate;
        house.portrait = portrait;
        house.ui = ui;
        house.Build();
        page.gameObject.SetActive(false);
        return house;
    }

    void Build()
    {
        var bg = ui.Texture(transform, "Room", "house_interior", 0, 0, PageW, PageH);
        bg.raycastTarget = true;

        // Name on top and the way out.
        var title = ui.Panel(transform, "Title", 150, 6, 270, 44, PixelUIFactory.Hex("#B86F3F"));
        ui.Label(title, "Text", "CASA DE PLORP", 0, 0, 270, 44, 20, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        ui.ActionButton(transform, "Back", "VOLVER", 12, 8, 104, 40, () => navigate?.Invoke(0));
        UiUtil.CaptionSize(transform, "Back", 15);

        // ---- Stove (kitchen) ----
        potGlow = ui.Block(transform, "Pot glow", Stove.x + 10, Stove.y - 80, Stove.width - 20, 110, new Color(1f, .85f, .3f, 0f));
        ui.Texture(transform, "Stove", "house_stove", Stove.x, Stove.y, Stove.width, Stove.height);
        pot = ui.Texture(transform, "Pot", "pot_cooking", Stove.x + 26, Stove.y - 66, 100, 86);
        potReady = ui.Label(transform, "Pot ready", "¡LISTO!", Stove.x, Stove.y - 104, Stove.width, 30, 20, PixelUIFactory.Gold,
            TextAnchor.MiddleCenter);
        potReadyBase = potReady.rectTransform.anchoredPosition;
        var stovePlate = ui.Panel(transform, "Stove plate", Stove.x - 4, Stove.y + Stove.height + 6, Stove.width + 8, 46,
            PixelUIFactory.Hex("#B86F3F"));
        ui.Label(stovePlate, "Name", "COCINA", 0, 4, Stove.width + 8, 22, 16, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        stoveInfo = ui.Label(stovePlate, "Info", "", 0, 24, Stove.width + 8, 18, 11, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Stove hit", Stove.x - 10, Stove.y - 110, Stove.width + 20, Stove.height + 170, () => navigate?.Invoke(6));

        // ---- Plorp's computer (minigames) ----
        ui.Texture(transform, "Computer", "house_computer", Computer.x, Computer.y, Computer.width, Computer.height);
        screen = ui.Rect(transform, "Computer screen", Computer.x + ScreenInSprite.x, Computer.y + ScreenInSprite.y,
            ScreenInSprite.width, ScreenInSprite.height);
        screenGlow = screen.gameObject.GetOrAdd<Image>();
        screenGlow.color = new Color(.5f, 1f, .5f, 0f);
        screenGlow.raycastTarget = false;
        var pcPlate = ui.Panel(transform, "Computer plate", Computer.x + 12, Computer.y + Computer.height + 6, Computer.width - 24, 46,
            PixelUIFactory.Hex("#2E6B34"));
        ui.Label(pcPlate, "Name", "COMPUTADOR", 0, 4, Computer.width - 24, 22, 15, PixelUIFactory.Hex("#B8F5A0"), TextAnchor.MiddleCenter);
        computerInfo = ui.Label(pcPlate, "Info", "", 0, 24, Computer.width - 24, 18, 11, Color.white, TextAnchor.MiddleCenter);
        ui.Hit(transform, "Computer hit", Computer.x - 8, Computer.y - 10, Computer.width + 16, Computer.height + 66,
            () => PlorpComputer.Open(portrait, state, message, screen));

        // ---- Cats ----
        catLayer = ui.Rect(transform, "Cats", 0, 0, PageW, PageH);
        for (int i = 0; i < CatColors.Length; i++) AddCat(CatColors[i]);
        Refresh();
    }

    void AddCat(string color)
    {
        var holder = ui.Rect(catLayer, "Cat " + color, 0, 0, 64, 48);
        holder.pivot = new Vector2(.5f, 0f);
        var hit = holder.gameObject.GetOrAdd<Image>();
        hit.color = new Color(0, 0, 0, 0);
        var art = ui.Texture(holder, "Art", "cat_" + color + "_0", 0, 0, 64, 48);
        var cat = new Cat
        {
            rect = holder,
            art = art,
            color = color,
            pos = RandomFloor(),
            speed = UnityEngine.Random.Range(38f, 70f),
            idle = UnityEngine.Random.Range(0f, 2f),
        };
        cat.target = RandomFloor();
        var button = holder.gameObject.GetOrAdd<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Pet(cat));
        cats.Add(cat);
        Place(cat);
    }

    static Vector2 RandomFloor() =>
        new Vector2(UnityEngine.Random.Range(50f, PageW - 50f), UnityEngine.Random.Range(FloorTop, FloorBottom));

    void Pet(Cat cat)
    {
        string line = CatLines[UnityEngine.Random.Range(0, CatLines.Length)];
        var label = ui.Label(catLayer, "Miau", line, 0, 0, 160, 28, 16, Color.white, TextAnchor.MiddleCenter);
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.anchoredPosition = new Vector2(cat.pos.x, -(cat.pos.y - 62f));
        RisingText.Attach(label, 1.2f, 30f);
        cat.idle = 1.2f; // stops to be petted
        cat.art.texture = Art.Tex("cat_" + cat.color + "_0");
    }


    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        foreach (var cat in cats)
        {
            if (cat.idle > 0f)
            {
                cat.idle -= dt;
                continue;
            }
            var to = cat.target - cat.pos;
            if (to.magnitude < 4f)
            {
                cat.idle = UnityEngine.Random.Range(.8f, 3f);
                cat.target = RandomFloor();
                cat.art.texture = Art.Tex("cat_" + cat.color + "_0");
                continue;
            }
            cat.pos += to.normalized * cat.speed * dt;
            cat.step += dt * 8f;
            cat.art.texture = Art.Tex("cat_" + cat.color + "_" + ((int)cat.step % 2));
            cat.art.uvRect = to.x >= 0f ? new Rect(0, 0, 1, 1) : new Rect(1, 0, -1, 1);
            Place(cat);
        }
        // Cats lower on the screen are drawn in front.
        cats.Sort((a, b) => a.pos.y.CompareTo(b.pos.y));
        for (int i = 0; i < cats.Count; i++) cats[i].rect.SetSiblingIndex(i);

        // Ready pot glows; the computer screen flickers.
        bool ready = Kitchen.ReadyCount() > 0;
        float pulse = .5f + .5f * Mathf.Sin(now * 5f);
        potGlow.color = new Color(1f, .85f, .3f, ready ? .15f + .25f * pulse : 0f);
        potReady.gameObject.SetActive(ready);
        potReady.rectTransform.anchoredPosition = potReadyBase + new Vector2(0f, pulse * 4f);
        screenGlow.color = new Color(.5f, 1f, .5f, .05f + .05f * Mathf.Sin(now * 13f) + .04f * pulse);

        if (now >= nextInfo)
        {
            nextInfo = now + .5f;
            Refresh();
        }
    }

    void Refresh()
    {
        int ready = Kitchen.ReadyCount();
        int cooking = Kitchen.CookingCount();
        stoveInfo.text = ready > 0 ? ready + (ready == 1 ? " plato listo" : " platos listos")
            : cooking > 0 ? "cocinando " + cooking : "toca para cocinar";
        UiUtil.SetTexture(pot, ready > 0 ? "pot_done" : "pot_cooking");
        pot.color = ready > 0 || cooking > 0 ? Color.white : new Color(1f, 1f, 1f, .6f);
        int tickets = MinigameTickets.Total;
        computerInfo.text = tickets + " boletos · minijuegos";
    }

    static void Place(Cat cat) => cat.rect.anchoredPosition = new Vector2(cat.pos.x, -cat.pos.y);



}
