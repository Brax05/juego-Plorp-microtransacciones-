using UnityEditor;
using UnityEngine;

public static class AndroidVersionCodeTool
{
    const int MaxGooglePlayVersionCode = 2100000000;

    [MenuItem("Plorp/Subir código de versión Android (+1)", priority = 10)]
    static void Increment()
    {
        int current = PlayerSettings.Android.bundleVersionCode;
        if (current >= MaxGooglePlayVersionCode)
        {
            EditorUtility.DisplayDialog("Código de versión Android",
                "El código de versión ya alcanzó el máximo permitido por Google Play.", "OK");
            return;
        }

        int next = current + 1;
        if (!EditorUtility.DisplayDialog("Código de versión Android",
                $"Cambiar el código de versión Android de {current} a {next}?",
                "Subir", "Cancelar"))
            return;

        PlayerSettings.Android.bundleVersionCode = next;
        Debug.Log($"Código de versión Android: {current} → {next}");
    }
}
