using System;
using UnityEngine;

/// <summary>
/// All the idle-game numbers in one place.
///
/// Every upgrade levels up one purchase at a time, and every 10 purchases is a "tier":
///  - Semillas: each tier opens a field with a more profitable crop
///    (zanahoria → repollo → tomate → calabaza). Older crops move to the other fields.
///    Once all crops are out, every 10 levels doubles crop profits.
///  - Animales: each purchase adds an animal; every tier brings a better species
///    (gallina/huevo → oveja/queso → vaca/leche), then every 10 levels doubles animal profits.
///  - Trabajadores: each level adds a small, stacking +5% to everything and makes the
///    automatic harvest a bit faster.
/// Each level inside a tier also raises that product's value a little.
/// </summary>
public static class FarmEconomy
{
    public const int Seeds = 0, Animals = 1, Workers = 2;
    public const int LevelsPerTier = 10;
    public static readonly int[] MaxLevels = { 100, 80, 50 };
    // The farm starts with carrots, one chicken and one worker.
    public static readonly int[] StartingLevels = { 1, 1, 1 };

    // Crops by tier: value per harvest and seconds per growth stage (2 stages to be ready).
    public static readonly CropKind[] CropTiers = { CropKind.Carrot, CropKind.Cabbage, CropKind.Tomato, CropKind.Pumpkin };
    // Balance v7: lower values and slower crops; prices climb faster than income (one purchase
    // every ~30-60 s at the start, a few minutes after the first hour).
    static readonly long[] CropValue = { 5, 20, 70, 260 };
    static readonly int[] CropStageSeconds = { 12, 18, 26, 36 };

    // Animals by tier: product value, seconds between products, animals shown per species.
    public static readonly string[] Species = { "chicken", "sheep", "cow" };
    public static readonly string[] SpeciesNames = { "Gallina", "Oveja", "Vaca" };
    static readonly long[] AnimalValue = { 15, 60, 220 };
    static readonly int[] AnimalSeconds = { 40, 60, 80 };
    public const int AnimalsPerSpecies = 6;

    // ---------- Levels and tiers ----------

    public static int Tier(int level) => Math.Max(0, level - 1) / LevelsPerTier;

    /// <summary>Purchases done inside the current tier (0..9).</summary>
    public static int TierProgress(int level) => Math.Max(0, level - 1) % LevelsPerTier;

    /// <summary>×2 for every 10 levels after the last product has been unlocked.</summary>
    static double Milestone(int level, int productTiers) => Math.Pow(2, Math.Max(0, Tier(level) - (productTiers - 1)));

    /// <summary>Workers (+5% each) times the gardening "todo" bonus.</summary>
    /// <summary>The running game state (for timers that have no reference to it, like crop growth).</summary>
    public static FarmGameState Current;
    static float Speed => Current != null && Current.Boosts != null ? Current.Boosts.SpeedMultiplier : 1f;

    /// <summary>Workers (+5% each) × garden "todo" bonus × the x2 income boost.</summary>
    public static double WorkerBonus(FarmGameState s) =>
        (1.0 + 0.05 * s.Levels[Workers]) * (s.Garden != null ? s.Garden.GlobalMultiplier : 1f)
        * (s.Boosts != null ? s.Boosts.IncomeMultiplier : 1f);

    /// <summary>Rough coins per second of the whole farm (used by the time-skip items).</summary>
    public static double IncomePerSecond(FarmGameState s)
    {
        double total = 0;
        for (int p = 0; p < ActivePlots(s); p++)
        {
            var kind = PlotKind(s, p);
            total += FarmArtLayout.CellsPerPlot * CropHarvestValue(s, kind) / (2.0 * CropStageSeconds[CropIndex(kind)]);
        }
        for (int sp = 0; sp < Species.Length; sp++)
            total += AnimalCount(s, sp) * AnimalProductValue(s, sp) / (double)AnimalSeconds[sp];
        return total;
    }

    static double GardenCrops(FarmGameState s) => s.Garden != null ? s.Garden.CropMultiplier : 1f;
    static double GardenAnimals(FarmGameState s) => s.Garden != null ? s.Garden.AnimalMultiplier : 1f;

    // ---------- Crops ----------

    public static int SeedTier(FarmGameState s) => Math.Min(CropTiers.Length - 1, Tier(s.Levels[Seeds]));
    /// <summary>One field per seed tier; the fifth field opens with the first x2 tier.</summary>
    public static int ActivePlots(FarmGameState s) => Math.Min(FarmArtLayout.Fields.Length, Tier(s.Levels[Seeds]) + 1);

    /// <summary>Field 0 always grows the newest crop; older crops move one field down.</summary>
    public static CropKind PlotKind(FarmGameState s, int plot) => CropTiers[Math.Max(0, SeedTier(s) - plot)];

    static int CropIndex(CropKind kind) => Math.Max(0, Array.IndexOf(CropTiers, kind));

    public static long CropHarvestValue(FarmGameState s, CropKind kind)
    {
        int level = s.Levels[Seeds];
        double perLevel = 1.0 + 0.08 * Math.Max(0, level - 1);
        return Round(CropValue[CropIndex(kind)] * perLevel * Milestone(level, CropTiers.Length) * WorkerBonus(s) * GardenCrops(s));
    }

    public static long CropStage(CropKind kind) => Math.Max(1, (long)Math.Round(CropStageSeconds[CropIndex(kind)] / Speed));

    // ---------- Animals ----------

    /// <summary>How many animals of this species are on the farm.</summary>
    public static int AnimalCount(FarmGameState s, int species) =>
        Mathf.Clamp(s.Levels[Animals] - species * LevelsPerTier, 0, AnimalsPerSpecies);

    public static int SpeciesUnlocked(FarmGameState s)
    {
        int n = 0;
        for (int i = 0; i < Species.Length; i++) if (AnimalCount(s, i) > 0) n++;
        return n;
    }

    public static int SpeciesOf(string actorId)
    {
        for (int i = 0; i < Species.Length; i++) if (actorId.StartsWith(Species[i])) return i;
        return -1;
    }

    public static long AnimalProductValue(FarmGameState s, int species)
    {
        int level = s.Levels[Animals];
        int levelsInSpecies = Math.Max(0, level - species * LevelsPerTier - 1);
        double perLevel = 1.0 + 0.08 * levelsInSpecies;
        return Round(AnimalValue[species] * perLevel * Milestone(level, Species.Length) * WorkerBonus(s) * GardenAnimals(s));
    }

    public static long AnimalCooldown(int species) =>
        Math.Max(1, (long)Math.Round(AnimalSeconds[Mathf.Clamp(species, 0, Species.Length - 1)] / Speed));

    // ---------- Workers ----------

    public static int VisibleWorkers(FarmGameState s) => Math.Min(4, s.Levels[Workers]);

    /// <summary>Seconds between automatic harvests; 0 = no workers yet.</summary>
    public static float AutoHarvestSeconds(FarmGameState s)
    {
        int w = s.Levels[Workers];
        float speed = s.Garden != null ? s.Garden.WorkerSpeed : 1f;
        return w <= 0 ? 0f : Mathf.Max(1.5f, 15f * Mathf.Pow(.93f, w - 1) / speed / Speed);
    }

    // ---------- Prices ----------

    public static long Cost(int upgrade, int level)
    {
        switch (upgrade)
        {
            case Seeds: return Round(150 * Math.Pow(1.21, level - 1));
            case Animals: return Round(250 * Math.Pow(1.24, level));
            default: return Round(1200 * Math.Pow(1.32, level - 1));
        }
    }

    // ---------- Texts for the UI ----------

    /// <summary>What the next tier of this upgrade brings ("Repollo", "x2", ...).</summary>
    public static string NextTierReward(FarmGameState s, int upgrade)
    {
        int tier = Tier(Math.Max(1, s.Levels[upgrade]));
        if (upgrade == Seeds)
            return tier + 1 < CropTiers.Length ? CropName(CropTiers[tier + 1]) : "x2 cultivos";
        if (upgrade == Animals)
        {
            int next = s.Levels[Animals] == 0 ? 0 : tier + 1;
            return next < Species.Length ? SpeciesNames[next] : "x2 animales";
        }
        return "";
    }

    /// <summary>Short line under each upgrade: what it produces now.</summary>
    public static string CurrentLabel(FarmGameState s, int upgrade)
    {
        switch (upgrade)
        {
            case Seeds:
                return CropName(PlotKind(s, 0)) + " " + FarmGameState.Format(CropHarvestValue(s, PlotKind(s, 0)));
            case Animals:
                if (s.Levels[Animals] == 0) return "Sin animales";
                int sp = SpeciesUnlocked(s) - 1;
                return SpeciesNames[sp] + " " + FarmGameState.Format(AnimalProductValue(s, sp));
            default:
                return s.Levels[Workers] == 0 ? "Sin ayuda"
                    : "+" + (s.Levels[Workers] * 5) + "% · " + AutoHarvestSeconds(s).ToString("0.#") + "s";
        }
    }

    public static string CropName(CropKind kind)
    {
        switch (kind)
        {
            case CropKind.Tomato: return "Tomate";
            case CropKind.Pumpkin: return "Calabaza";
            case CropKind.Cabbage: return "Repollo";
            default: return "Zanahoria";
        }
    }

    static long Round(double v) => (long)Math.Ceiling(v);
}
