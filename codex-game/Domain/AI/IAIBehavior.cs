using CodexGame.Domain.Abilities;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.AI;

public interface IAIBehavior
{
    AbilityChoice EvaluateAction(UnitState self, MapState mapState);
}

public sealed record AbilityChoice(string AbilityId, string TargetUnitId);

public sealed record MapState(int Width, int Height);
