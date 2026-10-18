using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A dish Plorp's kitchen can cook.</summary>
public sealed class Recipe
{
    public readonly string DishId;
    public readonly (string item, int amount)[] Ingredients;
    public readonly int Seconds;

    public Recipe(string dishId, int seconds, params (string, int)[] ingredients)
    {
        DishId = dishId;
        Seconds = seconds;
        Ingredients = ingredients;
    }

    public FarmItem Dish => FarmItems.Get(DishId);
    public string Name => Dish != null ? Dish.Single : DishId;
}

/// <summary>
/// Plorp's kitchen (Módulo 4): pots cook dishes from the barn's food. A dish is worth more than its
/// ingredients, Plorp asks for dishes in his orders and the fair truck buys them.
///  - 2 pots at the start, a 3rd one for gems. Cooking uses real time, so it continues with the game closed.
///  - A recipe can be cooked once the farm produces all its ingredients.
/// Saved in PlayerPrefs ("GlorpFarm.kitchen.*").
/// </summary>
public static class Kitchen
{
    const string Key = "GlorpFarm.kitchen.";
    public const int BaseSlots = 2, MaxSlots = 3, ExtraSlotGems = 50;
    /// <summary>A dish is worth this much more than its ingredients.</summary>
    public const double DishBonus = 1.6;

    public static readonly Recipe[] Recipes =
    {
        new Recipe("dish_pastel",   40,  ("carrot", 5), ("egg", 2)),
        new Recipe("dish_ensalada", 60,  ("cabbage", 4), ("carrot", 3)),
        new Recipe("dish_tortilla", 45,  ("egg", 3), ("tomato", 2)),
        new Recipe("dish_pizza",    120, ("tomato", 3), ("cheese", 2)),
        new Recipe("dish_sopa",     90,  ("pumpkin", 2), ("milk", 1)),
        new Recipe("dish_tarta",    75,  ("apple", 4), ("honey", 1), ("egg", 2)),
    };

    static readonly string[] slotDish = new string[MaxSlots];
    static readonly long[] slotEnd = new long[MaxSlots];
    static readonly bool[] announced = new bool[MaxSlots];
    static bool loaded;

    /// <summary>Something changed (cooking started, dish collected, pot bought).</summary>
    public static event Action Changed;
    /// <summary>A dish just finished (Pepito tells the player).</summary>
    public static event Action<Recipe> DishReady;

    static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 0; i < MaxSlots; i++)
        {
            slotDish[i] = PlayerPrefs.GetString(Key + "slot" + i, "");
            long.TryParse(PlayerPrefs.GetString(Key + "slot" + i + ".end", "0"), out slotEnd[i]);
            announced[i] = SlotReady(i); // dishes finished while away are not announced again
        }
    }

    static void SaveSlot(int i)
    {
        PlayerPrefs.SetString(Key + "slot" + i, slotDish[i] ?? "");
        PlayerPrefs.SetString(Key + "slot" + i + ".end", slotEnd[i].ToString());
        PlayerPrefs.Save();
    }

    // ---------- Pots ----------

    public static int Slots => Mathf.Clamp(PlayerPrefs.GetInt(Key + "slots", BaseSlots), BaseSlots, MaxSlots);

    public static Recipe SlotRecipe(int slot)
    {
        Load();
        if (slot < 0 || slot >= MaxSlots || string.IsNullOrEmpty(slotDish[slot])) return null;
        return Find(slotDish[slot]);
    }

    public static bool SlotEmpty(int slot) => SlotRecipe(slot) == null;
    public static bool SlotReady(int slot) => SlotRecipe(slot) != null && Now >= slotEnd[slot];

    public static long SecondsLeft(int slot) => SlotRecipe(slot) == null ? 0 : Math.Max(0, slotEnd[slot] - Now);

    public static float Progress(int slot)
    {
        var r = SlotRecipe(slot);
        if (r == null) return 0f;
        return Mathf.Clamp01(1f - SecondsLeft(slot) / (float)r.Seconds);
    }

    public static int FreeSlot()
    {
        for (int i = 0; i < Slots; i++) if (SlotEmpty(i)) return i;
        return -1;
    }

    public static int ReadyCount()
    {
        int n = 0;
        for (int i = 0; i < Slots; i++) if (SlotReady(i)) n++;
        return n;
    }

    /// <summary>Call every frame: announces dishes that just finished.</summary>
    public static void Tick()
    {
        Load();
        for (int i = 0; i < Slots; i++)
        {
            bool ready = SlotReady(i);
            if (ready && !announced[i])
            {
                announced[i] = true;
                DishReady?.Invoke(SlotRecipe(i));
                Changed?.Invoke();
            }
        }
    }

    public static bool BuySlot(FarmGameState s)
    {
        if (Slots >= MaxSlots || !s.Spend(0, ExtraSlotGems)) return false;
        PlayerPrefs.SetInt(Key + "slots", Slots + 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }

    // ---------- Recipes ----------

    public static Recipe Find(string dishId)
    {
        foreach (var r in Recipes) if (r.DishId == dishId) return r;
        return null;
    }

    /// <summary>The farm already produces every ingredient.</summary>
    public static bool Unlocked(FarmGameState s, Recipe r)
    {
        var producible = FarmItems.Unlocked(s);
        foreach (var (item, _) in r.Ingredients)
        {
            bool found = false;
            foreach (var p in producible) if (p.Id == item) { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>"Tomates y Quesos": what the farm still has to produce for this recipe.</summary>
    public static string MissingProduction(FarmGameState s, Recipe r)
    {
        var producible = FarmItems.Unlocked(s);
        var missing = new List<string>();
        foreach (var (item, _) in r.Ingredients)
        {
            bool found = false;
            foreach (var p in producible) if (p.Id == item) { found = true; break; }
            if (!found) missing.Add(FarmItems.Get(item)?.Name ?? item);
        }
        return string.Join(" y ", missing);
    }

    public static bool HasIngredients(FarmGameState s, Recipe r)
    {
        foreach (var (item, amount) in r.Ingredients) if (s.ItemCount(item) < amount) return false;
        return true;
    }

    /// <summary>Dishes Plorp may ask for (their ingredients are already produced).</summary>
    public static List<FarmItem> AvailableDishes(FarmGameState s)
    {
        var list = new List<FarmItem>();
        foreach (var r in Recipes) if (Unlocked(s, r) && r.Dish != null) list.Add(r.Dish);
        return list;
    }

    public static long DishValue(FarmGameState s, string dishId)
    {
        var r = Find(dishId);
        if (r == null) return 0;
        double sum = 0;
        foreach (var (item, amount) in r.Ingredients) sum += FarmEconomy.UnitValue(s, FarmItems.Get(item)) * (double)amount;
        return (long)Math.Ceiling(sum * DishBonus);
    }

    /// <summary>Puts the recipe in a free pot, taking the ingredients from the barn.</summary>
    public static bool Cook(FarmGameState s, Recipe r)
    {
        Load();
        int slot = FreeSlot();
        if (slot < 0 || !Unlocked(s, r) || !HasIngredients(s, r)) return false;
        foreach (var (item, amount) in r.Ingredients) s.TakeItems(item, amount);
        slotDish[slot] = r.DishId;
        slotEnd[slot] = Now + r.Seconds;
        announced[slot] = false;
        SaveSlot(slot);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Takes a finished dish out of the pot into the barn.</summary>
    public static Recipe Collect(FarmGameState s, int slot)
    {
        if (!SlotReady(slot)) return null;
        var r = SlotRecipe(slot);
        slotDish[slot] = "";
        slotEnd[slot] = 0;
        announced[slot] = false;
        SaveSlot(slot);
        s.GiveItem(r.DishId, 1);
        Changed?.Invoke();
        return r;
    }

    /// <summary>Collects every finished pot. Returns how many dishes.</summary>
    public static int CollectAll(FarmGameState s)
    {
        int n = 0;
        for (int i = 0; i < Slots; i++) if (Collect(s, i) != null) n++;
        return n;
    }

    /// <summary>Sells one dish for its value (the rest stay for Plorp).</summary>
    public static bool SellOne(FarmGameState s, Recipe r, out long coins)
    {
        coins = DishValue(s, r.DishId);
        if (s.TakeItems(r.DishId, 1) <= 0) return false;
        s.BuyGold(0, coins);
        Changed?.Invoke();
        return true;
    }

    public static string Time(int seconds) =>
        seconds < 60 ? seconds + " s" : seconds % 60 == 0 ? seconds / 60 + " min" : seconds / 60 + ":" + (seconds % 60).ToString("00");
}
