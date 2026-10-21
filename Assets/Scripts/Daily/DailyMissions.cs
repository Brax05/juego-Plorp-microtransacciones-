using System;
using UnityEngine;

public enum MissionKind { Crops, AnimalProducts, Upgrades, Orders, Bugs, Coins, Golden, FeedPepito }

/// <summary>One of today's three missions.</summary>
public sealed class DailyMission
{
    public int Slot;          // 0 easy, 1 medium, 2 hard
    public MissionKind Kind;
    public long Target;
    public long Progress;
    public bool Claimed;
    public bool Done => Progress >= Target;
    public int Gems => DailyMissions.MissionGems[Slot];
}

/// <summary>
/// Daily missions and the daily streak.
///  - Every day (local midnight) there are 3 new missions: easy, medium and hard. Each pays gems and
///    coins (coins scale with the farm's income); finishing all three opens a bonus chest.
///  - Coming back on consecutive days builds a streak with growing rewards (day 7 is the big one,
///    then it loops). Missing a day starts again at day 1.
/// Progress is reported from FarmGameState / GardenState with <see cref="Report"/>.
/// </summary>
public static class DailyMissions
{
    const string Key = "GlorpFarm.daily.";
    public const int Count = 3;
    public static readonly int[] MissionGems = { 2, 3, 5 };
    public const int BonusGems = 5;
    public static readonly int[] StreakGems = { 2, 3, 4, 5, 6, 8, 15 };
    static readonly int[] StreakCoinMinutes = { 3, 5, 8, 10, 15, 20, 40 };
    static readonly int[] MissionCoinMinutes = { 2, 5, 10 };

    static readonly MissionKind[][] Pools =
    {
        new[] { MissionKind.Crops, MissionKind.AnimalProducts, MissionKind.Upgrades, MissionKind.Bugs, MissionKind.FeedPepito },
        new[] { MissionKind.Crops, MissionKind.AnimalProducts, MissionKind.Coins, MissionKind.Orders, MissionKind.Bugs, MissionKind.Golden },
        new[] { MissionKind.Crops, MissionKind.AnimalProducts, MissionKind.Coins, MissionKind.Upgrades, MissionKind.Orders },
    };

    static readonly DailyMission[] missions = new DailyMission[Count];
    static string loadedDay;

    /// <summary>Progress changed or something was claimed (pages redraw).</summary>
    public static event Action Changed;

    /// <summary>While true, nothing counts (the "Mientras no estabas" coins aren't played today).</summary>
    public static bool Muted;

    static string Today => DateTime.Now.ToString("yyyy-MM-dd");
    static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

    // ---------- Missions ----------

    public static DailyMission[] Missions(FarmGameState s)
    {
        EnsureToday(s);
        return missions;
    }

    /// <summary>Counts progress for today's missions of that kind.</summary>
    public static void Report(MissionKind kind, long amount)
    {
        if (Muted || amount <= 0) return;
        if (loadedDay == null && FarmEconomy.Current == null) return;
        EnsureToday(FarmEconomy.Current);
        bool changed = false;
        for (int i = 0; i < Count; i++)
        {
            var m = missions[i];
            if (m == null || m.Kind != kind || m.Done) continue;
            m.Progress = Math.Min(m.Target, m.Progress + amount);
            SaveStore.SetString(Key + "m" + i + ".progress", m.Progress.ToString());
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    public static long MissionCoins(FarmGameState s, DailyMission m) =>
        Nice(Math.Max(100L * (m.Slot + 1), (long)(FarmEconomy.IncomePerSecond(s) * 60 * MissionCoinMinutes[m.Slot])));

    public static bool Claim(FarmGameState s, DailyMission m)
    {
        if (m == null || !m.Done || m.Claimed) return false;
        m.Claimed = true;
        SaveStore.SetInt(Key + "m" + m.Slot + ".claimed", 1);
        s.AddGems(m.Gems);
        s.BuyGold(0, MissionCoins(s, m));
        SaveStore.Save();
        Changed?.Invoke();
        return true;
    }

    public static bool AllClaimed(FarmGameState s)
    {
        foreach (var m in Missions(s)) if (!m.Claimed) return false;
        return true;
    }

    public static bool BonusClaimed => SaveStore.GetString(Key + "bonusDay", "") == Today;
    public static bool BonusReady(FarmGameState s) => AllClaimed(s) && !BonusClaimed;

    public static bool ClaimBonus(FarmGameState s)
    {
        if (!BonusReady(s)) return false;
        SaveStore.SetString(Key + "bonusDay", Today);
        s.AddGems(BonusGems);
        SaveStore.Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Missions, bonus and streak waiting to be collected (red badge on the Logros tab).</summary>
    public static int ReadyCount(FarmGameState s)
    {
        int n = 0;
        foreach (var m in Missions(s)) if (m.Done && !m.Claimed) n++;
        if (BonusReady(s)) n++;
        if (StreakReady) n++;
        return n;
    }

    public static TimeSpan TimeToReset => DateTime.Today.AddDays(1) - DateTime.Now;

    public static string Describe(MissionKind kind, long target)
    {
        string n = FarmGameState.Format(target);
        switch (kind)
        {
            case MissionKind.Crops: return "Cosecha " + n + " cultivos";
            case MissionKind.AnimalProducts: return "Junta " + n + " huevos, quesos o leches";
            case MissionKind.Upgrades: return "Compra " + n + " mejoras en la granja";
            case MissionKind.Orders: return "Entrega " + n + (target == 1 ? " pedido" : " pedidos") + " a Plorp";
            case MissionKind.Bugs: return "Aplasta " + n + " bichitos en la jardinería";
            case MissionKind.Coins: return "Gana " + n + " monedas";
            case MissionKind.Golden: return "Consigue " + n + (target == 1 ? " cosecha dorada" : " cosechas doradas");
            default: return "Dale a Plorp lo que pide Pepito";
        }
    }

    public static string Icon(MissionKind kind)
    {
        switch (kind)
        {
            case MissionKind.Crops: return "carrot_2";
            case MissionKind.AnimalProducts: return "egg";
            case MissionKind.Upgrades: return "mejora";
            case MissionKind.Orders: return "granja";
            case MissionKind.Bugs: return "bug_0";
            case MissionKind.Coins: return "icon_coin";
            case MissionKind.Golden: return "pumpkin_2";
            default: return "milk";
        }
    }

    static void EnsureToday(FarmGameState s)
    {
        string today = Today;
        if (loadedDay == today) return;
        loadedDay = today;
        if (SaveStore.GetString(Key + "day", "") == today)
        {
            for (int i = 0; i < Count; i++)
            {
                missions[i] = new DailyMission
                {
                    Slot = i,
                    Kind = (MissionKind)SaveStore.GetInt(Key + "m" + i + ".kind", 0),
                    Target = ReadLong(Key + "m" + i + ".target", 10),
                    Progress = ReadLong(Key + "m" + i + ".progress", 0),
                    Claimed = SaveStore.GetInt(Key + "m" + i + ".claimed", 0) == 1,
                };
            }
            return;
        }

        // A new day: three new missions, the same for the whole day.
        var rng = new System.Random(today.GetHashCode() ^ 0x5eed);
        SaveStore.SetString(Key + "day", today);
        bool[] used = new bool[Enum.GetValues(typeof(MissionKind)).Length];
        for (int i = 0; i < Count; i++)
        {
            MissionKind kind;
            int guard = 0;
            do kind = Pools[i][rng.Next(Pools[i].Length)];
            while (used[(int)kind] && ++guard < 50);
            used[(int)kind] = true;
            var m = new DailyMission { Slot = i, Kind = kind, Target = TargetFor(s, kind, i), Progress = 0, Claimed = false };
            missions[i] = m;
            SaveStore.SetInt(Key + "m" + i + ".kind", (int)kind);
            SaveStore.SetString(Key + "m" + i + ".target", m.Target.ToString());
            SaveStore.SetString(Key + "m" + i + ".progress", "0");
            SaveStore.SetInt(Key + "m" + i + ".claimed", 0);
        }
        SaveStore.Save();
    }

    /// <summary>Targets grow with the farm so missions stay a few minutes of play.</summary>
    static long TargetFor(FarmGameState s, MissionKind kind, int slot)
    {
        int seeds = s != null ? s.Levels[FarmEconomy.Seeds] : 1;
        int animals = s != null ? Math.Max(1, s.Levels[FarmEconomy.Animals]) : 1;
        switch (kind)
        {
            case MissionKind.Crops:
                return Nice((long)(new[] { 30, 80, 160 }[slot] * (1 + seeds / 15.0)));
            case MissionKind.AnimalProducts:
                return Math.Max(3, (long)(new[] { 4, 10, 20 }[slot] * (1 + Math.Min(animals, 18) / 6.0)));
            case MissionKind.Upgrades: return new[] { 3, 6, 10 }[slot];
            case MissionKind.Orders: return slot < 2 ? 1 : 2;
            case MissionKind.Bugs: return new[] { 2, 4, 6 }[slot];
            case MissionKind.Coins:
                double perSecond = s != null ? FarmEconomy.IncomePerSecond(s) : 5;
                return Nice(Math.Max(300, (long)(perSecond * new[] { 180, 480, 1200 }[slot])));
            case MissionKind.Golden: return slot < 2 ? 1 : 2;
            default: return 1;
        }
    }

    // ---------- Daily streak ----------

    public static int Streak => Math.Max(1, SaveStore.GetInt(Key + "streak", 1));
    /// <summary>0..6: which of the 7 reward days is today's.</summary>
    public static int StreakDay => (Streak - 1) % 7;
    public static bool StreakReady => SaveStore.GetString(Key + "streakClaimed", "") != Today;

    /// <summary>Call when the game opens (and when it comes back): advances or resets the streak.</summary>
    public static void RegisterLogin()
    {
        string today = Today;
        string last = SaveStore.GetString(Key + "lastLogin", "");
        if (last == today) return;
        int streak = last == Yesterday ? SaveStore.GetInt(Key + "streak", 0) + 1 : 1;
        SaveStore.SetInt(Key + "streak", streak);
        SaveStore.SetString(Key + "lastLogin", today);
        SaveStore.Save();
        Changed?.Invoke();
    }

    public static long StreakCoins(FarmGameState s, int day) =>
        Nice(Math.Max(200L * (day + 1), (long)(FarmEconomy.IncomePerSecond(s) * 60 * StreakCoinMinutes[day])));

    public static bool ClaimStreak(FarmGameState s)
    {
        if (!StreakReady) return false;
        int day = StreakDay;
        SaveStore.SetString(Key + "streakClaimed", Today);
        s.AddGems(StreakGems[day]);
        s.BuyGold(0, StreakCoins(s, day));
        SaveStore.Save();
        Changed?.Invoke();
        return true;
    }

    // ---------- Helpers ----------

    /// <summary>Rounds to 2 significant digits (1234 → 1200) so numbers look tidy.</summary>
    static long Nice(long v)
    {
        if (v < 100) return Math.Max(1, v);
        long p = (long)Math.Pow(10, Math.Floor(Math.Log10(v)) - 1);
        return v / p * p;
    }

    static long ReadLong(string key, long fallback) =>
        long.TryParse(SaveStore.GetString(key, fallback.ToString()), out long v) ? v : fallback;
}
