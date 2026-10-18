using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the time of day and the season over the farm view (Módulo 5): orange sunset, blue night with
/// fireflies, and falling petals / leaves / snow depending on the season. Also the small season badge
/// on the HUD (season, crop of the season, sun or moon). Nothing here blocks taps on the farm.
/// </summary>
public sealed class FarmAmbience : MonoBehaviour
{
    const float ViewW = 570f, ViewH = 700f;
    const int ParticleCount = 22, FireflyCount = 14;

    RectTransform root;
    Image tint;
    readonly List<RectTransform> particles = new List<RectTransform>();
    readonly List<Image> particleImages = new List<Image>();
    readonly List<float> particleSeed = new List<float>();
    readonly List<RectTransform> fireflies = new List<RectTransform>();
    readonly List<Image> fireflyImages = new List<Image>();
    Season shownSeason = (Season)(-1);

    // HUD badge
    Text badgeSeason, badgeCrop;
    RawImage badgeCropIcon, badgeSky;
    float nextBadge;

    /// <summary>Called when the badge is tapped (Pepito explains the season).</summary>
    public Action BadgeTapped;

    public static FarmAmbience Create(FarmWorldController world, Transform portrait, int hudSibling)
    {
        var map = world.transform.parent as RectTransform;
        var viewport = map != null ? map.parent as RectTransform : null;
        if (viewport == null) return null;
        var ui = new PixelUIFactory();
        var rect = ui.Rect(viewport, "Ambience", 0, 0, ViewW, ViewH);
        var ambience = rect.gameObject.AddComponent<FarmAmbience>();
        ambience.Build(ui, rect, portrait, hudSibling);
        return ambience;
    }

    void Build(PixelUIFactory ui, RectTransform rect, Transform portrait, int hudSibling)
    {
        root = rect;
        tint = ui.Block(root, "Sky tint", 0, 0, ViewW, ViewH, new Color(0, 0, 0, 0));
        for (int i = 0; i < ParticleCount; i++)
        {
            var img = ui.Block(root, "Season particle", 0, 0, 4, 4, Color.white);
            particles.Add(img.rectTransform);
            particleImages.Add(img);
            particleSeed.Add(UnityEngine.Random.value * 100f);
            img.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(0f, ViewW), -UnityEngine.Random.Range(0f, ViewH));
        }
        for (int i = 0; i < FireflyCount; i++)
        {
            var img = ui.Block(root, "Firefly", 0, 0, 3, 3, new Color(1f, .95f, .4f, 0f));
            fireflies.Add(img.rectTransform);
            fireflyImages.Add(img);
        }

        // Season badge on the HUD, under the coins and gems.
        var badge = ui.Panel(portrait, "Season badge", 384, 138, 178, 44, PixelUIFactory.Hex("#B86F3F"));
        badge.SetSiblingIndex(Mathf.Clamp(hudSibling, 0, portrait.childCount - 1));
        badgeCropIcon = ui.Texture(badge, "Crop", "World/Sprites/carrot_2", 8, 6, 32, 32);
        badgeSeason = ui.Label(badge, "Season", "", 42, 5, 100, 18, 13, PixelUIFactory.Gold);
        badgeCrop = ui.Label(badge, "Bonus", "", 42, 22, 100, 16, 11, PixelUIFactory.Cream);
        badgeSky = ui.Texture(badge, "Sky", "UI/Garden/icon_sun", 146, 10, 24, 24);
        ui.Hit(badge, "Tap", 0, 0, 178, 44, () => BadgeTapped?.Invoke());
        RefreshBadge();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        // Sky colour: orange at sunset/dawn, dark blue at night.
        float dark = FarmClock.Darkness(out float warm);
        var night = new Color(.04f, .07f, .24f, .48f * dark);
        var sunset = new Color(1f, .5f, .2f, .2f * warm);
        float a = Mathf.Max(night.a, sunset.a);
        Color c = a <= 0f ? new Color(0, 0, 0, 0)
            : new Color((night.r * night.a + sunset.r * sunset.a) / (night.a + sunset.a + 1e-4f),
                        (night.g * night.a + sunset.g * sunset.a) / (night.a + sunset.a + 1e-4f),
                        (night.b * night.a + sunset.b * sunset.a) / (night.a + sunset.a + 1e-4f), a);
        tint.color = c;

        // Fireflies at night.
        for (int i = 0; i < fireflies.Count; i++)
        {
            float s = i * 12.9898f;
            float x = Mathf.Repeat(i * 97f + Mathf.Sin(now * .3f + s) * 60f + now * 6f, ViewW);
            float y = 120f + Mathf.Repeat(i * 53f, ViewH - 200f) + Mathf.Sin(now * .5f + s * 2f) * 30f;
            fireflies[i].anchoredPosition = new Vector2(x, -y);
            float blink = Mathf.Clamp01(Mathf.Sin(now * 2.3f + s) * 1.5f);
            fireflyImages[i].color = new Color(1f, .95f, .4f, dark * blink);
        }

        UpdateParticles(dt, now);

        if (now >= nextBadge)
        {
            nextBadge = now + 1f;
            RefreshBadge();
        }
    }

    void UpdateParticles(float dt, float now)
    {
        var season = FarmClock.CurrentSeason;
        if (season != shownSeason)
        {
            shownSeason = season;
            for (int i = 0; i < particles.Count; i++)
            {
                Vector2 size;
                Color color;
                switch (season)
                {
                    case Season.Primavera:
                        size = new Vector2(5, 3);
                        color = i % 3 == 0 ? new Color(1f, 1f, 1f, .85f) : new Color(1f, .62f, .78f, .85f);
                        break;
                    case Season.Otono:
                        size = new Vector2(6, 4);
                        color = i % 3 == 0 ? new Color(.85f, .35f, .15f, .9f) : i % 3 == 1
                            ? new Color(.95f, .62f, .2f, .9f) : new Color(.7f, .45f, .2f, .9f);
                        break;
                    case Season.Invierno:
                        size = i % 4 == 0 ? new Vector2(4, 4) : new Vector2(3, 3);
                        color = new Color(1f, 1f, 1f, .9f);
                        break;
                    default: // Verano: a few drifting pollen sparkles
                        size = new Vector2(3, 3);
                        color = new Color(1f, .95f, .6f, i % 2 == 0 ? .6f : 0f);
                        break;
                }
                particles[i].sizeDelta = size;
                particleImages[i].color = color;
            }
        }

        float fall = season == Season.Invierno ? 38f : season == Season.Verano ? 8f : 46f;
        for (int i = 0; i < particles.Count; i++)
        {
            float s = particleSeed[i];
            var p = particles[i].anchoredPosition;
            p.y -= fall * (.7f + .6f * Mathf.Repeat(s, 1f)) * dt;
            p.x += (Mathf.Sin(now * 1.3f + s) * 22f + (season == Season.Otono ? 14f : 6f)) * dt;
            if (p.y < -ViewH || p.x > ViewW + 10f)
                p = new Vector2(UnityEngine.Random.Range(-40f, ViewW), 8f);
            particles[i].anchoredPosition = p;
            particles[i].localEulerAngles = new Vector3(0, 0, season == Season.Invierno ? 0f : Mathf.Sin(now * 2f + s) * 40f);
        }
    }

    void RefreshBadge()
    {
        if (badgeSeason == null) return;
        var season = FarmClock.CurrentSeason;
        var crop = FarmClock.SeasonCrop(season);
        badgeSeason.text = FarmClock.SeasonName(season);
        badgeCrop.text = FarmItems.ForCrop(crop).Name + " x1,5";
        SetTexture(badgeCropIcon, FarmItems.ForCrop(crop).Icon);
        SetTexture(badgeSky, FarmClock.IsNight ? "UI/Garden/icon_moon" : "UI/Garden/icon_sun");
    }

    static void SetTexture(RawImage image, string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null || image.texture == tex) return;
        tex.filterMode = FilterMode.Point;
        image.texture = tex;
    }
}
