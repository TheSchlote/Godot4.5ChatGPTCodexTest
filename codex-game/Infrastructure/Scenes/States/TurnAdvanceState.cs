namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Waits for movement/QTE to finish, then advances to the next unit and returns to idle.
/// </summary>
internal sealed class TurnAdvanceState : BattleState
{
    private bool _advanced;

    public TurnAdvanceState(BattleContext ctx) : base(ctx) { }

    public override void Enter()
    {
        _advanced = false;
        Ctx.Ui.HideAbilityPanel();
        Ctx.Root.HideAbilityPanel();
        Ctx.Root.ClearRangeIndicators();
        TryAdvance();
    }

    public override void Process(double delta)
    {
        if (_advanced) return;
        TryAdvance();
    }

    private void TryAdvance()
    {
        // Wait until movement/QTE are clear.
        if (Ctx.Movement.IsMoving || Ctx.Root.Phase == BattlePhase.Moving || Ctx.Root.Phase == BattlePhase.Qte)
            return;

        Ctx.Root.BeginNextTurn();
        _advanced = true;
        Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
    }

    public override void Exit()
    {
        Ctx.Ui.HideAbilityPanel();
        Ctx.Root.HideAbilityPanel();
        Ctx.Root.ClearRangeIndicators();
    }
}
