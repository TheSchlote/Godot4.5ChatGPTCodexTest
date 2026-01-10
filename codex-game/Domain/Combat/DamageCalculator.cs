using System;
using CodexGame.Domain.Abilities;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.Combat;

public sealed class DamageCalculator
{
    private readonly float _sideModifier;
    private readonly float _backModifier;

    public DamageCalculator(float sideModifier = 1.5f, float backModifier = 2.0f)
    {
        _sideModifier = sideModifier;
        _backModifier = backModifier;
    }

    public int CalculateDamage(AbilityContext context)
    {
        var attacker = context.Attacker;
        var defender = context.Defender;
        var ability = context.Ability;

        var useSpecial = ability is not null && ability.ElementType != Element.Neutral;
        var attackStat = useSpecial ? attacker.Stats.SpecialAttack : attacker.Stats.PhysicalAttack;
        var defenseStat = useSpecial ? defender.Stats.SpecialDefense : defender.Stats.PhysicalDefense;

        var baseDamage = attackStat - defenseStat;
        if (baseDamage < 0) baseDamage = 0;

        var facingMultiplier = context.Facing switch
        {
            Facing.Back => _backModifier,
            Facing.Side => _sideModifier,
            _ => 1f
        };

        var elementMultiplier = 1f;
        if (ability is not null && ability.ElementType != Element.Neutral)
        {
            if (ability.ElementType == attacker.Affinity)
                elementMultiplier *= 1.1f;
            if (ability.ElementType == defender.Affinity)
                elementMultiplier *= 0.9f;
        }

        var total = (int)(baseDamage * facingMultiplier * elementMultiplier * context.QTEResult.Multiplier);
        return total;
    }
}
