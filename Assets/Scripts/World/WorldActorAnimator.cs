using UnityEngine;

/// <summary>Visual idle and roaming animation; production timers stay in WorldActor.</summary>
public sealed class WorldActorAnimator : MonoBehaviour
{
    WorldSprite sprite;
    WorldActorRole role;
    Vector2 home;
    Vector2 offset;
    Vector2 destination;
    System.Random random;
    string texture;
    float phase;
    float stateUntil;
    float stepTime;
    float speed;
    bool moving;
    bool facingRight = true;

    public void Initialize(WorldSprite worldSprite, WorldActorRole actorRole, string actorId,
        string textureName, Vector2 homePosition)
    {
        sprite = worldSprite;
        role = actorRole;
        texture = textureName;
        home = homePosition;
        uint hash = 2166136261;
        foreach (char letter in actorId) hash = (hash ^ letter) * 16777619;
        random = new System.Random((int)(hash & 0x7fffffff));
        phase = (float)random.NextDouble() * Mathf.PI * 2f;
        stateUntil = Time.unscaledTime + Range(.4f, 2f);
        speed = role == WorldActorRole.Animal ? Range(8f, 12f) : Range(7f, 10f);
        if (role == WorldActorRole.Machine) speed = 0f;
    }

    void Update()
    {
        float time = Time.unscaledTime;
        if (role == WorldActorRole.Machine)
        {
            sprite.SetPose(true, 1f, Mathf.Sin(time * 1.4f + phase) * .35f, 0f);
            return;
        }

        if (moving)
        {
            offset = Vector2.MoveTowards(offset, destination, speed * Time.unscaledDeltaTime);
            stepTime += Time.unscaledDeltaTime * (role == WorldActorRole.Animal ? 9f : 7f);
            if ((offset - destination).sqrMagnitude < .01f)
            {
                moving = false;
                stateUntil = time + Range(1.2f, 3.1f);
            }
        }
        else if (time >= stateUntil)
        {
            StartWalk();
        }

        float walk = moving ? Mathf.Abs(Mathf.Sin(stepTime)) : 0f;
        float idle = Mathf.Sin(time * 2.5f + phase);
        float breathe = moving ? 1f - walk * .025f : 1f + idle * .018f;
        float bounce = moving ? walk * 1.3f : idle * .35f;
        float lean = moving ? Mathf.Sin(stepTime) * 2f : 0f;
        sprite.Rect.anchoredPosition = new Vector2(home.x + offset.x, -(home.y + offset.y));
        sprite.SetPose(facingRight, breathe, bounce, lean);
        if (role == WorldActorRole.Worker)
            sprite.SetTexture(texture + (moving && Mathf.Sin(stepTime) > 0f ? "_walk" : ""));
    }

    void StartWalk()
    {
        Vector2 radius = role == WorldActorRole.Animal ? new Vector2(13f, 7f) : new Vector2(8f, 5f);
        float x = Range(-radius.x, radius.x);
        // Every trip has enough horizontal travel to show a clear turn.
        if (Mathf.Abs(x - offset.x) < 6f)
            x = offset.x < 0f ? radius.x : -radius.x;
        destination = new Vector2(x, Range(-radius.y, radius.y));
        facingRight = destination.x >= offset.x;
        moving = true;
        stepTime = 0f;
    }

    float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
}
