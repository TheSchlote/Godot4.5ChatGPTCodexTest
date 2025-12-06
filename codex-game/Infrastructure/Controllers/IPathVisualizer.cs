using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface IPathVisualizer
{
    MeshInstance3D? VisualizePath(Vector3[] path, Node3D parent, MeshInstance3D? reuse = null);
    void Clear(MeshInstance3D? mesh);
}
