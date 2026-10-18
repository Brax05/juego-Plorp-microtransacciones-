using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "VIAJE ESTELAR" (Módulo 10, prestige). When Plorp is satisfied (all his main orders) the launch pad
/// opens in the southern clouds. Travelling restarts the farm (coins, upgrades, barn, orders) and gives
/// STAR SEEDS: each one is +10% to everything the farm earns, forever, plus more starting coins.
///   Seeds earned = √(coins harvested this run ÷ 20.000), rounded down.
/// Kept: gems, achievements, garden, kitchen pots, zones in the clouds, missions, streak and records.
/// </summary>
public static class StarTrip
{
    const string StarsKey = "GlorpFarm.stars", TripsKey = "GlorpFarm.trips", JustKey = "GlorpFarm.trip.just";
    public const double BonusPerStar = .10;
    const double CoinsPerSeed = 20000;

    public static int Stars => PlayerPrefs.GetInt(StarsKey, 0);
    public static int Trips => PlayerPrefs.GetInt(TripsKey, 0);
    /// <summary>Permanent multiplier for all income (read by FarmEconomy).</summary>
    public static double Multiplier => 1.0 + BonusPerStar * Stars;

    public static int SeedsToGain(FarmGameState s) =>
        (int)Math.Floor(Math.Sqrt(Math.Max(0, s.TotalHarvestGold) / CoinsPerSeed));

    /// <summary>Coins harvested this run needed for the next star seed.</summary>
    public static long CoinsForNextSeed(FarmGameState s)
    {
        int next = SeedsToGain(s) + 1;
        return (long)Math.Ceiling(next * next * CoinsPerSeed);
    }

    public static void Open(Transform portrait, FarmGameState state, Action<string> message)
    {
        var ui = new PixelUIFactory();
        var root = Popup.Open(portrait, "Star trip", "VIAJE ESTELAR", 100, 820, out var card);
        int gain = SeedsToGain(state);
        int stars = Stars;
        Popup.Pepito(ui, card, 22, 72, 400, gain > 0
            ? "¡Miau! Plorp quedó conforme y quiere llevarnos de viaje. Al volver, la granja parte de cero... pero más fuerte."
            : "Miau... Plorp quiere viajar, pero dice que aún no juntaste suficiente cosecha para el combustible.");

        ui.Texture(card, "Ship", "UI/Garden/ufo_plorp", 199, 210, 144, 128);
        ui.Block(card, "Info shade", 22, 350, 498, 196, new Color(.15f, .07f, .03f, .45f));
        ui.Label(card, "Now", "Semillas estelares: " + stars + "  (+" + Math.Round(BonusPerStar * 100 * stars) + "% a todo)",
            36, 360, 470, 28, 17, Color.white);
        ui.Label(card, "Gain", "Ganarías: +" + gain + (gain == 1 ? " semilla" : " semillas"), 36, 394, 470, 32, 22,
            gain > 0 ? PixelUIFactory.Gold : PixelUIFactory.Hex("#FFB39A"));
        ui.Label(card, "After", "Después del viaje: +" + Math.Round(BonusPerStar * 100 * (stars + gain)) + "% a todo",
            36, 430, 470, 26, 16, PixelUIFactory.Hex("#8FE86A"));
        ui.Paragraph(card, "Next", "Cosechado en esta granja: " + FarmGameState.Format(state.TotalHarvestGold) +
            " · próxima semilla con " + FarmGameState.Format(CoinsForNextSeed(state)), 36, 460, 470, 40, 13, PixelUIFactory.Cream);
        ui.Paragraph(card, "Rules", "Se reinicia: monedas, mejoras, granero y pedidos. Se mantiene: gemas, logros, " +
            "jardinería, cocina, zonas de las nubes, misiones y récords.", 36, 500, 470, 44, 13, PixelUIFactory.Cream);

        bool armed = false;
        ui.ActionButton(card, "Go", gain > 0 ? "¡DESPEGAR!" : "FALTA COSECHA", 60, 570, 422, 76, () =>
        {
            if (SeedsToGain(state) <= 0) { message("Junta más cosecha para el viaje"); return; }
            if (!armed)
            {
                armed = true;
                var caption = card.Find("Go")?.Find("Caption")?.GetComponent<Text>();
                if (caption != null) caption.text = "¿SEGURO? TOCA OTRA VEZ";
                return;
            }
            Travel(state);
        }, gain > 0);
        Popup.CaptionSize(card, "Go", 22);
        ui.Paragraph(card, "Trips", "Viajes hechos: " + Trips, 36, 664, 470, 26, 14, PixelUIFactory.Cream, TextAnchor.MiddleCenter);
    }

    /// <summary>Restarts the run with the new seeds and reloads the farm scene.</summary>
    static void Travel(FarmGameState s)
    {
        int gain = SeedsToGain(s);
        if (gain <= 0) return;
        int stars = Stars + gain;
        PlayerPrefs.SetInt(StarsKey, stars);
        PlayerPrefs.SetInt(TripsKey, Trips + 1);
        PlayerPrefs.SetInt(JustKey, gain);

        // This run's progress.
        PlayerPrefs.SetString("GlorpFarm.gold", (250 + 500L * stars).ToString());
        PlayerPrefs.SetInt("GlorpFarm.farmLevel", 1);
        PlayerPrefs.SetInt("GlorpFarm.purchases", 0);
        PlayerPrefs.SetString("GlorpFarm.harvestGold", "0");
        for (int i = 0; i < FarmEconomy.StartingLevels.Length; i++)
        {
            PlayerPrefs.SetInt("GlorpFarm.level." + i, FarmEconomy.StartingLevels[i]);
            PlayerPrefs.DeleteKey("GlorpFarm.price." + i);
        }
        foreach (string key in new[] { "plorpStage", "plorpOrder", "tasted", "totalFood" })
            PlayerPrefs.DeleteKey("GlorpFarm." + key);
        foreach (var item in FarmItems.All) PlayerPrefs.DeleteKey("GlorpFarm.item." + item.Id);
        for (int p = 0; p < 5; p++)
            for (int c = 0; c < FarmArtLayout.CellsPerPlot; c++)
            {
                PlayerPrefs.DeleteKey("GlorpFarm.world.crop." + p + "." + c + ".stage");
                PlayerPrefs.DeleteKey("GlorpFarm.world.crop." + p + "." + c + ".since");
            }
        foreach (string animal in new[] { "chicken", "sheep", "cow" })
            for (int n = 1; n <= 6; n++) PlayerPrefs.DeleteKey("GlorpFarm.world.actor." + animal + "-" + n + ".readyAt");
        OfflinePanel.MarkSeen();
        PlayerPrefs.Save();

        // Reload the farm: the UI is built again from scratch with the new numbers.
        SceneManager.sceneLoaded += Rebuild;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static void Rebuild(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= Rebuild;
        if (UnityEngine.Object.FindAnyObjectByType<FarmUIBootstrap>() == null)
            new GameObject("PlorpFarm UI").AddComponent<FarmUIBootstrap>();
    }

    /// <summary>After the trip Pepito tells what happened (call when the farm is visible).</summary>
    public static void AfterTrip(PepitoController pepito)
    {
        int gain = PlayerPrefs.GetInt(JustKey, 0);
        if (gain <= 0 || pepito == null) return;
        PlayerPrefs.DeleteKey(JustKey);
        pepito.Say("¡Miau! Volvimos del viaje estelar. Plorp trajo " + gain + (gain == 1 ? " semilla estelar" : " semillas estelares") +
                   ": ahora todo rinde +" + Math.Round(BonusPerStar * 100 * Stars) + "%. Y otra vez tiene hambre.", 9f);
    }
}
