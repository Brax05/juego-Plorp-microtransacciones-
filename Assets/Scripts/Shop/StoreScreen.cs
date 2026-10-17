using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Tienda" tab with two sections:
///  - BOOSTS: time boosts and items bought with gems (x2 income, turbo, garden, time skips, monedas de flor).
///  - GEMAS: gem packs for real money ($50 to $1.000 CLP) through PurchaseService.
/// </summary>
public sealed class StoreScreen : MonoBehaviour
{
    /// <summary>Tab to show next time the store opens (0 boosts, 1 gems). Set by the HUD buttons.</summary>
    public static int PendingTab = -1;

    FarmGameState state;
    Action<string> message;
    PixelUIFactory ui;
    Text subtitle;
    RectTransform content;
    Image[] tabFaces = new Image[2];
    int tab;
    float nextRefresh;

    public static StoreScreen Create(Transform secondary, FarmGameState state, Action<string> message)
    {
        var page = MenuPage.Create(secondary, "Page - Tienda (nueva)", "TIENDA", "", out var subtitle);
        var screen = page.gameObject.AddComponent<StoreScreen>();
        screen.state = state;
        screen.message = message;
        screen.subtitle = subtitle;
        screen.Build();
        page.gameObject.SetActive(false);
        return screen;
    }

    void Build()
    {
        ui = new PixelUIFactory();
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            var tabPanel = ui.Panel(transform, "Tab " + i, 30 + i * 260, 112, 250, 46, PixelUIFactory.Green);
            tabFaces[i] = tabPanel.Find("Face").GetComponent<Image>();
            ui.Label(tabPanel, "Caption", i == 0 ? "BOOSTS" : "GEMAS", 0, 0, 250, 46, 20, Color.white, TextAnchor.MiddleCenter);
            ui.Hit(transform, "Tab hit " + i, 30 + i * 260, 112, 250, 46, () => { tab = index; Rebuild(); });
        }
        content = MenuPage.ScrollList(transform, 166);
        state.Changed += OnStateChanged;
        PurchaseService.Changed += Rebuild;
    }

    void OnEnable()
    {
        if (PendingTab >= 0) { tab = PendingTab; PendingTab = -1; }
        if (ui != null) Rebuild();
    }

    float rebuildAt = -1f;

    // Gold/gems change on every harvest: rebuild at most twice per second while visible.
    void OnStateChanged()
    {
        if (gameObject.activeInHierarchy && rebuildAt < 0f) rebuildAt = Time.unscaledTime + .5f;
    }

    void Update()
    {
        if (rebuildAt >= 0f && Time.unscaledTime >= rebuildAt)
        {
            rebuildAt = -1f;
            Rebuild();
        }
        // Boost timers tick down on screen.
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        subtitle.text = Subtitle();
    }

    string Subtitle()
    {
        string boosts = state.Boosts.Summary;
        return tab == 0
            ? (boosts.Length > 0 ? "Activos: " + boosts : "Acelera tu granja con gemas")
            : (PurchaseService.TestMode ? "Modo prueba: las compras se simulan" : "Pago seguro con tu tienda de apps");
    }

    void Rebuild()
    {
        if (content == null) return;
        for (int i = 0; i < 2; i++) tabFaces[i].color = i == tab ? PixelUIFactory.Green : PixelUIFactory.Hex("#67594F");
        subtitle.text = Subtitle();
        MenuPage.Clear(content);
        float y = 6;
        if (tab == 0)
        {
            y = MenuPage.Section(ui, content, "BOOSTS DE TIEMPO", y);
            foreach (var boost in ShopCatalog.Boosts)
            {
                var b = boost;
                bool active = state.Boosts.Active(b.Kind);
                y = Row(y, b.Icon, b.Name + (active ? "  (activo, se suma)" : ""), b.Description,
                    b.Gems, () => message(state.BuyBoost(b) ? b.Name + " activado" : "Faltan gemas"));
            }
            y = MenuPage.Section(ui, content, "OBJETOS", y + 6);
            foreach (var item in ShopCatalog.Items)
            {
                var it = item;
                string desc = it.Description;
                if (it.Id.StartsWith("skip"))
                {
                    double perSecond = FarmEconomy.IncomePerSecond(state);
                    desc += " (≈" + FarmGameState.Format((long)(perSecond * (it.Id == "skip1h" ? 3600 : 14400))) + ")";
                }
                y = Row(y, it.Icon, it.Name, desc, it.Gems, () => message(state.BuyGemItem(it)));
            }
            y = MenuPage.Section(ui, content, "¿TE FALTAN GEMAS?", y + 6);
            ui.ActionButton(content, "Go gems", "VER PACKS DE GEMAS", 110, y, 320, 54, () => { tab = 1; Rebuild(); });
            y += 66;
        }
        else
        {
            y = MenuPage.Section(ui, content, "PACKS DE GEMAS", y);
            for (int i = 0; i < ShopCatalog.Packs.Length; i++)
            {
                var pack = ShopCatalog.Packs[i];
                float x = 6 + (i % 2) * 266;
                if (i % 2 == 0 && i > 0) y += 222;
                PackCard(pack, x, y, i);
            }
            y += 230;
            ui.Paragraph(content, "Legal", "Las gemas son un bien virtual sin valor monetario. Precios en pesos chilenos con IVA incluido.",
                12, y, 518, 36, 12, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
            y += 42;
            ui.ActionButton(content, "Restore", "RESTAURAR COMPRAS", 130, y, 280, 46, PurchaseService.Restore);
            y += 60;
        }
        MenuPage.SetHeight(content, y + 10);
    }

    float Row(float y, string icon, string title, string description, int gems, UnityEngine.Events.UnityAction buy)
    {
        var card = ui.Panel(content, "Row", 6, y, 530, 96, PixelUIFactory.Hex("#9A5A33"));
        ui.Block(card, "Icon back", 12, 14, 68, 68, PixelUIFactory.Hex("#5C3020"));
        ui.Texture(card, "Icon", icon, 20, 22, 52, 52);
        ui.Label(card, "Name", title, 90, 10, 300, 26, 17, Color.white);
        ui.Paragraph(card, "Desc", description, 90, 38, 290, 48, 13, PixelUIFactory.Cream);
        bool affordable = state.Gems >= gems;
        ui.ActionButton(card, "Buy", "", 392, 22, 126, 52, buy, affordable);
        ui.Texture(card, "Gem", "UI/Shop/icon_gem", 404, 34, 30, 26);
        ui.Label(card, "Price", gems.ToString(), 438, 26, 76, 44, 20, Color.white);
        return y + 104;
    }

    void PackCard(GemPack pack, float x, float y, int index)
    {
        bool sold = pack.OncePerPlayer && state.StarterPackBought;
        var card = ui.Panel(content, "Pack", x, y, 258, 212, PixelUIFactory.Hex(pack.OncePerPlayer ? "#7A3F5E" : "#2F5E7A"));
        ui.Texture(card, "Art", pack.OncePerPlayer ? "UI/Shop/starter_chest" : "UI/Shop/gems_" + Mathf.Min(index, 5),
            pack.OncePerPlayer ? 97 : 93, 30, pack.OncePerPlayer ? 64 : 72, pack.OncePerPlayer ? 48 : 72);
        ui.Label(card, "Name", pack.Name.ToUpperInvariant(), 0, 6, 258, 24, 15, Color.white, TextAnchor.MiddleCenter);
        string amount = pack.Gems + " gemas";
        if (pack.BonusFlowerCoins > 0) amount += "\n+" + pack.BonusFlowerCoins + " de flor · x2 por " + pack.BoostMinutes + " min";
        ui.Paragraph(card, "Amount", amount, 8, 104, 242, 46, 16, PixelUIFactory.Gold, TextAnchor.MiddleCenter);
        if (!string.IsNullOrEmpty(pack.Badge))
        {
            var badge = ui.Panel(card, "Badge", 150, -8, 112, 28, PixelUIFactory.Hex("#D23C3C"));
            ui.Label(badge, "Text", pack.Badge, 0, 0, 112, 28, 12, Color.white, TextAnchor.MiddleCenter);
        }
        ui.ActionButton(card, "Buy", sold ? "COMPRADO" : PurchaseService.PriceOf(pack), 34, 152, 190, 50,
            () => PurchaseService.Buy(pack), !sold);
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= OnStateChanged;
        PurchaseService.Changed -= Rebuild;
    }
}
