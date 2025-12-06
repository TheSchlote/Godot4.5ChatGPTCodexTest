using CodexGame.Infrastructure.Pathfinding;
using Godot;
using System;
using CodexGame.Infrastructure.Scenes;

namespace CodexGame.Infrastructure.Controllers;

/// <summary>
/// Handles path preview and movement tweening for a single active unit.
/// </summary>
internal sealed class MovementController
{
    private readonly IPathfinder _pathfinding;
    private readonly ICursor _cursor;
    private readonly Node3D _pathParent;
    private readonly Func<Vector2I, Vector3> _cellToWorld;
    private readonly ICameraFollower? _camera;
    private readonly ITweenRunner _tweens;
    private readonly IPathVisualizer _visualizer;
    private readonly Func<string, int> _moveRangeResolver;

    private MeshInstance3D? _pathMesh;

    public MovementController(IPathfinder pathfinding, ICursor cursor, Node3D pathParent, Func<Vector2I, Vector3> cellToWorld, ICameraFollower? camera, ITweenRunner tweens, IPathVisualizer visualizer, Func<string, int> moveRangeResolver)
    {
        _pathfinding = pathfinding;
        _cursor = cursor;
        _pathParent = pathParent;
        _cellToWorld = cellToWorld;
        _camera = camera;
        _tweens = tweens;
        _visualizer = visualizer;
        _moveRangeResolver = moveRangeResolver;
    }

    public bool IsMoving { get; private set; }
    public string? PreviewUnitId { get; private set; }
    public Node3D? PreviewNode { get; private set; }

    public void RefreshPathPreview(string activeUnitId, Node3D activeNode, UnitPresenter units, BattlePhase phase)
    {
        if (IsMoving || phase != BattlePhase.Idle)
        {
            ClearPathVisualization();
            return;
        }

        var targetCell = _pathfinding.WorldToCell(_cursor.GetSelectedTile());
        var occupied = units.IsCellOccupied(_pathfinding, targetCell, activeUnitId);
        _cursor.SetOccupied(occupied);
        if (occupied)
        {
            ClearPathVisualization();
            return;
        }

        var path = GetElevatedPath(activeUnitId, activeNode.GlobalPosition, _cursor.GetSelectedTile(), units, activeUnitId);
        if (path.Length < 2)
        {
            ClearPathVisualization();
            return;
        }

        PreviewNode = activeNode;
        PreviewUnitId = activeUnitId;
        _pathMesh = _visualizer.VisualizePath(path, _pathParent, _pathMesh);
    }

    public MoveResult TryStartMove(string unitId, Node3D node, Vector3 destination, UnitPresenter units, Action<Vector3> onCompleted)
    {
        var result = MovementPlanner.Plan(
            node.GlobalPosition,
            destination,
            _pathfinding.GetPath,
            _pathfinding.WorldToCell,
            cell => units.IsCellOccupied(_pathfinding, cell, unitId),
            _moveRangeResolver(unitId),
            out var path);

        if (result != MoveResult.Success)
        {
            ClearPathVisualization();
            return result;
        }

        var facing = (path[^1] - path[^2]).Normalized();
        IsMoving = true;
        PreviewNode = null;
        PreviewUnitId = null;
        _pathMesh = _visualizer.VisualizePath(path, _pathParent, _pathMesh);
        _camera?.BeginFollow(node);
        const float moveSpeed = 6f;
        _tweens.RunPath(node, path, moveSpeed, () =>
        {
            _camera?.StopFollow();
            IsMoving = false;
            ClearPathVisualization();
            onCompleted(facing);
        });

        return MoveResult.Success;
    }

    public void ClearPathVisualization()
    {
        _visualizer.Clear(_pathMesh);

        _pathMesh = null;
        PreviewNode = null;
        PreviewUnitId = null;
    }

    private Vector3[] GetElevatedPath(string unitId, Vector3 start, Vector3 end, UnitPresenter units, string activeUnitId)
    {
        var destCell = _pathfinding.WorldToCell(end);
        if (units.IsCellOccupied(_pathfinding, destCell, activeUnitId))
            return Array.Empty<Vector3>();

        var rawPath = _pathfinding.GetPath(start, end);
        if (rawPath.Length == 0) return rawPath;

        var moveRange = _moveRangeResolver(unitId);
        if (rawPath.Length - 1 > moveRange)
            return Array.Empty<Vector3>();

        var adjusted = new List<Vector3>();
        var ignoreId = unitId;
        for (int i = 0; i < rawPath.Length; i++)
        {
            var cell = _pathfinding.WorldToCell(rawPath[i]);
            var v2 = new Vector2I(cell.X, cell.Z);
            var world = _cellToWorld(v2);
            // Do not allow stepping through occupied cells (except starting cell).
            if (i > 0)
            {
                var cell3 = new Vector3I(v2.X, 0, v2.Y);
                if (units.IsCellOccupied(_pathfinding, cell3, ignoreId))
                    return Array.Empty<Vector3>();
            }
            adjusted.Add(world);
        }

        adjusted[0] = start;
        return adjusted.ToArray();
    }
}
