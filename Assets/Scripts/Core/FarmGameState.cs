using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Small persistent demo economy shared by every screen.</summary>
public sealed class FarmGameState
{
    public static readonly string[] UpgradeNames = { "Semillas", "Animales", "Trabajadores" };
    public static readonly string[] AchievementNames =
        { "Primera cosecha", "Granjero constante", "Gran inversion", "Alimenta a Plorp" };
    static readonly int[] AchievementGems = { 5, 20, 30, 15 };
    const int ProgressionVersion = 7; // 7: slower economy (old saves restart)
    const string ProgressionVersionKey = "GlorpFarm.progressionVersion";
    static readonly int[] StartingLevels = FarmEconomy.StartingLevels;

    public readonly int[] Levels = new int[3];
    public readonly long[] Prices = new long[3];
    public readonly bool[] Claimed = new bool[AchievementNames.Length];
    public long Gold { get; private set; }
    public int Gems { get; private set; }
    public int FarmLevel { get; private set; }
    public int Harvests { get; private set; }
    public int Purchases { get; private set; }
    public long TotalHarvestGold { get; private set; }
    /// <summary>Food delivered to Plorp over the whole game.</summary>
    public long TotalFood { get; private set; }
    /// <summary>Index of the current Plorp order (see PlorpGoals).</summary>
    public int PlorpStage { get; private set; }
    public int PepitoMeals { get; private set; }
    public bool PlorpSatisfied => PlorpStage >= PlorpGoals.MainCount;
    /// <summary>Animal species on the farm (0..3): gallina, oveja, vaca.</summary>
    public int AnimalSpecies => FarmEconomy.SpeciesUnlocked(this);

    // Barn: how many of each food is waiting to be delivered to Plorp.
    readonly Dictionary<string, long> barn = new Dictionary<string, long>();
    // Foods Plorp has already asked for at least once (new foods are asked first).
    readonly HashSet<string> tasted = new HashSet<string>();
    PlorpGoal currentGoal;

    public long ItemCount(string id) => barn.TryGetValue(id, out long n) ? n : 0;

    /// <summary>Plorp's current order. Generated from what the player can produce when the previous one is delivered.</summary>
    public PlorpGoal CurrentGoal
    {
        get
        {
            if (currentGoal == null)
            {
                currentGoal = PlorpGoals.Generate(PlorpStage, FarmItems.Unlocked(this), tasted,
                    Kitchen.AvailableDishes(this));
                PlayerPrefs.SetString("GlorpFarm.plorpOrder", currentGoal.Serialize());
            }
            return currentGoal;
        }
    }

    public bool GoalReady
    {
        get
        {
            foreach (var line in CurrentGoal.Lines)
                if (ItemCount(line.Item.Id) < line.Amount) return false;
            return true;
        }
    }
    public float FarmLevelProgress => (Purchases % 3) / 3f;
    public bool MusicEnabled { get; private set; }
    public bool SoundEnabled { get; private set; }
    public event Action Changed;

    public FarmGameState()
    {
        EnsureProgressionVersion();
        PurchaseDeliveryJournal.Recover();
        Gold = ReadLong("gold", 250);
        Gems = PlayerPrefs.GetInt("GlorpFarm.gems", 0);
        FarmLevel = PlayerPrefs.GetInt("GlorpFarm.farmLevel", 1);
        Harvests = PlayerPrefs.GetInt("GlorpFarm.harvests", 0);
        Purchases = PlayerPrefs.GetInt("GlorpFarm.purchases", 0);
        TotalHarvestGold = ReadLong("harvestGold", 0);
        TotalFood = ReadLong("totalFood", 0);
        PlorpStage = PlayerPrefs.GetInt("GlorpFarm.plorpStage", 0);
        foreach (var item in FarmItems.All)
        {
            long n = ReadLong("item." + item.Id, 0);
            if (n > 0) barn[item.Id] = n;
        }
        foreach (string id in PlayerPrefs.GetString("GlorpFarm.tasted", "").Split(','))
            if (id.Length > 0) tasted.Add(id);
        currentGoal = PlorpGoal.Parse(PlorpStage, PlayerPrefs.GetString("GlorpFarm.plorpOrder", ""));
        PepitoMeals = PlayerPrefs.GetInt("GlorpFarm.pepitoMeals", 0);
        MusicEnabled = PlayerPrefs.GetInt("GlorpFarm.music", 1) == 1;
        SoundEnabled = PlayerPrefs.GetInt("GlorpFarm.sound", 1) == 1;
        for (int i = 0; i < Levels.Length; i++)
        {
            Levels[i] = PlayerPrefs.GetInt("GlorpFarm.level." + i, StartingLevels[i]);
            Prices[i] = FarmEconomy.Cost(i, Levels[i]);
        }
        for (int i = 0; i < Claimed.Length; i++)
            Claimed[i] = PlayerPrefs.GetInt("GlorpFarm.claimed." + i, 0) == 1;
        GoldenHarvests = PlayerPrefs.GetInt("GlorpFarm.golden", 0);
        Boosts = new BoostState();
        FarmEconomy.Current = this;
        Garden = new GardenState(this);
    }

    /// <summary>One harvested thing: coins for the player, one food item into the barn for Plorp.</summary>
    public void Collect(long earned, FarmItem item)
    {
        if (earned <= 0) return;
        if (item != null) AddItem(item.Id, 1);
        if (item != null) DailyMissions.Report(item.FromAnimal ? MissionKind.AnimalProducts : MissionKind.Crops, 1);
        AddGold(earned);
        Save();
    }

    /// <summary>A batch harvest (COSECHAR button or workers).</summary>
    public void Collect(long earned, IDictionary<string, int> items)
    {
        if (earned <= 0) return;
        foreach (var pair in items)
        {
            AddItem(pair.Key, pair.Value);
            var item = FarmItems.Get(pair.Key);
            if (item != null) DailyMissions.Report(item.FromAnimal ? MissionKind.AnimalProducts : MissionKind.Crops, pair.Value);
        }
        AddGold(earned);
        Save();
    }

    /// <summary>Puts items in the barn without coins (a dish out of the kitchen).</summary>
    public void GiveItem(string id, int amount)
    {
        if (amount <= 0) return;
        AddItem(id, amount);
        Save();
    }

    /// <summary>Removes up to <paramref name="max"/> of an item from the barn (fox, fair truck). Returns how many.</summary>
    public int TakeItems(string id, int max)
    {
        int taken = (int)Math.Min(max, ItemCount(id));
        if (taken <= 0) return 0;
        barn[id] = ItemCount(id) - taken;
        Save();
        return taken;
    }

    void AddItem(string id, long amount)
    {
        if (amount <= 0) return;
        barn[id] = ItemCount(id) + amount;
    }

    void AddGold(long earned)
    {
        DailyMissions.Report(MissionKind.Coins, earned);
        Gold += earned;
        TotalHarvestGold += earned;
        Harvests++;
    }

    /// <summary>Hands the order to Plorp. Returns false when something is still missing.</summary>
    public bool DeliverToPlorp(out PlorpGoal delivered)
    {
        delivered = CurrentGoal;
        if (!GoalReady) return false;
        foreach (var line in delivered.Lines)
        {
            barn[line.Item.Id] = ItemCount(line.Item.Id) - line.Amount;
            TotalFood += line.Amount;
            tasted.Add(line.Item.Id);
        }
        Gold += delivered.RewardGold;
        Gems += delivered.RewardGems;
        PlorpStage++;
        DailyMissions.Report(MissionKind.Orders, 1);
        currentGoal = null; // next order is built from what the farm produces now
        Save();
        return true;
    }

    /// <summary>Pays for the snack Pepito asks for on Plorp's behalf. Exactly one of the two costs is normally non-zero.</summary>
    public bool FeedPepito(long goldCost, int gemCost)
    {
        if (Gold < goldCost || Gems < gemCost) return false;
        Gold -= goldCost;
        Gems -= gemCost;
        PepitoMeals++;
        DailyMissions.Report(MissionKind.FeedPepito, 1);
        Save();
        return true;
    }

    public bool BuyUpgrade(int index)
    {
        if (index < 0 || index >= Prices.Length || IsMaxed(index) || Gold < Prices[index]) return false;
        Gold -= Prices[index];
        Levels[index]++;
        Prices[index] = FarmEconomy.Cost(index, Levels[index]);
        Purchases++;
        DailyMissions.Report(MissionKind.Upgrades, 1);
        FarmLevel = 1 + Purchases / 3;
        Save();
        return true;
    }

    public bool IsMaxed(int index)
    {
        return index >= 0 && index < Levels.Length && Levels[index] >= FarmEconomy.MaxLevels[index];
    }

    /// <summary>The gardening tab's plant (its blooms give bonuses to the whole farm).</summary>
    public GardenState Garden { get; private set; }
    /// <summary>Time boosts bought in the store.</summary>
    public BoostState Boosts { get; private set; }
    public int GoldenHarvests { get; private set; }
    public bool StarterPackBought => PlayerPrefs.GetInt("GlorpFarm.iap.starter", 0) == 1;

    public void RegisterGolden(int count)
    {
        if (count <= 0) return;
        GoldenHarvests += count;
        DailyMissions.Report(MissionKind.Golden, count);
        PlayerPrefs.SetInt("GlorpFarm.golden", GoldenHarvests);
    }

    // ---------- Store ----------

    public bool BuyBoost(BoostOffer offer)
    {
        if (Gems < offer.Gems) return false;
        Gems -= offer.Gems;
        Boosts.Add(offer.Kind, offer.Minutes);
        Save();
        return true;
    }

    /// <summary>Gem items: time skips give the coins the farm would make in that time.</summary>
    public string BuyGemItem(GemItem item)
    {
        if (Gems < item.Gems) return "Faltan gemas";
        Gems -= item.Gems;
        string result;
        switch (item.Id)
        {
            case "skip1h":
            case "skip4h":
                long coins = (long)Math.Ceiling(FarmEconomy.IncomePerSecond(this) * (item.Id == "skip1h" ? 3600 : 14400));
                Gold += coins;
                TotalHarvestGold += coins;
                result = "+" + Format(coins) + " monedas";
                break;
            default:
                Garden.AddFlowerCoins(5);
                result = "+5 monedas de flor";
                break;
        }
        Save();
        return result;
    }

    /// <summary>Gives what a real-money pack contains (called by PurchaseService after a successful purchase).</summary>
    public bool GrantPack(GemPack pack, string purchaseToken)
    {
        if (pack == null || string.IsNullOrEmpty(purchaseToken)) throw new ArgumentException("Missing purchase.");
        if (PurchaseDeliveryJournal.WasDelivered(purchaseToken)) return false;
        bool granted = !(pack.OncePerPlayer && StarterPackBought);
        var delivery = PurchaseDeliveryJournal.Prepare(pack, purchaseToken, Gems, Garden.FlowerCoins,
            Boosts.IncomeUntil, StarterPackBought);
        try { PurchaseDeliveryJournal.Commit(delivery); }
        finally
        {
            // Keep runtime balances aligned even with staged preferences after a storage error.
            Gems = PlayerPrefs.GetInt("GlorpFarm.gems", Gems);
            Garden.ReloadPurchaseBalance();
            Boosts.ReloadIncome();
        }
        Changed?.Invoke();
        return granted;
    }

    /// <summary>Pays coins and/or gems for something (gardening tools, seeds).</summary>
    public bool Spend(long gold, int gems)
    {
        if (Gold < gold || Gems < gems) return false;
        Gold -= gold;
        Gems -= gems;
        Save();
        return true;
    }

    public void AddGems(int gems)
    {
        if (gems <= 0) return;
        Gems += gems;
        Save();
    }

    public bool BuyGold(int gemCost, long goldAmount)
    {
        if (Gems < gemCost) return false;
        Gems -= gemCost;
        Gold += goldAmount;
        Save();
        return true;
    }

    public string AchievementProgress(int index)
    {
        switch (index)
        {
            case 0: return Math.Min(Harvests, 1) + "/1 cosecha";
            case 1: return Math.Min(Harvests, 10) + "/10 cosechas";
            case 2: return Math.Min(Purchases, 5) + "/5 mejoras";
            default: return Math.Min(PepitoMeals, 1) + "/1 antojo de Plorp";
        }
    }

    public bool AchievementReady(int index)
    {
        if (index < 0 || index >= Claimed.Length || Claimed[index]) return false;
        switch (index)
        {
            case 0: return Harvests >= 1;
            case 1: return Harvests >= 10;
            case 2: return Purchases >= 5;
            default: return PepitoMeals >= 1;
        }
    }

    public bool ClaimAchievement(int index)
    {
        if (!AchievementReady(index)) return false;
        Claimed[index] = true;
        Gems += AchievementGems[index];
        Save();
        return true;
    }

    public void ToggleMusic() { MusicEnabled = !MusicEnabled; Save(); }
    public void ToggleSound() { SoundEnabled = !SoundEnabled; Save(); }

    void Save()
    {
        PlayerPrefs.SetString("GlorpFarm.gold", Gold.ToString());
        PlayerPrefs.SetInt("GlorpFarm.gems", Gems);
        PlayerPrefs.SetInt("GlorpFarm.farmLevel", FarmLevel);
        PlayerPrefs.SetInt("GlorpFarm.harvests", Harvests);
        PlayerPrefs.SetInt("GlorpFarm.purchases", Purchases);
        PlayerPrefs.SetString("GlorpFarm.harvestGold", TotalHarvestGold.ToString());
        PlayerPrefs.SetString("GlorpFarm.totalFood", TotalFood.ToString());
        foreach (var pair in barn) PlayerPrefs.SetString("GlorpFarm.item." + pair.Key, pair.Value.ToString());
        PlayerPrefs.SetString("GlorpFarm.tasted", string.Join(",", tasted));
        PlayerPrefs.SetString("GlorpFarm.plorpOrder", CurrentGoal.Serialize());
        PlayerPrefs.SetInt("GlorpFarm.plorpStage", PlorpStage);
        PlayerPrefs.SetInt("GlorpFarm.pepitoMeals", PepitoMeals);
        PlayerPrefs.SetInt("GlorpFarm.music", MusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("GlorpFarm.sound", SoundEnabled ? 1 : 0);
        for (int i = 0; i < Levels.Length; i++)
        {
            PlayerPrefs.SetInt("GlorpFarm.level." + i, Levels[i]);
        }
        for (int i = 0; i < Claimed.Length; i++)
            PlayerPrefs.SetInt("GlorpFarm.claimed." + i, Claimed[i] ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    static long ReadLong(string key, long fallback)
    {
        return long.TryParse(PlayerPrefs.GetString("GlorpFarm." + key, fallback.ToString()), out long result)
            ? result : fallback;
    }

    static void EnsureProgressionVersion()
    {
        if (PlayerPrefs.GetInt(ProgressionVersionKey, 0) >= ProgressionVersion) return;

        PlayerPrefs.SetString("GlorpFarm.gold", "250");
        PlayerPrefs.SetInt("GlorpFarm.gems", 0);
        PlayerPrefs.SetInt("GlorpFarm.farmLevel", 1);
        PlayerPrefs.SetInt("GlorpFarm.harvests", 0);
        PlayerPrefs.SetInt("GlorpFarm.purchases", 0);
        PlayerPrefs.SetString("GlorpFarm.harvestGold", "0");
        for (int i = 0; i < StartingLevels.Length; i++)
        {
            PlayerPrefs.SetInt("GlorpFarm.level." + i, StartingLevels[i]);
            PlayerPrefs.DeleteKey("GlorpFarm.price." + i);
        }
        for (int i = 0; i < AchievementNames.Length; i++) PlayerPrefs.SetInt("GlorpFarm.claimed." + i, 0);

        PlayerPrefs.DeleteKey("GlorpFarm.animalRosterBaselineLevel");
        PlayerPrefs.DeleteKey("GlorpFarm.animalTestPriceApplied");
        foreach (string key in new[] { "plorpStage", "plorpOrder", "tasted", "totalFood", "pepitoMeals" })
            PlayerPrefs.DeleteKey("GlorpFarm." + key);
        foreach (var item in FarmItems.All) PlayerPrefs.DeleteKey("GlorpFarm.item." + item.Id);
        PlayerPrefs.SetInt(ProgressionVersionKey, ProgressionVersion);
        PlayerPrefs.Save();
    }

    /// <summary>Clears only gameplay progress, leaving audio preferences intact.</summary>
    public static void ResetSavedProgress()
    {
        string[] coreKeys =
        {
            "gold", "gems", "farmLevel", "harvests", "purchases", "harvestGold",
            "food", "totalFood", "plorpStage", "pepitoMeals", "tasted", "plorpOrder",
            "progressionVersion", "animalRosterBaselineLevel", "animalTestPriceApplied"
        };
        foreach (string key in coreKeys) PlayerPrefs.DeleteKey("GlorpFarm." + key);

        for (int i = 0; i < 3; i++)
        {
            PlayerPrefs.DeleteKey("GlorpFarm.level." + i);
            PlayerPrefs.DeleteKey("GlorpFarm.price." + i);
        }
        // Remove the obsolete machinery upgrade from older editor saves.
        PlayerPrefs.DeleteKey("GlorpFarm.level.3");
        PlayerPrefs.DeleteKey("GlorpFarm.price.3");
        for (int i = 0; i < AchievementNames.Length; i++) PlayerPrefs.DeleteKey("GlorpFarm.claimed." + i);

        foreach (var item in FarmItems.All) PlayerPrefs.DeleteKey("GlorpFarm.item." + item.Id);
        GardenState.ResetSaved();
        BoostState.ResetSaved();
        PlayerPrefs.DeleteKey("GlorpFarm.golden");
        foreach (var a in AchievementCatalog.All) PlayerPrefs.DeleteKey("GlorpFarm.ach." + a.Id);

        for (int plot = 0; plot < FarmArtLayout.Fields.Length; plot++)
        {
            for (int cell = 0; cell < FarmArtLayout.CellsPerPlot; cell++)
            {
                string cropKey = "GlorpFarm.world.crop." + plot + "." + cell;
                PlayerPrefs.DeleteKey(cropKey + ".stage");
                PlayerPrefs.DeleteKey(cropKey + ".since");
            }
        }

        for (int i = 1; i <= 6; i++)
        {
            PlayerPrefs.DeleteKey("GlorpFarm.world.actor.chicken-" + i + ".readyAt");
            PlayerPrefs.DeleteKey("GlorpFarm.world.actor.sheep-" + i + ".readyAt");
            PlayerPrefs.DeleteKey("GlorpFarm.world.actor.cow-" + i + ".readyAt");
        }
        for (int i = 1; i <= 4; i++)
            PlayerPrefs.DeleteKey("GlorpFarm.world.actor.worker-" + i + ".readyAt");
        PlayerPrefs.Save();
    }

    public static string Format(long value)
    {
        if (value >= 1000000000000000) return (value / 1000000000000000d).ToString("0.##") + "Q";
        if (value >= 1000000000000) return (value / 1000000000000d).ToString("0.##") + "T";
        if (value >= 1000000000) return (value / 1000000000d).ToString("0.##") + "B";
        if (value >= 1000000) return (value / 1000000d).ToString("0.##") + "M";
        if (value >= 1000) return (value / 1000d).ToString("0.##") + "K";
        return value.ToString();
    }
}
