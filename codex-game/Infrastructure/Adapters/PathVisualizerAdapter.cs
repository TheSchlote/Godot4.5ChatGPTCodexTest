using CodexGame.Infrastructure.Controllers;
using CodexGame.Infrastructure.Pathfinding;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class PathVisualizerAdapter : IPathVisualizer
{
    private readonly IPathfinder _pathfinder;

    public PathVisualizerAdapter(IPathfinder pathfinder)
    {
        _pathfinder = pathfinder;
    }

    public MeshInstance3D? VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null) =>
        _pathfinder.VisualizePath(path, parent, reuse);

    public void Clear(MeshInstance3D? mesh)
    {
        if (mesh != null && mesh.IsInsideTree())
            mesh.QueueFree();
    }
}
