using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools ▸ Plorp ▸ Crear escena de inicio: creates Assets/Scenes/Inicio.unity (camera + BootScene) and
/// puts it first in Build Settings, with the game scene (Granja) second. Safe to run again.
/// </summary>
public static class CreateBootScene
{
    const string BootPath = "Assets/Scenes/Inicio.unity";
    const string GamePath = "Assets/Scenes/Granja.unity";

    [MenuItem("Tools/Plorp/Crear escena de inicio", priority = 20)]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previous = EditorSceneManager.GetActiveScene().path;

        if (!File.Exists(BootPath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.08f, .07f, .17f);
            cam.orthographic = true;
            new GameObject("Inicio", typeof(BootScene));
            EditorSceneManager.SaveScene(scene, BootPath);
            Debug.Log("Plorp: escena creada en " + BootPath);
        }

        // Build Settings: Inicio first, then the game, then anything else that was there.
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(BootPath, true),
            new EditorBuildSettingsScene(GamePath, true),
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path != BootPath && s.path != GamePath && File.Exists(s.path)) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("Plorp: Build Settings → 0 Inicio, 1 Granja.");

        if (!string.IsNullOrEmpty(previous) && previous != BootPath) EditorSceneManager.OpenScene(previous);
    }
}
