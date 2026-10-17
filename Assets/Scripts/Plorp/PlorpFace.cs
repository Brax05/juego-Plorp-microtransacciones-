using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plorp's static pixel portrait with a slow idle bob and a "plorp!" squash when he reacts.
/// Plorp never talks: Pepito translates for him.
/// </summary>
public sealed class PlorpFace : MonoBehaviour
{
    const float ArtWidth = 29f, ArtHeight = 32f;   // UI/Characters/plorp_face.png

    RectTransform art;
    Vector2 home;
    float bumpStarted = -10f;
    float phase;

    /// <summary>Creates the portrait centered inside the box (x, y, size, size).</summary>
    public static PlorpFace Create(Transform parent, string name, float x, float y, float size)
    {
        float height = size;
        float width = size * ArtWidth / ArtHeight;
        var raw = new PixelUIFactory().Texture(parent, name, "UI/Characters/plorp_face",
            x + (size - width) / 2f, y, width, height);
        var face = raw.gameObject.AddComponent<PlorpFace>();
        face.art = raw.rectTransform;
        face.art.pivot = new Vector2(.5f, 0f); // squash from the bottom
        face.art.anchoredPosition += new Vector2(width / 2f, -height);
        face.home = face.art.anchoredPosition;
        face.phase = Random.value * 6f;
        return face;
    }

    /// <summary>Quick squash-and-stretch: Plorp says "plorp".</summary>
    public void Bump() => bumpStarted = Time.unscaledTime;

    void Update()
    {
        float now = Time.unscaledTime;
        float t = (now - bumpStarted) / .45f;
        float squash = t < 1f ? Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * .18f : 0f;
        art.localScale = new Vector3(1f + squash, 1f - squash, 1f);
        art.anchoredPosition = home + new Vector2(0f, Mathf.Sin(now * 1.6f + phase) * 1.5f);
    }
}
