using CodexGame.Infrastructure.Pathfinding;
using Godot;

namespace InfrastructureTests;

internal sealed class FakePathfinder : IPathfinder
{
    private readonly int _maxRange;
    private readonly System.Func<Vector3, Vector3, Vector3[]> _pathFunc;

    public FakePathfinder(int maxRange)
    {
        _maxRange = maxRange;
        _pathFunc = BuildManhattanPath;
    }

    public Vector3[] GetPath(Vector3 start, Vector3 end) => _pathFunc(start, end);

    public Vector3I WorldToCell(Vector3 world) => new((int)world.X, 0, (int)world.Z);

    public Vector3 CellToWorld(Vector3I cell) => new(cell.X, cell.Y, cell.Z);

    public MeshInstance3D? VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null) => null;

    private Vector3[] BuildManhattanPath(Vector3 start, Vector3 end)
    {
        var path = new System.Collections.Generic.List<Vector3> { start };
        var dx = (int)(end.X - start.X);
        var dz = (int)(end.Z - start.Z);
        int steps = 0;
        int stepX = dx == 0 ? 0 : dx > 0 ? 1 : -1;
        int stepZ = dz == 0 ? 0 : dz > 0 ? 1 : -1;
        var current = start;
        while (current.X != end.X)
        {
            current = new Vector3(current.X + stepX, 0, current.Z);
            path.Add(current);
            steps++;
            if (steps > _maxRange) return System.Array.Empty<Vector3>();
        }
        while (current.Z != end.Z)
        {
            current = new Vector3(current.X, 0, current.Z + stepZ);
            path.Add(current);
            steps++;
            if (steps > _maxRange) return System.Array.Empty<Vector3>();
        }
        return path.ToArray();
    }
}
