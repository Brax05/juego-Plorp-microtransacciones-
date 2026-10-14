using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CropKind { Carrot, Tomato, Pumpkin, Cabbage }

/// <summary>A modular crop field that owns its soil bed visual, local furrow grid, and crop cells.</summary>
[ExecuteAlways]
public sealed class CropPlot : MonoBehaviour
{
    // Clean isometric furrow coordinates local to the plot (3 furrows x 4 columns = 12 crops):
    public static readonly Vector2[] LocalCropPositions =
    {
        // Furrow 1 (Top)
        new Vector2(85, 22), new Vector2(112, 36), new Vector2(139, 50), new Vector2(166, 64),
        // Furrow 2 (Middle)
        new Vector2(65, 35), new Vector2(92, 49), new Vector2(119, 63), new Vector2(146, 77),
        // Furrow 3 (Bottom)
        new Vector2(45, 48), new Vector2(72, 62), new Vector2(99, 76), new Vector2(126, 90)
    };

    readonly List<CropCell> cells = new List<CropCell>();
    public int PlotId { get; private set; }
    public CropKind Kind { get; private set; }

    void Awake()
    {
        EnsureSoilVisual();
    }

    public void Initialize(int plotId, CropKind kind)
    {
        PlotId = plotId;
        Kind = kind;
        EnsureSoilVisual();
    }

    public void EnsureSoilVisual()
    {
        var oldImg = GetComponent<Image>();
        if (oldImg != null)
        {
            if (Application.isPlaying) Destroy(oldImg);
            else DestroyImmediate(oldImg);
        }
        var raw = GetComponent<RawImage>();
        if (raw == null)
        {
            raw = gameObject.AddComponent<RawImage>();
        }
        if (raw != null)
        {
            if (raw.texture == null)
            {
                var tex = Resources.Load<Texture2D>("World/Sprites/soil_bed");
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    raw.texture = tex;
                }
            }
            raw.raycastTarget = false;
        }
        var rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(204, 108);
        }
    }

    public CropCell AddCell(PixelUIFactory ui, int cellId, Vector2 position, Action<CropCell> tapped)
    {
        var rect = ui.Rect(transform, "Crop " + PlotId + "." + cellId, position.x, position.y, 32, 32);
        var cell = rect.gameObject.AddComponent<CropCell>();
        cell.Initialize(ui, PlotId, cellId, Kind, tapped);
        cells.Add(cell);
        return cell;
    }

    public void SetKind(CropKind kind)
    {
        if (Kind == kind) return;
        Kind = kind;
        gameObject.name = "Plot " + PlotId + " - " + kind;
        foreach (var cell in cells) cell.SetKind(kind);
    }

    public long HarvestReady()
    {
        long earned = 0;
        foreach (var cell in cells) earned += cell.TakeHarvest();
        return earned;
    }
}
