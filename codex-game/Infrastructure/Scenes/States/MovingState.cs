using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Executes movement tween and transitions back to Idle on completion.
/// </summary>
internal sealed class MovingState : BattleState
{
    private readonly string _unitId;
    private readonly Node3D _node;
    private readonly Vector3 _destination;

    public MovingState(BattleContext ctx, string unitId, Node3D node, Vector3 destination) : base(ctx)
    {
        _unitId = unitId;
        _node = node;
        _destination = destination;
    }

    public override void Enter()
    {
        Ctx.Cursor.InputEnabled = false;
        Ctx.Gimbal.InputEnabled = true;
        var started = Ctx.Root.StartMoveAlongPath(_unitId, _node, _destination, OnCompleted);
        if (!started)
        {
            Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
        }
    }

    public override void Exit()
    {
    }

    private void OnCompleted()
    {
        Ctx.StateMachine.ChangeState(Ctx.Root.EnsureIdleState());
    }
}
