using System.Collections.Generic;

namespace CodexGame.Domain.Maps;

public sealed class MapData
{
    public int Version { get; init; } = 1;
    public int Width { get; init; }
    public int Height { get; init; }
    public List<MapCell> Cells { get; init; } = new();
    public List<SpawnPoint> Spawns { get; init; } = new();
}

public sealed record MapCell(int X, int Y, int Elevation, string Type);

public sealed record SpawnPoint(int Team, int X, int Y);
