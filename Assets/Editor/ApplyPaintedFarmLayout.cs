using UnityEditor;
using UnityEngine;

/// <summary>
/// The painted-farm layout is applied automatically when the game starts. This menu bakes it into the
/// prefab too, so the Scene view shows the same farm as Play mode.
/// </summary>
public static class ApplyPaintedFarmLayout
{
    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";

    [MenuItem("Tools/Plorp/Aplicar granja pintada al prefab")]
    public static void Build()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var world = FarmUIBootstrap.FindChildRecursive(root.transform, "Modular farm world") as RectTransform;
            if (world == null)
            {
                Debug.LogError("No se encontró 'Modular farm world' en " + PrefabPath);
                return;
            }
            FarmArtLayout.Apply(world);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Granja pintada aplicada al prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
