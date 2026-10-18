using UnityEngine;
using UnityEngine.UI;

/// <summary>Small helpers shared by every screen (no duplicated copies in each class).</summary>
public static class UiUtil
{
    /// <summary>The component if the object already has it (baked in the canvas), otherwise a new one.</summary>
    public static T GetOrAdd<T>(this GameObject go) where T : Component
    {
        var existing = go.GetComponent<T>();
        return existing != null ? existing : go.AddComponent<T>();
    }

    /// <summary>
    /// Destroys a UI object right away for the layout: it is taken out of its parent first, so a new one
    /// with the same name can be created in the same frame. Works in the editor (bake) too.
    /// </summary>
    public static void Kill(Object target)
    {
        if (target == null) return;
        var go = target as GameObject;
        if (go == null && target is Component c && (c is Transform)) go = c.gameObject;
        if (go != null) go.transform.SetParent(null, false);
        if (Application.isPlaying) Object.Destroy(go != null ? go : target);
        else Object.DestroyImmediate(go != null ? go : target);
    }

    /// <summary>Sets a picture by file name (see <see cref="Art"/>) only if it changed.</summary>
    public static void SetTexture(RawImage image, string picture)
    {
        if (image == null) return;
        var texture = Art.Tex(picture);
        if (texture != null && image.texture != texture) image.texture = texture;
    }

    /// <summary>Overshooting ease used by every pop-in animation.</summary>
    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x = Mathf.Clamp01(x);
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    /// <summary>"45 s", "2 min", "1:30".</summary>
    public static string Duration(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        if (seconds < 60) return seconds + " s";
        if (seconds < 3600) return seconds % 60 == 0 ? seconds / 60 + " min" : seconds / 60 + ":" + (seconds % 60).ToString("00");
        int h = seconds / 3600, m = seconds % 3600 / 60;
        return m == 0 ? h + " h" : h + " h " + m + " min";
    }

    /// <summary>Changes the caption size of an ActionButton called <paramref name="button"/>.</summary>
    public static void CaptionSize(Transform parent, string button, int size)
    {
        var caption = parent != null ? parent.Find(button)?.Find("Caption")?.GetComponent<Text>() : null;
        if (caption != null) caption.fontSize = size;
    }

    /// <summary>Recolours an ActionButton called <paramref name="button"/>.</summary>
    public static void ButtonColor(Transform parent, string button, Color color)
    {
        var face = parent != null ? parent.Find(button)?.Find("Face")?.GetComponent<Image>() : null;
        if (face != null) face.color = color;
    }
}

/// <summary>Text that rises and fades out ("+5", "¡Miau!").</summary>
public sealed class RisingText : MonoBehaviour
{
    float age, life = 1f, speed = 40f;
    Text text;

    public static void Attach(Text label, float life = 1f, float speed = 40f)
    {
        var f = label.gameObject.GetOrAdd<RisingText>();
        f.life = life;
        f.speed = speed;
        f.text = label;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        transform.localPosition += new Vector3(0f, speed * Time.unscaledDeltaTime, 0f);
        if (text != null) { var c = text.color; c.a = 1f - age / life; text.color = c; }
        if (age >= life) UiUtil.Kill(gameObject);
    }
}
