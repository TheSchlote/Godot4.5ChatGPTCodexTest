using System;
using System.Linq;
using CodexGame.Domain.Abilities;
using CodexGame.Domain.Units;

namespace CodexGame.Domain.AI;

/// <summary>
/// Simple heuristic AI that picks the first available damaging ability against the lowest HP target.
/// </summary>
public sealed class BruiserBehavior : IAIBehavior
{
    private readonly AbilityCatalog _abilities;

    public BruiserBehavior(AbilityCatalog abilities)
    {
        _abilities = abilities;
    }

    public AbilityChoice EvaluateAction(UnitState self, MapState mapState)
    {
        // Placeholder selection until pathfinding & range checks exist.
        var targetId = self.Id == "Player" ? "Enemy" : "Player";
        if (_abilities.TryGet("basic_attack", out var basic))
            return new AbilityChoice(basic.Id, targetId);

        var first = _abilities.GetAllIds().FirstOrDefault()
            ?? throw new InvalidOperationException("No abilities registered for AI selection.");

        return new AbilityChoice(first, targetId);
    }
}
