namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Runs QTE via QteController and returns to Idle on completion.
/// </summary>
internal sealed class QteState : BattleState
{
    public QteState(BattleContext ctx) : base(ctx) { }

    public override void Enter()
    {
        Ctx.Cursor.InputEnabled = false;
        Ctx.Gimbal.InputEnabled = false;
        Ctx.Root.SetPhase(BattlePhase.Qte);
    }

    public override void Exit()
    {
        Ctx.Cursor.InputEnabled = true;
        Ctx.Gimbal.InputEnabled = true;
        Ctx.Root.SetPhase(BattlePhase.Idle);
    }
}
