using CodexGame.Infrastructure.Pathfinding;
using Godot;
using System;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Handles path preview and movement tweening for a single active unit.
/// </summary>
internal sealed class MovementController
{
    private readonly AstarPathfinding _pathfinding;
    private readonly SelectionCursor _cursor;
    private readonly Node3D _pathParent;
    private readonly Func<Vector2I, Vector3> _cellToWorld;
    private readonly Gimbal? _gimbal;
    private readonly Func<string, int> _moveRangeResolver;

    private MeshInstance3D? _pathMesh;

    public MovementController(AstarPathfinding pathfinding, SelectionCursor cursor, Node3D pathParent, Func<Vector2I, Vector3> cellToWorld, Gimbal? gimbal, Func<string, int> moveRangeResolver)
    {
        _pathfinding = pathfinding;
        _cursor = cursor;
        _pathParent = pathParent;
        _cellToWorld = cellToWorld;
        _gimbal = gimbal;
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
        _pathMesh = _pathfinding.VisualizePath(path, _pathParent, _pathMesh);
    }

    public bool TryStartMove(string unitId, Node3D node, Vector3 destination, UnitPresenter units, Action<Vector3> onCompleted)
    {
        var destCell = _pathfinding.WorldToCell(destination);
        if (units.IsCellOccupied(_pathfinding, destCell, unitId))
        {
            GD.Print("Destination occupied.");
            return false;
        }

        var path = GetElevatedPath(unitId, node.GlobalPosition, destination, units, unitId);
        if (path.Length < 2)
        {
            ClearPathVisualization();
            GD.Print("No path found.");
            return false;
        }

        var facing = (path[^1] - path[^2]).Normalized();
        IsMoving = true;
        PreviewNode = null;
        PreviewUnitId = null;
        _pathMesh = _pathfinding.VisualizePath(path, _pathParent, _pathMesh);
        _gimbal?.BeginFollow(node);
        var tween = node.GetTree().CreateTween();
        const float moveSpeed = 6f;
        for (int i = 1; i < path.Length; i++)
        {
            var from = path[i - 1];
            var to = path[i];
            var segmentDist = from.DistanceTo(to);
            var duration = segmentDist / moveSpeed;
            tween.TweenProperty(node, "global_position", to, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            _gimbal?.StopFollow();
            IsMoving = false;
            ClearPathVisualization();
            onCompleted(facing);
        }));

        return true;
    }

    public void ClearPathVisualization()
    {
        if (_pathMesh != null && _pathMesh.IsInsideTree())
            _pathMesh.QueueFree();

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

        var adjusted = new Vector3[rawPath.Length];
        for (int i = 0; i < rawPath.Length; i++)
        {
            var cell = _pathfinding.WorldToCell(rawPath[i]);
            adjusted[i] = _cellToWorld(new Vector2I(cell.X, cell.Z));
        }

        adjusted[0] = start;
        return adjusted;
    }
}
