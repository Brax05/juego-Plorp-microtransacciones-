using System;
using System.Collections.Generic;
using UnityEngine;

public enum CropKind { Carrot, Tomato, Pumpkin, Cabbage }

/// <summary>A field that owns its crop cells and can switch crop type independently.</summary>
public sealed class CropPlot : MonoBehaviour
{
    readonly List<CropCell> cells = new List<CropCell>();
    public int PlotId { get; private set; }
    public CropKind Kind { get; private set; }

    public void Initialize(int plotId, CropKind kind)
    {
        PlotId = plotId;
        Kind = kind;
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
