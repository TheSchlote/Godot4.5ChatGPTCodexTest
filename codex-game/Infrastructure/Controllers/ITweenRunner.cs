using System;
using Godot;

namespace CodexGame.Infrastructure.Controllers;

public interface ITweenRunner
{
    void RunPath(Node3D node, Vector3[] path, float speed, Action onCompleted);
}
