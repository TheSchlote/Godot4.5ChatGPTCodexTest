namespace CodexGame.Infrastructure.Scenes;

internal sealed class BattleStateMachine
{
    private BattleState? _current;

    public void ChangeState(BattleState next)
    {
        _current?.Exit();
        _current = next;
        _current.Enter();
    }

    public void HandleInput(double delta) => _current?.HandleInput(delta);
    public void Process(double delta) => _current?.Process(delta);
}
