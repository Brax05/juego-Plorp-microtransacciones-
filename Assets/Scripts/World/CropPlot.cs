using System;
using System.Collections.Generic;
using UnityEngine;

public enum CropKind { Carrot, Tomato, Pumpkin, Cabbage }

/// <summary>A modular crop field that owns its soil bed visual, local furrow grid, and crop cells.</summary>
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

    public void Initialize(int plotId, CropKind kind)
    {
        PlotId = plotId;
        Kind = kind;
    }

    public void BindCells(Action<CropCell> tapped)
    {
        cells.Clear();
        var authoredCells = GetComponentsInChildren<CropCell>(true);
        for (int i = 0; i < authoredCells.Length; i++)
        {
            var cell = authoredCells[i];
            int id = i;
            int dot = cell.name.LastIndexOf('.');
            if (dot >= 0 && int.TryParse(cell.name.Substring(dot + 1), out int parsed)) id = parsed;
            cell.Initialize(PlotId, id, Kind, tapped);
            cells.Add(cell);
        }
    }

    public void SetKind(CropKind kind)
    {
        if (Kind == kind) return;
        Kind = kind;
        foreach (var cell in cells) cell.SetKind(kind);
    }

    public long HarvestReady(out int harvested)
    {
        long earned = 0;
        harvested = 0;
        foreach (var cell in cells)
        {
            long value = cell.TakeHarvest();
            if (value <= 0) continue;
            earned += value;
            harvested++;
        }
        return earned;
    }
}
