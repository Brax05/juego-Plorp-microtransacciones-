using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility that converts the farm world from a static background image into
/// modular, editable scene and prefab components (Terrain, Buildings, Animal Pens, Crop Plots)
/// directly manipulable in the Unity IDE.
/// </summary>
public static class ModularFarmEditor
{
    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";

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
                var worldTransform = FarmUIBootstrap.FindChildRecursive(prefabRoot.transform, "Modular farm world");
                if (worldTransform != null)
                {
                    ConfigureModularWorld(worldTransform);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                    Debug.Log("<color=green>[ModularFarmEditor]</color> Successfully updated modular layout in " + PrefabPath);
                }
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            // 2. Ensure Canvas is present in SampleScene.unity
            const string ScenePath = "Assets/Scenes/SampleScene.unity";
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
                    var sceneWorld = FarmUIBootstrap.FindChildRecursive(sceneCanvas.transform, "Modular farm world");
                    if (sceneWorld != null)
                    {
                        ConfigureModularWorld(sceneWorld);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        Debug.Log("<color=green>[ModularFarmEditor]</color> Updated modular world in SampleScene.unity");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[ModularFarmEditor] Error upgrading layout: " + ex);
        }
    }

    public static void ConfigureModularWorld(Transform world)
    {
        // 1. Remove legacy static monolithic background
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

        // 2. Terrain Root (Expandable / Tileable grass)
        var terrain = world.Find("Terrain");
        if (terrain == null)
        {
            var go = CreateRect(world, "Terrain", 0, 0, 570, 530);
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
        terrainRaw.uvRect = new Rect(0, 0, 1.11f, 1.04f);
        SetRect(terrain as RectTransform, 0, 0, 570, 530);

        // 3. Buildings Root (Granjas y Estructuras)
        var buildings = world.Find("Buildings");
        if (buildings == null)
        {
            var go = CreateRect(world, "Buildings", 0, 0, 570, 530);
            buildings = go.transform;
        }
        SetRect(buildings as RectTransform, 0, 0, 570, 530);

        SetupBuilding(buildings, "Main Barn", "World/Sprites/barn_yellow", 375, 20, 160, 155);
        SetupBuilding(buildings, "Coop Shed", "World/Sprites/coop_shed", 175, 25, 140, 120);
        SetupBuilding(buildings, "Red Barn", "World/Sprites/barn_red", 400, 355, 155, 145);

        // 4. Animal Pens (Corrales de Animales)
        var pens = world.Find("Animal Pens");
        if (pens == null)
        {
            var go = CreateRect(world, "Animal Pens", 0, 0, 570, 530);
            pens = go.transform;
        }
        SetRect(pens as RectTransform, 0, 0, 570, 530);

        // Chicken pen
        var chickenPen = pens.Find("Chicken Pen");
        if (chickenPen == null)
        {
            var go = CreateRect(pens, "Chicken Pen", 155, 30, 175, 135);
            chickenPen = go.transform;
        }
        SetRect(chickenPen as RectTransform, 155, 30, 175, 135);

        // Pasture pen
        var pasturePen = pens.Find("Pasture Pen");
        if (pasturePen == null)
        {
            var go = CreateRect(pens, "Pasture Pen", 350, 180, 200, 145);
            pasturePen = go.transform;
        }
        SetRect(pasturePen as RectTransform, 350, 180, 200, 145);

        var fence = pasturePen.Find("Fence");
        if (fence == null)
        {
            var go = CreateRect(pasturePen, "Fence", 0, 0, 200, 140);
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
        SetRect(fence as RectTransform, 0, 0, 200, 140);

        // 5. Crop Plots (Parcelas / Bancales de Cultivo)
        var crops = world.Find("Crop Plots");
        if (crops == null)
        {
            var go = CreateRect(world, "Crop Plots", 0, 0, 570, 530);
            crops = go.transform;
        }
        SetRect(crops as RectTransform, 0, 0, 570, 530);

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
            new Vector2(25, 175),
            new Vector2(195, 240),
            new Vector2(25, 310),
            new Vector2(195, 375)
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
