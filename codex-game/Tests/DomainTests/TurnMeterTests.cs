using CodexGame.Domain.TurnSystem;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class TurnMeterTests
{
    [Test]
    public void AdvanceStep_ReachesThreshold_WhenSpeedHighEnough()
    {
        var meter = new TurnMeter("unit", speed: 200, threshold: 1000, turnRateConstant: 1f);

        for (var i = 0; i < 5; i++)
            meter.AdvanceStep();

        Assert.That(meter.IsReady, Is.True);
    }

    [Test]
    public void Consume_ResetsTurnValue()
    {
        var meter = new TurnMeter("unit", speed: 100, threshold: 1000, turnRateConstant: 1f);
        for (var i = 0; i < 10; i++)
            meter.AdvanceStep();

        meter.ConsumeTurn();

        Assert.That(meter.TurnValue, Is.EqualTo(0));
    }
}
