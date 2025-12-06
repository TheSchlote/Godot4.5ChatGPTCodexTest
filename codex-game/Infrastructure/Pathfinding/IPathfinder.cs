using Godot;

namespace CodexGame.Infrastructure.Pathfinding;

/// <summary>
/// Abstraction for pathfinding to allow pure C# testing and swapping implementations.
/// </summary>
public interface IPathfinder
{
    Vector3[] GetPath(Vector3 start, Vector3 end);
    Vector3I WorldToCell(Vector3 world);
    Vector3 CellToWorld(Vector3I cell);
    MeshInstance3D? VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null);
}
