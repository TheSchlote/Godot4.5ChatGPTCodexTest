using CodexGame.Domain.Maps;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Handles building the map and spawning units into the scene.
/// </summary>
internal sealed class BattleComposition
{
    private readonly MapLoader _mapLoader = new();
    private readonly MapBuilder _mapBuilder = new();
    private readonly UnitContentLoader _unitLoader = new();

    internal sealed record MapContext(
        Vector3 Center,
        Vector2I Size,
        Dictionary<Vector2I, int> CellElevations,
        Dictionary<Vector2I, string> CellTypes,
        MapData? MapData);

    public MapContext BuildMap(Node mapRoot, string mapPath, Vector2I defaultSize, float tileSize, float tileHeight)
    {
        if (_mapLoader.TryLoad(mapPath, out var mapData))
        {
            var elevations = mapData.Cells.ToDictionary(c => new Vector2I(c.X, c.Y), c => c.Elevation);
            var types = mapData.Cells.ToDictionary(c => new Vector2I(c.X, c.Y), c => c.Type);
            var size = new Vector2I(mapData.Width, mapData.Height);
            var center = _mapBuilder.BuildFromMapData(mapRoot, mapData, tileSize, tileHeight).Center;
            return new MapContext(center, size, elevations, types, mapData);
        }

        GD.Print($"Falling back to flat grid because map could not be loaded from {mapPath}.");
        var flatElevations = new Dictionary<Vector2I, int>();
        var flatTypes = new Dictionary<Vector2I, string>();
        var flatCenter = _mapBuilder.BuildFlatGrid(mapRoot, defaultSize, tileSize, tileHeight).Center;
        return new MapContext(flatCenter, defaultSize, flatElevations, flatTypes, null);
    }

    public void SpawnUnits(MapContext context, UnitPresenter units, float tileSize, float tileHeight, string unitsPath)
    {
        var unitDefinitions = LoadUnitDefinitions(unitsPath, tileSize);
        var spawnCursorByTeam = new Dictionary<int, int>();

        foreach (var unit in unitDefinitions)
        {
            var state = CreateUnitState(unit.Blueprint);
            var position = ResolveSpawnPosition(unit, context, spawnCursorByTeam, tileSize, tileHeight);
            var node = units.CreateUnitNode(state.Id, unit.Color, position);
            units.RegisterUnit(state, node, unit.Team, unit.Blueprint.Abilities, unit.Blueprint.Name, unit.Color, unit.Blueprint.AIProfileId);
        }
    }

    public Vector3 ConvertSpawnToWorld(MapContext context, Vector2I cell, float tileSize, float tileHeight)
    {
        var elevation = context.CellElevations.TryGetValue(cell, out var e) ? e : 0;
        return _mapBuilder.CellToWorld(cell, tileSize, tileHeight, elevation);
    }

    private IReadOnlyList<UnitContentLoader.UnitDefinition> LoadUnitDefinitions(string unitsPath, float tileSize)
    {
        if (_unitLoader.TryLoad(unitsPath, out var units))
            return units;

        GD.Print($"Falling back to compiled demo unit data because {unitsPath} was not found or failed to load.");
        return DemoContent.GetUnits()
            .Select((u, index) => new UnitContentLoader.UnitDefinition(
                u.Blueprint,
                u.Color,
                Team: index,
                SpawnCell: new Vector2I(Mathf.RoundToInt(u.Position.X / tileSize), Mathf.RoundToInt(u.Position.Z / tileSize))))
            .ToList();
    }

    private Vector3 ResolveSpawnPosition(UnitContentLoader.UnitDefinition unit, MapContext context, Dictionary<int, int> spawnCursorByTeam, float tileSize, float tileHeight)
    {
        if (context.MapData == null) return GetFallbackSpawn(unit.SpawnCell, context, tileSize, tileHeight);

        if (!spawnCursorByTeam.TryGetValue(unit.Team, out var index))
            index = 0;

        var spawn = context.MapData.Spawns.Where(s => s.Team == unit.Team).Skip(index).FirstOrDefault();
        if (spawn != null)
        {
            spawnCursorByTeam[unit.Team] = index + 1;
            return ConvertSpawnToWorld(context, new Vector2I(spawn.X, spawn.Y), tileSize, tileHeight);
        }

        GD.Print($"No spawn point found for team {unit.Team}; falling back to unit-specified spawn.");
        return GetFallbackSpawn(unit.SpawnCell, context, tileSize, tileHeight);
    }

    private Vector3 GetFallbackSpawn(Vector2I cell, MapContext context, float tileSize, float tileHeight)
    {
        var elevation = context.CellElevations.TryGetValue(cell, out var e) ? e : 0;
        return _mapBuilder.CellToWorld(cell, tileSize, tileHeight, elevation);
    }

    private static UnitState CreateUnitState(UnitBlueprint blueprint) =>
        new(blueprint.Id, blueprint.BaseStats, blueprint.Affinity, blueprint.MoveRange, blueprint.DefaultQTE);
}
