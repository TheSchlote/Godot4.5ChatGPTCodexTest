using CodexGame.Domain.Abilities;
using CodexGame.Domain.Combat;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class AbilityFactoryTests
{
    private DamageCalculatorAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        _adapter = new DamageCalculatorAdapter(new DamageCalculator().CalculateDamage);
    }

    [Test]
    public void BasicAttack_HasRangeOne()
    {
        var ability = AbilityFactory.BasicAttack(_adapter);
        Assert.That(ability.Range.Min, Is.EqualTo(1));
        Assert.That(ability.Range.Max, Is.EqualTo(1));
    }

    [Test]
    public void RangedShot_HasRangeOneToThree()
    {
        var ability = AbilityFactory.RangedShot(_adapter);
        Assert.That(ability.Range.Min, Is.EqualTo(1));
        Assert.That(ability.Range.Max, Is.EqualTo(3));
    }

    [Test]
    public void RangedShot_ProducesDamageResult()
    {
        var attacker = new UnitState("attacker", new StatBlock(10, 5, 20, 0, 5, 5, 10), Element.Fire, moveRange: 3);
        var defender = new UnitState("defender", new StatBlock(10, 5, 5, 0, 5, 5, 8), Element.Water, moveRange: 3);
        var ability = AbilityFactory.RangedShot(_adapter);
        var qte = new QTEResult(QTEScore.Great, 1.2f, false);
        var context = new AbilityContext(attacker, defender, qte, Facing.Front);

        var result = ability.DamageFormula(context);

        Assert.That(result.Damage, Is.GreaterThan(0));
        Assert.That(result.QTEResult.Score, Is.EqualTo(QTEScore.Great));
    }
}
