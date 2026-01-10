using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.Abilities;

public sealed class AbilityContext
{
    public AbilityContext(UnitState attacker, UnitState defender, QTEResult qteResult, Facing facing, Ability? ability = null)
    {
        Attacker = attacker;
        Defender = defender;
        QTEResult = qteResult;
        Facing = facing;
        Ability = ability;
    }

    public UnitState Attacker { get; }
    public UnitState Defender { get; }
    public QTEResult QTEResult { get; }
    public Facing Facing { get; }
    public Ability? Ability { get; }
}
