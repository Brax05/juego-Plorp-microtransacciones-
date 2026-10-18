using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every picture of the game lives in <c>Assets/Resources/Art/&lt;sección&gt;/</c> (Farm, Garden, Kitchen,
/// House, Items, Icons...). Code asks for a picture only by its file name ("carrot_2", "icon_gem"), so files
/// can be replaced or moved between section folders without touching code — just keep the name.
/// Pixel art: every texture is loaded with point filtering.
/// </summary>
public static class Art
{
    const string Root = "Art";
    static Dictionary<string, Texture2D> byName;

    /// <summary>The picture called <paramref name="name"/> (a path is accepted too: only the file name counts).</summary>
    public static Texture2D Tex(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name.Substring(slash + 1);
        if (byName == null) Index();
        if (byName.TryGetValue(name, out var texture) && texture != null) return texture;
        Debug.LogWarning("Plorp: no encontré la imagen '" + name + "' en Resources/" + Root + ".");
        return null;
    }

    static void Index()
    {
        byName = new Dictionary<string, Texture2D>();
        foreach (var texture in Resources.LoadAll<Texture2D>(Root))
        {
            texture.filterMode = FilterMode.Point;
            if (byName.ContainsKey(texture.name))
                Debug.LogWarning("Plorp: hay dos imágenes llamadas '" + texture.name + "' en Resources/" + Root + ".");
            byName[texture.name] = texture;
        }
    }
}
