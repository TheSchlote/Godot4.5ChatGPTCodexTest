using System;
using CodexGame.Infrastructure.Controllers;
using Godot;

namespace CodexGame.Infrastructure.Adapters;

public sealed class TweenRunnerAdapter : ITweenRunner
{
    public void RunPath(Node3D node, Vector3[] path, float speed, Action onCompleted)
    {
        var tween = node.GetTree().CreateTween();
        for (int i = 1; i < path.Length; i++)
        {
            var from = path[i - 1];
            var to = path[i];
            var segmentDist = from.DistanceTo(to);
            var duration = segmentDist / speed;
            tween.TweenProperty(node, "global_position", to, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
        }
        tween.TweenCallback(Callable.From(() => onCompleted()));
    }
}
