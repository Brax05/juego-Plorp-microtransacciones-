using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plorp ▸ Hornear UI en el canvas
///
/// The game builds most of its screens by code. This command runs that same code once, in the editor,
/// on PixelFarmCanvas and saves the result into the prefab. From then on every panel, button, label and
/// picture is a normal object in the hierarchy: move it, resize it, recolour it or change its picture and
/// the game keeps it (PixelUIFactory reuses what it finds in the canvas).
///
/// Run it again after adding new screens in code: what already exists is kept as you left it and only
/// the missing pieces are added. Scripts are not saved in the prefab (the game adds them when it starts).
/// </summary>
public static class PlorpCanvasBaker
{
    const string PrefabPath = "Assets/Prefabs/PixelFarmCanvas.prefab";

    // Pieces of the first canvas version that the game no longer shows.
    static readonly string[] Legacy =
    {
        "Page - Tienda", "Page - Logros", "Page - Mejoras", "Painted farm (old)", "Terrain", "Buildings",
    };

    [MenuItem("Plorp/Hornear UI en el canvas", priority = 0)]
    static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Plorp", "Sal del modo Play antes de hornear.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Hornear UI en el canvas",
                "Se va a construir toda la UI del juego dentro de PixelFarmCanvas.prefab para que puedas editarla.\n\n" +
                "Lo que ya esté en el canvas se mantiene como lo dejaste. ¿Continuar?", "Hornear", "Cancelar"))
            return;

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // Our scripts that were already in the prefab (farm world, crops, actors...) stay.
            var before = new HashSet<Component>(root.GetComponentsInChildren<MonoBehaviour>(true));

            var bootstrap = root.AddComponent<FarmUIBootstrap>();
            bootstrap.BakeInto();
            Object.DestroyImmediate(bootstrap);

            // Remove the scripts the build added: the game adds them again when it starts.
            int removed = 0;
            var ours = typeof(FarmUIBootstrap).Assembly;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || before.Contains(behaviour) || behaviour.GetType().Assembly != ours) continue;
                Object.DestroyImmediate(behaviour);
                removed++;
            }

            int legacy = 0;
            foreach (string name in Legacy)
            {
                var t = FarmUIBootstrap.FindChildRecursive(root.transform, name);
                if (t == null || t.gameObject.activeSelf) continue;
                Object.DestroyImmediate(t.gameObject);
                legacy++;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Plorp: UI horneada en " + PrefabPath + " (" + removed + " scripts quitados, " + legacy +
                      " piezas antiguas borradas). Ya puedes editar el layout en la escena Granja.");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Plorp", "No se pudo hornear: " + e.Message + "\nEl prefab no se modificó.", "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            // The build touched static state (game state, events): start clean.
            EditorUtility.RequestScriptReload();
        }
    }
}
