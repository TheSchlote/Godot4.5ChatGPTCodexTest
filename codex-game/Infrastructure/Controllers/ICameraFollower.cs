using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface ICameraFollower
{
    void BeginFollow(Node3D target);
    void StopFollow();
}
