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
    long reward;
    long cooldownSeconds;
    long nextReady;
    float nextVisualCheck;

    public WorldActorRole Role => role;

    public void Initialize(FarmGameState gameState, Action<string> showMessage,
        string actorId, string title, string texture, WorldActorRole actorRole,
        Vector2 position, long payout, long cooldown)
    {
        state = gameState;
        message = showMessage;
        id = actorId;
        displayName = title;
        role = actorRole;
        reward = payout;
        cooldownSeconds = cooldown;
        long.TryParse(PlayerPrefs.GetString(SaveKey, "0"), out nextReady);
        sprite = GetComponent<WorldSprite>() ?? gameObject.AddComponent<WorldSprite>();
        sprite.Initialize(texture, OnTapped, true);
        (GetComponent<WorldActorAnimator>() ?? gameObject.AddComponent<WorldActorAnimator>())
            .Initialize(sprite, role, id, texture, position);
        RefreshReady();
    }

    public long TakeProduction()
    {
        if (role != WorldActorRole.Animal || !Ready()) return 0;
        SetCooldown();
        return reward;
    }

    void OnTapped()
    {
        if (!Ready())
        {
            long remaining = Math.Max(1, nextReady - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            message(displayName + ": listo en " + remaining + " s");
            return;
        }
        SetCooldown();
        state.Collect(reward);
        message(displayName + " +" + FarmGameState.Format(reward) + " · +1 alimento");
    }

    void Update()
    {
        if (Time.unscaledTime < nextVisualCheck) return;
        nextVisualCheck = Time.unscaledTime + .5f;
        RefreshReady();
    }

    bool Ready() => DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= nextReady;

    void SetCooldown()
    {
        nextReady = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + cooldownSeconds;
        PlayerPrefs.SetString(SaveKey, nextReady.ToString());
        RefreshReady();
    }

    void RefreshReady() { sprite.SetReady(Ready()); }

    string SaveKey => "GlorpFarm.world.actor." + id + ".readyAt";
}
