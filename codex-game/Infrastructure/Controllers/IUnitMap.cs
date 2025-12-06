using CodexGame.Infrastructure.Pathfinding;
using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface IUnitMap
{
    bool IsCellOccupied(IPathfinder pathfinder, Vector3I cell, string? ignoreId = null);
}
