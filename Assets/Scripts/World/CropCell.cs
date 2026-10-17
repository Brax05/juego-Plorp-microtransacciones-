using System;
using UnityEngine;

/// <summary>One saved crop. Stages advance with real time, including while the game is closed.</summary>
public sealed class CropCell : MonoBehaviour
{
    const long GrowthSeconds = 22;
    CropKind kind;
    WorldSprite sprite;
    Action<CropCell> tapped;
    string saveKey;
    int stage;
    long stageStarted;
    float nextCheck;

    public bool IsReady { get { AdvanceTime(); return stage == 2; } }
    public CropKind Kind => kind;

    public string CropName
    {
        get
        {
            switch (kind)
            {
                case CropKind.Tomato: return "Tomate";
                case CropKind.Pumpkin: return "Calabaza";
                case CropKind.Cabbage: return "Repollo";
                default: return "Zanahoria";
            }
        }
    }

    public void Initialize(int plotId, int cellId, CropKind initialKind,
        Action<CropCell> onTapped)
    {
        kind = initialKind;
        tapped = onTapped;
        saveKey = "GlorpFarm.world.crop." + plotId + "." + cellId;
        stage = Mathf.Clamp(PlayerPrefs.GetInt(saveKey + ".stage", 2), 0, 2);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!long.TryParse(PlayerPrefs.GetString(saveKey + ".since", now.ToString()), out stageStarted))
            stageStarted = now;
        sprite = GetComponent<WorldSprite>() ?? gameObject.AddComponent<WorldSprite>();
        sprite.Initialize(SpriteName(), () => tapped(this), true,
            kind.ToString().ToLowerInvariant() + "_2");
        AdvanceTime();
        Refresh();
    }

    public void SetKind(CropKind value)
    {
        kind = value;
        Refresh();
    }

    public long TakeHarvest()
    {
        AdvanceTime();
        if (stage != 2) return 0;
        stage = 0;
        stageStarted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
        Refresh();
        switch (kind)
        {
            case CropKind.Tomato: return 45;
            case CropKind.Pumpkin: return 55;
            case CropKind.Cabbage: return 40;
            default: return 35;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        AdvanceTime();
    }

    void AdvanceTime()
    {
        if (stage >= 2) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool changed = false;
        while (stage < 2 && now - stageStarted >= GrowthSeconds)
        {
            stage++;
            stageStarted += GrowthSeconds;
            changed = true;
        }
        if (!changed) return;
        Save();
        Refresh();
    }

    void Save()
    {
        PlayerPrefs.SetInt(saveKey + ".stage", stage);
        PlayerPrefs.SetString(saveKey + ".since", stageStarted.ToString());
    }

    void Refresh()
    {
        sprite.SetTexture(SpriteName());
    }

    string SpriteName()
    {
        return kind.ToString().ToLowerInvariant() + "_" + stage;
    }
}
