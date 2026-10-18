using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "COCINA DE PLORP" page (opened from the kitchen sign on the farm). Top: the stove with its pots
/// (cooking bar, ¡LISTO! glow, a locked 3rd pot for gems). Bottom: the recipes with what each needs,
/// COCINAR and VENDER. Coordinates are page pixels (570x840; the page starts at y = 108 of the portrait).
/// </summary>
public sealed class KitchenScreen : MonoBehaviour
{
    const float SlotW = 140f, StoveTop = 114f, StoveH = 214f;
    static readonly Color Glow = new Color(1f, .83f, .3f, 0f);

    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    Text subtitle;
    RectTransform content;
    float rebuildAt = -1f;
    string lastKey;

    // Stove
    readonly Text[] slotName = new Text[Kitchen.MaxSlots];
    readonly RawImage[] slotDish = new RawImage[Kitchen.MaxSlots];
    readonly RawImage[] slotPot = new RawImage[Kitchen.MaxSlots];
    readonly Image[] slotGlow = new Image[Kitchen.MaxSlots];
    readonly RectTransform[] slotFill = new RectTransform[Kitchen.MaxSlots];
    readonly Image[] slotFillImage = new Image[Kitchen.MaxSlots];
    readonly Text[] slotTime = new Text[Kitchen.MaxSlots];
    readonly GameObject[] slotLock = new GameObject[Kitchen.MaxSlots];
    readonly GameObject[] slotBar = new GameObject[Kitchen.MaxSlots];

    public static KitchenScreen Create(Transform secondary, FarmGameState state, Action<string> message, Action back)
    {
        var page = MenuPage.Create(secondary, "Page - Cocina", "COCINA DE PLORP",
            "Cocina con lo del granero. Los platos valen más.", out var subtitle);
        var screen = page.gameObject.AddComponent<KitchenScreen>();
        screen.state = state;
        screen.message = message;
        screen.subtitle = subtitle;
        screen.ui = new PixelUIFactory();
        screen.Build(back);
        page.gameObject.SetActive(false);
        return screen;
    }

    void Build(Action back)
    {
        // Back to the farm (top-left of the header).
        ui.ActionButton(transform, "Back", "VOLVER", 24, 22, 104, 38, () => back?.Invoke());
        var backCaption = FarmUIBootstrap.FindChildRecursive(transform, "Back");
        if (backCaption != null)
        {
            var c = backCaption.Find("Caption");
            if (c != null) c.GetComponent<Text>().fontSize = 15;
        }

        // Wall of tiles, counter and the pots.
        ui.Block(transform, "Wall", 16, StoveTop, 538, StoveH, PixelUIFactory.Hex("#2A1A14"));
        for (int row = 0; row < 7; row++)
            for (int col = 0; col < 13; col++)
            {
                float x = 16 + col * 44 + (row % 2 == 1 ? -22 : 0);
                float w = 42;
                if (x < 16) { w -= 16 - x; x = 16; }
                if (x + w > 554) w = 554 - x;
                if (w > 2) ui.Block(transform, "Tile", x, StoveTop + row * 22, w, 20, PixelUIFactory.Hex("#E9DCC6"));
            }
        ui.Block(transform, "Counter", 16, StoveTop + 150, 538, 64, PixelUIFactory.Hex("#5A5F6E"));
        ui.Block(transform, "Counter edge", 16, StoveTop + 150, 538, 6, PixelUIFactory.Hex("#7D8494"));

        for (int i = 0; i < Kitchen.MaxSlots; i++)
        {
            int slot = i;
            float x = 40 + i * 172;
            slotGlow[i] = ui.Block(transform, "Glow " + i, x - 6, StoveTop + 30, SlotW + 12, 150, Glow);
            slotName[i] = ui.Label(transform, "Pot name " + i, "", x - 10, StoveTop + 4, SlotW + 20, 22, 15, Color.white,
                TextAnchor.MiddleCenter);
            slotDish[i] = ui.Texture(transform, "Pot dish " + i, "World/Sprites/dish_tortilla", x + 46, StoveTop + 28, 48, 48);
            slotPot[i] = ui.Texture(transform, "Pot " + i, "World/Sprites/pot_cooking", x + 14, StoveTop + 80, 112, 96);

            var bar = ui.Rect(transform, "Pot bar " + i, x + 10, StoveTop + 184, 120, 16);
            slotBar[i] = bar.gameObject;
            ui.Block(bar, "Back", 0, 0, 120, 16, PixelUIFactory.Hex("#2A1A14"));
            var fill = ui.Block(bar, "Fill", 0, 0, 0, 16, PixelUIFactory.Hex("#7ED957"));
            slotFill[i] = fill.rectTransform;
            slotFillImage[i] = fill;
            slotTime[i] = ui.Label(bar, "Time", "", 0, -2, 120, 20, 12, Color.white, TextAnchor.MiddleCenter);

            var locked = ui.Rect(transform, "Pot locked " + i, x, StoveTop + 40, SlotW, 140);
            ui.Block(locked, "Back", 0, 0, SlotW, 140, PixelUIFactory.Hex("#3A2A24"));
            ui.Texture(locked, "Padlock", "UI/Icons/candado", 50, 18, 40, 40);
            ui.Label(locked, "Text", "+1 OLLA", 0, 64, SlotW, 26, 16, Color.white, TextAnchor.MiddleCenter);
            ui.Texture(locked, "Gem", "UI/Shop/icon_gem", 36, 98, 26, 22);
            ui.Label(locked, "Cost", Kitchen.ExtraSlotGems.ToString(), 66, 94, 60, 30, 17, Color.white);
            slotLock[i] = locked.gameObject;

            ui.Hit(transform, "Pot hit " + i, x - 6, StoveTop, SlotW + 12, StoveH, () => TapSlot(slot));
        }

        ui.Label(transform, "Recipes title", "RECETAS", 24, StoveTop + StoveH + 6, 200, 28, 18, PixelUIFactory.Gold);
        ui.Label(transform, "Recipes hint", "Toca una olla lista para sacar el plato", 220, StoveTop + StoveH + 8, 330, 26, 12,
            PixelUIFactory.Cream, TextAnchor.MiddleRight);
        content = MenuPage.ScrollList(transform, StoveTop + StoveH + 38);

        state.Changed += OnChanged;
        Kitchen.Changed += OnChanged;
    }

    /// <summary>Editor preview (OnEnable doesn't run outside Play mode).</summary>
    public void PreviewRefresh() { if (content != null) { Rebuild(); UpdateStove(); } }

    void OnEnable()
    {
        if (content == null) return;
        lastKey = null;
        Rebuild();
        UpdateStove();
    }

    void OnChanged()
    {
        if (!gameObject.activeInHierarchy) return;
        string key = Key();
        if (key == lastKey) return;
        if (rebuildAt < 0f) rebuildAt = Time.unscaledTime + .25f;
    }

    /// <summary>What the recipe cards depend on; they are redrawn only when it changes.</summary>
    string Key()
    {
        var sb = new StringBuilder();
        sb.Append(Kitchen.FreeSlot()).Append('|');
        foreach (var r in Kitchen.Recipes)
        {
            sb.Append(Kitchen.Unlocked(state, r) ? 'u' : 'l');
            foreach (var (item, amount) in r.Ingredients) sb.Append(Math.Min(state.ItemCount(item), amount)).Append(',');
            sb.Append(state.ItemCount(r.DishId)).Append(';');
        }
        return sb.ToString();
    }

    void Update()
    {
        UpdateStove();
        if (rebuildAt >= 0f && Time.unscaledTime >= rebuildAt && !MenuPage.IsScrolling(content))
        {
            rebuildAt = -1f;
            Rebuild();
        }
    }

    // ---------- Stove ----------

    void UpdateStove()
    {
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 5f);
        int slots = Kitchen.Slots;
        for (int i = 0; i < Kitchen.MaxSlots; i++)
        {
            bool locked = i >= slots;
            bool canBuy = locked && i == slots; // only the next pot is for sale
            var r = locked ? null : Kitchen.SlotRecipe(i);
            bool ready = !locked && Kitchen.SlotReady(i);
            bool empty = !locked && r == null;

            slotLock[i].SetActive(locked && canBuy);
            slotPot[i].gameObject.SetActive(!locked);
            slotBar[i].SetActive(!locked && !empty);
            slotDish[i].gameObject.SetActive(!locked && !empty);
            slotName[i].gameObject.SetActive(!locked);
            slotGlow[i].color = ready ? new Color(1f, .83f, .3f, .2f + .25f * pulse) : Glow;
            if (locked) continue;

            if (empty)
            {
                slotName[i].text = "VACÍA";
                slotPot[i].color = new Color(1f, 1f, 1f, .45f);
                SetTexture(slotPot[i], "World/Sprites/pot_cooking");
                continue;
            }
            slotPot[i].color = Color.white;
            slotName[i].text = r.Name.ToUpperInvariant();
            SetTexture(slotDish[i], r.Dish != null ? r.Dish.Icon : "World/Sprites/dish_tortilla");
            SetTexture(slotPot[i], ready ? "World/Sprites/pot_done" : "World/Sprites/pot_cooking");
            float p = ready ? 1f : Kitchen.Progress(i);
            slotFill[i].sizeDelta = new Vector2(120f * p, 16f);
            slotFillImage[i].color = ready ? PixelUIFactory.Gold : PixelUIFactory.Hex("#7ED957");
            slotTime[i].text = ready ? "¡LISTO! TÓCALA" : Kitchen.Time((int)Kitchen.SecondsLeft(i));
            slotTime[i].color = ready ? PixelUIFactory.Ink : Color.white;
            // Dish bobs above a ready pot.
            slotDish[i].rectTransform.anchoredPosition = new Vector2(40 + i * 172 + 46, -(StoveTop + 28 - (ready ? pulse * 6f : 0f)));
        }
        if (subtitle != null)
        {
            int ready = Kitchen.ReadyCount();
            subtitle.text = ready > 0 ? "¡" + ready + (ready == 1 ? " plato listo" : " platos listos") + "! Toca la olla."
                : "Cocina con lo del granero. Los platos valen más.";
        }
    }

    static void SetTexture(RawImage image, string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null || image.texture == tex) return;
        tex.filterMode = FilterMode.Point;
        image.texture = tex;
    }

    void TapSlot(int slot)
    {
        int slots = Kitchen.Slots;
        if (slot >= slots)
        {
            if (slot != slots) return;
            if (Kitchen.BuySlot(state)) message("¡Nueva olla! Ahora cocinas " + Kitchen.Slots + " platos a la vez");
            else message("Faltan " + (Kitchen.ExtraSlotGems - state.Gems) + " gemas para la olla");
            return;
        }
        if (Kitchen.SlotEmpty(slot)) { message("Olla vacía: elige una receta abajo"); return; }
        if (!Kitchen.SlotReady(slot))
        {
            message(Kitchen.SlotRecipe(slot).Name + ": faltan " + Kitchen.Time((int)Kitchen.SecondsLeft(slot)));
            return;
        }
        var r = Kitchen.Collect(state, slot);
        if (r == null) return;
        if (r.Dish != null) RewardFx.Collect(slotPot[slot].transform.position, r.Dish, 1, 0);
        message("¡" + r.Name + " lista! Quedó en el granero (tienes " + state.ItemCount(r.DishId) + ")");
    }

    // ---------- Recipes ----------

    void Rebuild()
    {
        lastKey = Key();
        MenuPage.Clear(content);
        float y = 6;
        foreach (var r in Kitchen.Recipes) y = Card(r, y);
        MenuPage.SetHeight(content, y + 10);
    }

    float Card(Recipe r, float y)
    {
        bool unlocked = Kitchen.Unlocked(state, r);
        bool has = Kitchen.HasIngredients(state, r);
        bool free = Kitchen.FreeSlot() >= 0;
        long stock = state.ItemCount(r.DishId);
        var dish = r.Dish;

        var card = ui.Panel(content, r.DishId, 6, y, 530, 104, PixelUIFactory.Hex(unlocked ? "#9A5A33" : "#6E4A36"));
        ui.Block(card, "Icon back", 12, 16, 64, 64, PixelUIFactory.Hex("#5C3020"));
        if (dish != null) ui.Texture(card, "Icon", dish.Icon, 14, 18, 60, 60);
        ui.Label(card, "Name", r.Name.ToUpperInvariant(), 86, 8, 250, 24, 16, Color.white);
        if (stock > 0) ui.Label(card, "Stock", "Tienes " + stock, 300, 8, 100, 24, 13, PixelUIFactory.Gold, TextAnchor.MiddleRight);

        if (!unlocked)
        {
            ui.Texture(card, "Padlock", "UI/Icons/candado", 86, 40, 24, 24);
            ui.Paragraph(card, "Missing", "Primero produce: " + Kitchen.MissingProduction(state, r), 116, 36, 290, 32, 13,
                PixelUIFactory.Cream);
        }
        else
        {
            float x = 86;
            foreach (var (item, amount) in r.Ingredients)
            {
                var it = FarmItems.Get(item);
                if (it != null) ui.Texture(card, "Ingredient " + item, it.Icon, x, 36, 28, 28);
                long have = state.ItemCount(item);
                ui.Label(card, "Need " + item, Math.Min(have, amount) + "/" + amount, x + 30, 38, 54, 24, 14,
                    have >= amount ? PixelUIFactory.Hex("#8FE86A") : PixelUIFactory.Hex("#FF7B6B"));
                x += 92;
            }
        }
        ui.Label(card, "Info", "Tarda " + Kitchen.Time(r.Seconds) + "  ·  vale " +
            FarmGameState.Format(Kitchen.DishValue(state, r.DishId)) + " monedas", 86, 72, 320, 22, 12, PixelUIFactory.Cream);

        string caption = !unlocked ? "BLOQUEADA" : !has ? "FALTAN" : !free ? "OLLAS LLENAS" : "COCINAR";
        var recipe = r;
        ui.ActionButton(card, "Cook", caption, 410, stock > 0 ? 8 : 24, 110, stock > 0 ? 46 : 56, () =>
        {
            if (Kitchen.Cook(state, recipe))
                message("¡A cocinar " + recipe.Name + "! Lista en " + Kitchen.Time(recipe.Seconds));
        }, unlocked && has && free);
        var cookCaption = card.Find("Cook")?.Find("Caption")?.GetComponent<Text>();
        if (cookCaption != null) cookCaption.fontSize = caption.Length > 9 ? 13 : 17;

        if (stock > 0)
        {
            ui.ActionButton(card, "Sell", "VENDER 1", 410, 58, 110, 38, () =>
            {
                if (Kitchen.SellOne(state, recipe, out long coins))
                    message("Vendiste 1 " + recipe.Name + ": +" + FarmGameState.Format(coins) + " monedas");
            });
            var sell = card.Find("Sell");
            if (sell != null)
            {
                sell.Find("Face").GetComponent<Image>().color = PixelUIFactory.Hex("#B07A2E");
                var c = sell.Find("Caption");
                if (c != null) c.GetComponent<Text>().fontSize = 14;
            }
        }
        return y + 112;
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= OnChanged;
        Kitchen.Changed -= OnChanged;
    }
}
