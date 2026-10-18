using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Music, ambience and sound effects. Every clip lives in <c>Assets/Resources/Audio/</c>
/// (Music, Ambience, SFX) and is asked for by its file name, like the pictures: replace a file keeping its
/// name and the game uses the new one.
///  - <see cref="Play"/>: a one-shot effect ("sfx_coin"). Slight random pitch so repeats don't tire.
///  - <see cref="SetMusic"/>: the track for the current place; changes cross-fade.
///  - <see cref="SetAmbience"/>: up to three looping beds (wind, birds, crickets, rain...).
/// Music and sounds follow the switches in AJUSTES. Survives scene reloads (the star trip).
/// </summary>
public sealed class GameAudio : MonoBehaviour
{
    const float MusicVolume = .55f, AmbienceVolume = .35f, FadeSeconds = 1.2f;
    const int Voices = 10;

    static GameAudio instance;
    static Dictionary<string, AudioClip> clips;

    readonly AudioSource[] voices = new AudioSource[Voices];
    readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    AudioSource musicA, musicB, jingle;
    readonly AudioSource[] beds = new AudioSource[3];
    readonly string[] bedNames = new string[3];
    string currentMusic;
    int nextVoice;

    static readonly List<string> overrides = new List<string>();
    /// <summary>Music of the window on top (computer, pond); null = the place decides.</summary>
    public static string MusicOverride => overrides.Count > 0 ? overrides[overrides.Count - 1] : null;
    /// <summary>A window with its own music opened (the last one opened wins).</summary>
    public static void PushMusic(string name) => overrides.Add(name);
    /// <summary>That window closed: back to the music underneath.</summary>
    public static void PopMusic(string name) { int i = overrides.LastIndexOf(name); if (i >= 0) overrides.RemoveAt(i); }
    /// <summary>The farm is on screen (no page or window over it): farm-only sounds play only then.</summary>
    public static bool FarmVisible;

    static GameAudio Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                var go = new GameObject("Plorp audio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
                instance.Setup();
            }
            return instance;
        }
    }

    static bool MusicOn => FarmEconomy.Current == null || FarmEconomy.Current.MusicEnabled;
    static bool SoundOn => FarmEconomy.Current == null || FarmEconomy.Current.SoundEnabled;

    void Setup()
    {
        for (int i = 0; i < Voices; i++)
        {
            voices[i] = NewSource(false);
            voices[i].volume = 1f; // PlayOneShot is scaled by this: at 0 no effect was heard
        }
        musicA = NewSource(true);
        musicB = NewSource(true);
        jingle = NewSource(false);
        for (int i = 0; i < beds.Length; i++) beds[i] = NewSource(true);
        if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
    }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.volume = 0f;
        return s;
    }

    static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (clips == null)
        {
            clips = new Dictionary<string, AudioClip>();
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio")) clips[clip.name] = clip;
        }
        if (clips.TryGetValue(name, out var c)) return c;
        Debug.LogWarning("Plorp: no encontré el sonido '" + name + "' en Resources/Audio.");
        clips[name] = null; // warn once
        return null;
    }

    // ---------- Effects ----------

    /// <summary>Plays an effect. <paramref name="minGap"/> seconds: ignored if it played more recently (pouring water...).</summary>
    public static void Play(string name, float volume = 1f, float pitchJitter = .04f, float minGap = 0f)
    {
        if (!Application.isPlaying || !SoundOn) return;
        var self = Instance;
        if (self == null) return;
        float now = Time.unscaledTime;
        if (minGap > 0f && self.lastPlayed.TryGetValue(name, out float last) && now - last < minGap) return;
        var clip = Clip(name);
        if (clip == null) return;
        self.lastPlayed[name] = now;
        var voice = self.voices[self.nextVoice];
        self.nextVoice = (self.nextVoice + 1) % Voices;
        voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.PlayOneShot(clip, volume);
    }

    /// <summary>A short musical piece over the music (the star trip fanfare), which ducks meanwhile.</summary>
    public static void PlayJingle(string name)
    {
        if (!Application.isPlaying || !MusicOn) return;
        var self = Instance;
        var clip = Clip(name);
        if (self == null || clip == null) return;
        self.jingle.clip = clip;
        self.jingle.volume = MusicVolume;
        self.jingle.Play();
    }

    // ---------- Music and ambience ----------

    public static void SetMusic(string name)
    {
        if (!Application.isPlaying) return;
        var self = Instance;
        if (self == null || self.currentMusic == name) return;
        self.currentMusic = name;
        // The new track starts on the quiet source; Update cross-fades.
        var swap = self.musicA;
        self.musicA = self.musicB;
        self.musicB = swap;
        self.musicA.clip = Clip(name);
        self.musicA.volume = 0f;
        if (self.musicA.clip != null) self.musicA.Play();
    }

    /// <summary>The looping beds that should sound now (up to 3, null = none).</summary>
    public static void SetAmbience(string a, string b = null, string c = null)
    {
        if (!Application.isPlaying) return;
        var self = Instance;
        if (self == null) return;
        string[] wanted = { a, b, c };
        for (int i = 0; i < self.beds.Length; i++)
        {
            if (self.bedNames[i] == wanted[i]) continue;
            self.bedNames[i] = wanted[i];
            var clip = Clip(wanted[i]);
            self.beds[i].clip = clip;
            if (clip != null) self.beds[i].Play(); else self.beds[i].Stop();
            self.beds[i].volume = 0f;
        }
    }

    void Update()
    {
        float step = Time.unscaledDeltaTime / FadeSeconds;
        float musicTarget = MusicOn ? MusicVolume * (jingle.isPlaying ? .25f : 1f) : 0f;
        musicA.volume = Mathf.MoveTowards(musicA.volume, musicTarget, step);
        musicB.volume = Mathf.MoveTowards(musicB.volume, 0f, step);
        if (musicB.volume <= 0f && musicB.isPlaying) musicB.Stop();
        if (!MusicOn && jingle.isPlaying) jingle.Stop();
        float bedTarget = SoundOn ? AmbienceVolume : 0f;
        foreach (var bed in beds) bed.volume = Mathf.MoveTowards(bed.volume, bed.clip != null ? bedTarget : 0f, step);
    }
}
