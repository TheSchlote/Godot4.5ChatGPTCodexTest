namespace CodexGame.Infrastructure.Scenes;

internal abstract class BattleState
{
    protected readonly BattleContext Ctx;

    protected BattleState(BattleContext ctx)
    {
        Ctx = ctx;
    }

    public abstract void Enter();
    public abstract void Exit();
    public virtual void HandleInput(double delta) { }
    public virtual void Process(double delta) { }
}
