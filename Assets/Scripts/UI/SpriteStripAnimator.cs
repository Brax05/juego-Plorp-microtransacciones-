using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the 34-frame cat portrait strip (Pepito / Plorp) on a RawImage.
/// Frames 1-30 are the talking loop, 31-34 the blink. Durations come from the original animation.
/// </summary>
public sealed class SpriteStripAnimator : MonoBehaviour
{
    public const int FrameCount = 34;
    const int TalkEnd = 30;      // frames [0, 30) talk
    const int BlinkStart = 30;   // frames [30, 34) blink

    static readonly float[] Durations =
    {
        .20f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .08f,
        .26f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .08f, .13f,
        .08f, .08f, .08f, .08f, .16f, .08f, .08f, .08f, .08f, .13f,
        .07f, .10f, .07f, .39f
    };

    RawImage image;
    int frame;
    float frameUntil;
    float nextBlink;
    int talkLoopsLeft;
    bool blinking;

    public static SpriteStripAnimator Create(Transform parent, string name, string resource,
        float x, float y, float size)
    {
        var raw = new PixelUIFactory().Texture(parent, name, resource, x, y, size, size);
        var anim = raw.gameObject.GetOrAdd<SpriteStripAnimator>();
        anim.image = raw;
        anim.SetFrame(0);
        anim.nextBlink = Time.unscaledTime + Random.Range(2f, 4f);
        return anim;
    }

    /// <summary>Moves the mouth through the talking frames a few times.</summary>
    public void Talk(int loops = 1)
    {
        talkLoopsLeft = Mathf.Max(1, loops);
        blinking = false;
        SetFrame(1);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (talkLoopsLeft > 0)
        {
            if (now < frameUntil) return;
            int next = frame + 1;
            if (next >= TalkEnd)
            {
                talkLoopsLeft--;
                next = talkLoopsLeft > 0 ? 1 : 0;
            }
            SetFrame(next);
            return;
        }

        if (blinking)
        {
            if (now < frameUntil) return;
            int next = frame + 1;
            if (next >= FrameCount)
            {
                blinking = false;
                SetFrame(0);
                nextBlink = now + Random.Range(2.5f, 5f);
            }
            else SetFrame(next);
            return;
        }

        if (now >= nextBlink)
        {
            blinking = true;
            SetFrame(BlinkStart);
        }
    }

    void SetFrame(int index)
    {
        frame = Mathf.Clamp(index, 0, FrameCount - 1);
        frameUntil = Time.unscaledTime + Durations[frame];
        if (image != null) image.uvRect = new Rect(frame / (float)FrameCount, 0f, 1f / FrameCount, 1f);
    }
}
