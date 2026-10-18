using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "ATRAPA HUEVOS" (Módulo 8): the hens on the perch drop eggs; slide the basket with your finger.
/// Egg +1, golden egg +5, stone −3. They fall faster and faster. 30 seconds.
/// The eggs you catch go to the barn (for Plorp and the kitchen) and give coins; good scores give gems.
/// </summary>
public sealed class EggCatchGame : ArcadeGame
{
    const float PerchY = 96f, BasketY = 500f, BasketW = 96f;
    static readonly float[] HenX = { 70f, 190f, 310f, 430f };

    sealed class Drop
    {
        public RectTransform rect;
        public float x, y, speed;
        public int kind; // 0 egg, 1 golden, 2 stone
    }

    RectTransform basket;
    RawImage basketArt;
    readonly RectTransform[] hens = new RectTransform[4];
    readonly List<Drop> drops = new List<Drop>();
    float basketX = AreaW / 2f, dropIn, elapsed, flash;
    int eggsCaught;

    protected override string GameId => "eggs";
    protected override string Title => "ATRAPA HUEVOS";
    protected override string HowTo =>
        "Arrastra el dedo para mover la canasta. Huevo +1, huevo dorado +5, piedra −3. Los huevos van a tu granero.";

    protected override void BuildArea()
    {
        ui.Texture(area, "Background", "eggs_bg", 0, 0, AreaW, AreaH); // Art/Minigames/eggs_bg.png
        for (int i = 0; i < hens.Length; i++)
        {
            hens[i] = Sprite(area, i % 2 == 0 ? "chicken_white" : "chicken_brown", 60, 60).rectTransform;
            Place(hens[i], HenX[i], PerchY);
        }
        basketArt = Sprite(area, "basket", BasketW, 54);
        basket = basketArt.rectTransform;
    }

    protected override void ResetRound()
    {
        foreach (var d in drops) if (d.rect != null) UiUtil.Kill(d.rect.gameObject);
        drops.Clear();
        basketX = AreaW / 2f;
        dropIn = .6f;
        elapsed = 0f;
        eggsCaught = 0;
        Place(basket, basketX, BasketY);
    }

    protected override void Tick(float dt)
    {
        elapsed += dt;
        float now = Time.unscaledTime;
        if (pointer.Pressed) basketX = Mathf.MoveTowards(basketX, Mathf.Clamp(pointer.Position.x, BasketW / 2f, AreaW - BasketW / 2f), 900f * dt);
        Place(basket, basketX, BasketY);
        flash = Mathf.Max(0f, flash - dt);
        basketArt.color = flash > 0f ? new Color(1f, .5f, .5f) : Color.white;

        for (int i = 0; i < hens.Length; i++)
            Place(hens[i], HenX[i], PerchY + Mathf.Abs(Mathf.Sin(now * 3f + i)) * -3f);

        // Faster drops and faster falling as the round goes on.
        float pace = 1f + elapsed / 15f;
        dropIn -= dt;
        if (dropIn <= 0f)
        {
            dropIn = Random.Range(.55f, 1.0f) / pace;
            Spawn(pace);
        }

        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            d.y += d.speed * dt;
            Place(d.rect, d.x, d.y);
            d.rect.localEulerAngles = new Vector3(0, 0, d.kind == 2 ? d.y * 2f : Mathf.Sin(d.y * .05f) * 10f);
            if (d.y >= BasketY - 18f && d.y <= BasketY + 6f && Mathf.Abs(d.x - basketX) < BasketW / 2f)
            {
                int points = d.kind == 0 ? 1 : d.kind == 1 ? 5 : -3;
                score += points;
                if (d.kind == 0) eggsCaught += 1;
                if (d.kind == 1) eggsCaught += 3;
                if (d.kind == 2) flash = .3f;
                Float(new Vector2(d.x, BasketY - 50f), (points > 0 ? "+" : "") + points,
                    points > 0 ? (d.kind == 1 ? PixelUIFactory.Gold : Color.white) : PixelUIFactory.Hex("#FF7B6B"));
                Remove(i);
                continue;
            }
            if (d.y > AreaH + 20f) Remove(i);
        }
    }

    void Spawn(float pace)
    {
        int hen = Random.Range(0, hens.Length);
        float roll = Random.value;
        int kind = roll < .08f ? 1 : roll < .30f ? 2 : 0;
        string path = kind == 0 ? "egg_white" : kind == 1 ? "egg_gold" : "stone";
        var img = Sprite(area, path, kind == 2 ? 30 : 26, kind == 2 ? 26 : 32);
        var d = new Drop { rect = img.rectTransform, x = HenX[hen], y = PerchY + 20f, speed = Random.Range(170f, 230f) * pace, kind = kind };
        Place(d.rect, d.x, d.y);
        drops.Add(d);
        basket.SetAsLastSibling();
        pointer.transform.SetAsLastSibling();
    }

    void Remove(int i)
    {
        if (drops[i].rect != null) UiUtil.Kill(drops[i].rect.gameObject);
        drops.RemoveAt(i);
    }

    protected override string GiveRewards(int finalScore)
    {
        long value = FarmEconomy.UnitValue(state, FarmItems.Egg);
        long coins = eggsCaught * value + Mathf.Max(0, finalScore) * CoinsPerPoint(3);
        int gems = finalScore >= 40 ? 3 : finalScore >= 28 ? 2 : finalScore >= 18 ? 1 : 0;
        if (eggsCaught > 0) state.Collect(coins, new Dictionary<string, int> { { "egg", eggsCaught } });
        else if (coins > 0) state.BuyGold(0, coins);
        if (gems > 0) state.AddGems(gems);
        if (finalScore <= 0) return "Las gallinas se ríen de ti. Pepito también, pero disimula.";
        return "+" + eggsCaught + " huevos al granero  ·  +" + FarmGameState.Format(coins) + " monedas" +
               (gems > 0 ? "\n+" + gems + " gemas" : "");
    }
}
