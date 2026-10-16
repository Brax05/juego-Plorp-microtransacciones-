using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Splits the decorative atlas into editable pieces without changing its artwork.</summary>
public static class ModularizeFarmCanvas
{
    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";

    [MenuItem("Tools/Plorp/Modularize Farm Canvas")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var portrait = Find(root.transform, "Portrait screen");
            var frame = Find(portrait, "Pixel UI frame");
            if (frame == null) throw new System.InvalidOperationException("Pixel UI frame is missing");
            var source = frame.GetComponent<RawImage>();
            if (source == null || source.texture == null)
                throw new System.InvalidOperationException("Pixel UI frame has no atlas texture");

            var existing = Find(portrait, "Modular chrome");
            if (existing == null)
            {
                var group = new GameObject("Modular chrome", typeof(RectTransform));
                group.transform.SetParent(portrait, false);
                group.transform.SetSiblingIndex(frame.GetSiblingIndex() + 1);
                var rect = (RectTransform)group.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(570, 1018);

                Add(group.transform, source.texture, "Level header", 0, 0, 350, 114);
                Add(group.transform, source.texture, "Currencies", 350, 0, 220, 114);
                Add(group.transform, source.texture, "Harvest bar", 0, 526, 570, 108);
                for (int i = 0; i < 3; i++)
                    Add(group.transform, source.texture, "Upgrade row " + i, 0, 634 + i * 104, 570, 104);
                for (int i = 0; i < 5; i++)
                    Add(group.transform, source.texture, "Navigation tile " + i, i * 114, 946, 114, 72);
                frame.gameObject.SetActive(false);
            }

            var ui = new PixelUIFactory();
            var progress = Find(portrait, "Level progress");
            if (progress == null)
            {
                var progressRect = ui.Rect(portrait, "Level progress", 98, 66, 0, 22);
                var image = progressRect.gameObject.AddComponent<Image>();
                image.color = PixelUIFactory.Gold;
                image.raycastTarget = false;
            }

            string[] tabs = { "Granja", "Tienda", "Logros", "Mejoras", "Ajustes" };
            string[] icons = { "granja", "tienda", "trofeos", "mejora", "ajustes" };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (Find(portrait, "Navigation icon " + tabs[i]) == null)
                    ui.Texture(portrait, "Navigation icon " + tabs[i], "UI/Icons/" + icons[i],
                        30 + i * 114, 956, 54, 54);
                if (Find(portrait, "Navigation label " + tabs[i]) == null)
                    ui.Label(portrait, "Navigation label " + tabs[i], tabs[i].ToUpperInvariant(),
                        5 + i * 114, 987, 104, 27, 15, Color.white, TextAnchor.MiddleCenter);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Add(Transform parent, Texture texture, string name, int x, int y, int width, int height)
    {
        var part = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        part.transform.SetParent(parent, false);
        var rect = (RectTransform)part.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        var image = part.GetComponent<RawImage>();
        image.texture = texture;
        image.uvRect = new Rect(x / 570f, 1f - (y + height) / 1018f,
            width / 570f, height / 1018f);
        image.raycastTarget = false;
    }

    static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var result = Find(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
