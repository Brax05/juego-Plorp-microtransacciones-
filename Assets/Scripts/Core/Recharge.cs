using System;
using UnityEngine;

/// <summary>
/// A stock that refills on its own over real time (fishing bait, minigame tickets): up to <c>max</c>,
/// one more every <c>seconds</c>, also while the game is closed. Saved in two PlayerPrefs keys.
/// </summary>
public readonly struct Recharge
{
    readonly string countKey, sinceKey;
    readonly int max, seconds;

    public Recharge(string countKey, string sinceKey, int max, int seconds)
    {
        this.countKey = countKey;
        this.sinceKey = sinceKey;
        this.max = max;
        this.seconds = seconds;
    }

    public int Max => max;
    static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    long Since => long.TryParse(SaveStore.GetString(sinceKey, Now.ToString()), out long v) ? v : Now;

    public int Count
    {
        get
        {
            int stored = SaveStore.GetInt(countKey, max);
            if (stored >= max) return max;
            return Mathf.Min(max, stored + (int)((Now - Since) / seconds));
        }
    }

    public int SecondsToNext => Count >= max ? 0 : seconds - (int)((Now - Since) % seconds);

    /// <summary>Spends one (keeps the time already waited for the next one). False when empty.</summary>
    public bool Use()
    {
        int n = Count;
        if (n <= 0) return false;
        long keep = n >= max ? 0 : (Now - Since) % seconds;
        SaveStore.SetInt(countKey, n - 1);
        SaveStore.SetString(sinceKey, (Now - keep).ToString());
        SaveStore.Save();
        return true;
    }
}
