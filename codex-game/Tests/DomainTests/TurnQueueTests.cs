using CodexGame.Domain.TurnSystem;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class TurnQueueTests
{
    [Test]
    public void AdvanceUntilReady_ReturnsNull_WhenNoUnitReachedThreshold()
    {
        var queue = new TurnQueue();
        queue.Register(new TurnMeter("a", speed: 10, threshold: 1000));
        queue.Register(new TurnMeter("b", speed: 5, threshold: 1000));

        var ready = queue.AdvanceUntilReady();

        Assert.That(ready, Is.Null);
    }

    [Test]
    public void AdvanceUntilReady_PicksHighestTurnValue()
    {
        var queue = new TurnQueue();
        queue.Register(new TurnMeter("fast", speed: 500, threshold: 1000));
        queue.Register(new TurnMeter("slow", speed: 200, threshold: 1000));

        // Two steps: fast reaches 1000, slow at 400.
        queue.AdvanceUntilReady();
        var ready = queue.AdvanceUntilReady();

        Assert.That(ready, Is.EqualTo("fast"));
    }

    [Test]
    public void GetOrderSnapshot_ReturnsDescendingByTurnValue()
    {
        var queue = new TurnQueue();
        var a = new TurnMeter("a", 100, threshold: 1000);
        var b = new TurnMeter("b", 300, threshold: 1000);
        queue.Register(a);
        queue.Register(b);

        queue.AdvanceUntilReady(); // b=300, a=100

        var order = queue.GetOrderSnapshot();

        Assert.That(order[0].UnitId, Is.EqualTo("b"));
        Assert.That(order[1].UnitId, Is.EqualTo("a"));
    }

    [Test]
    public void AdvanceUntilReady_EventuallyReturnsReadyUnit()
    {
        var queue = new TurnQueue();
        queue.Register(new TurnMeter("one", 500, threshold: 1000));

        string? ready = null;
        for (var i = 0; i < 3 && ready == null; i++)
            ready = queue.AdvanceUntilReady();

        Assert.That(ready, Is.EqualTo("one"));
    }

    [Test]
    public void AdvanceToNextReady_ComputesMinimumStepsOnce()
    {
        var queue = new TurnQueue();
        queue.Register(new TurnMeter("a", 200, threshold: 1000));
        queue.Register(new TurnMeter("b", 500, threshold: 1000));

        var ready = queue.AdvanceToNextReady();

        Assert.That(ready, Is.EqualTo("b"));
        var snapshot = queue.GetOrderSnapshot();
        Assert.That(snapshot[0].IsReady, Is.True);
    }

    [Test]
    public void PredictOrder_UsesCurrentMetersToProjectUpcomingTurns()
    {
        var queue = new TurnQueue();
        queue.Register(new TurnMeter("fast", 400, threshold: 1000));
        queue.Register(new TurnMeter("slow", 200, threshold: 1000));

        // Advance some steps to create differing turn values.
        queue.AdvanceUntilReady(); // fast 400, slow 200

        var predicted = queue.PredictOrder(3);

        Assert.That(predicted, Has.Count.EqualTo(3));
        Assert.That(predicted[0].UnitId, Is.EqualTo("fast"));
        Assert.That(predicted[1].UnitId, Is.EqualTo("slow"));
        Assert.That(predicted[2].UnitId, Is.EqualTo("fast"));
        Assert.That(predicted[0].IsReady, Is.True);
    }
}
