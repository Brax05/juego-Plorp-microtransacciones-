#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>Gives every Editor compilation a clean test save. This file is excluded from builds.</summary>
[InitializeOnLoad]
public static class ResetFarmProgressOnCompile
{
    static ResetFarmProgressOnCompile()
    {
        CompilationPipeline.compilationFinished -= OnCompilationFinished;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnCompilationFinished(object context)
    {
        ResetProgress("después de compilar");
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            ResetProgress("antes de entrar en Play Mode");
    }

    static void ResetProgress(string moment)
    {
        FarmGameState.ResetSavedProgress();
        Debug.Log("Plorp Farm: progreso de prueba reiniciado " + moment + ".");
    }
}
#endif
