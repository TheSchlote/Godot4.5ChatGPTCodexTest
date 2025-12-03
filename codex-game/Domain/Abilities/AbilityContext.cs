using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.Abilities;

public sealed class AbilityContext
{
    public AbilityContext(UnitState attacker, UnitState defender, QTEResult qteResult, Facing facing)
    {
        Attacker = attacker;
        Defender = defender;
        QTEResult = qteResult;
        Facing = facing;
    }

    public UnitState Attacker { get; }
    public UnitState Defender { get; }
    public QTEResult QTEResult { get; }
    public Facing Facing { get; }
}
