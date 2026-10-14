using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility that converts the farm world into a spacious, pannable viewport
/// with smooth 2D scrolling, and generous spacing between buildings, animal pens, and crop plots.
/// </summary>
public static class ModularFarmEditor
{
    public const float ViewportWidth = 570f;
    public const float ViewportHeight = 546f;
    public const float MapWidth = 1100f;
    public const float MapHeight = 950f;

    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Farm World/Rebuild Modular Farm Layout", false, 1)]
    public static void UpgradeModularFarmLayout()
    {
        try
        {
            AssetDatabase.Refresh();

            // 1. Upgrade Prefab
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot != null)
            {
                var portraitScreen = FarmUIBootstrap.FindChildRecursive(prefabRoot.transform, "Portrait screen");
                if (portraitScreen != null)
                {
                    ConfigureViewportAndWorld(portraitScreen);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                    Debug.Log("<color=green>[ModularFarmEditor]</color> Successfully updated pannable viewport and spacious layout in " + PrefabPath);
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // 2. Ensure Canvas & Viewport in SampleScene.unity
            if (System.IO.File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.OpenScene(ScenePath);
                var sceneCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
                if (sceneCanvas == null)
                {
                    var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                    if (prefabAsset != null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                        instance.name = "Pixel Farm Canvas";
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log("<color=green>[ModularFarmEditor]</color> Instantiated Pixel Farm Canvas in SampleScene.unity");
                    }
                }
                else
                {
                    var portraitScreen = FarmUIBootstrap.FindChildRecursive(sceneCanvas.transform, "Portrait screen");
                    if (portraitScreen != null)
                    {
                        ConfigureViewportAndWorld(portraitScreen);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log("<color=green>[ModularFarmEditor]</color> Updated pannable viewport in SampleScene.unity");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[ModularFarmEditor] Error upgrading layout: " + ex);
        }
    }

    public static void ConfigureViewportAndWorld(Transform portraitScreen)
    {
        // 1. Farm Viewport (Scrollable container / frame)
        var viewport = portraitScreen.Find("Farm Viewport");
        if (viewport == null)
        {
            var go = CreateRect(portraitScreen, "Farm Viewport", 0, 0, ViewportWidth, ViewportHeight);
            viewport = go.transform;
            viewport.SetSiblingIndex(0);
        }
        else
        {
            SetRect(viewport as RectTransform, 0, 0, ViewportWidth, ViewportHeight);
            viewport.SetSiblingIndex(0);
        }

        var hitImg = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
        hitImg.color = new Color(0, 0, 0, 0);
        hitImg.raycastTarget = true;

        var mask = viewport.GetComponent<RectMask2D>() ?? viewport.gameObject.AddComponent<RectMask2D>();

        var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 25f;

        // 2. Modular farm world (Spacious Content)
        var world = viewport.Find("Modular farm world") ?? portraitScreen.Find("Modular farm world");
        if (world == null)
        {
            var go = CreateRect(viewport, "Modular farm world", 0, 0, MapWidth, MapHeight);
            world = go.transform;
        }
        if (world.parent != viewport)
        {
            world.SetParent(viewport, false);
        }

        // Center map view inside viewport
        float initialX = -(MapWidth - ViewportWidth) * 0.5f;
        float initialY = (MapHeight - ViewportHeight) * 0.5f;
        var worldRect = world as RectTransform;
        worldRect.anchorMin = new Vector2(0, 1);
        worldRect.anchorMax = new Vector2(0, 1);
        worldRect.pivot = new Vector2(0, 1);
        worldRect.sizeDelta = new Vector2(MapWidth, MapHeight);
        worldRect.anchoredPosition = new Vector2(initialX, initialY);

        scroll.viewport = viewport as RectTransform;
        scroll.content = worldRect;

        // 3. Configure world modular elements inside
        ConfigureModularWorld(world);
    }

    public static void ConfigureModularWorld(Transform world)
    {
        // Remove legacy static monolithic background if present
        var legacy = world.Find("Static landscape and barns");
        if (legacy != null)
        {
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }

        // Ensure FarmWorldController component on world root
        if (world.GetComponent<FarmWorldController>() == null)
        {
            world.gameObject.AddComponent<FarmWorldController>();
        }

        // 1. Terrain Root (Expandable / Tileable grass map)
        var terrain = world.Find("Terrain");
        if (terrain == null)
        {
            var go = CreateRect(world, "Terrain", 0, 0, MapWidth, MapHeight);
            terrain = go.transform;
            terrain.SetAsFirstSibling();
        }
        var terrainRaw = terrain.GetComponent<RawImage>() ?? terrain.gameObject.AddComponent<RawImage>();
        var grassTex = Resources.Load<Texture2D>("World/Sprites/terrain_grass");
        if (grassTex != null)
        {
            grassTex.filterMode = FilterMode.Point;
            grassTex.wrapMode = TextureWrapMode.Repeat;
            terrainRaw.texture = grassTex;
        }
        terrainRaw.raycastTarget = false;
        terrainRaw.uvRect = new Rect(0, 0, MapWidth / 512f, MapHeight / 512f);
        SetRect(terrain as RectTransform, 0, 0, MapWidth, MapHeight);

        // 2. Buildings Root (Granjas y Estructuras)
        var buildings = world.Find("Buildings");
        if (buildings == null)
        {
            var go = CreateRect(world, "Buildings", 0, 0, MapWidth, MapHeight);
            buildings = go.transform;
        }
        SetRect(buildings as RectTransform, 0, 0, MapWidth, MapHeight);

        SetupBuilding(buildings, "Main Barn", "World/Sprites/barn_yellow", 750, 140, 160, 155);
        SetupBuilding(buildings, "Coop Shed", "World/Sprites/coop_shed", 380, 130, 140, 120);
        SetupBuilding(buildings, "Red Barn", "World/Sprites/barn_red", 780, 620, 155, 145);

        // 3. Animal Pens (Corrales de Animales)
        var pens = world.Find("Animal Pens");
        if (pens == null)
        {
            var go = CreateRect(world, "Animal Pens", 0, 0, MapWidth, MapHeight);
            pens = go.transform;
        }
        SetRect(pens as RectTransform, 0, 0, MapWidth, MapHeight);

        // Chicken pen
        var chickenPen = pens.Find("Chicken Pen");
        if (chickenPen == null)
        {
            var go = CreateRect(pens, "Chicken Pen", 340, 170, 200, 150);
            chickenPen = go.transform;
        }
        SetRect(chickenPen as RectTransform, 340, 170, 200, 150);

        // Pasture pen
        var pasturePen = pens.Find("Pasture Pen");
        if (pasturePen == null)
        {
            var go = CreateRect(pens, "Pasture Pen", 680, 350, 260, 185);
            pasturePen = go.transform;
        }
        SetRect(pasturePen as RectTransform, 680, 350, 260, 185);

        var fence = pasturePen.Find("Fence");
        if (fence == null)
        {
            var go = CreateRect(pasturePen, "Fence", 0, 0, 260, 175);
            fence = go.transform;
        }
        var fenceRaw = fence.GetComponent<RawImage>() ?? fence.gameObject.AddComponent<RawImage>();
        var fenceTex = Resources.Load<Texture2D>("World/Sprites/animal_pen");
        if (fenceTex != null)
        {
            fenceTex.filterMode = FilterMode.Point;
            fenceRaw.texture = fenceTex;
        }
        fenceRaw.raycastTarget = false;
        SetRect(fence as RectTransform, 0, 0, 260, 175);

        // 4. Crop Plots (Parcelas / Bancales de Cultivo)
        var crops = world.Find("Crop Plots");
        if (crops == null)
        {
            var go = CreateRect(world, "Crop Plots", 0, 0, MapWidth, MapHeight);
            crops = go.transform;
        }
        SetRect(crops as RectTransform, 0, 0, MapWidth, MapHeight);

        // Relocate any existing plots from world root into crops root
        for (int i = 0; i < 4; i++)
        {
            var unparented = world.Find("Plot " + i + " - Carrot") ?? world.Find("Plot " + i + " - Tomato")
                          ?? world.Find("Plot " + i + " - Pumpkin") ?? world.Find("Plot " + i + " - Cabbage")
                          ?? world.Find("Plot " + i);
            if (unparented != null && unparented.parent == world)
            {
                unparented.SetParent(crops, false);
            }
        }

        Vector2[] plotPos = {
            new Vector2(120, 260),
            new Vector2(340, 340),
            new Vector2(120, 430),
            new Vector2(340, 510)
        };
        string[] kinds = { "Carrot", "Tomato", "Pumpkin", "Cabbage" };

        for (int i = 0; i < 4; i++)
        {
            string pName = "Plot " + i + " - " + kinds[i];
            var plotT = crops.Find(pName) ?? crops.Find("Plot " + i);
            if (plotT == null)
            {
                var go = CreateRect(crops, pName, plotPos[i].x, plotPos[i].y, 204, 108);
                plotT = go.transform;
            }
            plotT.name = pName;
            var oldImg = plotT.GetComponent<Image>();
            if (oldImg != null) UnityEngine.Object.DestroyImmediate(oldImg);
            var raw = plotT.GetComponent<RawImage>() ?? plotT.gameObject.AddComponent<RawImage>();
            var soilTex = Resources.Load<Texture2D>("World/Sprites/soil_bed");
            if (soilTex != null)
            {
                soilTex.filterMode = FilterMode.Point;
                raw.texture = soilTex;
            }
            raw.raycastTarget = false;
            var plot = plotT.GetComponent<CropPlot>() ?? plotT.gameObject.AddComponent<CropPlot>();
            SetRect(plotT as RectTransform, plotPos[i].x, plotPos[i].y, 204, 108);
        }
    }

    static void SetupBuilding(Transform parent, string name, string resource, float x, float y, float w, float h)
    {
        var b = parent.Find(name);
        if (b == null)
        {
            var go = CreateRect(parent, name, x, y, w, h);
            b = go.transform;
        }
        var raw = b.GetComponent<RawImage>() ?? b.gameObject.AddComponent<RawImage>();
        var tex = Resources.Load<Texture2D>(resource);
        if (tex != null)
        {
            tex.filterMode = FilterMode.Point;
            raw.texture = tex;
        }
        raw.raycastTarget = false;
        SetRect(b as RectTransform, x, y, w, h);
    }

    static GameObject CreateRect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), x, y, w, h);
        return go;
    }

    static void SetRect(RectTransform rect, float x, float y, float w, float h)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }
}
