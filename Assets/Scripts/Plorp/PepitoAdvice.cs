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
        "Miau. Soy Pepito, gato callejero. Plorp me subió a su nave para que le traduzca. Dice que tiene hambre. Mucha.";

    // Everything Pepito says is his (very confident) translation of Plorp.
    static readonly string[] Flavor =
    {
        "Miau... Plorp dijo \"glerp\". Creo que significa zanahorias. O guerra.",
        "Miau. Plorp dijo \"zep zep zep\". No sé qué es, pero las vacas se ven nerviosas.",
        "Miau... Plorp me mira cuando ronroneo. Creo que quiere aprender.",
        "Miau. Plorp dice que en su planeta las gallinas son más grandes. No le creo.",
        "Miau... Plorp dijo \"plorp\" muy bajito. Eso significa que está pensando en comida.",
        "Miau. Plorp dice que los cultivos crecen aunque cierres el juego. Él nunca duerme.",
    };

    static readonly Random Rng = new Random();

    public static readonly AdviceDef[] All =
    {
        new AdviceDef("deliver", 100, s => s.GoalReady,
            s => "¡Miau! Plorp dice que ya juntaste su pedido. Toca ENTREGAR arriba antes de que haga \"zep zep\"."),
        new AdviceDef("first-harvest", 95, s => s.Harvests == 0,
            s => "Miau... Plorp dice \"glerp\": toca COSECHAR abajo. Todo lo que cosechas es alimento para él."),
        new AdviceDef("claim", 90, AnyAchievementReady,
            s => "Miau. Plorp dejó gemas en LOGROS. Dice que las cobres. Bueno, señaló con la antena."),
        new AdviceDef("animals", 80, s => s.AnimalCount < s.CurrentGoal.Animals,
            s => "Miau... Plorp quiere " + s.CurrentGoal.Animals + " animales y tienes " + s.AnimalCount +
                 ". Compra la mejora ANIMALES. No le preguntes para qué los quiere."),
        new AdviceDef("upgrade", 60, CanAffordUpgrade,
            s => "Miau. Plorp dice que tienes monedas para una mejora. Más granja, más comida para Plorp."),
        new AdviceDef("workers", 55, s => s.Levels[2] == 0 && s.Harvests >= 8,
            s => "Miau... Plorp dice que tocas muy lento. Contrata TRABAJADORES: cosechan solos."),
        new AdviceDef("food", 50, s => s.Food < s.CurrentGoal.Food,
            s => "Miau. A Plorp le faltan " + FarmGameState.Format(s.CurrentGoal.Food - s.Food) +
                 " de alimento. Toca a las gallinas, también cuentan."),
        new AdviceDef("satisfied", 40, s => s.PlorpSatisfied,
            s => "Miau... Plorp está conforme. Igual sigue pidiendo antojos. Es su naturaleza."),
        new AdviceDef("flavor", 10, s => true,
            s => Flavor[Rng.Next(Flavor.Length)]),
    };

    static bool AnyAchievementReady(FarmGameState s)
    {
        for (int i = 0; i < FarmGameState.AchievementNames.Length; i++)
            if (s.AchievementReady(i)) return true;
        return false;
    }

    static bool CanAffordUpgrade(FarmGameState s)
    {
        for (int i = 0; i < s.Prices.Length; i++)
            if (!s.IsMaxed(i) && s.Gold >= s.Prices[i]) return true;
        return false;
    }

    public static AdviceDef Pick(FarmGameState state, string lastId)
    {
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
