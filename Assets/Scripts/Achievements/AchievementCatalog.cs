using System;
using UnityEngine;

/// <summary>One achievement: a counter from the game, a target and a gem reward.</summary>
public sealed class AchievementDef
{
    public readonly string Id, Name, Description, Icon;
    public readonly long Target;
    public readonly int Gems;
    public readonly Func<FarmGameState, long> Progress;

    public AchievementDef(string id, string name, string description, string icon, long target, int gems,
        Func<FarmGameState, long> progress)
    {
        Id = id; Name = name; Description = description; Icon = icon; Target = target; Gems = gems; Progress = progress;
    }
}

/// <summary>
/// All achievements, grouped by family (each family has growing targets). Claimed flags are saved
/// as GlorpFarm.ach.&lt;id&gt;. Adding one is one line.
/// </summary>
public static class AchievementCatalog
{
    const string Coin = "icon_coin";
    const string Sprout = "icon_sprout";
    const string Egg = "egg";
    const string Flower = "icon_flowercoin";
    const string Bug = "bug_0";
    const string Plorp = "plorp_face";

    public static readonly AchievementDef[] All =
    {
        new AchievementDef("harvest1", "Primera cosecha", "Cosecha 1 vez", Sprout, 1, 5, s => s.Harvests),
        new AchievementDef("harvest50", "Granjero constante", "Cosecha 50 veces", Sprout, 50, 15, s => s.Harvests),
        new AchievementDef("harvest500", "Manos de tierra", "Cosecha 500 veces", Sprout, 500, 40, s => s.Harvests),
        new AchievementDef("harvest5000", "Leyenda del campo", "Cosecha 5.000 veces", Sprout, 5000, 100, s => s.Harvests),

        new AchievementDef("gold10k", "Primeros ahorros", "Gana 10K monedas en total", Coin, 10_000, 10, s => s.TotalHarvestGold),
        new AchievementDef("gold1m", "Millonario rural", "Gana 1M monedas en total", Coin, 1_000_000, 40, s => s.TotalHarvestGold),
        new AchievementDef("gold100m", "Magnate galáctico", "Gana 100M monedas en total", Coin, 100_000_000, 120, s => s.TotalHarvestGold),

        new AchievementDef("upgrades5", "Gran inversión", "Compra 5 mejoras", Coin, 5, 10, s => s.Purchases),
        new AchievementDef("upgrades25", "Granja en expansión", "Compra 25 mejoras", Coin, 25, 30, s => s.Purchases),
        new AchievementDef("upgrades100", "Imperio de Plorp", "Compra 100 mejoras", Coin, 100, 80, s => s.Purchases),

        new AchievementDef("orders1", "Primer pedido", "Entrega 1 pedido a Plorp", Plorp, 1, 10, s => s.PlorpStage),
        new AchievementDef("orders4", "Chef espacial", "Entrega 4 pedidos a Plorp", Plorp, 4, 25, s => s.PlorpStage),
        new AchievementDef("orders8", "Plorp conforme", "Completa todos los pedidos de Plorp", Plorp, PlorpGoals.MainCount, 60, s => s.PlorpStage),

        new AchievementDef("snack1", "Alimenta a Plorp", "Cómprale 1 antojo a Plorp (Pepito te avisa)", Egg, 1, 15, s => s.PepitoMeals),
        new AchievementDef("snack10", "Plorp regalón", "Cómprale 10 antojos a Plorp", Egg, 10, 40, s => s.PepitoMeals),

        new AchievementDef("golden1", "¡Dorada!", "Consigue 1 cosecha dorada", Coin, 1, 10, s => s.GoldenHarvests),
        new AchievementDef("golden25", "Toque de oro", "Consigue 25 cosechas doradas", Coin, 25, 40, s => s.GoldenHarvests),

        new AchievementDef("bloom1", "Primera flor", "Haz florecer una planta del jardín", Flower, 1, 20, BloomedSpecies),
        new AchievementDef("bloomAll", "Jardín completo", "Haz florecer todas las plantas", Flower, GardenState.Species.Length, 150, BloomedSpecies),
        new AchievementDef("bugs10", "Cazabichos", "Aplasta 10 bichitos", Bug, 10, 15, s => s.Garden != null ? s.Garden.BugsSquashed : 0),
        new AchievementDef("bugs100", "Exterminador", "Aplasta 100 bichitos", Bug, 100, 50, s => s.Garden != null ? s.Garden.BugsSquashed : 0),
    };

    static long BloomedSpecies(FarmGameState s)
    {
        if (s.Garden == null) return 0;
        int n = 0;
        foreach (bool b in s.Garden.EverBloomed) if (b) n++;
        return n;
    }

    public static bool Claimed(AchievementDef a) => SaveStore.GetInt("GlorpFarm.ach." + a.Id, 0) == 1;
    public static bool Ready(FarmGameState s, AchievementDef a) => !Claimed(a) && a.Progress(s) >= a.Target;

    public static bool AnyReady(FarmGameState s)
    {
        foreach (var a in All) if (Ready(s, a)) return true;
        return false;
    }

    public static int ReadyCount(FarmGameState s)
    {
        int n = 0;
        foreach (var a in All) if (Ready(s, a)) n++;
        return n;
    }

    public static bool Claim(FarmGameState s, AchievementDef a)
    {
        if (!Ready(s, a)) return false;
        SaveStore.SetInt("GlorpFarm.ach." + a.Id, 1);
        s.AddGems(a.Gems);
        return true;
    }
}
