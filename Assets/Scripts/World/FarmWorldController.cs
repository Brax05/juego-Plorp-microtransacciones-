using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the farm from one static landscape and independently changing objects.</summary>
public sealed class FarmWorldController : MonoBehaviour
{
    static readonly Vector2[][] CropPositions =
    {
        new[] { V(95,201), V(127,201), V(159,201), V(191,201), V(78,229), V(110,229), V(142,229), V(174,229), V(206,229), V(64,255), V(96,255), V(128,255), V(160,255), V(192,255) },
        new[] { V(251,258), V(284,258), V(317,258), V(350,258), V(235,284), V(268,284), V(301,284), V(334,284), V(367,284), V(217,313), V(250,313), V(283,313), V(316,313), V(349,313), V(382,313) },
        new[] { V(97,333), V(141,333), V(185,333), V(74,364), V(118,364), V(162,364), V(206,364), V(58,396), V(102,396), V(146,396), V(190,396) },
        new[] { V(254,405), V(298,405), V(342,405), V(230,436), V(274,436), V(318,436), V(362,436), V(214,470), V(258,470), V(302,470), V(346,470), V(390,470) }
    };
    static readonly Vector2[] PlotOrigins = { V(35,182), V(199,243), V(31,315), V(196,388) };
    static readonly CropKind[] InitialKinds = { CropKind.Carrot, CropKind.Tomato, CropKind.Pumpkin, CropKind.Cabbage };
    // Top-left sprite positions, chosen on the grass inside each visible fence.
    static readonly Vector2[] ChickenHomes = { V(258,130), V(205,116), V(300,135), V(238,147), V(329,129), V(275,112) };
    static readonly Vector2[] SheepHomes = { V(524,273), V(522,230), V(480,252), V(503,220), V(452,233), V(531,255) };
    static readonly Vector2[] CowHomes = { V(453,226), V(487,230), V(526,216), V(445,240), V(505,255), V(470,207) };

    readonly CropPlot[] plots = new CropPlot[4];
    readonly List<WorldActor> actors = new List<WorldActor>();
    readonly HashSet<string> actorIds = new HashSet<string>();
    FarmGameState state;
    PixelUIFactory ui;
    Action<string> message;

    public void Initialize(FarmGameState gameState, PixelUIFactory factory, Action<string> showMessage)
    {
        state = gameState;
        ui = factory;
        message = showMessage;
        ui.Texture(transform, "Static landscape and barns", "World/farm_background", 0, 0, 570, 530);
        for (int i = 0; i < plots.Length; i++) CreatePlot(i);

        // Each animal, worker and machine is a separate GameObject with its own state.
        Spawn("worker-1", "Trabajador", "worker_blue", WorldActorRole.Worker, V(139,302), 80, 45, 35, 43);
        Spawn("worker-2", "Trabajador", "worker_red", WorldActorRole.Worker, V(392,334), 80, 45, 35, 43);
        Spawn("tractor-1", "Tractor", "tractor", WorldActorRole.Machine, V(402,390), 160, 70, 44, 41);

        state.Changed += SyncUpgrades;
        SyncUpgrades();
    }

    public long HarvestAll()
    {
        long earned = 0;
        foreach (var plot in plots) earned += plot.HarvestReady();
        foreach (var actor in actors) earned += actor.TakeProduction();
        if (earned > 0) state.Collect(earned);
        return earned;
    }

    void CreatePlot(int index)
    {
        Vector2 origin = PlotOrigins[index];
        var root = ui.Rect(transform, "Plot " + index + " - " + InitialKinds[index], origin.x, origin.y, 235, 145);
        var plot = root.gameObject.AddComponent<CropPlot>();
        plot.Initialize(index, InitialKinds[index]);
        plots[index] = plot;
        for (int i = 0; i < CropPositions[index].Length; i++)
            plot.AddCell(ui, i, CropPositions[index][i] - origin, OnCropTapped);
    }

    void OnCropTapped(CropCell cell)
    {
        long earned = cell.TakeHarvest();
        if (earned == 0)
        {
            message("Cultivo creciendo");
            return;
        }
        state.Collect(earned);
        message(cell.CropName + " +" + FarmGameState.Format(earned));
    }

    void SyncUpgrades()
    {
        // Seed upgrades change a whole plot; the others add independent entities.
        int seedSteps = Mathf.Max(0, state.Levels[0] - 25);
        plots[0].SetKind((CropKind)(seedSteps % 4));

        for (int i = 0; i < state.AnimalGroups; i++) SpawnAnimalGroup(i);

        int extraWorkers = Mathf.Clamp(state.Levels[2] - 3, 0, 3);
        if (extraWorkers > 0) Spawn("worker-3", "Trabajador", "worker_blue", WorldActorRole.Worker, V(282,383), 80, 45, 35, 43);
        if (extraWorkers > 1) Spawn("worker-4", "Trabajador", "worker_red", WorldActorRole.Worker, V(94,437), 80, 45, 35, 43);
        if (extraWorkers > 2) Spawn("worker-5", "Trabajador", "worker_blue", WorldActorRole.Worker, V(373,226), 80, 45, 35, 43);

        if (state.Levels[3] > 1)
            Spawn("tractor-2", "Tractor", "tractor", WorldActorRole.Machine, V(270,378), 160, 70, 44, 41);
    }

    void SpawnAnimalGroup(int index)
    {
        int number = index + 1;
        Spawn("chicken-" + number, "Gallina", index % 2 == 0 ? "chicken_white" : "chicken_brown",
            WorldActorRole.Animal, AnimalHome(ChickenHomes, index), 55, 35, 35, 35);
        Spawn("sheep-" + number, "Oveja", index % 2 == 0 ? "sheep_white" : "sheep_gray",
            WorldActorRole.Animal, AnimalHome(SheepHomes, index), 90, 48, 36, 35);
        Spawn("cow-" + number, "Vaca", "cow", WorldActorRole.Animal,
            AnimalHome(CowHomes, index), 120, 65, 38, 36);
    }

    static Vector2 AnimalHome(Vector2[] homes, int index)
    {
        // Extra purchases continue to create actors, even after the first row fills.
        Vector2 home = homes[index % homes.Length];
        int row = index / homes.Length;
        if (row == 0) return home;
        return home + new Vector2((row % 3 - 1) * 3f, -(row % 2) * 3f);
    }

    void Spawn(string id, string title, string texture, WorldActorRole role, Vector2 position,
        long reward, long cooldown, float width, float height)
    {
        if (!actorIds.Add(id)) return;
        var rect = ui.Rect(transform, id, position.x, position.y, width, height);
        var actor = rect.gameObject.AddComponent<WorldActor>();
        actor.Initialize(ui, state, message, id, title, texture, role, position, reward, cooldown);
        actors.Add(actor);
    }

    void OnDestroy()
    {
        if (state != null) state.Changed -= SyncUpgrades;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);
}
