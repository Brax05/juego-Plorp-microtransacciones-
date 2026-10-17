using System.Collections.Generic;

/// <summary>Something the farm produces and Plorp eats.</summary>
public sealed class FarmItem
{
    public readonly string Id;
    public readonly string Name;      // plural, used in orders ("Tomates")
    public readonly string Single;    // one of them ("tomate")
    public readonly string Icon;      // Resources path
    public readonly bool FromAnimal;  // animal products come slower, so orders ask for fewer

    public FarmItem(string id, string name, string single, string icon, bool fromAnimal)
    {
        Id = id; Name = name; Single = single; Icon = icon; FromAnimal = fromAnimal;
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

    public static readonly FarmItem[] All = { Carrot, Tomato, Pumpkin, Cabbage, Egg, Cheese, Milk };
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
        return list;
    }
}
