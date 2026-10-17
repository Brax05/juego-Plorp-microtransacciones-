using System;
using System.Collections.Generic;
using UnityEngine;

public enum LeafState { Healthy, Strong, Dusty, Withered, Burnt, Empty }
public enum GardenBonusKind { Global, Crops, Animals, Workers }
public enum GardenWeather { Normal, Sunny, Cloudy }

/// <summary>A plant species for the gardening tab: what it likes, what it costs and its bloom bonus.</summary>
public sealed class GardenSpecies
{
    public readonly string Id, Name, LeafShape, BonusText;
    public readonly float WaterMin, WaterMax, LightMin, LightMax;
    public readonly GardenBonusKind Bonus;
    public readonly float BonusAmount;
    public readonly int CostFlowerCoins;

    public GardenSpecies(string id, string name, string leafShape, float waterMin, float waterMax,
        float lightMin, float lightMax, GardenBonusKind bonus, float amount, string bonusText, int cost)
    {
        Id = id; Name = name; LeafShape = leafShape; WaterMin = waterMin; WaterMax = waterMax;
        LightMin = lightMin; LightMax = lightMax; Bonus = bonus; BonusAmount = amount;
        BonusText = bonusText; CostFlowerCoins = cost;
    }
}

/// <summary>A bug crawling on a leaf; it hurts the plant until the player squashes it.</summary>
public sealed class GardenBug
{
    public int Leaf;
    public float Age;
    public float Along; // 0..1 position along the leaf blade
}

/// <summary>
/// The plant on the gardening tab.
///  - Water and light change over time and with the weather (sunny spells through the window,
///    cloudy/humid spells where it doesn't need watering). Outside the comfort zone leaves wither or burn.
///  - Care (0-100%) grows slowly while the plant is comfortable (≈30 min); dust, bugs and dry leaves slow it.
///  - At 100% the plant blooms: the flower makes "monedas de flor" (abono and new plants are bought with them)
///    and the species' bonus for the farm unlocks forever.
///  - Bugs appear now and then; squashing them gives gems.
/// Raises <see cref="Alert"/> with a Pepito line whenever something needs the player's attention.
/// </summary>
public sealed class GardenState
{
    public static readonly GardenSpecies[] Species =
    {
        new GardenSpecies("especial", "Planta especial", "especial", 35, 70, 35, 70,
            GardenBonusKind.Global, .10f, "Al florecer: +10% a todo", 0),
        new GardenSpecies("girasol", "Girasol", "girasol", 30, 60, 60, 95,
            GardenBonusKind.Crops, .15f, "Al florecer: +15% cultivos", 8),
        new GardenSpecies("cactus", "Cactus", "cactus", 5, 35, 55, 95,
            GardenBonusKind.Animals, .15f, "Al florecer: +15% animales", 16),
        new GardenSpecies("helecho", "Helecho", "helecho", 60, 95, 10, 45,
            GardenBonusKind.Workers, .15f, "Al florecer: trabajadores 15% más rápidos", 25),
        new GardenSpecies("rosal", "Rosal", "girasol", 40, 65, 45, 75,
            GardenBonusKind.Global, .15f, "Al florecer: +15% a todo", 40),
        new GardenSpecies("orquidea", "Orquídea", "especial", 50, 70, 20, 45,
            GardenBonusKind.Global, .25f, "Al florecer: +25% a todo", 70),
    };

    public const int LeafSlots = 6;
    public const int FertilizerCost = 2;          // monedas de flor
    public const int MaxStoredCoins = 10;
    const float CoinSeconds = 90f;
    const string Key = "GlorpFarm.garden.";

    readonly FarmGameState farm;
    public int Current { get; private set; }
    public float Water { get; private set; } = 55f;
    public float Light { get; private set; } = 55f;
    public readonly float[] Care = new float[Species.Length];
    public readonly bool[] Owned = new bool[Species.Length];
    public readonly bool[] EverBloomed = new bool[Species.Length];
    public bool Bloomed { get; private set; }
    public long FlowerCoins { get; private set; } = 3;
    public int StoredCoins { get; private set; }
    public readonly LeafState[] Leaves = new LeafState[LeafSlots];
    readonly float[] regrow = new float[LeafSlots];
    public readonly List<GardenBug> Bugs = new List<GardenBug>();
    public GardenWeather Weather { get; private set; }
    double weatherUntil;
    public double FertilizerUntil { get; private set; }
    /// <summary>Curtains closed: the sun can't get in (light slowly drops).</summary>
    public bool CurtainsClosed { get; private set; }

    float stress, dustTimer, coinTimer, saveTimer, bugTimer = 45f;
    readonly bool[] lastProblems = new bool[5];
    readonly System.Random rng = new System.Random();

    public event Action Changed;
    /// <summary>Something needs attention (Pepito says it).</summary>
    public event Action<string> Alert;
    public event Action BugSpawned;

    public GardenSpecies Plant => Species[Current];
    public float CurrentCare => Care[Current];
    public bool Fertilized => Now < FertilizerUntil;
    static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    /// <summary>0 brote, 1 joven, 2 adulta, 3 en flor.</summary>
    /// <summary>Plant level 1-5: every 25% of care is a level; at level 5 (100%) it flowers.</summary>
    public const int MaxLevel = 5;
    public int Level => Bloomed ? MaxLevel : LevelFor(CurrentCare);
    static int LevelFor(float care) => Mathf.Min(MaxLevel, 1 + Mathf.FloorToInt(care / 25f));
    public static readonly string[] LevelNames = { "", "Brote", "Pequeña", "Joven", "Adulta", "¡En flor!" };
    public int VisibleLeaves => Mathf.Min(LeafSlots, Level + 1);
    int lastLevel;

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
    public bool Comfortable => Condition == 0;

    int Condition => TooMuchWater ? 1 : TooLittleWater ? 2 : TooMuchLight ? 3 : TooLittleLight ? 4 : 0;

    public string StatusText
    {
        get
        {
            // Water and light problems can happen together: show both.
            var problems = new List<string>();
            if (TooMuchWater) problems.Add("¡Demasiada agua!");
            if (TooLittleWater) problems.Add("Se seca: riégala.");
            if (TooMuchLight) problems.Add(CurtainsClosed ? "¡Demasiado sol! (cortinas cerradas)" : "¡Demasiada luz! Cierra las cortinas.");
            if (TooLittleLight) problems.Add(CurtainsClosed ? "Muy oscuro: abre las cortinas." : "Le falta luz: usa la lámpara.");
            if (problems.Count > 0) return string.Join("  ", problems.ToArray());
            if (Bugs.Count > 0) return "¡Bichitos! Aplástalos (dan gemas).";
            if (CountLeaves(LeafState.Withered) + CountLeaves(LeafState.Burnt) > 0) return "Hojas secas: córtalas con las tijeras.";
            if (CountLeaves(LeafState.Dusty) > 0) return "Hojas con polvo: tócalas para limpiarlas.";
            if (Bloomed) return StoredCoins > 0 ? "Toca la flor para recoger monedas de flor." : "En flor: produciendo monedas de flor.";
            return (Fertilized ? "Creciendo rápido con abono." : "Está feliz y creciendo.") +
                   " Nivel " + Level + "/" + MaxLevel + ": a nivel " + MaxLevel + " da la flor.";
        }
    }

    string ConditionAlert(int condition)
    {
        string name = Plant.Name.ToLowerInvariant();
        switch (condition)
        {
            case 1: return "Miau... ¡la " + name + " de Plorp tiene demasiada agua! Espera a que se seque.";
            case 2: return "Miau... ¡la " + name + " de Plorp se está secando! Plorp dice \"glerp\". Riégala.";
            case 3: return "Miau... ¡hace demasiada luz! La " + name + " de Plorp se quema. Cierra las cortinas.";
            default: return "Miau... la " + name + " de Plorp está muy a oscuras." + (CurtainsClosed ? " Abre las cortinas o usa la lámpara." : " Dale luz con la lámpara.");
        }
    }

    public string WeatherText
    {
        get
        {
            switch (Weather)
            {
                case GardenWeather.Sunny: return "Día soleado: entra mucha luz por la ventana";
                case GardenWeather.Cloudy: return "Nublado y húmedo: no hace falta regar";
                default: return "Clima tranquilo";
            }
        }
    }

    public int CountLeaves(LeafState state)
    {
        int n = 0;
        for (int i = 0; i < VisibleLeaves; i++) if (Leaves[i] == state) n++;
        return n;
    }

    // ---------- Bonuses for the farm (unlocked forever on the first bloom) ----------

    float BonusOf(GardenBonusKind kind)
    {
        float total = 0f;
        for (int i = 0; i < Species.Length; i++)
            if (EverBloomed[i] && Species[i].Bonus == kind) total += Species[i].BonusAmount;
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
            return parts.Count == 0 ? Plant.BonusText : "Bono jardín: " + string.Join(" · ", parts.ToArray());
        }
    }

    static string Pct(float multiplier) => Mathf.RoundToInt((multiplier - 1f) * 100f) + "%";

    // ---------- Simulation ----------

    public void Tick(float dt) => Tick(dt, true);

    void Tick(float dt, bool live)
    {
        if (dt <= 0f) return;
        UpdateWeather(live);

        switch (Weather)
        {
            case GardenWeather.Sunny:
                Water -= dt * .12f; Light += dt * .15f; break;
            case GardenWeather.Cloudy:
                Water += dt * .03f; Light -= dt * .12f; break;
            default:
                Water -= dt * .08f; Light -= dt * .06f; break;
        }
        // Closed curtains block the window: no sun gets in, light fades.
        if (CurtainsClosed) Light -= dt * (Weather == GardenWeather.Sunny ? .25f : .1f);
        Water = Mathf.Clamp(Water, 0f, 100f);
        Light = Mathf.Clamp(Light, 0f, 100f);

        for (int i = 0; i < LeafSlots; i++)
        {
            if (Leaves[i] != LeafState.Empty) continue;
            regrow[i] -= dt;
            if (regrow[i] <= 0f) Leaves[i] = LeafState.Healthy;
        }

        // Care
        if (Comfortable)
        {
            stress = Mathf.Max(0f, stress - dt);
            int dry = CountLeaves(LeafState.Withered) + CountLeaves(LeafState.Burnt) + CountLeaves(LeafState.Empty);
            float rate = .055f
                         * (Fertilized ? 2f : 1f)
                         * (1f + .15f * CountLeaves(LeafState.Strong) - .08f * CountLeaves(LeafState.Dusty))
                         * Mathf.Max(.2f, 1f - dry / (float)VisibleLeaves)
                         * Mathf.Max(.1f, 1f - .3f * Bugs.Count)
                         * (farm.Boosts != null ? farm.Boosts.GardenMultiplier : 1f);
            Care[Current] = Mathf.Min(100f, Care[Current] + dt * rate);
        }
        else
        {
            Care[Current] = Mathf.Max(0f, Care[Current] - dt * .03f);
            stress += dt;
            if (stress >= 25f)
            {
                stress = 0f;
                WitherOne(TooMuchLight ? LeafState.Burnt : LeafState.Withered);
            }
        }

        // Alerts: each problem is announced when it starts (several can be active at once).
        bool[] problems = { false, TooMuchWater, TooLittleWater, TooMuchLight, TooLittleLight };
        for (int c = 1; c < problems.Length; c++)
        {
            if (live && problems[c] && !lastProblems[c]) Alert?.Invoke(ConditionAlert(c));
            lastProblems[c] = problems[c];
        }

        // Dust
        dustTimer += dt;
        if (dustTimer >= 40f)
        {
            dustTimer = 0f;
            int pick = RandomLeaf(s => s == LeafState.Healthy || s == LeafState.Strong);
            if (pick >= 0) Leaves[pick] = LeafState.Dusty;
        }

        // Bugs: appear now and then, and a bug left alone too long dries its leaf
        bugTimer -= dt;
        if (bugTimer <= 0f)
        {
            bugTimer = 50f + (float)rng.NextDouble() * 50f;
            int leaf = RandomLeaf(s => s != LeafState.Empty);
            if (leaf >= 0 && Bugs.Count < 3)
            {
                Bugs.Add(new GardenBug { Leaf = leaf, Along = .45f + (float)rng.NextDouble() * .4f });
                if (live)
                {
                    BugSpawned?.Invoke();
                    Alert?.Invoke("Miau... ¡hay bichitos en la planta de Plorp! Aplástalos, Plorp dice que dan gemas.");
                }
            }
        }
        for (int i = Bugs.Count - 1; i >= 0; i--)
        {
            var bug = Bugs[i];
            bug.Age += dt;
            if (bug.Leaf >= VisibleLeaves || Leaves[bug.Leaf] == LeafState.Empty) { Bugs.RemoveAt(i); continue; }
            if (bug.Age >= 60f && Leaves[bug.Leaf] != LeafState.Withered)
            {
                Leaves[bug.Leaf] = LeafState.Withered;
                bug.Age = 30f;
            }
        }

        // Level ups (1 gem each, the flower comes at level 5)
        int level = LevelFor(Care[Current]);
        if (level > lastLevel && lastLevel > 0 && level < MaxLevel)
        {
            farm.AddGems(1);
            if (live) Alert?.Invoke("¡Miau! La " + Plant.Name.ToLowerInvariant() + " de Plorp subió a nivel " + level +
                                    ". En el nivel " + MaxLevel + " dará su flor. +1 gema");
        }
        lastLevel = level;

        // Bloom (level 5)
        if (!Bloomed && Care[Current] >= 100f)
        {
            Bloomed = true;
            bool first = !EverBloomed[Current];
            EverBloomed[Current] = true;
            if (live) Alert?.Invoke(first
                ? "¡Miau! ¡La " + Plant.Name.ToLowerInvariant() + " floreció! " + Plant.BonusText.Replace("Al florecer: ", "Bono para siempre: ")
                : "¡Miau! Volvió a florecer la " + Plant.Name.ToLowerInvariant() + ".");
        }
        else if (Bloomed && Care[Current] < 60f)
        {
            Bloomed = false;
            if (live) Alert?.Invoke("Miau... la flor se marchitó. Cuida la planta hasta el 100% para que vuelva.");
        }
        if (Bloomed && StoredCoins < MaxStoredCoins)
        {
            coinTimer += dt;
            while (coinTimer >= CoinSeconds && StoredCoins < MaxStoredCoins)
            {
                coinTimer -= CoinSeconds;
                StoredCoins++;
                if (live && StoredCoins == MaxStoredCoins)
                    Alert?.Invoke("Miau. La flor está llena de monedas de flor. Plorp quiere que las recojas.");
            }
        }

        saveTimer += dt;
        if (live && saveTimer >= 10f) { saveTimer = 0f; Save(); }
        if (live) Changed?.Invoke();
    }

    void UpdateWeather(bool live)
    {
        double now = Now;
        if (now < weatherUntil) return;
        double roll = rng.NextDouble();
        var next = roll < .5 ? GardenWeather.Normal : roll < .75 ? GardenWeather.Sunny : GardenWeather.Cloudy;
        // Spells last 1.5-3 min; calm periods 2-4 min.
        weatherUntil = now + (next == GardenWeather.Normal ? 120 + rng.NextDouble() * 120 : 90 + rng.NextDouble() * 90);
        if (next == Weather) return;
        Weather = next;
        if (!live) return;
        if (next == GardenWeather.Sunny)
            Alert?.Invoke(CurtainsClosed
                ? "Miau... afuera hay mucho sol, menos mal que las cortinas están cerradas."
                : "Miau... ¡entra mucho sol por la ventana! Si hace demasiada luz, cierra las cortinas.");
        else if (next == GardenWeather.Cloudy)
            Alert?.Invoke("Miau. Se nubló y hay humedad: la planta no necesita agua por ahora.");
    }

    void WitherOne(LeafState how)
    {
        int pick = RandomLeaf(s => s == LeafState.Healthy || s == LeafState.Strong || s == LeafState.Dusty);
        if (pick >= 0) Leaves[pick] = how;
    }

    int RandomLeaf(Func<LeafState, bool> match)
    {
        var options = new List<int>();
        for (int i = 0; i < VisibleLeaves; i++) if (match(Leaves[i])) options.Add(i);
        return options.Count == 0 ? -1 : options[rng.Next(options.Count)];
    }

    // ---------- Player actions ----------

    public void ToggleCurtains()
    {
        CurtainsClosed = !CurtainsClosed;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Watering can held over the plant (per frame).</summary>
    public void PourWater(float dt) { Water = Mathf.Min(100f, Water + 14f * dt); Changed?.Invoke(); }

    /// <summary>Lamp held over the plant (per frame).</summary>
    public void PourLight(float dt) { Light = Mathf.Min(100f, Light + 14f * dt); Changed?.Invoke(); }

    /// <summary>Fertilizer poured on the plant: costs monedas de flor, doubles care growth for 3 minutes.</summary>
    public string ApplyFertilizer()
    {
        if (FlowerCoins < FertilizerCost) return "El abono cuesta " + FertilizerCost + " monedas de flor";
        FlowerCoins -= FertilizerCost;
        FertilizerUntil = Math.Max(Now, FertilizerUntil) + 180;
        Save();
        Changed?.Invoke();
        return "Abonada: el cuidado sube x2 por 3 minutos";
    }

    public bool IsDry(int leaf) => leaf >= 0 && leaf < VisibleLeaves &&
                                   (Leaves[leaf] == LeafState.Withered || Leaves[leaf] == LeafState.Burnt);

    /// <summary>Scissors on a dry leaf: it's cut and grows back later.</summary>
    public bool CutLeaf(int leaf)
    {
        if (!IsDry(leaf)) return false;
        Leaves[leaf] = LeafState.Empty;
        regrow[leaf] = 30f;
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Tap on a dusty leaf: cleaned and strong.</summary>
    public bool CleanLeaf(int leaf)
    {
        if (leaf < 0 || leaf >= VisibleLeaves || Leaves[leaf] != LeafState.Dusty) return false;
        Leaves[leaf] = LeafState.Strong;
        Care[Current] = Mathf.Min(100f, Care[Current] + .5f);
        Save();
        Changed?.Invoke();
        return true;
    }

    public int BugsSquashed { get; private set; }

    public void AddFlowerCoins(int amount)
    {
        FlowerCoins += amount;
        Save();
        Changed?.Invoke();
    }

    /// <summary>Squash a bug: gives 1-2 gems.</summary>
    public int SquashBug(GardenBug bug)
    {
        if (!Bugs.Remove(bug)) return 0;
        BugsSquashed++;
        PlayerPrefs.SetInt(Key + "bugs", BugsSquashed);
        int gems = rng.NextDouble() < .3 ? 2 : 1;
        farm.AddGems(gems);
        Changed?.Invoke();
        return gems;
    }

    /// <summary>Tap on the flower: collects the monedas de flor it made.</summary>
    public int CollectFlowerCoins()
    {
        int n = StoredCoins;
        if (n <= 0) return 0;
        StoredCoins = 0;
        FlowerCoins += n;
        Save();
        Changed?.Invoke();
        return n;
    }

    /// <summary>A species can be bought once the previous one has bloomed at least once.</summary>
    public bool CanUnlock(int index) => index == 0 || EverBloomed[index - 1];

    /// <summary>Buys (first time, with monedas de flor) and/or puts a species in the pot. Each keeps its own care.</summary>
    public string PlantSpecies(int index)
    {
        if (index == Current) return Species[index].Name + " ya está en la maceta";
        var sp = Species[index];
        if (!Owned[index])
        {
            if (!CanUnlock(index)) return "Primero haz florecer: " + Species[index - 1].Name;
            if (FlowerCoins < sp.CostFlowerCoins) return "Faltan monedas de flor";
            FlowerCoins -= sp.CostFlowerCoins;
            Owned[index] = true;
        }
        Current = index;
        lastLevel = LevelFor(Care[index]);
        Bloomed = false;
        StoredCoins = 0;
        Water = Light = 55f;
        stress = 0f;
        Bugs.Clear();
        for (int i = 0; i < LeafSlots; i++) Leaves[i] = LeafState.Healthy;
        Save();
        Changed?.Invoke();
        return sp.Name + " plantada";
    }

    // ---------- Save ----------

    void Load()
    {
        Current = Mathf.Clamp(PlayerPrefs.GetInt(Key + "current", 0), 0, Species.Length - 1);
        Water = PlayerPrefs.GetFloat(Key + "water", 55f);
        Light = PlayerPrefs.GetFloat(Key + "light", 55f);
        for (int i = 0; i < Species.Length; i++)
        {
            Care[i] = PlayerPrefs.GetFloat(Key + "care." + i, 0f);
            Owned[i] = i == 0 || PlayerPrefs.GetInt(Key + "owned." + i, 0) == 1;
            EverBloomed[i] = PlayerPrefs.GetInt(Key + "bloomed." + i, 0) == 1;
        }
        Bloomed = PlayerPrefs.GetInt(Key + "bloomNow", 0) == 1;
        FlowerCoins = long.TryParse(PlayerPrefs.GetString(Key + "flowerCoins", "3"), out long fc) ? fc : 3;
        StoredCoins = PlayerPrefs.GetInt(Key + "stored", 0);
        string leaves = PlayerPrefs.GetString(Key + "leaves", "");
        for (int i = 0; i < LeafSlots; i++)
            Leaves[i] = i < leaves.Length ? (LeafState)Mathf.Clamp(leaves[i] - '0', 0, 5) : LeafState.Healthy;
        FertilizerUntil = ReadDouble("fert");
        CurtainsClosed = PlayerPrefs.GetInt(Key + "curtains", 0) == 1;
        BugsSquashed = PlayerPrefs.GetInt(Key + "bugs", 0);
        lastLevel = LevelFor(Care[Current]);
        lastProblems[1] = TooMuchWater; lastProblems[2] = TooLittleWater;
        lastProblems[3] = TooMuchLight; lastProblems[4] = TooLittleLight;

        // Catch up on the time the game was closed (capped at 2 hours), quietly.
        double saved = ReadDouble("saved");
        if (saved > 0)
        {
            float away = (float)Math.Min(7200, Math.Max(0, Now - saved));
            while (away > 0f) { float step = Mathf.Min(5f, away); Tick(step, false); away -= step; }
        }
    }

    static double ReadDouble(string key) =>
        double.TryParse(PlayerPrefs.GetString(Key + key, "0"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;

    static void WriteDouble(string key, double v) =>
        PlayerPrefs.SetString(Key + key, v.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public void Save()
    {
        PlayerPrefs.SetInt(Key + "current", Current);
        PlayerPrefs.SetFloat(Key + "water", Water);
        PlayerPrefs.SetFloat(Key + "light", Light);
        for (int i = 0; i < Species.Length; i++)
        {
            PlayerPrefs.SetFloat(Key + "care." + i, Care[i]);
            PlayerPrefs.SetInt(Key + "owned." + i, Owned[i] ? 1 : 0);
            PlayerPrefs.SetInt(Key + "bloomed." + i, EverBloomed[i] ? 1 : 0);
        }
        PlayerPrefs.SetInt(Key + "bloomNow", Bloomed ? 1 : 0);
        PlayerPrefs.SetInt(Key + "curtains", CurtainsClosed ? 1 : 0);
        PlayerPrefs.SetString(Key + "flowerCoins", FlowerCoins.ToString());
        PlayerPrefs.SetInt(Key + "stored", StoredCoins);
        var chars = new char[LeafSlots];
        for (int i = 0; i < LeafSlots; i++) chars[i] = (char)('0' + (int)Leaves[i]);
        PlayerPrefs.SetString(Key + "leaves", new string(chars));
        WriteDouble("fert", FertilizerUntil);
        WriteDouble("saved", Now);
    }

    public static void ResetSaved()
    {
        foreach (string k in new[] { "current", "water", "light", "leaves", "fert", "saved", "bloomNow", "flowerCoins", "stored", "curtains", "bugs" })
            PlayerPrefs.DeleteKey(Key + k);
        for (int i = 0; i < Species.Length; i++)
        {
            PlayerPrefs.DeleteKey(Key + "care." + i);
            PlayerPrefs.DeleteKey(Key + "owned." + i);
            PlayerPrefs.DeleteKey(Key + "bloomed." + i);
        }
    }
}
