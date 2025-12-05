using CodexGame.Domain.Abilities;
using CodexGame.Domain.Combat;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using NUnit.Framework;

namespace CodexGame.Tests.DomainTests;

[TestFixture]
public class AbilityResultQteTests
{
    [Test]
    public void BasicAttack_PropagatesQTEResultIntoAbilityResult()
    {
        var attacker = new UnitState("a", new StatBlock(10, 5, 20, 0, 5, 5, 10), Element.Fire, moveRange: 3);
        var defender = new UnitState("b", new StatBlock(10, 5, 5, 0, 5, 5, 8), Element.Water, moveRange: 3);
        var calculator = new DamageCalculatorAdapter(new DamageCalculator().CalculateDamage);
        var ability = AbilityFactory.BasicAttack(calculator);
        var qte = new QTEResult(QTEScore.Critical, 1.5f, true);
        var context = new AbilityContext(attacker, defender, qte, Facing.Front);

        var result = ability.DamageFormula(context);

        Assert.That(result.QTEResult, Is.EqualTo(qte));
        Assert.That(result.Damage, Is.GreaterThan(0));
    }
}
