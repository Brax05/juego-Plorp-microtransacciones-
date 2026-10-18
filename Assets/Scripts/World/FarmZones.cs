using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The four areas hidden in the clouds around the farm (by position of their sign).</summary>
public enum ZoneId { Pond = 0, Orchard = 1, Hives = 2, Launch = 3 } // west, east, north, south

/// <summary>
/// The padlock signs in the clouds become new areas (Módulos 6, 9 and 10):
///  - OESTE · ESTANQUE: bought with coins; opens the fishing minigame (fish for Plorp and the kitchen).
///  - NORTE · COLMENAS and ESTE · HUERTO: bought with coins; produce honey / apples on their own over
///    time (more hives / trees = more production). Tap the sign to collect and to buy more.
///  - SUR · DESPEGUE: opens when Plorp is satisfied (all his main orders); starts the star trip (prestige).
/// Zones stay open after a star trip.
/// </summary>
public sealed class FarmZones : MonoBehaviour
{
    const string Key = "GlorpFarm.zone.";
    public const int MaxUnits = 5;

    static readonly string[] Names = { "ESTANQUE", "HUERTO", "COLMENAS", "DESPEGUE" };
    static readonly long[] OpenCost = { 2000, 20000, 8000, 0 };
    static readonly string[] Art = { "World/Sprites/zone_pond", "World/Sprites/zone_orchard", "World/Sprites/zone_hives", "World/Sprites/zone_pad" };
    static readonly Vector2[] ArtSize = { new Vector2(110, 66), new Vector2(110, 76), new Vector2(90, 60), new Vector2(120, 40) };
    // Production zones: seconds per item per unit, items stored per unit, price of the 2nd unit.
    static readonly int[] UnitSeconds = { 0, 75, 120, 0 };
    static readonly int[] StorePerUnit = { 0, 5, 4, 0 };
    static readonly long[] UnitBaseCost = { 0, 15000, 6000, 0 };
    static readonly string[] ItemId = { "fish", "apple", "honey", "" };
    static readonly long[] ItemBase = { 60, 35, 90, 0 };

    FarmGameState state;
    PepitoController pepito;
    Action<string> message;
    Transform portrait;
    PixelUIFactory ui;
    readonly RectTransform[] signs = new RectTransform[4];
    readonly RawImage[] arts = new RawImage[4];
    readonly Text[] signTitle = new Text[4], signInfo = new Text[4];
    readonly RawImage[] signIcon = new RawImage[4];
    readonly Image[] signGlow = new Image[4];
    RawImage launchShip;
    float nextRefresh;

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    static FarmGameState Current => FarmEconomy.Current;

    // ---------- State ----------

    public static bool IsOpen(ZoneId z)
    {
        if (z == ZoneId.Launch) return Current != null && Current.PlorpSatisfied;
        return PlayerPrefs.GetInt(Key + (int)z + ".open", 0) == 1;
    }

    public static long CostToOpen(ZoneId z) => OpenCost[(int)z];

    public static bool Open(FarmGameState s, ZoneId z)
    {
        if (IsOpen(z) || z == ZoneId.Launch || !s.Spend(CostToOpen(z), 0)) return false;
        PlayerPrefs.SetInt(Key + (int)z + ".open", 1);
        PlayerPrefs.SetInt(Key + (int)z + ".units", 1);
        PlayerPrefs.SetString(Key + (int)z + ".since", Now.ToString());
        PlayerPrefs.Save();
        return true;
    }

    public static int Units(ZoneId z) => Mathf.Clamp(PlayerPrefs.GetInt(Key + (int)z + ".units", 1), 1, MaxUnits);

    public static long UnitCost(ZoneId z) => (long)Math.Ceiling(UnitBaseCost[(int)z] * Math.Pow(1.8, Units(z) - 1));

    public static bool BuyUnit(FarmGameState s, ZoneId z)
    {
        if (!IsOpen(z) || Units(z) >= MaxUnits || !s.Spend(UnitCost(z), 0)) return false;
        Collect(s, z); // keep what was made so far
        PlayerPrefs.SetInt(Key + (int)z + ".units", Units(z) + 1);
        PlayerPrefs.Save();
        return true;
    }

    public static int Capacity(ZoneId z) => StorePerUnit[(int)z] * Units(z);

    static long Since(ZoneId z) =>
        long.TryParse(PlayerPrefs.GetString(Key + (int)z + ".since", Now.ToString()), out long v) ? v : Now;

    /// <summary>Items waiting in a production zone (honey / apples).</summary>
    public static int Ready(ZoneId z)
    {
        if (!IsOpen(z) || UnitSeconds[(int)z] <= 0) return 0;
        long elapsed = Math.Max(0, Now - Since(z));
        long made = elapsed / UnitSeconds[(int)z] * Units(z);
        return (int)Math.Min(Capacity(z), made);
    }

    /// <summary>Seconds until the next item.</summary>
    public static int SecondsToNext(ZoneId z)
    {
        int every = UnitSeconds[(int)z];
        if (every <= 0) return 0;
        long elapsed = Math.Max(0, Now - Since(z));
        return (int)(every - elapsed % every);
    }

    public static int Collect(FarmGameState s, ZoneId z)
    {
        int ready = Ready(z);
        long elapsed = Math.Max(0, Now - Since(z));
        int every = Math.Max(1, UnitSeconds[(int)z]);
        // Full storage: the clock restarts; otherwise the time already spent on the next item is kept.
        long since = ready >= Capacity(z) ? Now : Now - elapsed % every;
        PlayerPrefs.SetString(Key + (int)z + ".since", since.ToString());
        if (ready <= 0) return 0;
        string id = ItemId[(int)z];
        long coins = ItemValue(s, id) * ready;
        s.Collect(coins, new Dictionary<string, int> { { id, ready } });
        return ready;
    }

    /// <summary>Value of fish, honey and apples: grows with the farm level and the workers.</summary>
    public static long ItemValue(FarmGameState s, string id)
    {
        int i = Array.IndexOf(ItemId, id);
        if (i < 0) return 0;
        return (long)Math.Ceiling(ItemBase[i] * (1 + .06 * Math.Max(0, s.FarmLevel - 1)) * FarmEconomy.WorkerBonus(s));
    }

    // ---------- Signs in the clouds ----------

    public static FarmZones Create(FarmWorldController world, FarmGameState state, PepitoController pepito,
        Transform portrait, Action<string> message)
    {
        var map = world.transform.parent as RectTransform;
        if (map == null || map.name != "Farm map") return null;
        var zones = map.gameObject.AddComponent<FarmZones>();
        zones.state = state;
        zones.pepito = pepito;
        zones.portrait = portrait;
        zones.message = message;
        zones.ui = new PixelUIFactory();
        zones.Build(map);
        return zones;
    }

    void Build(RectTransform map)
    {
        // The four padlock signs (built by FarmArtLayout or saved in the prefab), sorted by where they are.
        var found = new List<RectTransform>();
        foreach (Transform child in map)
            if (child.name == "Locked zone" || child.name.StartsWith("Zone sign")) found.Add((RectTransform)child);
        if (found.Count < 4) return;
        Vector2 Center(RectTransform r) => new Vector2(r.anchoredPosition.x + r.sizeDelta.x / 2f, -r.anchoredPosition.y + r.sizeDelta.y / 2f);
        found.Sort((a, b) => Center(a).x.CompareTo(Center(b).x));
        signs[(int)ZoneId.Pond] = found[0];
        signs[(int)ZoneId.Orchard] = found[found.Count - 1];
        var middle = new List<RectTransform> { found[1], found[2] };
        middle.Sort((a, b) => Center(a).y.CompareTo(Center(b).y));
        signs[(int)ZoneId.Hives] = middle[0];
        signs[(int)ZoneId.Launch] = middle[1];

        for (int i = 0; i < 4; i++)
        {
            var sign = signs[i];
            var center = Center(sign);
            var zone = (ZoneId)i;
            sign.name = "Zone sign " + Names[i];
            for (int c = sign.childCount - 1; c >= 0; c--) Destroy(sign.GetChild(c).gameObject);

            // The area's drawing sits on the clouds, just above (below, for the north one) its sign.
            Vector2 size = ArtSize[i];
            float artY = zone == ZoneId.Hives ? center.y + 30f : center.y - 30f - size.y;
            arts[i] = ui.Texture(map, "Zone art " + Names[i], Art[i], center.x - size.x / 2f, artY, size.x, size.y);
            arts[i].transform.SetSiblingIndex(sign.GetSiblingIndex());
            if (zone == ZoneId.Launch)
            {
                launchShip = ui.Texture(map, "Zone ship", "UI/Garden/ufo_plorp", center.x - 34f, artY - 46f, 68, 60);
                launchShip.transform.SetSiblingIndex(sign.GetSiblingIndex());
            }

            const float w = 150f, h = 52f;
            sign.anchoredPosition = new Vector2(center.x - w / 2f, -(center.y - h / 2f));
            sign.sizeDelta = new Vector2(w, h);
            var bg = sign.GetComponent<Image>();
            if (bg == null) bg = sign.gameObject.AddComponent<Image>();
            bg.color = PixelUIFactory.Ink;
            bg.raycastTarget = true;
            ui.Block(sign, "Edge", 3, 3, w - 6, h - 6, PixelUIFactory.Hex("#7A3F2E"));
            ui.Block(sign, "Face", 7, 7, w - 14, h - 14, PixelUIFactory.Hex("#CE8854"));
            signGlow[i] = ui.Block(sign, "Glow", 7, 7, w - 14, h - 14, new Color(1f, .85f, .2f, 0f));
            signIcon[i] = ui.Texture(sign, "Icon", "UI/Icons/candado", 8, 9, 34, 34);
            signTitle[i] = ui.Label(sign, "Title", Names[i], 46, 6, 100, 22, 14, PixelUIFactory.Gold);
            signInfo[i] = ui.Label(sign, "Info", "", 46, 26, 100, 18, 11, Color.white);
            var button = sign.GetComponent<Button>();
            if (button == null) button = sign.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Tapped(zone));
        }
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
        float pulse = .5f + .5f * Mathf.Sin(now * 5f);
        for (int i = 0; i < 4; i++)
        {
            if (signGlow[i] == null) continue;
            var z = (ZoneId)i;
            bool glow = IsOpen(z) && (z == ZoneId.Launch || Ready(z) > 0);
            signGlow[i].color = new Color(1f, .85f, .2f, glow ? .15f + .3f * pulse : 0f);
        }
        if (launchShip != null && launchShip.gameObject.activeSelf)
            launchShip.rectTransform.localPosition += new Vector3(0f, Mathf.Sin(now * 2f) * .15f, 0f);
    }

    void Refresh()
    {
        for (int i = 0; i < 4; i++)
        {
            if (signs[i] == null) continue;
            var z = (ZoneId)i;
            bool open = IsOpen(z);
            arts[i].gameObject.SetActive(open);
            if (z == ZoneId.Launch && launchShip != null) launchShip.gameObject.SetActive(open);
            string icon = open ? IconFor(z) : "UI/Icons/candado";
            SetTexture(signIcon[i], icon);
            if (!open)
            {
                signInfo[i].text = z == ZoneId.Launch ? "Más adelante..." : FarmGameState.Format(CostToOpen(z)) + " monedas";
                signInfo[i].color = z != ZoneId.Launch && state.Gold >= CostToOpen(z) ? PixelUIFactory.Hex("#8FE86A") : Color.white;
                continue;
            }
            signInfo[i].color = Color.white;
            switch (z)
            {
                case ZoneId.Pond:
                    signInfo[i].text = "Pescar · cebos " + FishingGame.Bait() + "/" + FishingGame.MaxBait;
                    break;
                case ZoneId.Launch:
                    signInfo[i].text = "¡Viaje estelar!";
                    break;
                default:
                    int ready = Ready(z);
                    signInfo[i].text = ready > 0 ? "¡LISTO! x" + ready : "próximo en " + Kitchen.Time(SecondsToNext(z));
                    break;
            }
        }
    }

    static string IconFor(ZoneId z)
    {
        switch (z)
        {
            case ZoneId.Pond: return "World/Sprites/fish_common";
            case ZoneId.Orchard: return "World/Sprites/item_apple";
            case ZoneId.Hives: return "World/Sprites/item_honey";
            default: return "UI/Garden/ufo_plorp";
        }
    }

    static void SetTexture(RawImage image, string path)
    {
        if (image == null) return;
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null || image.texture == tex) return;
        tex.filterMode = FilterMode.Point;
        image.texture = tex;
    }

    void Say(string text) { if (pepito != null) pepito.Say(text, 7f); }

    // ---------- Taps ----------

    void Tapped(ZoneId z)
    {
        if (z == ZoneId.Launch)
        {
            if (!IsOpen(z))
            {
                message("Zona bloqueada: más adelante...");
                Say("Miau... aquí Plorp va a estacionar su nave cuando quede conforme. Cumple todos sus pedidos.");
                return;
            }
            StarTrip.Open(portrait, state, message);
            return;
        }
        if (!IsOpen(z)) { OpenUnlockWindow(z); return; }
        if (z == ZoneId.Pond) { FishingGame.Open(portrait, state, message); return; }
        OpenProductionWindow(z);
    }

    void OpenUnlockWindow(ZoneId z)
    {
        var root = Popup.Open(portrait, "Zone unlock", Names[(int)z], 250, 470, out var card);
        string what = z == ZoneId.Pond
            ? "Detrás de estas nubes hay un ESTANQUE. Ahí se pesca: los pescados son comida para Plorp y dan monedas."
            : z == ZoneId.Hives
                ? "Aquí hay lugar para COLMENAS. Las abejas hacen miel solas, aunque cierres el juego."
                : "Aquí cabe un HUERTO de manzanos. Dan manzanas solos, aunque cierres el juego.";
        Popup.Pepito(ui, card, 22, 76, 400, "Miau... " + what);
        ui.Texture(card, "Art", Art[(int)z], 541 / 2f - ArtSize[(int)z].x, 220, ArtSize[(int)z].x * 2, ArtSize[(int)z].y * 2);
        long cost = CostToOpen(z);
        bool can = state.Gold >= cost;
        ui.ActionButton(card, "Open", "ABRIR · " + FarmGameState.Format(cost) + " MONEDAS", 60, 384, 422, 66, () =>
        {
            if (Open(state, z))
            {
                Popup.Close(root);
                Refresh();
                message("¡" + Names[(int)z] + " abierto!");
                Say(z == ZoneId.Pond
                    ? "¡Miau! El estanque está listo. Toca el cartel para pescar. Plorp ya trajo una cuchara."
                    : "¡Miau! Ya está. Vuelve de vez en cuando a recoger lo que produzca.");
            }
            else message("Faltan " + FarmGameState.Format(cost - state.Gold) + " monedas");
        }, true);
        if (!can) Popup.ButtonColor(card, "Open", PixelUIFactory.Hex("#67594F"));
        Popup.CaptionSize(card, "Open", 18);
    }

    void OpenProductionWindow(ZoneId z)
    {
        RectTransform root = null;
        void Build()
        {
            if (root != null) Popup.Close(root);
            root = Popup.Open(portrait, "Zone production", Names[(int)z], 200, 600, out var card);
            var item = FarmItems.Get(ItemId[(int)z]);
            int units = Units(z), ready = Ready(z);
            string unitName = z == ZoneId.Hives ? (units == 1 ? "colmena" : "colmenas") : (units == 1 ? "manzano" : "manzanos");
            Popup.Pepito(ui, card, 22, 76, 400, ready > 0
                ? "¡Miau! Hay " + ready + " " + (ready == 1 ? item.Single : item.Name.ToLowerInvariant()) + " listas. Plorp las está mirando."
                : "Miau... todavía no hay nada. Cada " + (z == ZoneId.Hives ? "colmena" : "manzano") + " hace 1 cada " +
                  Kitchen.Time(UnitSeconds[(int)z]) + ".");

            ui.Block(card, "Shade", 22, 216, 498, 150, new Color(.15f, .07f, .03f, .45f));
            ui.Texture(card, "Art", Art[(int)z], 40, 232, ArtSize[(int)z].x * 1.1f, ArtSize[(int)z].y * 1.1f);
            ui.Label(card, "Units", units + " " + unitName + " (máx. " + MaxUnits + ")", 180, 228, 330, 26, 17, Color.white);
            ui.Label(card, "Rate", "Hace " + units + " cada " + Kitchen.Time(UnitSeconds[(int)z]) + " · guarda hasta " + Capacity(z),
                180, 258, 330, 22, 13, PixelUIFactory.Cream);
            if (item != null) ui.Texture(card, "Item", item.Icon, 180, 290, 40, 40);
            ui.Label(card, "Ready", "x" + ready + "  ·  vale " + FarmGameState.Format(ItemValue(state, item.Id)) + " c/u",
                226, 294, 290, 34, 20, PixelUIFactory.Gold);

            ui.ActionButton(card, "Collect", ready > 0 ? "RECOGER " + ready : "NADA AÚN", 60, 390, 422, 70, () =>
            {
                int got = Collect(state, z);
                if (got > 0)
                {
                    RewardFx.Collect(signs[(int)z].position, item, Mathf.Min(6, got), ItemValue(state, item.Id) * got);
                    message("+" + got + " " + item.Name.ToLowerInvariant());
                }
                Build();
                Refresh();
            }, ready > 0);

            bool maxed = units >= MaxUnits;
            long cost = UnitCost(z);
            ui.ActionButton(card, "More", maxed ? "MÁXIMO" : "+1 " + (z == ZoneId.Hives ? "COLMENA" : "MANZANO") + " · " +
                FarmGameState.Format(cost), 60, 476, 422, 62, () =>
            {
                if (BuyUnit(state, z)) { message("¡Más producción!"); Build(); Refresh(); }
                else if (!maxed) message("Faltan " + FarmGameState.Format(cost - state.Gold) + " monedas");
            }, !maxed);
            if (!maxed && state.Gold < cost) Popup.ButtonColor(card, "More", PixelUIFactory.Hex("#67594F"));
            Popup.CaptionSize(card, "More", 17);
        }
        Build();
    }
}
