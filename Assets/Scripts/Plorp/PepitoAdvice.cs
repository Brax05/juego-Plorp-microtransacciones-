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
        "Hola. Soy Pepito, gato callejero. Plorp me subió a su nave. Quiere comida. Mucha.";

    static readonly string[] Flavor =
    {
        "Plorp dijo \"glerp\". Creo que significa zanahorias. O guerra.",
        "Antes vivía detrás de una pescadería. Extraño ese olor.",
        "Zep zep zep. No sé qué significa, pero las vacas se ven nerviosas.",
        "Plorp me mira cuando ronroneo. Creo que quiere aprender.",
        "No soy el trabajador. Yo solo doy consejos. Y duermo.",
        "Los cultivos crecen aunque cierres el juego. Plorp no duerme nunca.",
    };

    static readonly Random Rng = new Random();

    public static readonly AdviceDef[] All =
    {
        new AdviceDef("deliver", 100, s => s.GoalReady,
            s => "¡Ya juntaste lo que pide Plorp! Toca ENTREGAR arriba antes de que haga \"zep zep\"."),
        new AdviceDef("first-harvest", 95, s => s.Harvests == 0,
            s => "Toca COSECHAR abajo o toca los cultivos. Todo cuenta como alimento para Plorp."),
        new AdviceDef("claim", 90, AnyAchievementReady,
            s => "Hay logros por cobrar. Son gemas gratis. Ve a LOGROS."),
        new AdviceDef("animals", 80, s => s.AnimalCount < s.CurrentGoal.Animals,
            s => "Plorp quiere " + s.CurrentGoal.Animals + " animales y tienes " + s.AnimalCount +
                 ". Compra la mejora ANIMALES. No preguntes para qué los quiere."),
        new AdviceDef("upgrade", 60, CanAffordUpgrade,
            s => "Tienes monedas para una mejora. Más semillas, más comida. Más comida, Plorp feliz."),
        new AdviceDef("workers", 55, s => s.Levels[2] == 0 && s.Harvests >= 8,
            s => "¿Cansado de tocar todo? Contrata TRABAJADORES: cosechan solos. Yo no, yo soy gato."),
        new AdviceDef("food", 50, s => s.Food < s.CurrentGoal.Food,
            s => "Faltan " + FarmGameState.Format(s.CurrentGoal.Food - s.Food) +
                 " de alimento para Plorp. Toca a las gallinas, también cuentan."),
        new AdviceDef("satisfied", 40, s => s.PlorpSatisfied,
            s => "Plorp está conforme. Igual sigue pidiendo antojos. Es su naturaleza."),
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
