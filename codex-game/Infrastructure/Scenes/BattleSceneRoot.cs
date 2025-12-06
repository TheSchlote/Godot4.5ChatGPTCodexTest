using CodexGame.Application.Battle;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Maps;
using CodexGame.Domain.Units;
using CodexGame.Infrastructure.Pathfinding;
using CodexGame.Infrastructure.Controllers;
using Godot;
using System.Collections.Generic;
using System.Linq;
using CodexGame.Domain.Abilities;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Scene entry that wires Godot nodes to the domain battle manager.
/// Now delegates unit presentation, movement, and AI to focused helpers.
/// </summary>
public partial class BattleSceneRoot : Node3D
{
    [Export] public Node3D? MapRoot { get; set; }
    [Export] public Node3D? UnitsRoot { get; set; }
    [Export] public Control? BattleUiRoot { get; set; }
    [Export] public Gimbal? GimbalNode { get; set; }

    private BattleManager? _battleManager;
    private UnitPresenter? _units;
    private MovementController? _movement;
    private AiTurnController? _aiController;
    private SelectionCursor? _cursor;
    private AstarPathfinding? _pathfinding;
    private DirectionalLight3D? _light;
    private Gimbal? _gimbal;
    private BattleUi? _battleUi;
    private QteController? _qteController;
    private readonly List<MeshInstance3D> _rangeIndicators = new();

    private readonly BattleComposition _composition = new();
    private BattleStateMachine? _stateMachine;
    private BattleContext? _battleContext;
    private IdleState? _idleState;
    private AbilitySelectState? _abilityState;
    private QteState? _qteState;
    private EndedState? _endedState;
    private TurnAdvanceState? _turnAdvanceState;

    private bool _battleEnded;
    private bool _pendingTurnAdvance;
    private bool _aiPending;
    private bool _abilityExecuting;
    private bool _moveAvailable = true;
    private bool _actionAvailable = true;
    private bool _turnConsumed;
    private int _inputCooldownFrames;
    private string _activeUnitId = PlayerId;
    private Vector3 _mapCenter = Vector3.Zero;
    private Vector2I _mapSize = new(8, 8);
    private BattleComposition.MapContext? _mapContext;
    private BattlePhase _phase = BattlePhase.Idle;

    private const string PlayerId = "Player";
    private const float TileSize = 2f;
    private const float TileHeight = 0.2f;
    private const string DemoMapPath = "res://Infrastructure/Scenes/Maps/demo_map.json";
    private const string DemoUnitsPath = "res://Infrastructure/Scenes/Maps/demo_units.json";
    private const string DefaultAbilityId = "basic_attack";
    private string _pendingAbilityId = DefaultAbilityId;

    internal string ActiveUnitId => _activeUnitId;
    internal bool PendingTurnAdvance => _pendingTurnAdvance;
    internal BattlePhase Phase => _phase;
    internal bool MoveAvailable => _moveAvailable;
    internal bool ActionAvailable => _actionAvailable;
    internal IdleState EnsureIdleState()
    {
        if (_idleState is null && _battleContext is not null)
            _idleState = new IdleState(_battleContext);
        return _idleState!;
    }

    internal AbilitySelectState EnsureAbilityState(string attackerId, string targetId)
    {
        if (_abilityState is null && _battleContext is not null)
            _abilityState = new AbilitySelectState(_battleContext);
        _abilityState?.Configure(attackerId, targetId);
        return _abilityState!;
    }

    internal TurnAdvanceState EnsureTurnAdvanceState()
    {
        if (_turnAdvanceState is null && _battleContext is not null)
            _turnAdvanceState = new TurnAdvanceState(_battleContext);
        return _turnAdvanceState!;
    }

    public override void _Ready()
    {
        EnsureInputMap();
        _battleManager = new BattleManager();
        AddChild(_battleManager);
        _units = new UnitPresenter(UnitsRoot ?? this, _battleManager);

        BuildMap();
        CreateCamera();
        CreateLight();
        CreatePathfinding();
        InitializeUi();
        SpawnUnits();
        CreateCursor();
        CreateMovementController();
        _aiController = new AiTurnController(_units);
        InitializeStateMachine();
    }

    private void InitializeUi()
    {
        _battleUi = new BattleUi(this, RestartBattle, EndTurn, BattleUiRoot);
        _qteController = new QteController(_battleUi.Qte);
        _qteController.Completed += OnQteCompleted;
        UpdateTurnOrderUi();
        UpdatePhaseUi();
    }

    private void InitializeStateMachine()
    {
        if (_battleManager is null || _units is null || _movement is null || _qteController is null || _battleUi is null || _cursor is null || _gimbal is null || _pathfinding is null || _aiController is null) return;
        _stateMachine = new BattleStateMachine();
        _battleContext = new BattleContext(
            this,
            _stateMachine,
            _battleManager,
            _units,
            _movement,
            _qteController,
            _battleUi,
            _cursor,
            _gimbal,
            _pathfinding,
            _aiController);

        _idleState = new IdleState(_battleContext);
        _abilityState = new AbilitySelectState(_battleContext);
        _qteState = new QteState(_battleContext);
        _endedState = new EndedState(_battleContext);
        _turnAdvanceState = new TurnAdvanceState(_battleContext);
        _stateMachine.ChangeState(_idleState);
    }

    private void SpawnUnits()
    {
        if (_battleManager is null || _units is null || _mapContext is null) return;

        _composition.SpawnUnits(_mapContext, _units, TileSize, TileHeight, DemoUnitsPath);

        BeginNextTurn();
        UpdateTurnOrderUi();
    }

    public override void _Process(double delta)
    {
        if (_battleEnded) return;
        if (_battleManager is null || _units is null) return;

        _qteController?.Update(delta);
        _stateMachine?.HandleInput(delta);
        _stateMachine?.Process(delta);
        if (_inputCooldownFrames > 0)
            _inputCooldownFrames--;

        if (_cursor is not null && _gimbal?.Camera is not null)
        {
            _cursor.Camera = _gimbal.Camera;
            _cursor.SyncToCameraLook();
        }

        UpdateLight();
        UpdatePathPreview();
        _units.UpdateHealthBarFacing(_gimbal?.Camera);
        UpdatePhaseUi();
    }

    internal void HandleUnitInput()
    {
        if (_battleEnded || _battleManager is null || _phase != BattlePhase.Idle) return;
        if (_units is not null && _units.IsAiControlled(_activeUnitId)) return; // player only
        if (_inputCooldownFrames > 0) return;

        if (Input.IsActionJustPressed("end_turn"))
        {
            EndTurn();
            return;
        }

        if (IsAttackJustPressed())
        {
        HandleActionAtCursor();
        }
    }

    private bool TryGetActiveNode(out Node3D node)
    {
        var resolved = GetNodeById(_activeUnitId);
        node = resolved!;
        return resolved is not null;
    }

    internal void HandleActionAtCursor()
    {
        if (_battleManager is null || _cursor is null || _units is null) return;
        if (_battleEnded) return;

        var activeNode = _movement?.PreviewNode;
        var attackerId = _movement?.PreviewUnitId ?? _activeUnitId;
        if (activeNode is null || string.IsNullOrEmpty(attackerId))
        {
            if (!TryGetActiveNode(out activeNode))
                return;
            attackerId = activeNode.Name;
            _activeUnitId = attackerId;
        }

        var cursorPos = _cursor.GetSelectedTile();
        var targetNode = _units.GetNodeAtPosition(_pathfinding!, cursorPos, excludeId: attackerId);

        if (targetNode != null)
        {
            if (!_actionAvailable) return;
            if (_battleContext is null || _stateMachine is null) return;
            _stateMachine.ChangeState(EnsureAbilityState(attackerId, targetNode.Name));
        }
        else
        {
            if (!_moveAvailable) return;
            if (_battleContext is null || _stateMachine is null) return;
            _stateMachine.ChangeState(new MovingState(_battleContext, attackerId, activeNode, cursorPos));
            return;
        }

        UpdateTurnOrderUi();
    }

    private void EndTurn()
    {
        _moveAvailable = false;
        _actionAvailable = false;
        HideAbilityPanel();
        _movement?.ClearPathVisualization();
        UpdateTurnOrderUi();
        _battleUi?.UpdateActions(_moveAvailable, _actionAvailable);
        TryAutoAdvance();
    }

    private void ShowAbilityPanel(string attackerId, string targetId)
    {
        if (_battleUi is null || _units is null || _pathfinding is null || _battleManager is null) return;
        var abilityIds = _units.GetAbilities(attackerId);
        if (abilityIds.Count == 0) return;

        var attackerNode = _units.GetNode(attackerId);
        if (attackerNode is null) return;

        var abilities = abilityIds
            .Select(id => new AbilityOption(id, GetAbilityLabel(id)))
            .ToList();

        _battleUi.ShowAbilityPanel(abilities, abilityId => OnAbilitySelected(attackerId, targetId, abilityId), abilityId => ShowAbilityRange(attackerId, abilityId), HideAbilityPanel);
        ShowAbilityRange(attackerId, abilities.First().Id);
    }

    internal void HideAbilityPanel()
    {
        _battleUi?.HideAbilityPanel();
        ClearRangeIndicators();
    }

    internal void OnAbilitySelected(string attackerId, string targetId, string abilityId)
    {
        if (_abilityExecuting) return;
        if (!_actionAvailable) return;
        _abilityExecuting = true;
        _pendingAbilityId = string.IsNullOrEmpty(abilityId) ? DefaultAbilityId : abilityId;
        var isPlayerControlled = _units is not null && _units.TryGetTeam(attackerId, out var team) && team <= 1;

        if (!IsTargetInRange(attackerId, targetId, _pendingAbilityId))
        {
            if (_moveAvailable && TryMoveIntoRangeAndUse(attackerId, targetId, _pendingAbilityId, isPlayerControlled))
                return;
            _abilityExecuting = false;
            return;
        }
        ExecuteAbilityFlow(attackerId, targetId, _pendingAbilityId, isPlayerControlled);
    }

    private void ExecuteAbilityFlow(string attackerId, string targetId, string abilityId, bool isPlayerControlled)
    {
        HideAbilityPanel();
        if (isPlayerControlled)
        {
            if (_qteController is null) return;
            _phase = BattlePhase.Qte;
            UpdatePhaseUi();
            _qteController.Begin(attackerId, targetId);
            if (_stateMachine is not null && _qteState is not null)
            {
                _stateMachine.ChangeState(_qteState);
            }
        }
        else
        {
            var targetTime = _qteController?.TargetTime ?? 0.75f;
            ExecuteAttack(attackerId, targetId, new TimingBarInput(targetTime, targetTime), abilityId);
        }
    }

    internal bool StartMoveAlongPath(string unitId, Node3D node, Vector3 destination, Action? onCompleted = null)
    {
        if (_battleManager is null || _movement is null || _units is null) return false;
        var moveResult = _movement.TryStartMove(unitId, node, destination, _units, facing =>
        {
            _units.SetFacing(unitId, facing);
            _phase = BattlePhase.Idle;
            UpdatePhaseUi();
            _moveAvailable = false;
            onCompleted?.Invoke();
            UpdateTurnOrderUi();
            TryAutoAdvance();
        });

        if (moveResult == MoveResult.Success)
        {
            _phase = BattlePhase.Moving;
            UpdatePhaseUi();
            // If AI initiated movement, ensure idle does not immediately re-enter AI state.
            if (_units is not null && _units.IsAiControlled(unitId))
            {
                _pendingTurnAdvance = true;
            }
            return true;
        }

        if (_battleUi is not null)
        {
            var msg = moveResult == MoveResult.Occupied ? "Tile occupied." : "No path.";
            _battleUi.ShowToast(msg);
        }

        return false;
    }

    private void OnQteCompleted(string attackerId, string targetId, TimingBarInput input)
    {
        ExecuteAttack(attackerId, targetId, input, _pendingAbilityId);
        _phase = BattlePhase.Idle;
        UpdatePhaseUi();
        if (_stateMachine is not null && _battleContext is not null)
        {
            _stateMachine.ChangeState(EnsureTurnAdvanceState());
        }
    }

    internal void ExecuteAttack(string attackerId, string targetId, TimingBarInput input, string abilityId)
    {
        if (_battleManager is null) return;
        _actionAvailable = false;
        var resolvedAbility = string.IsNullOrEmpty(abilityId) ? DefaultAbilityId : abilityId;
        var facing = GetFacingForAttack(attackerId, targetId);
        _battleManager.ExecuteAbility(resolvedAbility, attackerId, targetId, input, facing);
        _units?.UpdateHealthBar(targetId);
        HandleDeaths(targetId);
        HideAbilityPanel();
        _pendingAbilityId = DefaultAbilityId;
        _abilityExecuting = false;
        _turnConsumed = true;
        UpdateTurnOrderUi();
        TryAutoAdvance();
    }

    private void TryAutoAdvance()
    {
        if (_movement?.IsMoving ?? false) return;
        if (_moveAvailable || _actionAvailable) return;
        _pendingTurnAdvance = true;
        if (!_turnConsumed)
            _battleManager?.ConsumeTurn(_activeUnitId);
        _turnConsumed = true;
        HideAbilityPanel();
        ClearRangeIndicators();
        _battleUi?.UpdateActions(_moveAvailable, _actionAvailable);
        _stateMachine?.ChangeState(EnsureTurnAdvanceState());
    }

    internal void ProcessAiTurn()
    {
        if (_battleManager is null || _units is null || _aiController is null) return;
        if (_activeUnitId == string.Empty) return;
        if (!_units.IsAiControlled(_activeUnitId)) return;
        if (_aiPending) return;

        _aiPending = true;
        try
        {
            _gimbal?.SmoothFocus(GetNodeById(_activeUnitId)?.GlobalPosition ?? _mapCenter);
            var team = _units.TryGetTeam(_activeUnitId, out var t) ? t : 0;
            var target = _aiController.SelectTarget(_activeUnitId, team);
            if (target == null)
            {
                _battleManager.ConsumeTurn(_activeUnitId);
                _pendingTurnAdvance = true;
                return;
            }

            var targetTime = _qteController?.TargetTime ?? 0.75f;
            var abilityId = ChooseAiAbility(_activeUnitId);

            var activeNode = _units.GetNode(_activeUnitId);
        var targetNode = _units.GetNode(target);
        if (activeNode is null || targetNode is null)
        {
            _battleManager.ConsumeTurn(_activeUnitId);
            _pendingTurnAdvance = true;
            if (_stateMachine is not null && _turnAdvanceState is not null)
            {
                _stateMachine.ChangeState(_turnAdvanceState);
            }
            return;
        }

            if (IsTargetInRange(activeNode, targetNode, abilityId))
            {
                ExecuteAttack(_activeUnitId, target, new TimingBarInput(targetTime, targetTime), abilityId);
                return;
            }

            // Move closer toward target to get in range.
            if (TryMoveAiIntoRange(activeNode, targetNode, abilityId))
            {
                return;
            }

            // If we can't move, advance turn.
            _battleManager.ConsumeTurn(_activeUnitId);
            _pendingTurnAdvance = true;
            if (_stateMachine is not null && _turnAdvanceState is not null)
            {
                _stateMachine.ChangeState(_turnAdvanceState);
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[AI] Error during AI turn: {ex}");
            _pendingTurnAdvance = true;
        }
        finally
        {
            _aiPending = false;
            if (!_pendingTurnAdvance && !(_movement?.IsMoving ?? false))
            {
                // Safety: advance turn if nothing else is pending.
                _pendingTurnAdvance = true;
                if (_stateMachine is not null && _turnAdvanceState is not null)
                {
                    _stateMachine.ChangeState(_turnAdvanceState);
                }
            }
        }
    }

    internal string ChooseAiAbility(string unitId)
    {
        if (_units is null) return DefaultAbilityId;
        var abilities = _units.GetAbilities(unitId);
        return abilities.Count > 0 ? abilities[0] : DefaultAbilityId;
    }

    internal bool TryMoveAiIntoRange(Node3D activeNode, Node3D targetNode, string abilityId)
    {
        if (_pathfinding is null || _movement is null || _units is null || _mapContext is null) return false;
        if (!_battleManager!.TryGetAbility(abilityId, out var ability)) return false;

        var targetCell = _pathfinding.WorldToCell(targetNode.GlobalPosition);
        var activeCell = _pathfinding.WorldToCell(activeNode.GlobalPosition);

        // For now, only support simple diamond range; expand as abilities grow.
        var candidateCells = new List<Vector3I>();
        var maxRange = Mathf.Max(ability.Range.Min, ability.Range.Max);
        for (int dx = -maxRange; dx <= maxRange; dx++)
        {
            for (int dz = -maxRange; dz <= maxRange; dz++)
            {
                var manhattan = Mathf.Abs(dx) + Mathf.Abs(dz);
                if (manhattan < ability.Range.Min || manhattan > ability.Range.Max) continue;
                var cell = new Vector3I(targetCell.X + dx, 0, targetCell.Z + dz);
                if (cell == activeCell) continue;
                candidateCells.Add(cell);
            }
        }

        var best = FindReachableClosestCell(_activeUnitId, activeNode.GlobalPosition, candidateCells);
        if (best is null) return false;

        var destWorld = ConvertSpawnToWorld(new Vector2I(best.Value.X, best.Value.Z));
        var started = StartMoveAlongPath(_activeUnitId, activeNode, destWorld);
        if (started)
        {
            // Prevent immediate re-entry into AI state while movement completes.
            _pendingTurnAdvance = true;
        }
        return started;
    }

    private bool TryMoveIntoRangeAndUse(string attackerId, string targetId, string abilityId, bool isPlayerControlled)
    {
        if (_pathfinding is null || _movement is null || _units is null || _mapContext is null) return false;
        if (!_battleManager!.TryGetAbility(abilityId, out var ability)) return false;

        var attackerNode = _units.GetNode(attackerId);
        var targetNode = _units.GetNode(targetId);
        if (attackerNode is null || targetNode is null) return false;

        var targetCell = _pathfinding.WorldToCell(targetNode.GlobalPosition);
        var attackerCell = _pathfinding.WorldToCell(attackerNode.GlobalPosition);

        var candidateCells = new List<Vector3I>();
        var maxRange = Mathf.Max(ability.Range.Min, ability.Range.Max);
        for (int dx = -maxRange; dx <= maxRange; dx++)
        {
            for (int dz = -maxRange; dz <= maxRange; dz++)
            {
                var manhattan = Mathf.Abs(dx) + Mathf.Abs(dz);
                if (manhattan < ability.Range.Min || manhattan > ability.Range.Max) continue;
                var cell = new Vector3I(targetCell.X + dx, 0, targetCell.Z + dz);
                if (cell == attackerCell) continue;
                if (cell.X < 0 || cell.Z < 0 || cell.X >= _mapContext.Size.X || cell.Z >= _mapContext.Size.Y) continue;
                candidateCells.Add(cell);
            }
        }

        var best = FindReachableClosestCell(attackerId, attackerNode.GlobalPosition, candidateCells);
        if (best is null) return false;

        var destWorld = ConvertSpawnToWorld(new Vector2I(best.Value.X, best.Value.Z));
        return StartMoveAlongPath(attackerId, attackerNode, destWorld, () =>
        {
            if (_battleEnded || !_actionAvailable) { TryAutoAdvance(); return; }
            ExecuteAbilityFlow(attackerId, targetId, abilityId, isPlayerControlled);
        });
    }

    private Vector3I? FindReachableClosestCell(string unitId, Vector3 startWorld, IEnumerable<Vector3I> cells)
    {
        if (_pathfinding is null || _units is null) return null;
        Vector3I? bestCell = null;
        float bestScore = float.MaxValue;
        var moveRange = GetMoveRange(unitId);

        foreach (var cell in cells)
        {
            if (_units.IsCellOccupied(_pathfinding, cell, unitId)) continue;
            var world = _pathfinding.CellToWorld(cell);
            var path = _pathfinding.GetPath(startWorld, world);
            if (path.Length == 0) continue;
            if (path.Length - 1 > moveRange) continue;
            var score = path.Length;
            if (score < bestScore)
            {
                bestScore = score;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private void CreateCamera()
    {
        if (GimbalNode != null)
        {
            _gimbal = GimbalNode;
            _gimbal.Position = _mapCenter;
            return;
        }

        var existing = GetNodeOrNull<Gimbal>("Gimbal");
        if (existing != null)
        {
            _gimbal = existing;
            _gimbal.Position = _mapCenter;
            return;
        }

        _gimbal = new Gimbal
        {
            Position = _mapCenter
        };
        AddChild(_gimbal);
    }

    private void CreateCursor()
    {
        _cursor = new SelectionCursor
        {
            Name = "SelectionCursor",
            Position = _mapCenter,
            MapSize = _mapSize,
            TileSize = TileSize,
            Camera = _gimbal?.Camera
        };
        AddChild(_cursor);

        var mesh = new CylinderMesh
        {
            TopRadius = 0.4f,
            BottomRadius = 0.4f,
            Height = 0.1f
        };
        var normalColor = new Color(0.9f, 0.9f, 0.2f);
        var occupiedColor = new Color(0.35f, 0.8f, 1.0f);
        var mat = new StandardMaterial3D { AlbedoColor = normalColor, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = null };
        var meshInstance = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
        _cursor.AddChild(meshInstance);
        _cursor.RegisterIndicatorMesh(meshInstance, normalColor, occupiedColor);
    }

    private void CreateMovementController()
    {
        if (_pathfinding is null || _cursor is null) return;

        _movement = new MovementController(
            _pathfinding,
            new CodexGame.Infrastructure.Adapters.CursorAdapter(_cursor),
            this,
            cell => ConvertSpawnToWorld(cell),
            _gimbal is null ? null : new CodexGame.Infrastructure.Adapters.CameraFollowerAdapter(_gimbal),
            new CodexGame.Infrastructure.Adapters.TweenRunnerAdapter(),
            new CodexGame.Infrastructure.Adapters.NullPathVisualizer(),
            GetMoveRange);
    }

    private void CreatePathfinding()
    {
        _pathfinding = new AstarPathfinding();
        _pathfinding.SetupGrid(_mapSize.X, _mapSize.Y, TileSize);
        AddChild(_pathfinding);
    }

    private void BuildMap()
    {
        _mapContext = _composition.BuildMap(MapRoot ?? this, DemoMapPath, _mapSize, TileSize, TileHeight);
        _mapCenter = _mapContext.Center;
        _mapSize = _mapContext.Size;
    }

    private void EnsureInputMap()
    {
        AddActionIfMissing("cam_forward", new InputEventKey { Keycode = Key.W });
        AddActionIfMissing("cam_back", new InputEventKey { Keycode = Key.S });
        AddActionIfMissing("cam_left", new InputEventKey { Keycode = Key.A });
        AddActionIfMissing("cam_right", new InputEventKey { Keycode = Key.D });
        AddActionIfMissing("attack", new InputEventKey { Keycode = Key.Space });
        AddActionIfMissing("ui_accept", new InputEventKey { Keycode = Key.Enter });
        AddActionIfMissing("ui_cancel", new InputEventKey { Keycode = Key.Escape });
        AddActionIfMissing("cam_rotate_left", new InputEventKey { Keycode = Key.Q });
        AddActionIfMissing("cam_rotate_right", new InputEventKey { Keycode = Key.E });
        AddActionIfMissing("cam_rotate_up", new InputEventKey { Keycode = Key.Up });
        AddActionIfMissing("cam_rotate_down", new InputEventKey { Keycode = Key.Down });
        AddActionIfMissing("cam_zoom_in", new InputEventKey { Keycode = Key.Plus });
        AddActionIfMissing("cam_zoom_out", new InputEventKey { Keycode = Key.Minus });
        // Duplicate WASD to UI nav to allow ability selection with WASD.
        AddEventIfMissing("ui_up", new InputEventKey { Keycode = Key.W });
        AddEventIfMissing("ui_down", new InputEventKey { Keycode = Key.S });
        AddEventIfMissing("ui_left", new InputEventKey { Keycode = Key.A });
        AddEventIfMissing("ui_right", new InputEventKey { Keycode = Key.D });
        AddEventIfMissing("ui_accept", new InputEventKey { Keycode = Key.Space });
        AddActionIfMissing("end_turn", new InputEventKey { Keycode = Key.T });
        AddEventIfMissing("end_turn", new InputEventJoypadButton { ButtonIndex = JoyButton.Start });

        // Controller mappings
        AddEventIfMissing("attack", new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        AddEventIfMissing("cam_forward", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp });
        AddEventIfMissing("cam_back", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadDown });
        AddEventIfMissing("cam_left", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadLeft });
        AddEventIfMissing("cam_right", new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight });
        AddEventIfMissing("ui_accept", new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        AddEventIfMissing("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B });
        AddEventIfMissing("ui_up", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1f });
        AddEventIfMissing("ui_down", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1f });
        AddEventIfMissing("ui_left", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1f });
        AddEventIfMissing("ui_right", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1f });
        AddEventIfMissing("cam_forward", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1f });
        AddEventIfMissing("cam_back", new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1f });
        AddEventIfMissing("cam_left", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1f });
        AddEventIfMissing("cam_right", new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1f });
        AddEventIfMissing("cam_rotate_left", new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = -1f });
        AddEventIfMissing("cam_rotate_right", new InputEventJoypadMotion { Axis = JoyAxis.RightX, AxisValue = 1f });
        AddEventIfMissing("cam_rotate_up", new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = -1f });
        AddEventIfMissing("cam_rotate_down", new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 1f });
        AddEventIfMissing("cam_zoom_in", new InputEventJoypadMotion { Axis = JoyAxis.TriggerRight, AxisValue = 1f });
        AddEventIfMissing("cam_zoom_out", new InputEventJoypadMotion { Axis = JoyAxis.TriggerLeft, AxisValue = 1f });
    }

    private static void AddActionIfMissing(string name, InputEvent @event)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, @event);
    }

    private static void AddEventIfMissing(string actionName, InputEvent @event)
    {
        if (!InputMap.HasAction(actionName))
            InputMap.AddAction(actionName);

        foreach (var existing in InputMap.ActionGetEvents(actionName))
        {
            if (existing.AsText() == @event.AsText())
                return;
        }

        InputMap.ActionAddEvent(actionName, @event);
    }

    private void CreateLight()
    {
        _light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(50, 30, 0),
            LightEnergy = 1.2f
        };
        AddChild(_light);
    }

    private void UpdateLight()
    {
        if (_gimbal?.Camera is null || _light is null) return;

        var direction = (_mapCenter - _gimbal.Camera.GlobalPosition).Normalized();
        _light.GlobalPosition = _gimbal.Camera.GlobalPosition + direction * 2f + Vector3.Up * 10f;
        _light.LookAt(_mapCenter, Vector3.Up);
    }

    private void UpdatePathPreview()
    {
        if (_cursor is null || _movement is null || _pathfinding is null || _units is null) return;
        if (!TryGetActiveNode(out var activeNode)) return;
        _movement.RefreshPathPreview(_activeUnitId, activeNode, _units, _phase);
    }

    private void FocusCameraOnActiveUnit()
    {
        if (_gimbal is null) return;
        var targetNode = GetNodeById(_activeUnitId);
        if (targetNode is null) return;

        _gimbal.SmoothFocus(targetNode.GlobalPosition);
    }

    private Node3D? GetNodeById(string unitId) => _units?.GetNode(unitId);

    private static bool IsAttackJustPressed() => Input.IsActionJustPressed("attack");

    internal Facing GetFacingForAttack(string attackerId, string targetId)
    {
        if (_units is null) return Facing.Front;
        var attackerNode = _units.GetNode(attackerId);
        var targetNode = _units.GetNode(targetId);
        if (attackerNode is null || targetNode is null) return Facing.Front;

        // Face attacker toward target for readability.
        attackerNode.LookAt(targetNode.GlobalPosition, Vector3.Up);

        var defenderForward = _units.GetFacing(targetId);
        var dirToAttacker = (attackerNode.GlobalPosition - targetNode.GlobalPosition).Normalized();
        _units.SetFacing(attackerId, dirToAttacker);
        var dot = defenderForward.Dot(dirToAttacker);
        if (dot < -0.5f) return Facing.Back;
        if (dot > 0.5f) return Facing.Front;
        return Facing.Side;
    }

    internal bool IsTargetInRange(Node3D attacker, Node3D target, string abilityId)
    {
        if (_pathfinding is null || _battleManager is null) return false;
        if (!_battleManager.TryGetAbility(abilityId, out var ability)) return false;

        var attackerCell = _pathfinding.WorldToCell(attacker.GlobalPosition);
        var targetCell = _pathfinding.WorldToCell(target.GlobalPosition);
        var distance = Mathf.Abs(attackerCell.X - targetCell.X) + Mathf.Abs(attackerCell.Z - targetCell.Z);
        return distance >= ability.Range.Min && distance <= ability.Range.Max;
    }

    internal bool IsTargetInRange(string attackerId, string targetId, string abilityId)
    {
        if (_units is null) return false;
        var attackerNode = _units.GetNode(attackerId);
        var targetNode = _units.GetNode(targetId);
        if (attackerNode is null || targetNode is null) return false;
        return IsTargetInRange(attackerNode, targetNode, abilityId);
    }

    internal void ShowAbilityRange(string attackerId, string abilityId)
    {
        ClearRangeIndicators();
        if (_units is null || _pathfinding is null || _mapContext is null) return;
        if (_battleManager is null || !_battleManager.TryGetAbility(abilityId, out var ability)) return;
        var attackerNode = _units.GetNode(attackerId);
        if (attackerNode is null) return;

        var attackerCell = _pathfinding.WorldToCell(attackerNode.GlobalPosition);
        var cells = GetCellsInRange(attackerCell, ability.Range);
        foreach (var cell in cells)
        {
            var world = ConvertSpawnToWorld(new Vector2I(cell.X, cell.Z));
            var mesh = new BoxMesh { Size = new Vector3(TileSize * 0.9f, 0.05f, TileSize * 0.9f) };
            var indicator = new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.7f, 1f, 0.35f) },
            };
            var parent = MapRoot ?? this;
            parent.AddChild(indicator);
            indicator.GlobalPosition = world + new Vector3(0, 0.03f, 0);
            _rangeIndicators.Add(indicator);
        }
    }

    private IEnumerable<Vector3I> GetCellsInRange(Vector3I center, RangePattern range)
    {
        var max = range.Max;
        var min = range.Min;
        var bounds = _mapContext?.Size ?? _mapSize;
        for (int dx = -max; dx <= max; dx++)
        {
            for (int dz = -max; dz <= max; dz++)
            {
                var manhattan = Mathf.Abs(dx) + Mathf.Abs(dz);
                if (manhattan < min || manhattan > max) continue;
                var cell = new Vector3I(center.X + dx, 0, center.Z + dz);
                if (cell.X < 0 || cell.Z < 0 || cell.X >= bounds.X || cell.Z >= bounds.Y) continue;
                yield return cell;
            }
        }
    }

    internal void ClearRangeIndicators()
    {
        foreach (var node in _rangeIndicators)
        {
            if (IsInstanceValid(node))
                node.QueueFree();
        }
        _rangeIndicators.Clear();
    }

    private Vector3 ConvertSpawnToWorld(Vector2I cell)
    {
        if (_mapContext is null) return Vector3.Zero;
        return _composition.ConvertSpawnToWorld(_mapContext, cell, TileSize, TileHeight);
    }

    private int GetMoveRange(string unitId)
    {
        if (_battleManager is not null && _battleManager.TryGetUnit(unitId, out var state) && state is not null)
            return state.MoveRange;
        return 0;
    }

    internal void BeginNextTurn()
    {
        if (_battleManager is null) return;

        _pendingTurnAdvance = false;
        _moveAvailable = true;
        _actionAvailable = true;
        _abilityExecuting = false;
        _pendingAbilityId = DefaultAbilityId;
        _turnConsumed = false;
        _inputCooldownFrames = 2;

        var ready = _battleManager.AdvanceToNextReady();
        if (!string.IsNullOrEmpty(ready))
        {
            _activeUnitId = ready;
            FocusCameraOnActiveUnit();
            _aiPending = false;
        }

        HideAbilityPanel();
        ClearRangeIndicators();
        _movement?.ClearPathVisualization();
        UpdateTurnOrderUi();
        _battleUi?.UpdateActions(_moveAvailable, _actionAvailable);
    }

    private void HandleDeaths(string unitId)
    {
        if (_battleManager is null || _units is null) return;
        if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) return;
        if (state.IsAlive) return;

        _units.RemoveUnit(unitId);

        if (_activeUnitId == unitId)
            BeginNextTurn();

        CheckBattleEnd();
        UpdateTurnOrderUi();
    }

    private void ShowGameOver()
    {
        _qteController?.Cancel();
        _phase = BattlePhase.Ended;
        _battleUi?.ShowGameOver();
        if (_stateMachine is not null && _endedState is not null)
        {
            _stateMachine.ChangeState(_endedState);
        }
    }

    private void RestartBattle()
    {
        GetTree().ReloadCurrentScene();
    }

    private void UpdateTurnOrderUi()
    {
        if (_battleManager is null || _battleUi is null) return;

        var predicted = _battleManager.GetPredictedTurnOrder(_battleUi.TurnOrderSlotCount).ToList();
        _battleUi.UpdateTurnOrder(predicted, _activeUnitId, GetDisplayName, GetColorForUnit);
        _battleUi.UpdateActions(_moveAvailable, _actionAvailable);
    }

    private void UpdatePhaseUi() => _battleUi?.UpdatePhase(_phase);

    internal void SetPhase(BattlePhase phase)
    {
        _phase = phase;
        UpdatePhaseUi();
    }

    private void CheckBattleEnd()
    {
        if (_units is null) return;
        var aliveTeams = _units.GetAliveTeams().Distinct().ToList();
        if (aliveTeams.Count <= 1 && aliveTeams.Count > 0)
        {
            _battleEnded = true;
            GD.Print($"Battle ended. Winning team: {aliveTeams[0]}");
            ShowGameOver();
        }
        else if (aliveTeams.Count == 0)
        {
            _battleEnded = true;
            GD.Print("Battle ended. No units remaining.");
            ShowGameOver();
        }

        UpdateTurnOrderUi();
    }

    private string GetDisplayName(string unitId) => _units is null ? unitId : _units.GetDisplayName(unitId);

    internal string GetAbilityLabel(string abilityId)
    {
        if (_battleManager is not null && _battleManager.TryGetAbility(abilityId, out var ability) && !string.IsNullOrEmpty(ability.Name))
            return ability.Name;
        return abilityId;
    }

    internal Color GetColorForUnit(string unitId) => _units is null ? Colors.White : _units.GetColor(unitId);
}
