using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the scene look like the game. Most of the UI (Plorp's order card, Pepito, the painted farm with
/// clouds, Jardinería, Tienda, Logros, golden tickets...) is built by code when Play starts, so the plain
/// scene showed the old layout. This tool runs that same code in Edit mode on a <b>preview copy</b> of
/// PixelFarmCanvas:
///  - The copy is never saved (HideFlags.DontSave). The real canvas is hidden while the preview is on
///    and restored automatically before saving the scene, entering Play mode or recompiling.
///  - Window: Tools ▸ Plorp ▸ Vista previa de la escena (buttons for each tab and the title screen).
/// To change the layout itself, edit the scripts (FarmArtLayout, GardenScreen, StoreScreen...) and press
/// "Actualizar" again.
/// </summary>
[InitializeOnLoad]
public sealed class PlorpScenePreview : EditorWindow
{
    const string PreviewName = "PixelFarmCanvas (VISTA PREVIA - no se guarda)";
    const string OriginalKey = "PlorpScenePreview.original";
    const string RebuildKey = "PlorpScenePreview.rebuildAfterSave";
    const string TabKey = "PlorpScenePreview.tab";

    static readonly string[] Tabs = { "Granja", "Tienda", "Logros", "Jardinería", "Ajustes" };

    static PlorpScenePreview()
    {
        // A preview left over from before a recompile: remove it and give the real canvas back.
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Remove(); };

        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Remove();
        };
        EditorSceneManager.sceneSaving += (scene, path) =>
        {
            if (Find() == null) return;
            SessionState.SetBool(RebuildKey, true);
            Remove(); // the real canvas is saved active and untouched
        };
        EditorSceneManager.sceneSaved += scene =>
        {
            if (!SessionState.GetBool(RebuildKey, false)) return;
            SessionState.SetBool(RebuildKey, false);
            EditorApplication.delayCall += () => Build(SessionState.GetInt(TabKey, 0));
        };
    }

    [MenuItem("Tools/Plorp/Vista previa de la escena", priority = 0)]
    static void Open() => GetWindow<PlorpScenePreview>("Plorp · Vista previa").Show();

    [MenuItem("Tools/Plorp/Actualizar vista previa %#p", priority = 1)]
    static void BuildFromMenu() => Build(SessionState.GetInt(TabKey, 0));

    [MenuItem("Tools/Plorp/Quitar vista previa", priority = 2)]
    static void RemoveFromMenu() => Remove();

    void OnGUI()
    {
        bool on = Find() != null;
        EditorGUILayout.HelpBox(
            "Construye en la escena lo mismo que arma el código al dar Play (granja pintada, nubes, Plorp, " +
            "Pepito, Jardinería, Tienda, Logros...). Es una copia que NO se guarda: el canvas real queda intacto.",
            MessageType.Info);

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button(on ? "Actualizar vista previa" : "Crear vista previa", GUILayout.Height(32)))
            Build(SessionState.GetInt(TabKey, 0));

        GUI.enabled = on && !EditorApplication.isPlaying;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mostrar pestaña", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < Tabs.Length; i++)
                if (GUILayout.Button(Tabs[i])) ShowTab(i);
        }
        if (GUILayout.Button("Pantalla de inicio")) Bootstrap()?.PreviewStartScreen();
        if (GUILayout.Button("Seleccionar la vista previa")) Selection.activeGameObject = Find();

        EditorGUILayout.Space();
        if (GUILayout.Button("Quitar vista previa")) Remove();
        GUI.enabled = true;

        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("En Play el juego ya se ve completo.", MessageType.None);
    }

    // ---------- Build / remove ----------

    static GameObject Find()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == PreviewName) return root;
        return null;
    }

    static FarmUIBootstrap Bootstrap()
    {
        var preview = Find();
        return preview != null ? preview.GetComponent<FarmUIBootstrap>() : null;
    }

    static GameObject FindOriginal()
    {
        int id = SessionState.GetInt(OriginalKey, 0);
        if (id != 0 && EditorUtility.InstanceIDToObject(id) is GameObject saved) return saved;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == PreviewName) continue;
            if (root.GetComponentInChildren<Canvas>(true) == null) continue;
            if (FarmUIBootstrap.FindChildRecursive(root.transform, "Portrait screen") != null) return root;
        }
        return null;
    }

    static void Build(int tab)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Remove();

        var original = FindOriginal();
        if (original == null)
        {
            Debug.LogError("Plorp: no encontré PixelFarmCanvas (con 'Portrait screen') en la escena abierta.");
            return;
        }

        // Copy the canvas (a plain copy, not linked to the prefab, so the code may reorganize it freely).
        var preview = Object.Instantiate(original);
        preview.name = PreviewName;
        preview.SetActive(true);
        SessionState.SetInt(OriginalKey, original.GetInstanceID());
        original.SetActive(false);

        try
        {
            // Awake doesn't run in Edit mode, so the component just waits for BuildPreview().
            var bootstrap = preview.AddComponent<FarmUIBootstrap>();
            bootstrap.BuildPreview();
            bootstrap.PreviewTab(tab);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Debug.LogError("Plorp: la vista previa falló; se restauró el canvas real.");
            Remove();
            return;
        }

        MarkDontSave(preview);
        SessionState.SetInt(TabKey, tab);
        Selection.activeGameObject = preview;
        SceneView.RepaintAll();
    }

    static void ShowTab(int tab)
    {
        var bootstrap = Bootstrap();
        if (bootstrap == null) { Build(tab); return; }
        bootstrap.PreviewTab(tab);
        MarkDontSave(bootstrap.gameObject); // pages may have created new cards
        SessionState.SetInt(TabKey, tab);
        SceneView.RepaintAll();
    }

    static void MarkDontSave(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.hideFlags = HideFlags.DontSave;
    }

    static void Remove()
    {
        var preview = Find();
        if (preview != null) Object.DestroyImmediate(preview);

        // Give the real canvas back (it is only hidden while the preview exists).
        var original = FindOriginal();
        if (original != null && !original.activeSelf) original.SetActive(true);
        SessionState.EraseInt(OriginalKey);
        SceneView.RepaintAll();
    }
}
