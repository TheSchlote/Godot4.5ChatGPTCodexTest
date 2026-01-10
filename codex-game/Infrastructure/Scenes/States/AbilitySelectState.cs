using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Shows the ability panel and handles accept/cancel and range preview.
/// </summary>
internal sealed class AbilitySelectState : BattleState
{
    private string _attackerId = string.Empty;
    private string _targetId = string.Empty;
    private bool _active;

    public AbilitySelectState(BattleContext ctx) : base(ctx) { }

    public void Configure(string attackerId, string targetId)
    {
        if (_active) return; // avoid reconfiguring while active
        _attackerId = attackerId;
        _targetId = targetId;
    }

    public override void Enter()
    {
        if (_active)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
            return;
        }
        if (_attackerId != Ctx.Root.ActiveUnitId || !Ctx.Root.ActionAvailable)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
            return;
        }
        _active = true;
        Ctx.Cursor.InputEnabled = false;
        Ctx.Gimbal.InputEnabled = false;
        var options = BuildOptions().ToList();
        if (options.Count == 0)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
            return;
        }

        Ctx.Ui.ShowAbilityPanel(
            options,
            abilityId => SelectAbility(abilityId),
            abilityId => Ctx.Root.ShowAbilityRange(_attackerId, abilityId),
            () => Cancel());

        var first = options.FirstOrDefault();
        if (!string.IsNullOrEmpty(first.Id))
            Ctx.Root.ShowAbilityRange(_attackerId, first.Id);
    }

    public override void Exit()
    {
        _active = false;
        Ctx.Ui.HideAbilityPanel();
        Ctx.Root.ClearRangeIndicators();
        Ctx.Cursor.InputEnabled = true;
        Ctx.Gimbal.InputEnabled = true;
        Ctx.Root.ClearRangeIndicators();
    }

    private IEnumerable<AbilityOption> BuildOptions()
    {
        var ids = Ctx.Units.GetAbilities(_attackerId);
        return ids.Select(id => Ctx.Root.GetAbilityOption(_attackerId, id));
    }

    private void SelectAbility(string abilityId)
    {
        Ctx.Root.OnAbilitySelected(_attackerId, _targetId, abilityId);
    }

    private void Cancel()
    {
        Ctx.Root.HideAbilityPanel();
        Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
    }
}
