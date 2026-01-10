using CodexGame.Domain.AI;
using Godot;
using System.Linq;
using System.Collections.Generic;
using CodexGame.Infrastructure.Scenes;
using CodexGame.Application.Battle;

namespace CodexGame.Infrastructure.Controllers;

/// <summary>
/// Domain-driven AI helper that selects an ability/target based on configured behaviors.
/// </summary>
public sealed class AiTurnController
{
    private readonly UnitPresenter _units;
    private readonly BattleManager _battleManager;

    public AiTurnController(UnitPresenter units, BattleManager battleManager)
    {
        _units = units;
        _battleManager = battleManager;
    }

    public AbilityChoice? ChooseAction(string activeUnitId, Vector2I mapSize)
    {
        if (!_battleManager.TryGetUnit(activeUnitId, out var self) || self is null)
            return null;

        var behavior = ResolveBehavior(activeUnitId);
        var units = BuildSnapshots();
        var mapState = new MapState(mapSize.X, mapSize.Y, units);
        var choice = behavior.EvaluateAction(self, mapState);
        if (string.IsNullOrEmpty(choice.AbilityId) || string.IsNullOrEmpty(choice.TargetUnitId))
            return null;

        return choice;
    }

    private IReadOnlyList<UnitSnapshot> BuildSnapshots()
    {
        var list = new List<UnitSnapshot>();
        foreach (var unitId in _units.GetAllUnitIds())
        {
            if (!_units.TryGetTeam(unitId, out var team)) continue;
            if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) continue;
            var abilities = _units.GetAbilities(unitId);
            list.Add(new UnitSnapshot(unitId, team, state, abilities));
        }
        return list;
    }

    private IAIBehavior ResolveBehavior(string unitId)
    {
        var profile = _units.GetAiProfileId(unitId);
        return profile?.ToLowerInvariant() switch
        {
            "bruiser" => new BruiserBehavior(_battleManager.AbilityCatalog),
            _ => new BruiserBehavior(_battleManager.AbilityCatalog)
        };
    }
}
