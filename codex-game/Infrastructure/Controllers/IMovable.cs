using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface IMovable
{
    Vector3 GlobalPosition { get; set; }
    void LookAt(Vector3 target, Vector3 up);
}
