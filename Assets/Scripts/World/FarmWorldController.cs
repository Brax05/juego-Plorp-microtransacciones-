using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds and manages the modular farm world with separated terrain, buildings, animal pens, and crop plots.</summary>
public sealed class FarmWorldController : MonoBehaviour
{
    static readonly CropKind[] InitialKinds = { CropKind.Carrot, CropKind.Tomato, CropKind.Pumpkin, CropKind.Cabbage };

    readonly List<CropPlot> plots = new List<CropPlot>();
    readonly List<WorldActor> actors = new List<WorldActor>();
    readonly HashSet<string> actorIds = new HashSet<string>();
    FarmGameState state;
    Action<string> message;

    Transform chickenPen;
    Transform pasturePen;

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

        plots.Clear();
        var existingPlots = cropsRoot.GetComponentsInChildren<CropPlot>(true);
        for (int i = 0; i < existingPlots.Length; i++)
        {
            var p = existingPlots[i];
            var kind = i < InitialKinds.Length ? InitialKinds[i] : (CropKind)(i % 4);
            p.Initialize(i, kind);
            p.BindCells(OnCropTapped);
            plots.Add(p);
        }

        foreach (var actor in GetComponentsInChildren<WorldActor>(true))
            actor.gameObject.SetActive(false);

        state.Changed += SyncUpgrades;
        SyncUpgrades();

    }

    /// <summary>Food units gathered by the last HarvestAll call.</summary>
    public int LastHarvestFood { get; private set; }

    public long HarvestAll()
    {
        long earned = 0;
        int food = 0;
        var items = new Dictionary<string, int>();
        foreach (var plot in plots)
        {
            if (!plot.gameObject.activeInHierarchy) continue;
            earned += plot.HarvestReady(out int crops);
            if (crops <= 0) continue;
            string id = FarmItems.ForCrop(plot.Kind).Id;
            items[id] = (items.TryGetValue(id, out int n) ? n : 0) + crops;
            food += crops;
        }
        foreach (var actor in actors)
        {
            long produced = actor.TakeProduction();
            if (produced <= 0) continue;
            earned += produced;
            var product = actor.Product;
            if (product == null) continue;
            items[product.Id] = (items.TryGetValue(product.Id, out int n) ? n : 0) + 1;
            food++;
        }
        LastHarvestFood = food;
        if (earned > 0) state.Collect(earned, items);
        return earned;
    }

    void OnCropTapped(CropCell cell)
    {
        long earned = cell.TakeHarvest();
        if (earned == 0)
        {
            message("Cultivo creciendo");
            return;
        }
        state.Collect(earned, FarmItems.ForCrop(cell.Kind));
        message(cell.CropName + " +" + FarmGameState.Format(earned));
    }

    void SyncUpgrades()
    {
        int unlockedPlots = Mathf.Clamp(state.Levels[0], 1, plots.Count);
        for (int i = 0; i < plots.Count; i++) plots[i].gameObject.SetActive(i < unlockedPlots);

        for (int i = 0; i < state.AnimalCount; i++) SpawnAnimal(i);

        int workers = Mathf.Clamp(state.Levels[2], 0, 4);
        if (workers > 0) Spawn(transform, "worker-1", "Trabajador", "worker_blue", WorldActorRole.Worker, 80, 45);
        if (workers > 1) Spawn(transform, "worker-2", "Trabajador", "worker_red", WorldActorRole.Worker, 80, 45);
        if (workers > 2) Spawn(transform, "worker-3", "Trabajador", "worker_blue", WorldActorRole.Worker, 80, 45);
        if (workers > 3) Spawn(transform, "worker-4", "Trabajador", "worker_red", WorldActorRole.Worker, 80, 45);

    }

    void SpawnAnimal(int index)
    {
        int species = index % 3;
        int slot = index / 3;
        int number = slot + 1;
        if (species == 0)
            Spawn(chickenPen, "chicken-" + number, "Gallina",
                slot % 2 == 0 ? "chicken_white" : "chicken_brown",
                WorldActorRole.Animal, 55, 35);
        else if (species == 1)
            Spawn(pasturePen, "sheep-" + number, "Oveja",
                slot % 2 == 0 ? "sheep_white" : "sheep_gray",
                WorldActorRole.Animal, 90, 48);
        else
            Spawn(pasturePen, "cow-" + number, "Vaca", "cow",
                WorldActorRole.Animal, 120, 65);
    }

    void Spawn(Transform parent, string id, string title, string texture, WorldActorRole role,
        long reward, long cooldown)
    {
        if (parent == null || actorIds.Contains(id)) return;
        var marker = parent.Find(id) as RectTransform;
        if (marker == null) return;
        var actor = marker.GetComponent<WorldActor>();
        if (actor == null) return;
        actorIds.Add(id);
        marker.gameObject.SetActive(true);
        Vector2 home = new Vector2(marker.anchoredPosition.x, -marker.anchoredPosition.y);
        actor.Initialize(state, message, id, title, texture, role, home, reward, cooldown);
        actors.Add(actor);
    }

    float nextAutoHarvest;

    // Idle loop: each worker level makes the farm harvest itself more often (15s, 12s, 9s, 6s).
    void Update()
    {
        if (state == null) return;
        int workers = Mathf.Clamp(state.Levels[2], 0, 4);
        if (workers == 0) return;
        float now = Time.unscaledTime;
        if (now < nextAutoHarvest) return;
        nextAutoHarvest = now + 18f - 3f * workers;
        HarvestAll();
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= SyncUpgrades;
    }

}
