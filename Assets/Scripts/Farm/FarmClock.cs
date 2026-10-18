using System;
using UnityEngine;

public enum Season { Primavera, Verano, Otono, Invierno }

/// <summary>
/// Time of day and season of the farm (Módulo 5).
///  - Day and night: one farm day lasts 16 real minutes (day → sunset → night → dawn). It follows the
///    real clock, so it keeps turning with the game closed. At night golden harvests are twice as likely.
///  - Seasons: one per real day, in order (primavera, verano, otoño, invierno). Each season has a
///    "cultivo de temporada" worth ×1.5.
/// </summary>
public static class FarmClock
{
    public const int CycleSeconds = 16 * 60;
    // Fractions of the cycle: day until DuskStart, sunset, night, dawn.
    const float DuskStart = .62f, NightStart = .72f, NightEnd = .90f;
    public const float SeasonBonus = 1.5f;

    static readonly string[] SeasonNames = { "PRIMAVERA", "VERANO", "OTOÑO", "INVIERNO" };
    static readonly CropKind[] SeasonCrops = { CropKind.Carrot, CropKind.Tomato, CropKind.Pumpkin, CropKind.Cabbage };

    static double Unix => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    /// <summary>0..1 through the farm day.</summary>
    public static float DayPhase => (float)(Unix % CycleSeconds / CycleSeconds);

    public static bool IsNight
    {
        get { float t = DayPhase; return t >= NightStart && t < NightEnd; }
    }

    /// <summary>"día", "atardecer", "noche" or "amanecer".</summary>
    public static string PartOfDay
    {
        get
        {
            float t = DayPhase;
            return t < DuskStart ? "día" : t < NightStart ? "atardecer" : t < NightEnd ? "noche" : "amanecer";
        }
    }

    /// <summary>Real seconds until the night starts (0 while it's night).</summary>
    public static int SecondsToNight
    {
        get
        {
            float t = DayPhase;
            if (t >= NightStart && t < NightEnd) return 0;
            float left = t < NightStart ? NightStart - t : 1f - t + NightStart;
            return Mathf.CeilToInt(left * CycleSeconds);
        }
    }

    /// <summary>
    /// Sky over the farm: 0 = clear day, 1 = deep night. <paramref name="warm"/> is how orange the light is
    /// (sunset and dawn).
    /// </summary>
    public static float Darkness(out float warm)
    {
        float t = DayPhase;
        warm = 0f;
        if (t < DuskStart) return 0f;
        if (t < NightStart)
        {
            float k = (t - DuskStart) / (NightStart - DuskStart);
            warm = Mathf.Sin(k * Mathf.PI);
            return k * k;
        }
        if (t < NightEnd) return 1f;
        float d = (t - NightEnd) / (1f - NightEnd);
        warm = Mathf.Sin(d * Mathf.PI) * .7f;
        return 1f - d;
    }

    // ---------- Seasons ----------

    public static Season CurrentSeason
    {
        get
        {
            long days = (long)Math.Floor((DateTime.Now - new DateTime(2026, 1, 1)).TotalDays);
            return (Season)(int)(((days % 4) + 4) % 4);
        }
    }

    public static string SeasonName(Season s) => SeasonNames[(int)s];
    public static CropKind SeasonCrop(Season s) => SeasonCrops[(int)s];
    public static CropKind CurrentSeasonCrop => SeasonCrop(CurrentSeason);

    /// <summary>×1.5 for the season's crop.</summary>
    public static float CropBonus(CropKind kind) => kind == CurrentSeasonCrop ? SeasonBonus : 1f;

    /// <summary>Golden harvests are twice as likely at night.</summary>
    public static float GoldenMultiplier => IsNight ? 2f : 1f;

    /// <summary>What Pepito says when the season badge on the HUD is tapped.</summary>
    public static string BadgeLine()
    {
        var season = CurrentSeason;
        string crop = FarmItems.ForCrop(SeasonCrop(season)).Name.ToLowerInvariant();
        string sky = IsNight
            ? "Ahora es de noche: las cosechas doradas salen el doble."
            : "Falta " + Mathf.Max(1, SecondsToNight / 60) + " min para la noche (cosechas doradas x2).";
        return "Miau. Es " + SeasonName(season) + ": los " + crop + " valen x1,5 hoy. Mañana cambia la estación. " + sky;
    }

    public static string SeasonLine(Season s)
    {
        string crop = FarmItems.ForCrop(SeasonCrop(s)).Name.ToLowerInvariant();
        switch (s)
        {
            case Season.Primavera:
                return "Miau... llegó la PRIMAVERA. Todo huele a flores y las " + crop + " valen x1,5. Plorp estornudó.";
            case Season.Verano:
                return "Miau, ¡VERANO! Hace calor y los " + crop + " valen x1,5. Yo voy a dormir a la sombra.";
            case Season.Otono:
                return "Miau... llegó el OTOÑO. Caen hojas por todos lados y las " + crop + " valen x1,5.";
            default:
                return "Brrr... miau. Es INVIERNO. Nieva sobre la granja y los " + crop + " valen x1,5. Plorp quiere sopa.";
        }
    }
}
