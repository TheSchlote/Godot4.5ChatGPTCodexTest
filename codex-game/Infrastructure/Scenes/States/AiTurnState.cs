using System;
using CodexGame.Domain.QTE;
using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Runs AI turn without player input; moves or attacks, then returns to Idle.
/// </summary>
internal sealed class AiTurnState : BattleState
{
    public AiTurnState(BattleContext ctx) : base(ctx) { }

    public override void Enter()
    {
        Ctx.Cursor.InputEnabled = false;
        Ctx.Gimbal.InputEnabled = false;
        if (!Ctx.Units.IsAiControlled(Ctx.Root.ActiveUnitId))
        {
            Finish();
            return;
        }
        try
        {
            Ctx.Root.ProcessAiTurn();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"AI turn failed: {ex}");
        }
        Finish();
    }

    public override void Exit()
    {
        Ctx.Cursor.InputEnabled = true;
        Ctx.Gimbal.InputEnabled = true;
    }

    private void Finish()
    {
        Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
    }

    private int GetTeam()
    {
        return Ctx.Units.TryGetTeam(Ctx.Root.ActiveUnitId, out var team) ? team : 0;
    }
}
