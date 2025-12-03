using CodexGame.Domain.Abilities;
using CodexGame.Domain.Combat;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class DamageCalculatorTests
{
    [Test]
    public void CalculateDamage_ScalesWithFacingAndQTE()
    {
        var attacker = new UnitState("attacker", new StatBlock(10, 5, 20, 0, 5, 5, 10), Element.Fire, moveRange: 3);
        var defender = new UnitState("defender", new StatBlock(10, 5, 5, 0, 5, 5, 8), Element.Water, moveRange: 3);
        var context = new AbilityContext(attacker, defender, new QTEResult(QTEScore.Great, 1.2f, false), Facing.Back);
        var calculator = new DamageCalculator();

        var damage = calculator.CalculateDamage(context);

        Assert.That(damage, Is.EqualTo(36)); // (20-5) * back(2.0) * qte(1.2)
    }
}
