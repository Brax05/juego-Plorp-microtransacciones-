using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// The single door to the saved game. It is used exactly like <c>PlayerPrefs</c> (same calls, same keys
/// <c>GlorpFarm.*</c>) and stores everything there, but it also remembers which keys the game uses, so the
/// whole save can be copied as one piece (<see cref="Capture"/>) to Google Play Juegos and brought back
/// (<see cref="Apply"/>) on another phone or after reinstalling. See <see cref="CloudSave"/>.
/// </summary>
public static class SaveStore
{
    const string IndexKey = "GlorpFarm.__index";
    /// <summary>Seconds played in this save (all phones): the cloud keeps the save that was played longer.</summary>
    public const string PlaySecondsKey = "GlorpFarm.cloud.playSeconds";

    // key → 'i' int, 'f' float, 's' string
    static Dictionary<string, char> index;
    static bool indexDirty;

    /// <summary>Raised after every <see cref="Save"/> (the cloud copy is then out of date).</summary>
    public static event Action Saved;

    /// <summary>
    /// While true every write is ignored: after a save is brought back from the cloud, the old farm still
    /// runs until the scene reloads and must not write its old numbers over it.
    /// </summary>
    public static bool Frozen { get; set; }

    static Dictionary<string, char> Index
    {
        get
        {
            if (index == null) LoadIndex();
            return index;
        }
    }

    static void LoadIndex()
    {
        index = new Dictionary<string, char>();
        string raw = PlayerPrefs.GetString(IndexKey, "");
        foreach (string line in raw.Split('\n'))
            if (line.Length > 2 && line[1] == ':') index[line.Substring(2)] = line[0];
    }

    static void Track(string key, char type)
    {
        var known = Index;
        if (known.TryGetValue(key, out char current) && current == type) return;
        known[key] = type;
        indexDirty = true;
    }

    // A key saved by an older version of the game enters the index the first time it is read.
    static void TrackRead(string key, char type)
    {
        if (!Index.ContainsKey(key) && PlayerPrefs.HasKey(key)) Track(key, type);
    }

    // ---------- Same calls as PlayerPrefs ----------

    public static int GetInt(string key, int fallback = 0)
    {
        TrackRead(key, 'i');
        return PlayerPrefs.GetInt(key, fallback);
    }

    public static void SetInt(string key, int value)
    {
        if (Frozen) return;
        Track(key, 'i');
        PlayerPrefs.SetInt(key, value);
    }

    public static float GetFloat(string key, float fallback = 0f)
    {
        TrackRead(key, 'f');
        return PlayerPrefs.GetFloat(key, fallback);
    }

    public static void SetFloat(string key, float value)
    {
        if (Frozen) return;
        Track(key, 'f');
        PlayerPrefs.SetFloat(key, value);
    }

    public static string GetString(string key, string fallback = "")
    {
        TrackRead(key, 's');
        return PlayerPrefs.GetString(key, fallback);
    }

    public static void SetString(string key, string value)
    {
        if (Frozen) return;
        Track(key, 's');
        PlayerPrefs.SetString(key, value);
    }

    public static bool HasKey(string key) => PlayerPrefs.HasKey(key);

    public static void DeleteKey(string key)
    {
        if (Frozen) return;
        if (Index.Remove(key)) indexDirty = true;
        PlayerPrefs.DeleteKey(key);
    }

    public static void Save()
    {
        if (Frozen) return;
        WriteIndex();
        PlayerPrefs.Save();
        Saved?.Invoke();
    }

    static void WriteIndex()
    {
        if (!indexDirty) return;
        var lines = new List<string>(Index.Count);
        foreach (var pair in Index) lines.Add(pair.Value + ":" + pair.Key);
        PlayerPrefs.SetString(IndexKey, string.Join("\n", lines));
        indexDirty = false;
    }

    // ---------- Play time ----------

    public static double PlaySeconds
    {
        get
        {
            return double.TryParse(GetString(PlaySecondsKey, "0"), NumberStyles.Float, CultureInfo.InvariantCulture,
                out double seconds) ? seconds : 0d;
        }
    }

    /// <summary>Adds play time (kept in memory until the next <see cref="Save"/>).</summary>
    public static void AddPlayTime(double seconds)
    {
        if (seconds <= 0d) return;
        SetString(PlaySecondsKey, (PlaySeconds + seconds).ToString("R", CultureInfo.InvariantCulture));
    }

    // ---------- The whole save as one piece ----------

    /// <summary>Everything the game saved, as JSON-friendly arrays (JsonUtility can't write dictionaries).</summary>
    [Serializable]
    public sealed class Snapshot
    {
        public int version = 1;
        public double playSeconds;
        public long savedAtUnix;
        public string[] intKeys = new string[0];
        public int[] intValues = new int[0];
        public string[] floatKeys = new string[0];
        public float[] floatValues = new float[0];
        public string[] stringKeys = new string[0];
        public string[] stringValues = new string[0];
    }

    public static Snapshot Capture()
    {
        var ik = new List<string>(); var iv = new List<int>();
        var fk = new List<string>(); var fv = new List<float>();
        var sk = new List<string>(); var sv = new List<string>();
        foreach (var pair in Index)
        {
            if (!PlayerPrefs.HasKey(pair.Key)) continue;
            switch (pair.Value)
            {
                case 'i': ik.Add(pair.Key); iv.Add(PlayerPrefs.GetInt(pair.Key, 0)); break;
                case 'f': fk.Add(pair.Key); fv.Add(PlayerPrefs.GetFloat(pair.Key, 0f)); break;
                default: sk.Add(pair.Key); sv.Add(PlayerPrefs.GetString(pair.Key, "")); break;
            }
        }
        return new Snapshot
        {
            playSeconds = PlaySeconds,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            intKeys = ik.ToArray(), intValues = iv.ToArray(),
            floatKeys = fk.ToArray(), floatValues = fv.ToArray(),
            stringKeys = sk.ToArray(), stringValues = sv.ToArray(),
        };
    }

    /// <summary>Replaces the whole local save with <paramref name="snapshot"/> and saves it.</summary>
    public static void Apply(Snapshot snapshot)
    {
        if (snapshot == null || Frozen) return;
        foreach (string key in new List<string>(Index.Keys)) PlayerPrefs.DeleteKey(key);
        Index.Clear();
        indexDirty = true;
        int n = Math.Min(snapshot.intKeys.Length, snapshot.intValues.Length);
        for (int i = 0; i < n; i++) SetInt(snapshot.intKeys[i], snapshot.intValues[i]);
        n = Math.Min(snapshot.floatKeys.Length, snapshot.floatValues.Length);
        for (int i = 0; i < n; i++) SetFloat(snapshot.floatKeys[i], snapshot.floatValues[i]);
        n = Math.Min(snapshot.stringKeys.Length, snapshot.stringValues.Length);
        for (int i = 0; i < n; i++) SetString(snapshot.stringKeys[i], snapshot.stringValues[i]);
        Save();
    }

    public static string ToJson(Snapshot snapshot) => JsonUtility.ToJson(snapshot);

    /// <summary>Null when the text is empty or isn't a save of this game.</summary>
    public static Snapshot FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var snapshot = JsonUtility.FromJson<Snapshot>(json);
            if (snapshot == null || snapshot.version < 1 || snapshot.intKeys == null || snapshot.intValues == null ||
                snapshot.floatKeys == null || snapshot.floatValues == null || snapshot.stringKeys == null ||
                snapshot.stringValues == null) return null;
            return snapshot;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
