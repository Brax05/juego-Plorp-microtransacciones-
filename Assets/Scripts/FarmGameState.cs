using System;
using UnityEngine;

/// <summary>Small persistent demo economy shared by every screen.</summary>
public sealed class FarmGameState
{
    public static readonly string[] UpgradeNames = { "Semillas", "Animales", "Trabajadores", "Maquinaria" };
    public static readonly string[] AchievementNames = { "Primera cosecha", "Granjero constante", "Gran inversion" };
    static readonly long[] StartingPrices = { 150000, 1200000, 5000000, 10000000 };
    static readonly int[] StartingLevels = { 25, 14, 3, 1 };

    public readonly int[] Levels = new int[4];
    public readonly long[] Prices = new long[4];
    public readonly bool[] Claimed = new bool[3];
    public long Gold { get; private set; }
    public int Gems { get; private set; }
    public int FarmLevel { get; private set; }
    public int Harvests { get; private set; }
    public int Purchases { get; private set; }
    public long TotalHarvestGold { get; private set; }
    public bool MusicEnabled { get; private set; }
    public bool SoundEnabled { get; private set; }
    public event Action Changed;

    public FarmGameState()
    {
        Gold = ReadLong("gold", 15240000);
        Gems = PlayerPrefs.GetInt("GlorpFarm.gems", 320);
        FarmLevel = PlayerPrefs.GetInt("GlorpFarm.farmLevel", 12);
        Harvests = PlayerPrefs.GetInt("GlorpFarm.harvests", 0);
        Purchases = PlayerPrefs.GetInt("GlorpFarm.purchases", 0);
        TotalHarvestGold = ReadLong("harvestGold", 0);
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
        long earned = 10 + Levels[0] * 25L + Levels[1] * 10L + Levels[2] * 50L + Levels[3] * 100L;
        Collect(earned);
        return earned;
    }

    public void Collect(long earned)
    {
        if (earned <= 0) return;
        Gold += earned;
        TotalHarvestGold += earned;
        Harvests++;
        Save();
    }

    public bool BuyUpgrade(int index)
    {
        if (index < 0 || index >= Prices.Length || Gold < Prices[index]) return false;
        Gold -= Prices[index];
        Levels[index]++;
        Prices[index] = (long)Math.Ceiling(Prices[index] * 1.65d);
        Purchases++;
        FarmLevel = Math.Max(FarmLevel, 12 + Purchases / 4);
        Save();
        return true;
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
            default: return Math.Min(Purchases, 5) + "/5 mejoras";
        }
    }

    public bool AchievementReady(int index)
    {
        if (index < 0 || index >= Claimed.Length || Claimed[index]) return false;
        return index == 0 ? Harvests >= 1 : index == 1 ? Harvests >= 10 : Purchases >= 5;
    }

    public bool ClaimAchievement(int index)
    {
        if (!AchievementReady(index)) return false;
        Claimed[index] = true;
        Gems += index == 0 ? 5 : index == 1 ? 20 : 30;
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

    public static string Format(long value)
    {
        if (value >= 1000000) return (value / 1000000d).ToString("0.##") + "M";
        if (value >= 1000) return (value / 1000d).ToString("0.##") + "K";
        return value.ToString();
    }
}
