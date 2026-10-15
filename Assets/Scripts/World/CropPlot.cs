using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum CropKind { Carrot, Tomato, Pumpkin, Cabbage }

/// <summary>A modular crop field that owns its soil bed visual, local furrow grid, and crop cells.</summary>
[ExecuteAlways]
public sealed class CropPlot : MonoBehaviour
{
    // Sprite top-left positions. Their bottom-center planting points follow the soil perspective:
    // four plants along each diagonal furrow, ordered back-to-front for correct UI overlap.
    public static readonly Vector2[] LocalCropPositions =
    {
        // Back furrow
        new Vector2(75, -4), new Vector2(97, 7), new Vector2(120, 19), new Vector2(142, 30),
        // Middle furrow
        new Vector2(53, 7), new Vector2(75, 18), new Vector2(98, 29), new Vector2(120, 40),
        // Front furrow
        new Vector2(31, 17), new Vector2(53, 28), new Vector2(76, 40), new Vector2(98, 50)
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
