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

        var baseDamage = attacker.Stats.PhysicalAttack - defender.Stats.PhysicalDefense;
        if (baseDamage < 0) baseDamage = 0;

        var facingMultiplier = context.Facing switch
        {
            Facing.Back => _backModifier,
            Facing.Side => _sideModifier,
            _ => 1f
        };

        var total = (int)(baseDamage * facingMultiplier * context.QTEResult.Multiplier);
        return total;
    }
}
