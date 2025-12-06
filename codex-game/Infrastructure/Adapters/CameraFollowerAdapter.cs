using CodexGame.Infrastructure.Controllers;
using CodexGame.Infrastructure.Scenes;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class CameraFollowerAdapter : ICameraFollower
{
    private readonly Gimbal _gimbal;

    public CameraFollowerAdapter(Gimbal gimbal)
    {
        _gimbal = gimbal;
    }

    public void BeginFollow(Node3D target) => _gimbal.BeginFollow(target);
    public void StopFollow() => _gimbal.StopFollow();
}
