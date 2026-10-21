using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if PLORP_IAP
using UnityEditor.Purchasing;
#endif

/// <summary>Stores only the public Google Play licensing key, obfuscated with Unity IAP's tool.</summary>
public sealed class PurchaseValidationSetup : EditorWindow
{
    const string KeyPath = "Assets/Scripts/Shop/GooglePlayLicenseKey.cs";
    string publicKey = "";
    string error;

    [MenuItem("Plorp/Configurar validación de compras", priority = 11)]
    static void Open() => GetWindow<PurchaseValidationSetup>("Validación de compras");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Pega la clave pública RSA de LICENCIA de esta app en Google Play Console. " +
            "No es la clave privada de firma ni la contraseña del keystore.", MessageType.Info);
        EditorGUILayout.LabelField("Clave pública de Google Play (Base64)", EditorStyles.boldLabel);
        publicKey = EditorGUILayout.TextArea(publicKey, GUILayout.MinHeight(100));
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        EditorGUILayout.LabelField(GooglePlayLicenseKey.IsConfigured ? "Clave configurada" : "Falta configurar la clave");
#if PLORP_IAP
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(publicKey)))
            if (GUILayout.Button("Guardar clave y activar validación"))
            {
                try
                {
                    SaveKey(publicKey);
                    publicKey = "";
                    error = null;
                    ShowNotification(new GUIContent("Clave guardada. Unity recompilará los scripts."));
                }
                catch { error = "La clave no es una clave pública RSA válida. Copia la cadena Base64 completa desde Licencias."; }
            }
#else
        EditorGUILayout.HelpBox("Activa PLORP_IAP e instala Unity IAP para configurar las compras.", MessageType.Error);
#endif
    }

#if PLORP_IAP
    public static void SaveKey(string base64)
    {
        byte[] key = Convert.FromBase64String(base64);
        // Accept the SubjectPublicKeyInfo format used by Play, never a private key.
        GooglePlayPublicKeyFormat.Validate(key);
        var order = new int[key.Length / 20 + 1];
        byte[] tangled = TangleObfuscator.Obfuscate(key, order, out int xorKey);
        string source = "// Generated public Google Play license key. Do not replace with an upload signing key.\n" +
            "public static class GooglePlayLicenseKey\n{\n" +
            "    public static readonly bool IsConfigured = true;\n" +
            "    public static byte[] Data()\n    {\n" +
            "#if PLORP_IAP && UNITY_ANDROID && !UNITY_EDITOR\n" +
            "        return UnityEngine.Purchasing.Security.Obfuscator.DeObfuscate(\n" +
            "            System.Convert.FromBase64String(\"" + Convert.ToBase64String(tangled) + "\"),\n" +
            "            new int[] { " + string.Join(", ", order) + " }, " + xorKey + ");\n" +
            "#else\n        return null;\n#endif\n    }\n}\n";
        File.WriteAllText(KeyPath, source, new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(KeyPath);
    }
#endif
}

public sealed class PurchaseValidationBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        string symbols = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android);
        if (!symbols.Split(';').Contains("PLORP_IAP"))
            throw new BuildFailedException("Las compras Android requieren PLORP_IAP. Configúralo antes de generar el AAB.");
        if (!GooglePlayLicenseKey.IsConfigured)
            throw new BuildFailedException("Falta la clave pública de Google Play: Plorp > Configurar validación de compras.");
    }
}
