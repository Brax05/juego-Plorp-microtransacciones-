using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the farm world: fields, animals, workers and the golden harvest tickets.
/// What exists and what it is worth comes from FarmEconomy, so every upgrade is reflected here.
/// </summary>
public sealed class FarmWorldController : MonoBehaviour
{
    readonly List<CropPlot> plots = new List<CropPlot>();
    readonly List<WorldActor> actors = new List<WorldActor>();
    readonly HashSet<string> actorIds = new HashSet<string>();
    FarmGameState state;
    Action<string> message;

    Transform chickenPen;
    Transform pasturePen;
    Transform ticketLayer;
    float nextAutoHarvest;

    public void Initialize(FarmGameState gameState, Action<string> showMessage)
    {
        state = gameState;
        message = showMessage;

        // Put plots, animals and workers on the painted farm before anything reads their positions.
        FarmArtLayout.Apply((RectTransform)transform);

        var pensRoot = transform.Find("Animal Pens");
        chickenPen = pensRoot != null ? pensRoot.Find("Chicken Pen") : null;
        pasturePen = pensRoot != null ? pensRoot.Find("Pasture Pen") : null;
        var cropsRoot = transform.Find("Crop Plots");
        if (cropsRoot == null)
            throw new InvalidOperationException("PixelFarmCanvas is missing Crop Plots.");

        // Tickets are drawn above every field and animal.
        ticketLayer = new PixelUIFactory().Rect(transform, "Harvest tickets", 0, 0, FarmArtLayout.Width, FarmArtLayout.Height);
        ticketLayer.SetAsLastSibling();

        plots.Clear();
        var existingPlots = cropsRoot.GetComponentsInChildren<CropPlot>(true);
        for (int i = 0; i < existingPlots.Length; i++)
        {
            var plot = existingPlots[i];
            plot.Initialize(i, FarmEconomy.PlotKind(state, i));
            plot.BindCells(OnCropTapped);
            plots.Add(plot);
            HarvestTicket.Create(ticketLayer, "Ticket plot " + i, (RectTransform)plot.transform,
                FarmArtLayout.PlotTicketPoint(i), () => plot.ReadyCount,
                () => FarmItems.ForCrop(plot.Kind), () => CollectPlot(plot));
        }

        foreach (var actor in GetComponentsInChildren<WorldActor>(true))
            actor.gameObject.SetActive(false);

        state.Changed += SyncUpgrades;
        SyncUpgrades();
    }

    /// <summary>Food units gathered by the last HarvestAll call.</summary>
    public int LastHarvestFood { get; private set; }

    /// <summary>Collects everything that is ready (used by the workers).</summary>
    public long HarvestAll()
    {
        long earned = 0;
        int food = 0;
        var items = new Dictionary<string, int>();
        foreach (var plot in plots)
        {
            if (!plot.gameObject.activeInHierarchy) continue;
            int crops = plot.HarvestReady();
            if (crops <= 0) continue;
            earned += crops * FarmEconomy.CropHarvestValue(state, plot.Kind);
            Add(items, FarmItems.ForCrop(plot.Kind).Id, crops);
            food += crops;
        }
        foreach (var actor in actors)
        {
            long produced = actor.TakeProduction();
            if (produced <= 0) continue;
            earned += produced;
            if (actor.Product == null) continue;
            Add(items, actor.Product.Id, 1);
            food++;
        }
        LastHarvestFood = food;
        if (earned > 0) state.Collect(earned, items);
        return earned;
    }

    static void Add(Dictionary<string, int> items, string id, int amount) =>
        items[id] = (items.TryGetValue(id, out int n) ? n : 0) + amount;

    /// <summary>Tap on a field's golden ticket: picks every ripe crop in that field.</summary>
    void CollectPlot(CropPlot plot)
    {
        int crops = plot.HarvestReady();
        if (crops <= 0) return;
        long earned = crops * FarmEconomy.CropHarvestValue(state, plot.Kind);
        var item = FarmItems.ForCrop(plot.Kind);
        state.Collect(earned, new Dictionary<string, int> { { item.Id, crops } });
        message("+" + FarmGameState.Format(earned) + " monedas · +" + crops + " " +
                (crops == 1 ? item.Single : item.Name.ToLowerInvariant()));
    }

    void OnCropTapped(CropCell cell)
    {
        if (cell.TakeHarvest() == 0)
        {
            message(cell.CropName + " creciendo");
            return;
        }
        long earned = FarmEconomy.CropHarvestValue(state, cell.Kind);
        state.Collect(earned, FarmItems.ForCrop(cell.Kind));
        message(cell.CropName + " +" + FarmGameState.Format(earned));
    }

    void SyncUpgrades()
    {
        // Fields: one more per seed tier; the newest (most profitable) crop goes in field 0.
        int active = FarmEconomy.ActivePlots(state);
        for (int i = 0; i < plots.Count; i++)
        {
            bool on = i < active;
            plots[i].gameObject.SetActive(on);
            if (on) plots[i].SetKind(FarmEconomy.PlotKind(state, i));
        }

        // Animals: one per purchase, a new species every 10 purchases.
        for (int species = 0; species < FarmEconomy.Species.Length; species++)
            for (int n = 0; n < FarmEconomy.AnimalCount(state, species); n++)
                SpawnAnimal(species, n);

        string[] workerTextures = { "worker_blue", "worker_red" };
        for (int i = 0; i < FarmEconomy.VisibleWorkers(state); i++)
            Spawn(transform, "worker-" + (i + 1), "Trabajador", workerTextures[i % 2], WorldActorRole.Worker);
    }

    void SpawnAnimal(int species, int slot)
    {
        int number = slot + 1;
        switch (species)
        {
            case 0:
                Spawn(chickenPen, "chicken-" + number, "Gallina",
                    slot % 2 == 0 ? "chicken_white" : "chicken_brown", WorldActorRole.Animal);
                break;
            case 1:
                Spawn(pasturePen, "sheep-" + number, "Oveja",
                    slot % 2 == 0 ? "sheep_white" : "sheep_gray", WorldActorRole.Animal);
                break;
            default:
                Spawn(pasturePen, "cow-" + number, "Vaca", "cow", WorldActorRole.Animal);
                break;
        }
    }

    void Spawn(Transform parent, string id, string title, string texture, WorldActorRole role)
    {
        if (parent == null || actorIds.Contains(id)) return;
        var marker = parent.Find(id) as RectTransform;
        if (marker == null) return;
        var actor = marker.GetComponent<WorldActor>();
        if (actor == null) return;
        actorIds.Add(id);
        marker.gameObject.SetActive(true);
        Vector2 home = new Vector2(marker.anchoredPosition.x, -marker.anchoredPosition.y);
        actor.Initialize(state, message, id, title, texture, role, home, 0, 0);
        actors.Add(actor);

        if (role == WorldActorRole.Animal)
        {
            // Ticket floats just above the animal's head and walks with it.
            Vector2 head = new Vector2(marker.sizeDelta.x / 2f, 2f);
            HarvestTicket.Create(ticketLayer, "Ticket " + id, marker, head,
                () => actor.IsReady ? 1 : 0, () => actor.Product, actor.Collect);
        }
    }

    // Idle loop: workers harvest everything on their own, faster with every level.
    void Update()
    {
        if (state == null) return;
        float interval = FarmEconomy.AutoHarvestSeconds(state);
        if (interval <= 0f) return;
        float now = Time.unscaledTime;
        if (nextAutoHarvest <= 0f) nextAutoHarvest = now + interval;
        if (now < nextAutoHarvest) return;
        nextAutoHarvest = now + interval;
        HarvestAll();
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= SyncUpgrades;
    }
}
