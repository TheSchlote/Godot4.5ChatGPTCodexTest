namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Battle ended; disables player interaction.
/// </summary>
internal sealed class EndedState : BattleState
{
    public EndedState(BattleContext ctx) : base(ctx) { }

    public override void Enter()
    {
        Ctx.Cursor.InputEnabled = false;
        Ctx.Gimbal.InputEnabled = false;
        Ctx.Root.SetPhase(BattlePhase.Ended);
    }

    public override void Exit()
    {
    }
}
