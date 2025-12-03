using System.Collections.Generic;
using Godot;

namespace CodexGame.Infrastructure.Pathfinding;

/// <summary>
/// Lightweight grid-based A* for tile-aligned movement/visualization.
/// </summary>
public partial class AstarPathfinding : Node3D
{
    private readonly AStar3D _astar = new();
    private readonly HashSet<Vector3I> _blocked = new();
    private int _width;
    private int _height;
    private float _tileSize = 2f;

    public void SetupGrid(int width, int height, float tileSize = 2f)
    {
        _width = width;
        _height = height;
        _tileSize = tileSize;
        _astar.Clear();

        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                var cell = new Vector3I(x, 0, z);
                AddCell(cell);
            }
        }

        foreach (var idObj in _astar.GetPointIds())
            ConnectNeighbors((int)(long)idObj);
    }

    public void BlockCell(Vector3I cell)
    {
        _blocked.Add(cell);
        RefreshCell(cell);
    }

    public void UnblockCell(Vector3I cell)
    {
        _blocked.Remove(cell);
        RefreshCell(cell);
    }

    public Vector3[] GetPath(Vector3 start, Vector3 end)
    {
        var startCell = WorldToCell(start);
        var endCell = WorldToCell(end);
        var startId = CellToId(startCell);
        var endId = CellToId(endCell);
        if (!_astar.HasPoint(startId) || !_astar.HasPoint(endId))
            return System.Array.Empty<Vector3>();
        return _astar.GetPointPath(startId, endId).ToArray();
    }

    public Vector3I WorldToCell(Vector3 world) =>
        new(
            Mathf.Clamp(Mathf.RoundToInt(world.X / _tileSize), 0, _width - 1),
            0,
            Mathf.Clamp(Mathf.RoundToInt(world.Z / _tileSize), 0, _height - 1));

    public Vector3 CellToWorld(Vector3I cell) =>
        new(cell.X * _tileSize, 0, cell.Z * _tileSize);

    public MeshInstance3D VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null)
    {
        if (reuse != null && reuse.IsInsideTree())
            reuse.QueueFree();

        var lineMesh = new ImmediateMesh();
        var meshInstance = new MeshInstance3D
        {
            Mesh = lineMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(meshInstance);

        lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        for (int i = 0; i < path.Length - 1; i++)
        {
            var start = path[i] + new Vector3(0, 0.2f, 0);
            var end = path[i + 1] + new Vector3(0, 0.2f, 0);

            lineMesh.SurfaceAddVertex(start);
            lineMesh.SurfaceAddVertex(end);
        }

        lineMesh.SurfaceEnd();

        var mat = new StandardMaterial3D
        {
            AlbedoColor = Colors.Cyan,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        meshInstance.MaterialOverride = mat;
        return meshInstance;
    }

    private void RefreshCell(Vector3I cell)
    {
        var id = CellToId(cell);
        if (_astar.HasPoint(id))
            _astar.RemovePoint(id);

        if (IsWalkable(cell))
            AddCell(cell);
    }

    private void AddCell(Vector3I cell)
    {
        if (!IsWalkable(cell)) return;
        var id = CellToId(cell);
        _astar.AddPoint(id, CellToWorld(cell), 1);
    }

    private bool IsWalkable(Vector3I cell)
    {
        if (cell.X < 0 || cell.Z < 0 || cell.X >= _width || cell.Z >= _height)
            return false;
        return !_blocked.Contains(cell);
    }

    private void ConnectNeighbors(int id)
    {
        var cell = IdToCell(id);
        Vector3I[] directions = { Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };
        foreach (var dir in directions)
        {
            var neighbor = cell + dir;
            if (!IsWalkable(neighbor)) continue;
            var neighborId = CellToId(neighbor);
            if (_astar.HasPoint(neighborId))
                _astar.ConnectPoints(id, neighborId, true);
        }
    }

    private int CellToId(Vector3I pos) => pos.X + pos.Z * 1000;
    private Vector3I IdToCell(int id) => new(id % 1000, 0, id / 1000);
}
