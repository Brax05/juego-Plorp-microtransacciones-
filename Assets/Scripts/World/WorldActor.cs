using System;
using UnityEngine;

public enum WorldActorRole { Animal, Worker, Machine }

/// <summary>Independent animal, worker or machine with motion and a saved collection timer.</summary>
public sealed class WorldActor : MonoBehaviour
{
    WorldSprite sprite;
    FarmGameState state;
    Action<string> message;
    WorldActorRole role;
    Vector2 home;
    string id;
    string displayName;
    long reward;
    long cooldownSeconds;
    long nextReady;
    float phase;
    float nextVisualCheck;

    public WorldActorRole Role => role;

    public void Initialize(PixelUIFactory ui, FarmGameState gameState, Action<string> showMessage,
        string actorId, string title, string texture, WorldActorRole actorRole,
        Vector2 position, long payout, long cooldown)
    {
        state = gameState;
        message = showMessage;
        id = actorId;
        displayName = title;
        role = actorRole;
        home = position;
        reward = payout;
        cooldownSeconds = cooldown;
        phase = Mathf.Abs(actorId.GetHashCode() % 1000) / 100f;
        long.TryParse(PlayerPrefs.GetString(SaveKey, "0"), out nextReady);
        sprite = gameObject.AddComponent<WorldSprite>();
        sprite.Initialize(ui, texture, OnTapped);
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
        message(displayName + " +" + FarmGameState.Format(reward));
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float x = role == WorldActorRole.Machine ? 0 : Mathf.Sin(t * .8f + phase) * 3.5f;
        float y = Mathf.Sin(t * 1.7f + phase) * (role == WorldActorRole.Machine ? .7f : 1.7f);
        sprite.Rect.anchoredPosition = new Vector2(home.x + x, -(home.y + y));
        if (t < nextVisualCheck) return;
        nextVisualCheck = t + .5f;
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
