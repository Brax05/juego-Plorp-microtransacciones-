using System;
using UnityEngine;

public enum BoostKind { Income, Turbo, Garden }

/// <summary>A time boost bought with gems.</summary>
public sealed class BoostOffer
{
    public readonly string Id, Name, Description, Icon;
    public readonly BoostKind Kind;
    public readonly int Minutes, Gems;

    public BoostOffer(string id, string name, string description, string icon, BoostKind kind, int minutes, int gems)
    {
        Id = id; Name = name; Description = description; Icon = icon; Kind = kind; Minutes = minutes; Gems = gems;
    }
}

/// <summary>Something sold for gems that is not a timed boost.</summary>
public sealed class GemItem
{
    public readonly string Id, Name, Description, Icon;
    public readonly int Gems;
    public GemItem(string id, string name, string description, string icon, int gems)
    {
        Id = id; Name = name; Description = description; Icon = icon; Gems = gems;
    }
}

/// <summary>A real-money product (Google Play / App Store). Prices in Chilean pesos.</summary>
public sealed class GemPack
{
    public readonly string ProductId, Name, Badge;
    public readonly int PriceClp, Gems, BonusFlowerCoins, BoostMinutes;
    public readonly bool OncePerPlayer;

    public GemPack(string productId, string name, int priceClp, int gems, string badge = null,
        int flowerCoins = 0, int boostMinutes = 0, bool once = false)
    {
        ProductId = productId; Name = name; PriceClp = priceClp; Gems = gems; Badge = badge;
        BonusFlowerCoins = flowerCoins; BoostMinutes = boostMinutes; OncePerPlayer = once;
    }

    /// <summary>"$1.000" — Chilean format, used until the store returns the real localized price.</summary>
    public string FallbackPrice
    {
        get
        {
            string digits = PriceClp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (int i = digits.Length - 3; i > 0; i -= 3) digits = digits.Insert(i, ".");
            return "$" + digits;
        }
    }
}

/// <summary>
/// Everything the store sells. Product ids must match the ones created in Google Play Console /
/// App Store Connect (consumables, except the starter pack which is sold once per player).
/// </summary>
public static class ShopCatalog
{
    public static readonly BoostOffer[] Boosts =
    {
        new BoostOffer("income15", "Ganancias x2", "Todo lo que ganas vale el doble por 15 min", "icon_coin", BoostKind.Income, 15, 20),
        new BoostOffer("income60", "Ganancias x2 (1 h)", "El doble de monedas por 1 hora", "icon_coin", BoostKind.Income, 60, 60),
        new BoostOffer("turbo10", "Cosecha turbo", "Cultivos y animales el doble de rápido, trabajadores también (10 min)", "icon_sprout", BoostKind.Turbo, 10, 25),
        new BoostOffer("garden10", "Jardín mágico", "El cuidado de la planta sube x3 (10 min)", "icon_water", BoostKind.Garden, 10, 30),
    };

    public static readonly GemItem[] Items =
    {
        new GemItem("skip1h", "Salto de tiempo 1 h", "Recibe al instante las monedas de 1 hora de granja", "icon_sun", 40),
        new GemItem("skip4h", "Salto de tiempo 4 h", "Recibe al instante las monedas de 4 horas", "icon_sun", 140),
        new GemItem("flower5", "5 monedas de flor", "Para abono y plantas nuevas del jardín", "icon_flowercoin", 25),
    };

    public static readonly GemPack[] Packs =
    {
        new GemPack("plorp.gems.50", "Puñado de gemas", 50, 20),
        new GemPack("plorp.gems.100", "Bolsita de gemas", 100, 45),
        new GemPack("plorp.starter", "Pack de inicio", 300, 150, "UNA VEZ", flowerCoins: 10, boostMinutes: 60, once: true),
        new GemPack("plorp.gems.250", "Bolsa de gemas", 250, 120, "+10%"),
        new GemPack("plorp.gems.500", "Cofre de gemas", 500, 260, "POPULAR"),
        new GemPack("plorp.gems.1000", "Nave de gemas", 1000, 560, "MEJOR VALOR"),
    };

    public static GemPack Pack(string productId) => Array.Find(Packs, p => p.ProductId == productId);
}

/// <summary>Active time boosts (saved as end times, so they keep running while the game is closed).</summary>
public sealed class BoostState
{
    const string Key = "GlorpFarm.boost.";
    readonly double[] until = new double[3];

    static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public BoostState()
    {
        for (int i = 0; i < until.Length; i++)
            double.TryParse(PlayerPrefs.GetString(Key + i, "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out until[i]);
    }

    public bool Active(BoostKind kind) => Now < until[(int)kind];
    public float SecondsLeft(BoostKind kind) => (float)Math.Max(0, until[(int)kind] - Now);

    /// <summary>Adds time (stacks with what's left).</summary>
    public void Add(BoostKind kind, int minutes)
    {
        int i = (int)kind;
        until[i] = Math.Max(Now, until[i]) + minutes * 60;
        PlayerPrefs.SetString(Key + i, until[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    public float IncomeMultiplier => Active(BoostKind.Income) ? 2f : 1f;
    public float SpeedMultiplier => Active(BoostKind.Turbo) ? 2f : 1f;
    public float GardenMultiplier => Active(BoostKind.Garden) ? 3f : 1f;

    /// <summary>"x2 12:30 · Turbo 4:10" for the farm HUD; empty when nothing is active.</summary>
    public string Summary
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Active(BoostKind.Income)) parts.Add("x2 " + Clock(SecondsLeft(BoostKind.Income)));
            if (Active(BoostKind.Turbo)) parts.Add("Turbo " + Clock(SecondsLeft(BoostKind.Turbo)));
            if (Active(BoostKind.Garden)) parts.Add("Jardín " + Clock(SecondsLeft(BoostKind.Garden)));
            return string.Join(" · ", parts.ToArray());
        }
    }

    public static string Clock(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return s >= 3600 ? (s / 3600) + ":" + ((s / 60) % 60).ToString("00") + ":" + (s % 60).ToString("00")
                         : (s / 60) + ":" + (s % 60).ToString("00");
    }

    public static void ResetSaved()
    {
        for (int i = 0; i < 3; i++) PlayerPrefs.DeleteKey(Key + i);
    }
}
