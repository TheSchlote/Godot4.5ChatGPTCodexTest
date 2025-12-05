using CodexGame.Application.Battle;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Maps;
using CodexGame.Domain.Units;
using CodexGame.Infrastructure.Pathfinding;
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
    private InputState _inputState = InputState.Idle;

    private readonly BattleComposition _composition = new();

    private bool _battleEnded;
    private bool _pendingTurnAdvance;
    private bool _aiPending;
    private bool _abilityExecuting;
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
    }

    private void InitializeUi()
    {
        _battleUi = new BattleUi(this, RestartBattle, BattleUiRoot);
        _qteController = new QteController(_battleUi.Qte);
        _qteController.Completed += OnQteCompleted;
        UpdateTurnOrderUi();
        UpdatePhaseUi();
    }

    private void SpawnUnits()
    {
        if (_battleManager is null || _units is null || _mapContext is null) return;

        _composition.SpawnUnits(_mapContext, _units, TileSize, TileHeight, DemoUnitsPath);

        AdvanceTurnAndFocus();
        UpdateTurnOrderUi();
    }

    public override void _Process(double delta)
    {
        if (_battleEnded) return;
        if (_battleManager is null || _units is null) return;

        _qteController?.Update(delta);
        if (_phase == BattlePhase.Idle && _inputState == InputState.Idle)
        {
            HandleUnitInput();
            ProcessAiTurn();
        }

        if (_battleUi is not null && _battleUi.AbilityPanelVisible && Input.IsActionJustPressed("ui_cancel"))
        {
            HideAbilityPanel();
        }

        if (_cursor is not null && _gimbal?.Camera is not null)
        {
            _cursor.Camera = _gimbal.Camera;
            _cursor.SyncToCameraLook();
        }

        UpdateLight();
        UpdatePathPreview();
        _units.UpdateHealthBarFacing(_gimbal?.Camera);
        TryAdvanceAfterMovement();
        UpdatePhaseUi();
    }

    private void HandleUnitInput()
    {
        if (_battleEnded || _battleManager is null || _phase != BattlePhase.Idle) return;
        if (_units is not null && _units.TryGetTeam(_activeUnitId, out var team) && team > 1) return; // player only

        if (IsAttackJustPressed())
            HandleActionAtCursor();
    }

    private bool TryGetActiveNode(out Node3D node)
    {
        var resolved = GetNodeById(_activeUnitId);
        node = resolved!;
        return resolved is not null;
    }

    private void HandleActionAtCursor()
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
            HideAbilityPanel();
            ShowAbilityPanel(attackerId, targetNode.Name);
        }
        else
        {
            StartMoveAlongPath(attackerId, activeNode, cursorPos);
            return;
        }

        UpdateTurnOrderUi();
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
        if (_gimbal is not null) _gimbal.InputEnabled = false;
        _inputState = InputState.Ability;
        if (_cursor is not null) _cursor.InputEnabled = false;
    }

    private void HideAbilityPanel()
    {
        _battleUi?.HideAbilityPanel();
        ClearRangeIndicators();
        if (_gimbal is not null) _gimbal.InputEnabled = true;
        _inputState = InputState.Idle;
        if (_cursor is not null) _cursor.InputEnabled = true;
    }

    private void OnAbilitySelected(string attackerId, string targetId, string abilityId)
    {
        if (_abilityExecuting) return;
        _pendingAbilityId = string.IsNullOrEmpty(abilityId) ? DefaultAbilityId : abilityId;
        var isPlayerControlled = _units is not null && _units.TryGetTeam(attackerId, out var team) && team <= 1;

        if (!IsTargetInRange(attackerId, targetId, _pendingAbilityId))
        {
            GD.Print("Target out of range.");
            return;
        }
        _abilityExecuting = true;
        HideAbilityPanel();
        if (isPlayerControlled)
        {
            if (_qteController is null) return;
            _phase = BattlePhase.Qte;
            _inputState = InputState.Qte;
            UpdatePhaseUi();
            _qteController.Begin(attackerId, targetId);
        }
        else
        {
            var targetTime = _qteController?.TargetTime ?? 0.75f;
            ExecuteAttack(attackerId, targetId, new TimingBarInput(targetTime, targetTime), _pendingAbilityId);
        }
        if (_gimbal is not null) _gimbal.InputEnabled = true;
        if (_cursor is not null) _cursor.InputEnabled = _inputState == InputState.Idle;
    }

    private bool StartMoveAlongPath(string unitId, Node3D node, Vector3 destination)
    {
        if (_battleManager is null || _movement is null || _units is null) return false;
        if (_movement.TryStartMove(unitId, node, destination, _units, facing =>
        {
            _battleManager.ConsumeTurn(unitId);
            _units.SetFacing(unitId, facing);
            _phase = BattlePhase.Idle;
            _inputState = InputState.Idle;
            if (_cursor is not null) _cursor.InputEnabled = true;
            UpdatePhaseUi();
            _pendingTurnAdvance = true;
        }))
        {
            _phase = BattlePhase.Moving;
            _inputState = InputState.Moving;
            if (_cursor is not null) _cursor.InputEnabled = false;
            UpdatePhaseUi();
            return true;
        }
        return false;
    }

    private void OnQteCompleted(string attackerId, string targetId, TimingBarInput input)
    {
        ExecuteAttack(attackerId, targetId, input, _pendingAbilityId);
        _phase = BattlePhase.Idle;
        _inputState = InputState.Idle;
        if (_cursor is not null) _cursor.InputEnabled = true;
        UpdatePhaseUi();
    }

    private void ExecuteAttack(string attackerId, string targetId, TimingBarInput input, string abilityId)
    {
        if (_battleManager is null) return;

        var resolvedAbility = string.IsNullOrEmpty(abilityId) ? DefaultAbilityId : abilityId;
        var facing = GetFacingForAttack(attackerId, targetId);
        _battleManager.ExecuteAbility(resolvedAbility, attackerId, targetId, input, facing);
        _units?.UpdateHealthBar(targetId);
        HandleDeaths(targetId);
        _pendingTurnAdvance = true;
        HideAbilityPanel();
        _pendingAbilityId = DefaultAbilityId;
        _abilityExecuting = false;
    }

    private void ProcessAiTurn()
    {
        if (_battleManager is null || _units is null || _aiController is null) return;
        if (_activeUnitId == string.Empty) return;
        if (!_units.TryGetTeam(_activeUnitId, out var team)) return;
        const int AiTeam = 2;
        if (team != AiTeam) return;
        if (_aiPending) return;

        _aiPending = true;
        _inputState = InputState.AiTurn;
        if (_cursor is not null) _cursor.InputEnabled = false;
        try
        {
            var target = _aiController.SelectTarget(_activeUnitId, team);
            if (target == null)
            {
                _pendingTurnAdvance = true;
                return;
            }

            var targetTime = _qteController?.TargetTime ?? 0.75f;
            var abilityId = ChooseAiAbility(_activeUnitId);

            var activeNode = _units.GetNode(_activeUnitId);
            var targetNode = _units.GetNode(target);
            if (activeNode is null || targetNode is null)
            {
                _pendingTurnAdvance = true;
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
            _pendingTurnAdvance = true;
        }
        finally
        {
            _aiPending = false;
            _inputState = InputState.Idle;
            if (_cursor is not null) _cursor.InputEnabled = true;
        }
    }

    private string ChooseAiAbility(string unitId)
    {
        if (_units is null) return DefaultAbilityId;
        var abilities = _units.GetAbilities(unitId);
        return abilities.Count > 0 ? abilities[0] : DefaultAbilityId;
    }

    private bool TryMoveAiIntoRange(Node3D activeNode, Node3D targetNode, string abilityId)
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

        var best = FindReachableClosestCell(activeNode.GlobalPosition, candidateCells);
        if (best is null) return false;

        var destWorld = ConvertSpawnToWorld(new Vector2I(best.Value.X, best.Value.Z));
        return StartMoveAlongPath(_activeUnitId, activeNode, destWorld);
    }

    private Vector3I? FindReachableClosestCell(Vector3 startWorld, IEnumerable<Vector3I> cells)
    {
        if (_pathfinding is null || _units is null) return null;
        Vector3I? bestCell = null;
        float bestScore = float.MaxValue;
        var moveRange = GetMoveRange(_activeUnitId);

        foreach (var cell in cells)
        {
            if (_units.IsCellOccupied(_pathfinding, cell, _activeUnitId)) continue;
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
            _cursor,
            this,
            cell => ConvertSpawnToWorld(cell),
            _gimbal,
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

    private Facing GetFacingForAttack(string attackerId, string targetId)
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

    private bool IsTargetInRange(Node3D attacker, Node3D target, string abilityId)
    {
        if (_pathfinding is null || _battleManager is null) return false;
        if (!_battleManager.TryGetAbility(abilityId, out var ability)) return false;

        var attackerCell = _pathfinding.WorldToCell(attacker.GlobalPosition);
        var targetCell = _pathfinding.WorldToCell(target.GlobalPosition);
        var distance = Mathf.Abs(attackerCell.X - targetCell.X) + Mathf.Abs(attackerCell.Z - targetCell.Z);
        return distance >= ability.Range.Min && distance <= ability.Range.Max;
    }

    private bool IsTargetInRange(string attackerId, string targetId, string abilityId)
    {
        if (_units is null) return false;
        var attackerNode = _units.GetNode(attackerId);
        var targetNode = _units.GetNode(targetId);
        if (attackerNode is null || targetNode is null) return false;
        return IsTargetInRange(attackerNode, targetNode, abilityId);
    }

    private void ShowAbilityRange(string attackerId, string abilityId)
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

    private void ClearRangeIndicators()
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

    private void AdvanceTurnAndFocus()
    {
        if (_battleManager is null) return;

        var ready = _battleManager.AdvanceToNextReady();
        if (!string.IsNullOrEmpty(ready))
        {
            _activeUnitId = ready;
            FocusCameraOnActiveUnit();
            _aiPending = false;
        }
        HideAbilityPanel();
        UpdateTurnOrderUi();
    }

    private void HandleDeaths(string unitId)
    {
        if (_battleManager is null || _units is null) return;
        if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) return;
        if (state.IsAlive) return;

        _units.RemoveUnit(unitId);

        if (_activeUnitId == unitId)
            AdvanceTurnAndFocus();

        CheckBattleEnd();
        UpdateTurnOrderUi();
    }

    private void ShowGameOver()
    {
        _qteController?.Cancel();
        _phase = BattlePhase.Ended;
        _battleUi?.ShowGameOver();
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
    }

    private void UpdatePhaseUi() => _battleUi?.UpdatePhase(_phase);

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

    private void TryAdvanceAfterMovement()
    {
        if ((_movement?.IsMoving ?? false) || _phase == BattlePhase.Moving || _phase == BattlePhase.Qte) return;
        if (!_pendingTurnAdvance) return;

        _pendingTurnAdvance = false;
        AdvanceTurnAndFocus();
    }

    private string GetDisplayName(string unitId) => _units is null ? unitId : _units.GetDisplayName(unitId);

    private string GetAbilityLabel(string abilityId)
    {
        if (_battleManager is not null && _battleManager.TryGetAbility(abilityId, out var ability) && !string.IsNullOrEmpty(ability.Name))
            return ability.Name;
        return abilityId;
    }

    private Color GetColorForUnit(string unitId) => _units is null ? Colors.White : _units.GetColor(unitId);
}
