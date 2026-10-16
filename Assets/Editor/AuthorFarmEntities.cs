using UnityEditor;
using UnityEngine;

/// <summary>One-time migration of runtime positions into editable prefab objects.</summary>
public static class AuthorFarmEntities
{
    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";
    const string MarkerName = "Authored entity layout";

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

    [MenuItem("Tools/Plorp/Create Editable Farm Entities")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var world = FarmUIBootstrap.FindChildRecursive(root.transform, "Modular farm world");
            if (world == null || world.Find(MarkerName) != null) return;

            var ui = new PixelUIFactory();
            var plots = world.GetComponentsInChildren<CropPlot>(true);
            string[] kinds = { "carrot", "tomato", "pumpkin", "cabbage" };
            for (int p = 0; p < plots.Length; p++)
            {
                for (int c = 0; c < CropPlot.LocalCropPositions.Length; c++)
                {
                    string name = "Crop " + p + "." + c;
                    if (plots[p].transform.Find(name) != null) continue;
                    Vector2 pos = CropPlot.LocalCropPositions[c];
                    var rect = ui.Rect(plots[p].transform, name, pos.x, pos.y, 32, 32);
                    rect.gameObject.AddComponent<CropCell>();
                    ui.Texture(rect, "Sprite art", "World/Sprites/" + kinds[p % kinds.Length] + "_2",
                        0, 0, 32, 32);
                }
            }

            var pens = world.Find("Animal Pens");
            var chickenPen = pens != null ? pens.Find("Chicken Pen") : null;
            var pasturePen = pens != null ? pens.Find("Pasture Pen") : null;
            for (int i = 0; i < 6; i++)
            {
                int number = i + 1;
                AddActor(ui, chickenPen, "chicken-" + number,
                    i % 2 == 0 ? "chicken_white" : "chicken_brown", ChickenHomes[i], 35, 35);
                AddActor(ui, pasturePen, "sheep-" + number,
                    i % 2 == 0 ? "sheep_white" : "sheep_gray", SheepHomes[i], 36, 35);
                AddActor(ui, pasturePen, "cow-" + number, "cow", CowHomes[i], 38, 36);
            }

            AddActor(ui, world, "worker-1", "worker_blue", new Vector2(280, 420), 35, 43);
            AddActor(ui, world, "worker-2", "worker_red", new Vector2(650, 560), 35, 43);
            AddActor(ui, world, "worker-3", "worker_blue", new Vector2(500, 380), 35, 43);
            AddActor(ui, world, "worker-4", "worker_red", new Vector2(220, 550), 35, 43);

            ui.Rect(world, MarkerName, 0, 0, 0, 0);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void AddActor(PixelUIFactory ui, Transform parent, string id, string texture,
        Vector2 position, float width, float height)
    {
        if (parent == null || parent.Find(id) != null) return;
        var rect = ui.Rect(parent, id, position.x, position.y, width, height);
        rect.gameObject.AddComponent<WorldActor>();
        ui.Texture(rect, "Sprite art", "World/Sprites/" + texture, 0, 0, width, height);
    }
}
