using CodexGame.Infrastructure.Controllers;
using CodexGame.Infrastructure.Scenes;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class MovableAdapter : IMovable
{
    private readonly Node3D _node;

    public MovableAdapter(Node3D node)
    {
        _node = node;
    }

    public Vector3 GlobalPosition
    {
        get => _node.GlobalPosition;
        set => _node.GlobalPosition = value;
    }

    public void LookAt(Vector3 target, Vector3 up) => _node.LookAt(target, up);
}
