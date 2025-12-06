using CodexGame.Application.Battle;
using CodexGame.Infrastructure.Pathfinding;
using CodexGame.Infrastructure.Controllers;
using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Shared references passed to battle states.
/// </summary>
internal sealed class BattleContext
{
    public BattleManager BattleManager { get; }
    public UnitPresenter Units { get; }
    public MovementController Movement { get; }
    public QteController Qte { get; }
    public BattleUi Ui { get; }
    public SelectionCursor Cursor { get; }
    public Gimbal Gimbal { get; }
    public AstarPathfinding Pathfinding { get; }
    public AiTurnController Ai { get; }
    public BattleSceneRoot Root { get; }
    public BattleStateMachine StateMachine { get; }

    public BattleContext(
        BattleSceneRoot root,
        BattleStateMachine stateMachine,
        BattleManager battleManager,
        UnitPresenter units,
        MovementController movement,
        QteController qte,
        BattleUi ui,
        SelectionCursor cursor,
        Gimbal gimbal,
        AstarPathfinding pathfinding,
        AiTurnController ai)
    {
        Root = root;
        StateMachine = stateMachine;
        BattleManager = battleManager;
        Units = units;
        Movement = movement;
        Qte = qte;
        Ui = ui;
        Cursor = cursor;
        Gimbal = gimbal;
        Pathfinding = pathfinding;
        Ai = ai;
    }
}
