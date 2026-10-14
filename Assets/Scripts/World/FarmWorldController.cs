using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds and manages the modular farm world with separated terrain, buildings, animal pens, and crop plots.</summary>
public sealed class FarmWorldController : MonoBehaviour
{
    public const float MapWidth = 1100f;
    public const float MapHeight = 950f;

    static readonly Vector2[] DefaultPlotPositions =
    {
        new Vector2(120, 260),  // Plot 0
        new Vector2(340, 340),  // Plot 1
        new Vector2(120, 430),  // Plot 2
        new Vector2(340, 510)   // Plot 3
    };

    static readonly CropKind[] InitialKinds = { CropKind.Carrot, CropKind.Tomato, CropKind.Pumpkin, CropKind.Cabbage };

    // Animal spawn offsets relative to their pen:
    static readonly Vector2[] ChickenHomes =
    {
        new Vector2(40, 75), new Vector2(85, 65), new Vector2(130, 80),
        new Vector2(60, 95), new Vector2(105, 105), new Vector2(150, 75)
    };
    static readonly Vector2[] SheepHomes =
    {
        new Vector2(45, 55), new Vector2(95, 45), new Vector2(145, 65),
        new Vector2(65, 85), new Vector2(115, 75), new Vector2(160, 95)
    };
    static readonly Vector2[] CowHomes =
    {
        new Vector2(35, 75), new Vector2(85, 90), new Vector2(135, 60),
        new Vector2(55, 105), new Vector2(105, 110), new Vector2(150, 80)
    };

    readonly List<CropPlot> plots = new List<CropPlot>();
    readonly List<WorldActor> actors = new List<WorldActor>();
    readonly HashSet<string> actorIds = new HashSet<string>();
    FarmGameState state;
    PixelUIFactory ui;
    Action<string> message;

    Transform terrainRoot;
    Transform buildingsRoot;
    Transform pensRoot;
    Transform chickenPen;
    Transform pasturePen;
    Transform cropsRoot;

    void Start()
    {
        CenterInViewport();
    }

    public void CenterInViewport()
    {
        var rect = GetComponent<RectTransform>();
        var parentRect = transform.parent as RectTransform;
        if (rect != null && parentRect != null && rect.sizeDelta.x > parentRect.sizeDelta.x)
        {
            rect.anchoredPosition = new Vector2(
                -(rect.sizeDelta.x - parentRect.sizeDelta.x) * 0.5f,
                (rect.sizeDelta.y - parentRect.sizeDelta.y) * 0.5f
            );
        }
    }

    public void Initialize(FarmGameState gameState, PixelUIFactory factory, Action<string> showMessage)
    {
        state = gameState;
        ui = factory;
        message = showMessage;

        // Remove legacy static baked background if present
        var legacyBg = transform.Find("Static landscape and barns");
        if (legacyBg != null)
        {
            legacyBg.gameObject.SetActive(false);
            Destroy(legacyBg.gameObject);
        }

        // 1. Terrain Layer (expandable green grass)
        terrainRoot = transform.Find("Terrain");
        if (terrainRoot == null)
        {
            var raw = ui.Texture(transform, "Terrain", "World/Sprites/terrain_grass", 0, 0, MapWidth, MapHeight);
            terrainRoot = raw.transform;
            terrainRoot.SetAsFirstSibling();
            raw.uvRect = new Rect(0, 0, MapWidth / 512f, MapHeight / 512f);
        }

        // 2. Granjas y Estructuras (Buildings)
        buildingsRoot = transform.Find("Buildings");
        if (buildingsRoot == null)
        {
            buildingsRoot = ui.Rect(transform, "Buildings", 0, 0, MapWidth, MapHeight);
            ui.Texture(buildingsRoot, "Main Barn", "World/Sprites/barn_yellow", 750, 140, 160, 155);
            ui.Texture(buildingsRoot, "Coop Shed", "World/Sprites/coop_shed", 380, 130, 140, 120);
            ui.Texture(buildingsRoot, "Red Barn", "World/Sprites/barn_red", 780, 620, 155, 145);
        }

        // 3. Corrales de Animales (Animal Pens)
        pensRoot = transform.Find("Animal Pens");
        if (pensRoot == null)
        {
            pensRoot = ui.Rect(transform, "Animal Pens", 0, 0, MapWidth, MapHeight);
        }

        chickenPen = pensRoot.Find("Chicken Pen") ?? transform.Find("Chicken Pen");
        if (chickenPen == null)
        {
            chickenPen = ui.Rect(pensRoot, "Chicken Pen", 330, 180, 240, 165);
        }
        var chickenFence = chickenPen.Find("Fence");
        if (chickenFence == null)
        {
            ui.Texture(chickenPen, "Fence", "World/Sprites/animal_pen", 0, 0, 240, 155);
        }

        pasturePen = pensRoot.Find("Pasture Pen") ?? transform.Find("Pasture Pen");
        if (pasturePen == null)
        {
            pasturePen = ui.Rect(pensRoot, "Pasture Pen", 680, 350, 260, 185);
            ui.Texture(pasturePen, "Fence", "World/Sprites/animal_pen", 0, 0, 260, 175);
        }

        // 4. Zona de Cultivos (Crop Plots)
        cropsRoot = transform.Find("Crop Plots") ?? transform.Find("Crops") ?? transform;
        plots.Clear();
        var existingPlots = cropsRoot.GetComponentsInChildren<CropPlot>(true);
        if (existingPlots != null && existingPlots.Length > 0)
        {
            for (int i = 0; i < existingPlots.Length; i++)
            {
                var p = existingPlots[i];
                var kind = i < InitialKinds.Length ? InitialKinds[i] : (CropKind)(i % 4);
                p.Initialize(i, kind);
                plots.Add(p);
                if (p.transform.childCount == 0 || p.GetComponentInChildren<CropCell>(true) == null)
                {
                    for (int c = 0; c < CropPlot.LocalCropPositions.Length; c++)
                        p.AddCell(ui, c, CropPlot.LocalCropPositions[c], OnCropTapped);
                }
            }
        }
        else
        {
            for (int i = 0; i < DefaultPlotPositions.Length; i++) CreatePlot(i);
        }

        // Workers & Tractor
        Spawn(transform, "worker-1", "Trabajador", "worker_blue", WorldActorRole.Worker, V(280, 420), 80, 45, 35, 43);
        Spawn(transform, "worker-2", "Trabajador", "worker_red", WorldActorRole.Worker, V(650, 560), 80, 45, 35, 43);
        Spawn(transform, "tractor-1", "Tractor", "tractor", WorldActorRole.Machine, V(800, 780), 160, 70, 44, 41);

        state.Changed += SyncUpgrades;
        SyncUpgrades();

        CenterInViewport();
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
        var kind = index < InitialKinds.Length ? InitialKinds[index] : (CropKind)(index % 4);
        string plotName = "Plot " + index + " - " + kind;
        var existing = cropsRoot.Find(plotName) ?? cropsRoot.Find("Plot " + index)
                    ?? transform.Find(plotName) ?? transform.Find("Plot " + index);
        CropPlot plot;
        if (existing != null)
        {
            plot = existing.GetComponent<CropPlot>() ?? existing.gameObject.AddComponent<CropPlot>();
        }
        else
        {
            Vector2 origin = index < DefaultPlotPositions.Length
                ? DefaultPlotPositions[index]
                : new Vector2(120 + (index % 2) * 220, 260 + (index / 2) * 170);
            var root = ui.Rect(cropsRoot, plotName, origin.x, origin.y, 204, 108);
            plot = root.gameObject.AddComponent<CropPlot>();
        }
        plot.Initialize(index, kind);
        plots.Add(plot);

        // If plot does not have cells yet, add them using clean furrow positions:
        if (plot.transform.childCount == 0 || plot.GetComponentInChildren<CropCell>(true) == null)
        {
            for (int i = 0; i < CropPlot.LocalCropPositions.Length; i++)
                plot.AddCell(ui, i, CropPlot.LocalCropPositions[i], OnCropTapped);
        }
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
        int seedSteps = Mathf.Max(0, state.Levels[0] - 25);
        if (plots.Count > 0)
            plots[0].SetKind((CropKind)(seedSteps % 4));

        for (int i = 0; i < state.AnimalGroups; i++) SpawnAnimalGroup(i);

        int extraWorkers = Mathf.Clamp(state.Levels[2] - 3, 0, 3);
        if (extraWorkers > 0) Spawn(transform, "worker-3", "Trabajador", "worker_blue", WorldActorRole.Worker, V(500, 380), 80, 45, 35, 43);
        if (extraWorkers > 1) Spawn(transform, "worker-4", "Trabajador", "worker_red", WorldActorRole.Worker, V(220, 550), 80, 45, 35, 43);
        if (extraWorkers > 2) Spawn(transform, "worker-5", "Trabajador", "worker_blue", WorldActorRole.Worker, V(700, 260), 80, 45, 35, 43);

        if (state.Levels[3] > 1)
            Spawn(transform, "tractor-2", "Tractor", "tractor", WorldActorRole.Machine, V(600, 740), 160, 70, 44, 41);
    }

    void SpawnAnimalGroup(int index)
    {
        int number = index + 1;
        Spawn(chickenPen != null ? chickenPen : transform, "chicken-" + number, "Gallina",
            index % 2 == 0 ? "chicken_white" : "chicken_brown",
            WorldActorRole.Animal, AnimalHome(ChickenHomes, index), 55, 35, 35, 35);

        Spawn(pasturePen != null ? pasturePen : transform, "sheep-" + number, "Oveja",
            index % 2 == 0 ? "sheep_white" : "sheep_gray",
            WorldActorRole.Animal, AnimalHome(SheepHomes, index), 90, 48, 36, 35);

        Spawn(pasturePen != null ? pasturePen : transform, "cow-" + number, "Vaca", "cow",
            WorldActorRole.Animal, AnimalHome(CowHomes, index), 120, 65, 38, 36);
    }

    static Vector2 AnimalHome(Vector2[] homes, int index)
    {
        Vector2 home = homes[index % homes.Length];
        int row = index / homes.Length;
        if (row == 0) return home;
        return home + new Vector2((row % 3 - 1) * 3f, -(row % 2) * 3f);
    }

    void Spawn(Transform parent, string id, string title, string texture, WorldActorRole role, Vector2 position,
        long reward, long cooldown, float width, float height)
    {
        if (!actorIds.Add(id)) return;
        var rect = ui.Rect(parent, id, position.x, position.y, width, height);
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
