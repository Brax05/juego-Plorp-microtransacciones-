using System;

/// <summary>One thing Pepito can say. Text can depend on the state (e.g. how much food is missing).</summary>
public sealed class AdviceDef
{
    public readonly string Id;
    public readonly int Priority;
    public readonly Func<FarmGameState, bool> When;
    public readonly Func<FarmGameState, string> Text;

    public AdviceDef(string id, int priority, Func<FarmGameState, bool> when, Func<FarmGameState, string> text)
    {
        Id = id; Priority = priority; When = when; Text = text;
    }
}

/// <summary>
/// Pepito's advice. He shows the highest-priority line that applies, avoiding repeating the last one.
/// Adding advice = adding one entry.
/// </summary>
public static class PepitoAdvice
{
    public const string Intro =
        "Miau. Soy Pepito. Plorp me subió a su nave para que le traduzca. Dice que tiene hambre. Mucha.";

    // Everything Pepito says is his (very confident) translation of Plorp.
    static readonly string[] Flavor =
    {
        "Miau... Plorp dijo \"glerp\". Creo que significa tomates. O guerra.",
        "Miau. Plorp dijo \"zep zep zep\". No sé qué es, pero las vacas se ven nerviosas.",
        "Miau... Plorp me mira cuando ronroneo. Creo que quiere aprender.",
        "Miau. Plorp no come animales. Solo lo que dan: huevos, queso, leche. Es muy educado.",
        "Miau... Plorp dijo \"plorp\" muy bajito. Eso significa que está pensando en comida.",
        "Miau. Plorp dice que los cultivos crecen aunque cierres el juego. Él nunca duerme.",
    };

    static readonly Random Rng = new Random();

    public static readonly AdviceDef[] All =
    {
        new AdviceDef("deliver", 100, s => s.GoalReady,
            s => "¡Miau! Plorp dice que ya juntaste su pedido. Toca ENTREGAR arriba antes de que haga \"zep zep\"."),
        new AdviceDef("first-harvest", 95, s => s.Harvests == 0,
            s => "Miau... cuando algo esté listo verás un ticket dorado encima. Tócalo. Plorp dice \"glerp\"."),
        new AdviceDef("claim", 90, AnyAchievementReady,
            s => "Miau. Plorp dejó gemas en LOGROS. Dice que las cobres. Bueno, señaló con la antena."),
        new AdviceDef("missing", 80, s => MissingLine(s) != null,
            s => HowToGet(MissingLine(s).Value, s)),
        new AdviceDef("garden", 75, s => s.Garden != null && !s.Garden.Comfortable,
            s => "Miau... Plorp señala la ventana: la planta de JARDINERÍA no está bien. " + s.Garden.StatusText),
        new AdviceDef("garden-dust", 45, s => s.Garden != null && s.Garden.CountLeaves(LeafState.Dusty) > 0,
            s => "Miau. En JARDINERÍA hay hojas con polvo. Tócalas y se ponen fuertes. Como yo después de comer."),
        new AdviceDef("new-food", 70, s => s.AnimalSpecies < 3 && s.Gold >= s.Prices[1],
            s => "Miau... Plorp nunca ha probado " + NextAnimalFood(s) + ". Sigue comprando ANIMALES: cada 10 llega uno nuevo. Le brillan las antenas."),
        new AdviceDef("upgrade", 60, CanAffordUpgrade,
            s => "Miau. Tienes monedas para una mejora. Cada 10 SEMILLAS sale un cultivo mejor. Plorp ya está babeando."),
        new AdviceDef("workers", 55, s => s.Levels[2] == 0 && s.Harvests >= 8,
            s => "Miau... Plorp dice que tocas muy lento. Contrata TRABAJADORES: cosechan solos y cada uno suma +5%."),
        new AdviceDef("satisfied", 40, s => s.PlorpSatisfied,
            s => "Miau... Plorp está conforme. Igual sigue pidiendo antojos. Es su naturaleza."),
        new AdviceDef("flavor", 10, s => true,
            s => Flavor[Rng.Next(Flavor.Length)]),
    };

    /// <summary>First order line the barn doesn't cover yet.</summary>
    static OrderLine? MissingLine(FarmGameState s)
    {
        foreach (var line in s.CurrentGoal.Lines)
            if (s.ItemCount(line.Item.Id) < line.Amount) return line;
        return null;
    }

    static string HowToGet(OrderLine line, FarmGameState s)
    {
        long missing = line.Amount - s.ItemCount(line.Item.Id);
        string what = missing + " " + line.Item.Name.ToLowerInvariant();
        if (line.Item == FarmItems.Egg) return "Miau. A Plorp le faltan " + what + ". Toca a las gallinas cuando brillen.";
        if (line.Item == FarmItems.Cheese) return "Miau... Plorp quiere " + what + ". Las ovejas dan queso. No me preguntes cómo.";
        if (line.Item == FarmItems.Milk) return "Miau. Faltan " + what + " para Plorp. Toca a las vacas.";
        return "Miau... a Plorp le faltan " + what + ". Toca los tickets dorados sobre los campos. Él no sabe esperar.";
    }

    static string NextAnimalFood(FarmGameState s)
    {
        switch (s.AnimalSpecies)
        {
            case 0: return "un huevo";
            case 1: return "el queso";
            default: return "la leche";
        }
    }

    static bool AnyAchievementReady(FarmGameState s) => AchievementCatalog.AnyReady(s);

    static bool CanAffordUpgrade(FarmGameState s)
    {
        for (int i = 0; i < s.Prices.Length; i++)
            if (!s.IsMaxed(i) && s.Gold >= s.Prices[i]) return true;
        return false;
    }

    public static AdviceDef Pick(FarmGameState state, string lastId)
    {
        // Now and then Pepito just shares one of Plorp's random thoughts.
        if (lastId != "flavor" && !state.GoalReady && Rng.NextDouble() < .25)
            return All[All.Length - 1];

        AdviceDef best = null, fallback = null;
        foreach (var advice in All)
        {
            if (!advice.When(state)) continue;
            if (fallback == null || advice.Priority > fallback.Priority) fallback = advice;
            if (advice.Id == lastId && advice.Id != "flavor") continue;
            if (best == null || advice.Priority > best.Priority) best = advice;
        }
        return best ?? fallback;
    }
}
