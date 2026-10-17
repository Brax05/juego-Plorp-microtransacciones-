using System;
using UnityEngine;

/// <summary>Small persistent demo economy shared by every screen.</summary>
public sealed class FarmGameState
{
    public static readonly string[] UpgradeNames = { "Semillas", "Animales", "Trabajadores" };
    public static readonly string[] AchievementNames =
        { "Primera cosecha", "Granjero constante", "Gran inversion", "Alimenta a Pepito" };
    static readonly int[] AchievementGems = { 5, 20, 30, 15 };
    const int ProgressionVersion = 4;
    const string ProgressionVersionKey = "GlorpFarm.progressionVersion";
    static readonly long[] StartingPrices = { 300, 750, 2000 };
    static readonly int[] StartingLevels = { 1, 0, 0 };
    static readonly int[] MaxLevels = { 4, 18, 4 };

    public readonly int[] Levels = new int[3];
    public readonly long[] Prices = new long[3];
    public readonly bool[] Claimed = new bool[AchievementNames.Length];
    public long Gold { get; private set; }
    public int Gems { get; private set; }
    public int FarmLevel { get; private set; }
    public int Harvests { get; private set; }
    public int Purchases { get; private set; }
    public long TotalHarvestGold { get; private set; }
    /// <summary>Food waiting in the barn to be delivered to Plorp.</summary>
    public long Food { get; private set; }
    public long TotalFood { get; private set; }
    /// <summary>Index of the current Plorp order (see PlorpGoals).</summary>
    public int PlorpStage { get; private set; }
    public int PepitoMeals { get; private set; }
    public PlorpGoal CurrentGoal => PlorpGoals.For(PlorpStage);
    public bool PlorpSatisfied => PlorpStage >= PlorpGoals.MainCount;
    public bool GoalReady => Food >= CurrentGoal.Food && AnimalCount >= CurrentGoal.Animals;
    public int AnimalCount => Levels[1];
    public float FarmLevelProgress => (Purchases % 3) / 3f;
    public bool MusicEnabled { get; private set; }
    public bool SoundEnabled { get; private set; }
    public event Action Changed;

    public FarmGameState()
    {
        EnsureProgressionVersion();
        Gold = ReadLong("gold", 250);
        Gems = PlayerPrefs.GetInt("GlorpFarm.gems", 0);
        FarmLevel = PlayerPrefs.GetInt("GlorpFarm.farmLevel", 1);
        Harvests = PlayerPrefs.GetInt("GlorpFarm.harvests", 0);
        Purchases = PlayerPrefs.GetInt("GlorpFarm.purchases", 0);
        TotalHarvestGold = ReadLong("harvestGold", 0);
        Food = ReadLong("food", 0);
        TotalFood = ReadLong("totalFood", 0);
        PlorpStage = PlayerPrefs.GetInt("GlorpFarm.plorpStage", 0);
        PepitoMeals = PlayerPrefs.GetInt("GlorpFarm.pepitoMeals", 0);
        MusicEnabled = PlayerPrefs.GetInt("GlorpFarm.music", 1) == 1;
        SoundEnabled = PlayerPrefs.GetInt("GlorpFarm.sound", 1) == 1;
        for (int i = 0; i < Levels.Length; i++)
        {
            Levels[i] = PlayerPrefs.GetInt("GlorpFarm.level." + i, StartingLevels[i]);
            Prices[i] = ReadLong("price." + i, StartingPrices[i]);
        }
        for (int i = 0; i < Claimed.Length; i++)
            Claimed[i] = PlayerPrefs.GetInt("GlorpFarm.claimed." + i, 0) == 1;
    }

    public long Harvest()
    {
        long earned = 10 + Levels[0] * 20L + Levels[1] * 12L + Levels[2] * 35L;
        Collect(earned);
        return earned;
    }

    /// <summary>Adds a harvest: coins for the player and food units for Plorp.</summary>
    public void Collect(long earned, int foodUnits = 1)
    {
        if (earned <= 0) return;
        Gold += earned;
        TotalHarvestGold += earned;
        Food += Math.Max(0, foodUnits);
        TotalFood += Math.Max(0, foodUnits);
        Harvests++;
        Save();
    }

    /// <summary>Hands the stored food to Plorp. Returns false when the order is not complete yet.</summary>
    public bool DeliverToPlorp(out PlorpGoal delivered)
    {
        delivered = CurrentGoal;
        if (!GoalReady) return false;
        Food -= delivered.Food;
        Gold += delivered.RewardGold;
        Gems += delivered.RewardGems;
        PlorpStage++;
        Save();
        return true;
    }

    /// <summary>Pays for Pepito's snack. Exactly one of the two costs is normally non-zero.</summary>
    public bool FeedPepito(long goldCost, int gemCost)
    {
        if (Gold < goldCost || Gems < gemCost) return false;
        Gold -= goldCost;
        Gems -= gemCost;
        PepitoMeals++;
        Save();
        return true;
    }

    public bool BuyUpgrade(int index)
    {
        if (index < 0 || index >= Prices.Length || IsMaxed(index) || Gold < Prices[index]) return false;
        Gold -= Prices[index];
        Levels[index]++;
        Prices[index] = (long)Math.Ceiling(Prices[index] * 1.48d);
        Purchases++;
        FarmLevel = 1 + Purchases / 3;
        Save();
        return true;
    }

    public bool IsMaxed(int index)
    {
        return index >= 0 && index < Levels.Length && Levels[index] >= MaxLevels[index];
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
            default: return Math.Min(PepitoMeals, 1) + "/1 comida a Pepito";
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
        PlayerPrefs.SetString("GlorpFarm.food", Food.ToString());
        PlayerPrefs.SetString("GlorpFarm.totalFood", TotalFood.ToString());
        PlayerPrefs.SetInt("GlorpFarm.plorpStage", PlorpStage);
        PlayerPrefs.SetInt("GlorpFarm.pepitoMeals", PepitoMeals);
        PlayerPrefs.SetInt("GlorpFarm.music", MusicEnabled ? 1 : 0);
        PlayerPrefs.SetInt("GlorpFarm.sound", SoundEnabled ? 1 : 0);
        for (int i = 0; i < Levels.Length; i++)
        {
            PlayerPrefs.SetInt("GlorpFarm.level." + i, Levels[i]);
            PlayerPrefs.SetString("GlorpFarm.price." + i, Prices[i].ToString());
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
            PlayerPrefs.SetString("GlorpFarm.price." + i, StartingPrices[i].ToString());
        }
        for (int i = 0; i < AchievementNames.Length; i++) PlayerPrefs.SetInt("GlorpFarm.claimed." + i, 0);

        PlayerPrefs.DeleteKey("GlorpFarm.animalRosterBaselineLevel");
        PlayerPrefs.DeleteKey("GlorpFarm.animalTestPriceApplied");
        PlayerPrefs.SetInt(ProgressionVersionKey, ProgressionVersion);
        PlayerPrefs.Save();
    }

    /// <summary>Clears only gameplay progress, leaving audio preferences intact.</summary>
    public static void ResetSavedProgress()
    {
        string[] coreKeys =
        {
            "gold", "gems", "farmLevel", "harvests", "purchases", "harvestGold",
            "food", "totalFood", "plorpStage", "pepitoMeals",
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

        for (int plot = 0; plot < 4; plot++)
        {
            for (int cell = 0; cell < 12; cell++)
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
