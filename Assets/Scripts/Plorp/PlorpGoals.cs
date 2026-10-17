using System;

/// <summary>One order from Plorp: how much food and how many animals he wants, and what he pays.</summary>
public readonly struct PlorpGoal
{
    public readonly string Title;
    public readonly string Quote;
    public readonly long Food;
    public readonly int Animals;
    public readonly long RewardGold;
    public readonly int RewardGems;

    public PlorpGoal(string title, string quote, long food, int animals, long rewardGold, int rewardGems)
    {
        Title = title; Quote = quote; Food = food; Animals = animals;
        RewardGold = rewardGold; RewardGems = rewardGems;
    }
}

/// <summary>
/// The game's main goal: fill Plorp's orders until he is satisfied ("conforme").
/// Adding a stage is one line. Animals = level of the "Animales" upgrade (one animal per level).
/// After the last main order Plorp keeps asking for optional "antojos" that scale up.
/// </summary>
public static class PlorpGoals
{
    static readonly PlorpGoal[] Main =
    {
        new PlorpGoal("Primer bocado",   "¡Plorp!",          15,   0,   200,  3),
        new PlorpGoal("Desayuno",        "Glerp glerp.",     40,   1,   500,  5),
        new PlorpGoal("Almuerzo",        "Zep zep zep.",     90,   3,  1500,  8),
        new PlorpGoal("Banquete",        "¡PLORP!",         200,   6,  4000, 12),
        new PlorpGoal("Festin espacial", "Plorp... plorp.", 420,  10, 12000, 20),
        new PlorpGoal("Gran cosecha",    "Zeeeep.",         800,  15, 30000, 40),
    };

    public static int MainCount => Main.Length;

    public static PlorpGoal For(int stage)
    {
        if (stage < Main.Length) return Main[Math.Max(0, stage)];
        // Endless optional cravings after Plorp is satisfied.
        int extra = stage - Main.Length + 1;
        var last = Main[Main.Length - 1];
        double scale = Math.Pow(1.5, extra);
        return new PlorpGoal("Antojo extra " + extra, "Plorp. (Otra vez hambre.)",
            (long)Math.Ceiling(last.Food * scale), last.Animals,
            (long)Math.Ceiling(last.RewardGold * scale), 10);
    }
}
