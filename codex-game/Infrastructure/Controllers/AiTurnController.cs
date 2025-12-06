using CodexGame.Domain.QTE;
using Godot;
using System.Linq;
using System.Collections.Generic;
using CodexGame.Infrastructure.Scenes;

namespace CodexGame.Infrastructure.Controllers;

/// <summary>
/// Minimal AI helper: picks nearest enemy and triggers a basic attack.
/// </summary>
public sealed class AiTurnController
{
    private readonly UnitPresenter _units;

    public AiTurnController(UnitPresenter units)
    {
        _units = units;
    }

    public string? SelectTarget(string activeUnitId, int aiTeam)
    {
        var enemies = _units.GetAliveTeams()
            .Where(team => team != aiTeam)
            .SelectMany(team => _units.GetUnitsByTeam(team))
            .ToList();

        if (enemies.Count == 0) return null;

        var selfNode = _units.GetNode(activeUnitId);
        if (selfNode is null) return enemies.First();

        // Search entire map for nearest enemy (no range cap).
        return enemies
            .OrderBy(id => selfNode.GlobalPosition.DistanceTo(_units.GetNode(id)!.GlobalPosition))
            .First();
    }

}
