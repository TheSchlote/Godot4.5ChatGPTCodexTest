using CodexGame.Infrastructure.Controllers;
using CodexGame.Infrastructure.Pathfinding;
using CodexGame.Infrastructure.Scenes;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class UnitMapAdapter : IUnitMap
{
    private readonly UnitPresenter _units;
    private readonly IPathfinder _pathfinder;

    public UnitMapAdapter(UnitPresenter units, IPathfinder pathfinder)
    {
        _units = units;
        _pathfinder = pathfinder;
    }

    public bool IsCellOccupied(IPathfinder pathfinder, Vector3I cell, string? ignoreId = null)
    {
        if (_pathfinder is AstarPathfinding concrete)
            return _units.IsCellOccupied(concrete, cell, ignoreId);
        return false;
    }
}
