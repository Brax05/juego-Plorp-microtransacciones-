using System;
using System.Collections.Generic;
using UnityEngine;

public enum LeafState { Healthy, Strong, Dusty, Withered, Burnt, Empty }
public enum GardenBonusKind { Global, Crops, Animals, Workers }

/// <summary>A plant species for the gardening tab: what it likes and what it gives the farm.</summary>
public sealed class GardenSpecies
{
    public readonly string Id, Name, Shape, BonusText;
    public readonly float WaterMin, WaterMax, LightMin, LightMax;
    public readonly GardenBonusKind Bonus;
    public readonly float BonusPerLevel;
    public readonly long CostGold;
    public readonly int CostGems;

    public GardenSpecies(string id, string name, string shape, float waterMin, float waterMax,
        float lightMin, float lightMax, GardenBonusKind bonus, float perLevel, string bonusText,
        long costGold, int costGems)
    {
        Id = id; Name = name; Shape = shape; WaterMin = waterMin; WaterMax = waterMax;
        LightMin = lightMin; LightMax = lightMax; Bonus = bonus; BonusPerLevel = perLevel;
        BonusText = bonusText; CostGold = costGold; CostGems = costGems;
    }
}

/// <summary>
/// The plant on the gardening tab. Water and light drain over time (also while the game is closed);
/// inside the plant's comfort range it grows, outside it leaves wither (too much sun burns them).
/// Dust settles on leaves now and then: tapping a dusty leaf cleans it and makes it strong (faster growth).
/// Each plant level gives a permanent bonus to the farm, different for every species.
/// </summary>
public sealed class GardenState
{
    public static readonly GardenSpecies[] Species =
    {
        new GardenSpecies("especial", "Planta especial", "especial", 35, 70, 35, 70,
            GardenBonusKind.Global, .02f, "+2% a todo por nivel", 0, 0),
        new GardenSpecies("girasol", "Girasol", "girasol", 30, 60, 60, 95,
            GardenBonusKind.Crops, .04f, "+4% cultivos por nivel", 5000, 0),
        new GardenSpecies("cactus", "Cactus", "cactus", 5, 35, 55, 95,
            GardenBonusKind.Animals, .04f, "+4% animales por nivel", 40000, 0),
        new GardenSpecies("helecho", "Helecho", "helecho", 60, 95, 10, 45,
            GardenBonusKind.Workers, .03f, "+3% rapidez trabajadores por nivel", 0, 30),
    };

    public const int LeafSlots = 6;
    public static readonly string[] ToolNames = { "Regadera", "Tijeras", "Abono", "Lámpara" };
    static readonly long[] ToolBaseCost = { 20, 40, 60, 40 };
    const string Key = "GlorpFarm.garden.";

    readonly FarmGameState farm;
    public int Current { get; private set; }
    public float Water { get; private set; } = 55f;
    public float Light { get; private set; } = 55f;
    public readonly int[] Levels = new int[Species.Length];
    public readonly float[] Xp = new float[Species.Length];
    public readonly bool[] Owned = new bool[Species.Length];
    public readonly LeafState[] Leaves = new LeafState[LeafSlots];
    readonly float[] regrow = new float[LeafSlots];
    public readonly int[] ToolUses = new int[4];
    public double FertilizerUntil { get; private set; }

    float stress, dustTimer, saveTimer;
    readonly System.Random rng = new System.Random();

    public event Action Changed;
    /// <summary>Raised with the new level when the plant levels up.</summary>
    public event Action<int> LeveledUp;
    /// <summary>Raised with a short explanation when a leaf withers.</summary>
    public event Action<string> LeafLost;

    public GardenSpecies Plant => Species[Current];
    public int Level => Levels[Current];
    public float XpNeeded => 60f * Mathf.Pow(1.4f, Level - 1);
    public float XpProgress => Mathf.Clamp01(Xp[Current] / XpNeeded);
    public int VisibleLeaves => Mathf.Min(LeafSlots, 2 + Level);
    public bool Fertilized => Now < FertilizerUntil;

    static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    public GardenState(FarmGameState farm)
    {
        this.farm = farm;
        Load();
    }

    // ---------- Conditions ----------

    public bool TooMuchWater => Water > Plant.WaterMax;
    public bool TooLittleWater => Water < Plant.WaterMin;
    public bool TooMuchLight => Light > Plant.LightMax;
    public bool TooLittleLight => Light < Plant.LightMin;
    public bool Comfortable => !TooMuchWater && !TooLittleWater && !TooMuchLight && !TooLittleLight;

    public string StatusText
    {
        get
        {
            if (TooMuchWater) return "¡Demasiada agua! Las raíces se ahogan.";
            if (TooLittleWater) return "Tiene sed. Riégala.";
            if (TooMuchLight) return "¡Demasiado sol! Las hojas se queman.";
            if (TooLittleLight) return "Le falta luz. Enciende la lámpara.";
            if (CountLeaves(LeafState.Dusty) > 0) return "Hay hojas con polvo: tócalas para limpiarlas.";
            if (CountLeaves(LeafState.Withered) + CountLeaves(LeafState.Burnt) > 0) return "Hojas secas: usa las tijeras.";
            return Fertilized ? "Creciendo rápido con abono." : "Está feliz y creciendo.";
        }
    }

    public int CountLeaves(LeafState state)
    {
        int n = 0;
        for (int i = 0; i < VisibleLeaves; i++) if (Leaves[i] == state) n++;
        return n;
    }

    // ---------- Bonuses for the farm ----------

    float BonusOf(GardenBonusKind kind)
    {
        float total = 0f;
        for (int i = 0; i < Species.Length; i++)
            if (Owned[i] && Species[i].Bonus == kind) total += Species[i].BonusPerLevel * (Levels[i] - 1);
        return total;
    }

    public float GlobalMultiplier => 1f + BonusOf(GardenBonusKind.Global);
    public float CropMultiplier => 1f + BonusOf(GardenBonusKind.Crops);
    public float AnimalMultiplier => 1f + BonusOf(GardenBonusKind.Animals);
    public float WorkerSpeed => 1f + BonusOf(GardenBonusKind.Workers);

    public string BonusSummary
    {
        get
        {
            var parts = new List<string>();
            if (GlobalMultiplier > 1f) parts.Add("+" + Pct(GlobalMultiplier) + " todo");
            if (CropMultiplier > 1f) parts.Add("+" + Pct(CropMultiplier) + " cultivos");
            if (AnimalMultiplier > 1f) parts.Add("+" + Pct(AnimalMultiplier) + " animales");
            if (WorkerSpeed > 1f) parts.Add("+" + Pct(WorkerSpeed) + " trabajadores");
            return parts.Count == 0 ? "Sube de nivel tus plantas para ganar bonos" : "Bono jardín: " + string.Join(" · ", parts.ToArray());
        }
    }

    static string Pct(float multiplier) => Mathf.RoundToInt((multiplier - 1f) * 100f) + "%";

    // ---------- Simulation ----------

    /// <summary>Advances the plant; called every frame by the bootstrap (and with a big dt after being offline).</summary>
    public void Tick(float dt)
    {
        if (dt <= 0f) return;
        Water = Mathf.Max(0f, Water - dt * .08f);
        Light = Mathf.Max(0f, Light - dt * .06f);

        for (int i = 0; i < LeafSlots; i++)
        {
            if (Leaves[i] != LeafState.Empty) continue;
            regrow[i] -= dt;
            if (regrow[i] <= 0f) Leaves[i] = LeafState.Healthy;
        }

        if (Comfortable)
        {
            stress = Mathf.Max(0f, stress - dt);
            int alive = VisibleLeaves - CountLeaves(LeafState.Withered) - CountLeaves(LeafState.Burnt) - CountLeaves(LeafState.Empty);
            float rate = (Fertilized ? 2f : 1f)
                         * (1f + .15f * CountLeaves(LeafState.Strong) - .1f * CountLeaves(LeafState.Dusty))
                         * Mathf.Max(.2f, alive / (float)VisibleLeaves);
            Xp[Current] += dt * rate;
            while (Xp[Current] >= XpNeeded)
            {
                Xp[Current] -= XpNeeded;
                Levels[Current]++;
                farm.AddGems(1 + Levels[Current] / 3);
                LeveledUp?.Invoke(Levels[Current]);
            }
        }
        else
        {
            stress += dt;
            if (stress >= 25f)
            {
                stress = 0f;
                WitherOne(TooMuchLight ? LeafState.Burnt : LeafState.Withered);
            }
        }

        dustTimer += dt;
        if (dustTimer >= 40f)
        {
            dustTimer = 0f;
            int pick = RandomLeaf(s => s == LeafState.Healthy || s == LeafState.Strong);
            if (pick >= 0) Leaves[pick] = LeafState.Dusty;
        }

        saveTimer += dt;
        if (saveTimer >= 10f) { saveTimer = 0f; Save(); }
        Changed?.Invoke();
    }

    void WitherOne(LeafState how)
    {
        int pick = RandomLeaf(s => s == LeafState.Healthy || s == LeafState.Strong || s == LeafState.Dusty);
        if (pick < 0) return;
        Leaves[pick] = how;
        LeafLost?.Invoke(how == LeafState.Burnt ? "Una hoja se quemó con tanto sol"
            : TooMuchWater ? "Una hoja se pudrió por exceso de agua"
            : TooLittleWater ? "Una hoja se secó por falta de agua" : "Una hoja se marchitó sin luz");
    }

    int RandomLeaf(Func<LeafState, bool> match)
    {
        var options = new List<int>();
        for (int i = 0; i < VisibleLeaves; i++) if (match(Leaves[i])) options.Add(i);
        return options.Count == 0 ? -1 : options[rng.Next(options.Count)];
    }

    // ---------- Player actions ----------

    public int ToolLevel(int tool) => 1 + ToolUses[tool] / 10;
    public long ToolCost(int tool) => (long)Math.Ceiling(ToolBaseCost[tool] * Math.Pow(1.25, Level - 1));

    /// <summary>Uses a tool. Returns a message for the player.</summary>
    public string UseTool(int tool)
    {
        if (tool == 1 && CountLeaves(LeafState.Withered) + CountLeaves(LeafState.Burnt) == 0)
            return "No hay hojas secas que cortar";
        long cost = ToolCost(tool);
        if (!farm.Spend(cost, 0)) return "Faltan monedas";
        int lvl = ToolLevel(tool);
        ToolUses[tool]++;
        string result;
        switch (tool)
        {
            case 0:
                Water = Mathf.Min(100f, Water + 18f + 4f * lvl);
                result = Water > Plant.WaterMax ? "¡Cuidado! Mucha agua" : "Regada";
                break;
            case 1:
                int cut = 0;
                for (int i = 0; i < LeafSlots; i++)
                {
                    if (Leaves[i] != LeafState.Withered && Leaves[i] != LeafState.Burnt) continue;
                    Leaves[i] = LeafState.Empty;
                    regrow[i] = 25f / (1f + .2f * (lvl - 1));
                    cut++;
                }
                result = cut + (cut == 1 ? " hoja cortada" : " hojas cortadas") + ": volverán a crecer";
                break;
            case 2:
                FertilizerUntil = Math.Max(Now, FertilizerUntil) + 60 + 20 * lvl;
                result = "Abonada: crece x2 un rato";
                break;
            default:
                Light = Mathf.Min(100f, Light + 18f + 4f * lvl);
                result = Light > Plant.LightMax ? "¡Cuidado! Mucho sol" : "Más luz";
                break;
        }
        if (ToolLevel(tool) > lvl) result += " · ¡" + ToolNames[tool] + " Nv." + ToolLevel(tool) + "!";
        Save();
        Changed?.Invoke();
        return result;
    }

    /// <summary>Tap on a leaf: dusty leaves get cleaned and become strong.</summary>
    public bool CleanLeaf(int index)
    {
        if (index < 0 || index >= VisibleLeaves || Leaves[index] != LeafState.Dusty) return false;
        Leaves[index] = LeafState.Strong;
        Xp[Current] += 2f;
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Buys (first time) and/or plants a species in the pot.</summary>
    public string PlantSpecies(int index)
    {
        if (index == Current) return Species[index].Name + " ya está en la maceta";
        var sp = Species[index];
        if (!Owned[index])
        {
            if (!farm.Spend(sp.CostGold, sp.CostGems))
                return sp.CostGems > 0 ? "Faltan gemas" : "Faltan monedas";
            Owned[index] = true;
        }
        Current = index;
        Water = Light = 55f;
        stress = 0f;
        for (int i = 0; i < LeafSlots; i++) Leaves[i] = LeafState.Healthy;
        Save();
        Changed?.Invoke();
        return sp.Name + " plantado";
    }

    // ---------- Save ----------

    void Load()
    {
        Current = Mathf.Clamp(PlayerPrefs.GetInt(Key + "current", 0), 0, Species.Length - 1);
        Water = PlayerPrefs.GetFloat(Key + "water", 55f);
        Light = PlayerPrefs.GetFloat(Key + "light", 55f);
        for (int i = 0; i < Species.Length; i++)
        {
            Levels[i] = Mathf.Max(1, PlayerPrefs.GetInt(Key + "level." + i, 1));
            Xp[i] = PlayerPrefs.GetFloat(Key + "xp." + i, 0f);
            Owned[i] = i == 0 || PlayerPrefs.GetInt(Key + "owned." + i, 0) == 1;
        }
        string leaves = PlayerPrefs.GetString(Key + "leaves", "");
        for (int i = 0; i < LeafSlots; i++)
            Leaves[i] = i < leaves.Length ? (LeafState)Mathf.Clamp(leaves[i] - '0', 0, 5) : LeafState.Healthy;
        for (int i = 0; i < 4; i++) ToolUses[i] = PlayerPrefs.GetInt(Key + "tool." + i, 0);
        double.TryParse(PlayerPrefs.GetString(Key + "fert", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fert);
        FertilizerUntil = fert;

        // Catch up on the time the game was closed (capped at 2 hours), in 5-second steps.
        double.TryParse(PlayerPrefs.GetString(Key + "saved", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double saved);
        if (saved > 0)
        {
            float away = (float)Math.Min(7200, Math.Max(0, Now - saved));
            var lost = LeafLost; LeafLost = null;
            var up = LeveledUp; LeveledUp = null;
            while (away > 0f) { float step = Mathf.Min(5f, away); Tick(step); away -= step; }
            LeafLost = lost; LeveledUp = up;
        }
    }

    public void Save()
    {
        PlayerPrefs.SetInt(Key + "current", Current);
        PlayerPrefs.SetFloat(Key + "water", Water);
        PlayerPrefs.SetFloat(Key + "light", Light);
        for (int i = 0; i < Species.Length; i++)
        {
            PlayerPrefs.SetInt(Key + "level." + i, Levels[i]);
            PlayerPrefs.SetFloat(Key + "xp." + i, Xp[i]);
            PlayerPrefs.SetInt(Key + "owned." + i, Owned[i] ? 1 : 0);
        }
        var chars = new char[LeafSlots];
        for (int i = 0; i < LeafSlots; i++) chars[i] = (char)('0' + (int)Leaves[i]);
        PlayerPrefs.SetString(Key + "leaves", new string(chars));
        for (int i = 0; i < 4; i++) PlayerPrefs.SetInt(Key + "tool." + i, ToolUses[i]);
        PlayerPrefs.SetString(Key + "fert", FertilizerUntil.ToString(System.Globalization.CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(Key + "saved", Now.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public static void ResetSaved()
    {
        foreach (string k in new[] { "current", "water", "light", "leaves", "fert", "saved" })
            PlayerPrefs.DeleteKey(Key + k);
        for (int i = 0; i < Species.Length; i++)
        {
            PlayerPrefs.DeleteKey(Key + "level." + i);
            PlayerPrefs.DeleteKey(Key + "xp." + i);
            PlayerPrefs.DeleteKey(Key + "owned." + i);
        }
        for (int i = 0; i < 4; i++) PlayerPrefs.DeleteKey(Key + "tool." + i);
    }
}
