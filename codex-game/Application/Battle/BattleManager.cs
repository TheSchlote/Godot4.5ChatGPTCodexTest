using CodexGame.Domain.Abilities;
using CodexGame.Domain.Combat;
using CodexGame.Domain.QTE;
using CodexGame.Domain.TurnSystem;
using CodexGame.Domain.Units;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace CodexGame.Application.Battle;

/// <summary>
/// Godot-side coordinator that bridges scenes with pure domain systems.
/// </summary>
public partial class BattleManager : Node
{
    private readonly TurnQueue _turnQueue = new();
    private readonly AbilityCatalog _abilityCatalog = new();
    private readonly DamageCalculator _damageCalculator = new();
    private readonly TimingBarEvaluator _timingBarEvaluator = new();
    private readonly Dictionary<string, UnitState> _units = new();

    public override void _Ready()
    {
        GD.Print("BattleManager ready: domain bridge initialized.");
        RegisterDefaultContent();
    }

    public void RegisterUnit(Node3D unitNode, UnitState state)
    {
        _units[state.Id] = state;
        _turnQueue.Register(new TurnMeter(state.Id, state.Stats.Speed));
        unitNode.Name = state.Id;
    }

    public void RegisterAbility(Ability ability) => _abilityCatalog.Register(ability);

    /// <summary>
    /// Advances the turn queue deterministically by one logical step (no real-time dependency).
    /// </summary>
    public void AdvanceTurns()
    {
        var readyUnitId = _turnQueue.AdvanceUntilReady();
        if (readyUnitId == null) return;

        // In a full implementation, signal UI input or AI. Here we simply log.
        GD.Print($"Unit {readyUnitId} is ready to act.");
    }

    public AbilityResult ExecuteAbility(string abilityId, string attackerId, string defenderId, TimingBarInput qteInput, Facing facing)
    {
        var ability = _abilityCatalog.Get(abilityId);
        var attacker = _units[attackerId];
        var defender = _units[defenderId];

        var qteProfile = new QTEProfile(ability.QTE);
        var qteResult = _timingBarEvaluator.Evaluate(qteInput, qteProfile);

        var context = new AbilityContext(attacker, defender, qteResult, facing);
        var result = ability.DamageFormula(context);

        defender.ApplyDamage(result.Damage);
        _turnQueue.Consume(attackerId);

        return result;
    }

    public string? AdvanceToNextReady() => _turnQueue.AdvanceToNextReady();

    public void ConsumeTurn(string unitId) => _turnQueue.Consume(unitId);

    public IReadOnlyList<TurnOrderEntry> GetTurnOrderSnapshot() => _turnQueue.GetOrderSnapshot();
    public IReadOnlyList<TurnOrderEntry> GetPredictedTurnOrder(int count) => _turnQueue.PredictOrder(count);

    public bool TryGetUnit(string unitId, out UnitState? state) => _units.TryGetValue(unitId, out state);

    public void RemoveUnit(string unitId)
    {
        _units.Remove(unitId);
        _turnQueue.Remove(unitId);
    }

    private void RegisterDefaultContent()
    {
        var adapter = new DamageCalculatorAdapter(_damageCalculator.CalculateDamage);
        RegisterAbility(AbilityFactory.BasicAttack(adapter));
    }
}
