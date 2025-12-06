using CodexGame.Infrastructure.Controllers;
using Godot;
using NUnit.Framework;

namespace InfrastructureTests;

public sealed class MovementPlannerTests
{
    [Test]
    public void OutOfRange_ReturnsNoPath()
    {
        var start = new Vector3(0,0,0);
        var dest = new Vector3(3,0,0);
        var result = MovementPlanner.Plan(
            start,
            dest,
            (s, d) => new[]{ s, new Vector3(1,0,0), new Vector3(2,0,0), d },
            v => new Vector3I((int)v.X, 0, (int)v.Z),
            _ => false,
            moveRange: 2,
            out var path);

        Assert.That(result, Is.EqualTo(MoveResult.NoPath));
    }

    [Test]
    public void Occupied_ReturnsOccupied()
    {
        var start = new Vector3(0,0,0);
        var dest = new Vector3(2,0,0);
        var result = MovementPlanner.Plan(
            start,
            dest,
            (s, d) => new[]{ s, new Vector3(1,0,0), d },
            v => new Vector3I((int)v.X, 0, (int)v.Z),
            cell => cell.X == 1,
            moveRange: 3,
            out var path);

        Assert.That(result, Is.EqualTo(MoveResult.Occupied));
    }

    [Test]
    public void Success_WhenWithinRangeAndUnoccupied()
    {
        var start = new Vector3(0,0,0);
        var dest = new Vector3(2,0,0);
        var result = MovementPlanner.Plan(
            start,
            dest,
            (s, d) => new[]{ s, new Vector3(1,0,0), d },
            v => new Vector3I((int)v.X, 0, (int)v.Z),
            _ => false,
            moveRange: 3,
            out var path);

        Assert.That(result, Is.EqualTo(MoveResult.Success));
        Assert.That(path.Length, Is.EqualTo(3));
    }
}
