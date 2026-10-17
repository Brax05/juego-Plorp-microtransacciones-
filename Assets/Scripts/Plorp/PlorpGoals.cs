using System;
using System.Collections.Generic;
using System.Text;

/// <summary>One line of an order: an item and how many Plorp wants.</summary>
public readonly struct OrderLine
{
    public readonly FarmItem Item;
    public readonly int Amount;
    public OrderLine(FarmItem item, int amount) { Item = item; Amount = amount; }
}

/// <summary>An order from Plorp: a few foods the player can already produce, plus the reward.</summary>
public sealed class PlorpGoal
{
    public readonly int Stage;
    public readonly string Title;
    public readonly string Quote;
    public readonly List<OrderLine> Lines;
    public readonly long RewardGold;
    public readonly int RewardGems;

    public PlorpGoal(int stage, List<OrderLine> lines)
    {
        Stage = stage;
        Lines = lines;
        Title = PlorpGoals.TitleFor(stage);
        Quote = PlorpGoals.QuoteFor(stage);
        int total = 0;
        foreach (var line in lines) total += line.Amount * (line.Item.FromAnimal ? 5 : 1);
        RewardGold = 40L * total + 100L * stage;
        RewardGems = 3 + stage * 2;
    }

    public int TotalAmount
    {
        get { int t = 0; foreach (var l in Lines) t += l.Amount; return t; }
    }

    /// <summary>"carrot:10,egg:2" — how the order is saved.</summary>
    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var line in Lines)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(line.Item.Id).Append(':').Append(line.Amount);
        }
        return sb.ToString();
    }

    public static PlorpGoal Parse(int stage, string saved)
    {
        if (string.IsNullOrEmpty(saved)) return null;
        var lines = new List<OrderLine>();
        foreach (var part in saved.Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length != 2) continue;
            var item = FarmItems.Get(kv[0]);
            if (item != null && int.TryParse(kv[1], out int amount) && amount > 0)
                lines.Add(new OrderLine(item, amount));
        }
        return lines.Count > 0 ? new PlorpGoal(stage, lines) : null;
    }
}

/// <summary>
/// The game's main goal: fill Plorp's orders until he is satisfied ("conforme").
/// Orders are progressive: they only ask for food the player can already produce, and they
/// prefer foods Plorp has never tasted, so every new crop or animal shows up in the next order.
/// </summary>
public static class PlorpGoals
{
    static readonly string[] Titles =
    {
        "Primer bocado", "Desayuno", "Merienda", "Almuerzo",
        "Cena", "Banquete", "Festin espacial", "Gran cosecha",
    };
    static readonly string[] Quotes =
    {
        "¡Plorp!", "Glerp glerp.", "Zep.", "Zep zep zep.",
        "Plorp... plorp.", "¡PLORP!", "Glooorp.", "Zeeeep.",
    };

    /// <summary>Orders needed until Plorp is satisfied. After that come optional cravings.</summary>
    public static int MainCount => Titles.Length;

    public static string TitleFor(int stage) =>
        stage < Titles.Length ? Titles[stage] : "Antojo extra " + (stage - Titles.Length + 1);

    public static string QuoteFor(int stage) =>
        stage < Quotes.Length ? Quotes[stage] : "Plorp. (Otra vez hambre.)";

    /// <summary>Builds the order for this stage from what the player can produce right now.</summary>
    public static PlorpGoal Generate(int stage, List<FarmItem> unlocked, ICollection<string> tasted)
    {
        if (unlocked.Count == 0) unlocked = new List<FarmItem> { FarmItems.Carrot };
        var rng = new Random(7919 * (stage + 1));

        // Untasted foods first (newest unlock last in the list → reversed), then a random mix.
        var pool = new List<FarmItem>();
        for (int i = unlocked.Count - 1; i >= 0; i--)
            if (!tasted.Contains(unlocked[i].Id)) pool.Add(unlocked[i]);
        var rest = new List<FarmItem>();
        foreach (var item in unlocked) if (!pool.Contains(item)) rest.Add(item);
        while (rest.Count > 0)
        {
            int k = rng.Next(rest.Count);
            pool.Add(rest[k]);
            rest.RemoveAt(k);
        }

        int kinds = Math.Min(pool.Count, Math.Min(3, 1 + stage / 2));
        double size = 10 * Math.Pow(1.5, Math.Min(stage, 12)) / kinds;
        var lines = new List<OrderLine>();
        for (int i = 0; i < kinds; i++)
        {
            var item = pool[i];
            // Animal products come one at a time per animal, so they are asked in smaller numbers.
            int amount = item.FromAnimal
                ? Math.Max(2, (int)Math.Round(size / 6.0))
                : Math.Max(5, (int)Math.Round(size / 5.0) * 5);
            lines.Add(new OrderLine(item, amount));
        }
        return new PlorpGoal(stage, lines);
    }
}
