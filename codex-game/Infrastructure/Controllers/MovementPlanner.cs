using System;
using Godot;

namespace CodexGame.Infrastructure.Controllers;

internal static class MovementPlanner
{
    public static MoveResult Plan(
        Vector3 start,
        Vector3 destination,
        Func<Vector3, Vector3, Vector3[]> getPath,
        Func<Vector3, Vector3I> worldToCell,
        Func<Vector3I, bool> isOccupied,
        int moveRange,
        out Vector3[] plannedPath)
    {
        plannedPath = getPath(start, destination);
        if (plannedPath.Length < 2)
            return MoveResult.NoPath;

        // range check: steps = path length - 1
        if (plannedPath.Length - 1 > moveRange)
            return MoveResult.NoPath;

        for (int i = 1; i < plannedPath.Length; i++)
        {
            var cell = worldToCell(plannedPath[i]);
            if (isOccupied(cell))
                return MoveResult.Occupied;
        }

        return MoveResult.Success;
    }
}
