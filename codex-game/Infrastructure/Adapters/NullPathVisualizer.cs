using CodexGame.Infrastructure.Controllers;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class NullPathVisualizer : IPathVisualizer
{
    public MeshInstance3D? VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null) => null;
    public void Clear(MeshInstance3D? mesh) { }
}
