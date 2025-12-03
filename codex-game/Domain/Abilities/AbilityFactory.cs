using System;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.Abilities;

public static class AbilityFactory
{
    public static Ability BasicAttack(DamageCalculatorAdapter calculator)
    {
        return new Ability(
            id: "basic_attack",
            name: "Basic Attack",
            mpCost: 0,
            range: new RangePattern(RangeShape.Single, 1, 1),
            aoe: new AreaPattern(AreaShape.Single, 1),
            element: Element.Neutral,
            qte: QTEType.TimingBar,
            damageFormula: ctx => new AbilityResult(calculator.Calculate(ctx), ConsumedTurn: true),
            tags: new[] { AbilityTag.Knockback });
    }
}

/// <summary>
/// Adapter to allow pluggable damage calculator implementations.
/// </summary>
public sealed class DamageCalculatorAdapter
{
    private readonly Func<AbilityContext, int> _calculate;

    public DamageCalculatorAdapter(Func<AbilityContext, int> calculate) => _calculate = calculate;

    public int Calculate(AbilityContext context) => _calculate(context);
}
