using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Default player-ready state. Processes player input and may transition to ability or movement.
/// </summary>
internal sealed class IdleState : BattleState
{
    public IdleState(BattleContext ctx) : base(ctx) { }

    public override void Enter()
    {
        if (Ctx.Root.PendingTurnAdvance)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureTurnAdvanceState());
            return;
        }

        if (IsAiTurn())
        {
            Ctx.StateMachine.ChangeState(new AiTurnState(Ctx));
            return;
        }

        Ctx.Cursor.InputEnabled = true;
        Ctx.Gimbal.InputEnabled = true;
        Ctx.Ui.HideAbilityPanel();
        Ctx.Root.HideAbilityPanel();
        Ctx.Root.SetPhase(BattlePhase.Idle);
        Ctx.Ui.UpdateActions(Ctx.Root.MoveAvailable, Ctx.Root.ActionAvailable);
    }

    public override void Exit()
    {
    }

    public override void HandleInput(double delta)
    {
        if (Ctx.Root.PendingTurnAdvance)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureTurnAdvanceState());
            return;
        }
        if (IsAiTurn()) return;
        // Player only; AI turns are handled in AiTurnState.
        Ctx.Root.HandleUnitInput();
    }

    private bool IsAiTurn()
    {
        return Ctx.Units.IsAiControlled(Ctx.Root.ActiveUnitId);
    }
}
