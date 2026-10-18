using System;
using UnityEngine;

public enum WorldActorRole { Animal, Worker }

/// <summary>Independent animal or worker with motion and a saved collection timer.</summary>
public sealed class WorldActor : MonoBehaviour
{
    WorldSprite sprite;
    FarmGameState state;
    Action<string> message;
    WorldActorRole role;
    string id;
    string displayName;
    int species = -1;
    long nextReady;
    float nextVisualCheck;

    public WorldActorRole Role => role;
    /// <summary>Food this animal gives Plorp (huevo, queso, leche). Null for workers.</summary>
    public FarmItem Product => FarmItems.ForActor(id);

    public void Initialize(FarmGameState gameState, Action<string> showMessage,
        string actorId, string title, string texture, WorldActorRole actorRole,
        Vector2 position, long payout, long cooldown)
    {
        state = gameState;
        message = showMessage;
        id = actorId;
        displayName = title;
        role = actorRole;
        species = FarmEconomy.SpeciesOf(actorId);
        long.TryParse(PlayerPrefs.GetString(SaveKey, "0"), out nextReady);
        sprite = gameObject.GetOrAdd<WorldSprite>();
        sprite.Initialize(texture, OnTapped, true);
        (gameObject.GetOrAdd<WorldActorAnimator>())
            .Initialize(sprite, role, id, texture, position);
        RefreshReady();
    }

    /// <summary>Current value of one product (grows with the Animales level and workers).</summary>
    long Reward => species >= 0 ? FarmEconomy.AnimalProductValue(state, species) : 0;

    public bool IsReady => role == WorldActorRole.Animal && Ready();

    public long TakeProduction()
    {
        if (!IsReady) return 0;
        long value = Reward;
        SetCooldown();
        GameAudio.Play("sfx_egg_catch", .9f, .1f);
        return value;
    }

    /// <summary>Player collects this animal's product (tap on the animal or its ticket).</summary>
    public void Collect()
    {
        if (role != WorldActorRole.Animal) return;
        if (!Ready())
        {
            long remaining = Math.Max(1, nextReady - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            message(displayName + ": listo en " + remaining + " s");
            return;
        }
        long value = Reward;
        SetCooldown();
        state.Collect(value, Product);
        RewardFx.Collect(transform.position, Product, 1, value);
        message(displayName + " +" + FarmGameState.Format(value) +
                (Product != null ? " · +1 " + Product.Single : ""));
    }

    void OnTapped() => Collect();

    void Update()
    {
        if (sprite == null || Time.unscaledTime < nextVisualCheck) return;
        nextVisualCheck = Time.unscaledTime + .5f;
        RefreshReady();
    }

    bool Ready() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= nextReady;

    void SetCooldown()
    {
        nextReady = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + FarmEconomy.AnimalCooldown(species);
        PlayerPrefs.SetString(SaveKey, nextReady.ToString());
        RefreshReady();
    }

    void RefreshReady() { if (sprite != null) sprite.SetReady(role != WorldActorRole.Animal || Ready()); }

    string SaveKey => "GlorpFarm.world.actor." + id + ".readyAt";
}
