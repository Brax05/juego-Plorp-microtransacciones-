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
    const float ViewW = 570f, AuthoredH = 700f; // ScreenLayout grows the overlay with the farm
    static float ViewH => ScreenLayout.FarmViewHeight;
    const int ParticleCount = 22, FireflyCount = 14;

    RectTransform root;
    Image tint;
    readonly List<RectTransform> particles = new List<RectTransform>();
    readonly List<RawImage> particleImages = new List<RawImage>();
    readonly List<float> particleSeed = new List<float>();
    readonly List<RectTransform> fireflies = new List<RectTransform>();
    readonly List<RawImage> fireflyImages = new List<RawImage>();
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
        var rect = ui.Rect(viewport, "Ambience", 0, 0, ViewW, AuthoredH);
        var ambience = rect.gameObject.GetOrAdd<FarmAmbience>();
        ambience.Build(ui, rect, portrait, hudSibling);
        return ambience;
    }

    void Build(PixelUIFactory ui, RectTransform rect, Transform portrait, int hudSibling)
    {
        root = rect;
        tint = ui.Block(root, "Sky tint", 0, 0, ViewW, AuthoredH, new Color(0, 0, 0, 0));
        for (int i = 0; i < ParticleCount; i++)
        {
            var img = ui.Texture(root, "Season particle", "fx_snow", 0, 0, 4, 4); // Art/Effects/fx_*.png
            particles.Add(img.rectTransform);
            particleImages.Add(img);
            particleSeed.Add(UnityEngine.Random.value * 100f);
            img.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(0f, ViewW), -UnityEngine.Random.Range(0f, ViewH));
        }
        for (int i = 0; i < FireflyCount; i++)
        {
            var img = ui.Texture(root, "Firefly", "fx_firefly", 0, 0, 3, 3);
            img.color = new Color(1f, 1f, 1f, 0f);
            fireflies.Add(img.rectTransform);
            fireflyImages.Add(img);
        }

        // Season badge on the HUD, under the coins and gems.
        var badge = ui.Panel(portrait, "Season badge", 384, 138, 178, 44, PixelUIFactory.Hex("#B86F3F"));
        badge.SetSiblingIndex(Mathf.Clamp(hudSibling, 0, portrait.childCount - 1));
        badgeCropIcon = ui.Texture(badge, "Crop", "carrot_2", 8, 6, 32, 32);
        badgeSeason = ui.Label(badge, "Season", "", 42, 5, 100, 18, 13, PixelUIFactory.Gold);
        badgeCrop = ui.Label(badge, "Bonus", "", 42, 22, 100, 16, 11, PixelUIFactory.Cream);
        badgeSky = ui.Texture(badge, "Sky", "icon_sun", 146, 10, 24, 24);
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
            fireflyImages[i].color = new Color(1f, 1f, 1f, dark * blink);
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
                // Each season's particle is a small PNG in Art/Effects.
                string picture;
                float alpha = 1f;
                switch (season)
                {
                    case Season.Primavera: picture = i % 3 == 0 ? "fx_petal_white" : "fx_petal"; break;
                    case Season.Otono: picture = i % 3 == 0 ? "fx_leaf_red" : i % 3 == 1 ? "fx_leaf_orange" : "fx_leaf_brown"; break;
                    case Season.Invierno: picture = "fx_snow"; break;
                    default: picture = "fx_pollen"; alpha = i % 2 == 0 ? 1f : 0f; break; // Verano: a few pollen sparkles
                }
                var tex = Art.Tex(picture);
                particleImages[i].texture = tex;
                if (tex != null) particles[i].sizeDelta = new Vector2(tex.width, tex.height) * (season == Season.Invierno && i % 4 == 0 ? 1.4f : 1f);
                particleImages[i].color = new Color(1f, 1f, 1f, alpha);
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
        UiUtil.SetTexture(badgeCropIcon, FarmItems.ForCrop(crop).Icon);
        UiUtil.SetTexture(badgeSky, FarmClock.IsNight ? "icon_moon" : "icon_sun");
    }

}
