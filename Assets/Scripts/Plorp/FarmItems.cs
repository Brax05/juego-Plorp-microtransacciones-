using System.Collections.Generic;

/// <summary>Something the farm produces and Plorp eats.</summary>
public sealed class FarmItem
{
    public readonly string Id;
    public readonly string Name;      // plural, used in orders ("Tomates")
    public readonly string Single;    // one of them ("tomate")
    public readonly string Icon;      // Resources path
    public readonly bool FromAnimal;  // animal products come slower, so orders ask for fewer
    public readonly bool IsDish;      // cooked in Plorp's kitchen

    public FarmItem(string id, string name, string single, string icon, bool fromAnimal, bool isDish = false)
    {
        Id = id; Name = name; Single = single; Icon = icon; FromAnimal = fromAnimal; IsDish = isDish;
    }
}

/// <summary>
/// Catalog of everything Plorp can ask for. Crops come from the seed tiers (every 10 "Semillas");
/// animal products come from the animals unlocked with "Animales" (gallina → huevo, oveja → queso, vaca → leche).
/// </summary>
public static class FarmItems
{
    public static readonly FarmItem Carrot  = new FarmItem("carrot",  "Zanahorias", "zanahoria", "World/Sprites/carrot_2",  false);
    public static readonly FarmItem Tomato  = new FarmItem("tomato",  "Tomates",    "tomate",    "World/Sprites/tomato_2",  false);
    public static readonly FarmItem Pumpkin = new FarmItem("pumpkin", "Calabazas",  "calabaza",  "World/Sprites/pumpkin_2", false);
    public static readonly FarmItem Cabbage = new FarmItem("cabbage", "Repollos",   "repollo",   "World/Sprites/cabbage_2", false);
    public static readonly FarmItem Egg     = new FarmItem("egg",     "Huevos",     "huevo",     "UI/Items/egg",            true);
    public static readonly FarmItem Cheese  = new FarmItem("cheese",  "Quesos",     "queso",     "UI/Items/cheese",         true);
    public static readonly FarmItem Milk    = new FarmItem("milk",    "Leche",      "leche",     "UI/Items/milk",           true);

    // Dishes from Plorp's kitchen (see Kitchen.Recipes).
    public static readonly FarmItem CarrotCake = new FarmItem("dish_pastel",   "Pasteles de zanahoria", "pastel de zanahoria", "World/Sprites/dish_pastel",   false, true);
    public static readonly FarmItem Salad      = new FarmItem("dish_ensalada", "Ensaladas",             "ensalada",            "World/Sprites/dish_ensalada", false, true);
    public static readonly FarmItem Omelette   = new FarmItem("dish_tortilla", "Tortillas",             "tortilla",            "World/Sprites/dish_tortilla", false, true);
    public static readonly FarmItem Pizza      = new FarmItem("dish_pizza",    "Pizzas de queso",       "pizza de queso",      "World/Sprites/dish_pizza",    false, true);
    public static readonly FarmItem Soup       = new FarmItem("dish_sopa",     "Sopas de calabaza",     "sopa de calabaza",    "World/Sprites/dish_sopa",     false, true);

    // From the zones behind the clouds (FarmZones).
    public static readonly FarmItem Fish  = new FarmItem("fish",  "Pescados", "pescado", "World/Sprites/fish_common", true);
    public static readonly FarmItem Honey = new FarmItem("honey", "Mieles",   "miel",    "World/Sprites/item_honey",  true);
    public static readonly FarmItem Apple = new FarmItem("apple", "Manzanas", "manzana", "World/Sprites/item_apple",  false);
    public static readonly FarmItem ApplePie = new FarmItem("dish_tarta", "Tartas de manzana", "tarta de manzana", "World/Sprites/dish_tarta", false, true);

    public static readonly FarmItem[] All = { Carrot, Tomato, Pumpkin, Cabbage, Egg, Cheese, Milk,
                                              CarrotCake, Salad, Omelette, Pizza, Soup,
                                              Fish, Honey, Apple, ApplePie };
    static readonly FarmItem[] CropsByTier = { Carrot, Cabbage, Tomato, Pumpkin }; // FarmEconomy.CropTiers
    static readonly FarmItem[] AnimalProducts = { Egg, Cheese, Milk };             // species 0..2

    public static FarmItem Get(string id)
    {
        foreach (var item in All) if (item.Id == id) return item;
        return null;
    }

    public static FarmItem ForCrop(CropKind kind)
    {
        switch (kind)
        {
            case CropKind.Tomato: return Tomato;
            case CropKind.Pumpkin: return Pumpkin;
            case CropKind.Cabbage: return Cabbage;
            default: return Carrot;
        }
    }

    /// <summary>Product of an animal from its actor id ("chicken-2" → huevo). Workers give nothing.</summary>
    public static FarmItem ForActor(string actorId)
    {
        if (actorId.StartsWith("chicken")) return Egg;
        if (actorId.StartsWith("sheep")) return Cheese;
        if (actorId.StartsWith("cow")) return Milk;
        return null;
    }

    /// <summary>Everything the player can currently produce, in the order it unlocks.</summary>
    public static List<FarmItem> Unlocked(FarmGameState state)
    {
        var list = new List<FarmItem>();
        for (int i = 0; i <= FarmEconomy.SeedTier(state); i++) list.Add(CropsByTier[i]);
        for (int i = 0; i < AnimalProducts.Length; i++)
            if (FarmEconomy.AnimalCount(state, i) > 0) list.Add(AnimalProducts[i]);
        if (FarmZones.IsOpen(ZoneId.Pond)) list.Add(Fish);
        if (FarmZones.IsOpen(ZoneId.Hives)) list.Add(Honey);
        if (FarmZones.IsOpen(ZoneId.Orchard)) list.Add(Apple);
        return list;
    }
}
