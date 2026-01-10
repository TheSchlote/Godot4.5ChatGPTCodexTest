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
        var selfSnapshot = mapState.Units.FirstOrDefault(u => u.UnitId == self.Id);
        if (selfSnapshot is null)
            return new AbilityChoice(string.Empty, string.Empty);

        var enemies = mapState.Units
            .Where(u => u.Team != selfSnapshot.Team && u.State.IsAlive)
            .OrderBy(u => u.State.CurrentHP)
            .ToList();

        if (enemies.Count == 0)
            return new AbilityChoice(string.Empty, string.Empty);

        var targetId = enemies[0].UnitId;
        var abilities = selfSnapshot.Abilities;
        if (abilities.Count == 0)
            throw new InvalidOperationException("No abilities registered for AI selection.");

        if (abilities.Contains("basic_attack") && _abilities.TryGet("basic_attack", out var basic))
            return new AbilityChoice(basic.Id, targetId);

        var firstAvailable = abilities.FirstOrDefault(id => _abilities.TryGet(id, out _));
        if (!string.IsNullOrEmpty(firstAvailable))
            return new AbilityChoice(firstAvailable, targetId);

        var fallback = _abilities.GetAllIds().FirstOrDefault()
            ?? throw new InvalidOperationException("No abilities registered for AI selection.");

        return new AbilityChoice(fallback, targetId);
    }
}
