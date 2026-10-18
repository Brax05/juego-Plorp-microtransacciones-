using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ABDUCCIÓN" (Módulo 7): cows and sheep escaped from the farm and run across the field at night.
/// Hold your finger on the screen: Plorp's ship follows it and turns on the beam. Keep the beam on an
/// animal for a moment to lift it (vaca +2, oveja +1). Careful: if you lift Pepito it's −3.
/// 30 seconds. Rewards: coins per point and gems for good scores.
/// </summary>
public sealed class AbductionGame : ArcadeGame
{
    const float GroundY = 500f;
    const float BeamHalf = 34f, LiftTime = .45f;

    sealed class Walker
    {
        public RectTransform rect;
        public RawImage art;
        public float x, speed, beam, lift;
        public int points;
        public bool lifting, isPepito;
    }

    RectTransform ship, beam;
    Image beamImage;
    float shipX = AreaW / 2f, spawnIn;
    readonly List<Walker> walkers = new List<Walker>();

    protected override string GameId => "abduction";
    protected override string Title => "ABDUCCIÓN";
    protected override string HowTo =>
        "Mantén el dedo apretado: la nave te sigue y prende el rayo. Sube vacas (+2) y ovejas (+1). ¡A Pepito no! (−3)";

    protected override void BuildArea()
    {
        ui.Texture(area, "Background", "abduction_bg", 0, 0, AreaW, AreaH); // Art/Minigames/abduction_bg.png

        beam = ui.Rect(area, "Beam", 0, 0, BeamHalf * 2f, GroundY);
        beamImage = beam.gameObject.GetOrAdd<Image>();
        beamImage.color = new Color(.6f, 1f, .6f, 0f);
        beamImage.raycastTarget = false;

        ship = Sprite(area, "ufo_plorp", 104, 92).rectTransform;
    }

    protected override void ResetRound()
    {
        foreach (var w in walkers) if (w.rect != null) UiUtil.Kill(w.rect.gameObject);
        walkers.Clear();
        shipX = AreaW / 2f;
        spawnIn = .5f;
        Place(ship, shipX, 70f);
    }

    protected override void Tick(float dt)
    {
        float now = Time.unscaledTime;
        bool on = pointer.Pressed;
        if (on) shipX = Mathf.MoveTowards(shipX, Mathf.Clamp(pointer.Position.x, 50f, AreaW - 50f), 520f * dt);
        Place(ship, shipX, 70f + Mathf.Sin(now * 3f) * 4f);
        beam.anchoredPosition = new Vector2(shipX - BeamHalf, -100f);
        beam.sizeDelta = new Vector2(BeamHalf * 2f, GroundY - 80f);
        beamImage.color = new Color(.6f, 1f, .6f, on ? .22f + .08f * Mathf.Sin(now * 20f) : 0f);

        // New runaways come in from both sides, faster as time goes on.
        spawnIn -= dt;
        if (spawnIn <= 0f)
        {
            spawnIn = Random.Range(.7f, 1.4f);
            Spawn();
        }

        for (int i = walkers.Count - 1; i >= 0; i--)
        {
            var w = walkers[i];
            if (w.lifting)
            {
                w.lift += dt / .5f;
                float y = Mathf.Lerp(GroundY - 20f, 100f, w.lift);
                w.rect.localScale = Vector3.one * Mathf.Lerp(1f, .3f, w.lift);
                Place(w.rect, Mathf.Lerp(w.x, shipX, w.lift), y);
                if (w.lift >= 1f) Remove(i);
                continue;
            }
            w.x += w.speed * dt;
            float bob = Mathf.Abs(Mathf.Sin(now * 10f + w.speed)) * 3f;
            Place(w.rect, w.x, GroundY - 20f - bob);
            if (on && Mathf.Abs(w.x - shipX) < BeamHalf)
            {
                w.beam += dt;
                w.art.color = Color.Lerp(Color.white, new Color(.6f, 1f, .6f), w.beam / LiftTime);
                if (w.beam >= LiftTime)
                {
                    w.lifting = true;
                    GameAudio.Play(w.isPepito ? "sfx_pepito_meow" : "sfx_beam", .8f, .1f);
                    score += w.points;
                    Float(new Vector2(w.x, GroundY - 70f), (w.points > 0 ? "+" : "") + w.points,
                        w.points > 0 ? PixelUIFactory.Gold : PixelUIFactory.Hex("#FF7B6B"));
                    if (w.isPepito) message("Pepito: \"¡Miau! ¡Bájame! ¡Soy el traductor!\"");
                }
            }
            else
            {
                w.beam = Mathf.Max(0f, w.beam - dt);
                w.art.color = Color.white;
            }
            if (w.x < -60f || w.x > AreaW + 60f) Remove(i);
        }
    }

    void Spawn()
    {
        float roll = Random.value;
        bool pepito = roll < .12f;
        bool cow = !pepito && roll < .5f;
        bool fromLeft = Random.value < .5f;
        var holder = ui.Rect(area, "Walker", 0, 0, 10, 10);
        holder.pivot = new Vector2(.5f, .5f);
        RawImage art;
        if (pepito)
        {
            art = ui.Texture(holder, "Art", "pepito_talk", -24, 24, 48, 48);
            art.uvRect = new Rect(0f, 0f, 1f / SpriteStripAnimator.FrameCount, 1f);
        }
        else art = ui.Texture(holder, "Art", cow ? "cow" : "sheep_white", -28, 26, 56, 52);
        // Face the way they run.
        if (!pepito && !fromLeft) art.uvRect = new Rect(1f, 0f, -1f, 1f);
        var w = new Walker
        {
            rect = holder,
            art = art,
            x = fromLeft ? -40f : AreaW + 40f,
            speed = (fromLeft ? 1f : -1f) * Random.Range(70f, 130f),
            points = pepito ? -3 : cow ? 2 : 1,
            isPepito = pepito,
        };
        walkers.Add(w);
        // The touch layer must stay on top.
        pointer.transform.SetAsLastSibling();
    }

    void Remove(int i)
    {
        if (walkers[i].rect != null) UiUtil.Kill(walkers[i].rect.gameObject);
        walkers.RemoveAt(i);
    }

    protected override string GiveRewards(int finalScore)
    {
        long coins = finalScore * CoinsPerPoint(8);
        int gems = finalScore >= 24 ? 3 : finalScore >= 16 ? 2 : finalScore >= 10 ? 1 : 0;
        if (coins > 0) state.BuyGold(0, coins);
        if (gems > 0) state.AddGems(gems);
        if (finalScore <= 0) return "Plorp no subió a nadie... Pepito dice que la nave está bien, que fuiste tú.";
        return "+" + FarmGameState.Format(coins) + " monedas" + (gems > 0 ? "  ·  +" + gems + " gemas" : "") +
               "\nPlorp devolvió los animales a la granja. Dice \"plorp\".";
    }
}
