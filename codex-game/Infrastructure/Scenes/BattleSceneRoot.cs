using CodexGame.Application.Battle;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Maps;
using CodexGame.Domain.Units;
using CodexGame.Infrastructure.Pathfinding;
using Godot;
using System.Linq;
using System.Collections.Generic;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Minimal scene entry that wires Godot nodes to the domain battle manager.
/// </summary>
public partial class BattleSceneRoot : Node3D
{
    private enum BattlePhase
    {
        Idle,
        Moving,
        Qte,
        Ended
    }

    private BattleManager? _battleManager;
    private SelectionCursor? _cursor;
    private AstarPathfinding? _pathfinding;
    private MeshInstance3D? _pathMesh;
    private Node3D? _pathOwnerNode;
    private string? _pathOwnerId;
    private const string PlayerId = "Player";
    private const string EnemyId = "Enemy";
    private const float TileHeight = 0.2f;
    private Node3D? _playerNode;
    private Node3D? _enemyNode;
    private DirectionalLight3D? _light;
    private Gimbal? _gimbal;
    private readonly MapLoader _mapLoader = new();
    private readonly MapBuilder _mapBuilder = new();
    private readonly UnitContentLoader _unitLoader = new();
    private readonly Dictionary<string, Label3D> _healthBars = new();
    private readonly Dictionary<string, int> _unitTeams = new();
    private readonly Dictionary<string, Node3D> _unitNodes = new();
    private readonly Dictionary<string, List<string>> _unitAbilities = new();
    private readonly List<Label> _turnOrderLabels = new();
    private bool _isMoving;
    private bool _battleEnded;
    private bool _pendingTurnAdvance;
    private bool _aiPending;
    private string _activeUnitId = PlayerId;
    private Vector3 _mapCenter = Vector3.Zero;
    private Vector2I _mapSize = new(8, 8);
    private MapData? _loadedMap;
    private Dictionary<Vector2I, int> _cellElevations = new();
    private const string DemoMapPath = "res://Infrastructure/Scenes/Maps/demo_map.json";
    private const string DemoUnitsPath = "res://Infrastructure/Scenes/Maps/demo_units.json";
    private Control? _uiRoot;
    private Button? _restartButton;
    private Label? _gameOverLabel;
    private TextureProgressBar? _qteBar;
    private Control? _qteRoot;
    private Label? _qteLabel;
    private Label? _phaseLabel;
    private PanelContainer? _abilityRoot;
    private VBoxContainer? _abilityList;
    private Control? _qteTrackContainer;
    private ColorRect? _qteTrack;
    private ColorRect? _qteGoodZone;
    private ColorRect? _qteCritZone;
    private ColorRect? _qteGreatZone;
    private Label? _qteGoodLabel;
    private Label? _qteGreatLabel;
    private Label? _qteCritLabel;
    private ColorRect? _qteIndicator;
    private bool _qteActive;
    private bool _abilityPanelOpen;
    private float _qteTimer;
    private float _qteDuration = 1.5f;
    private float _qteTargetTime = 0.75f;
    private float _qteCritWindow = 0.1f;
    private float _qteGreatWindow;
    private float _qteGoodWindow;
    private string? _qteAttacker;
    private string? _qteTarget;
    private string? _pendingTargetId;
    private string? _pendingAttackerId;
    private BattlePhase _phase = BattlePhase.Idle;
    private const float TileSize = 2f;

    public override void _Ready()
    {
        EnsureInputMap();
        _battleManager = new BattleManager();
        AddChild(_battleManager);

        BuildMap();
        CreateCamera();
        CreateLight();
        CreatePathfinding();
        SpawnUnits();
        CreateBattleUi();
        CreateCursor();
    }

    private IReadOnlyList<UnitContentLoader.UnitDefinition> LoadUnitDefinitions()
    {
        if (_unitLoader.TryLoad(DemoUnitsPath, out var units))
            return units;

        GD.Print($"Falling back to compiled demo unit data because {DemoUnitsPath} was not found or failed to load.");
        return DemoContent.GetUnits()
            .Select((u, index) => new UnitContentLoader.UnitDefinition(
                u.Blueprint,
                u.Color,
                Team: index, // simple 0/1 assignment for fallback
                SpawnCell: new Vector2I(Mathf.RoundToInt(u.Position.X / TileSize), Mathf.RoundToInt(u.Position.Z / TileSize))))
            .ToList();
    }

    private void SpawnUnits()
    {
        if (_battleManager is null) return;

        var unitDefinitions = LoadUnitDefinitions();
        var spawnCursorByTeam = new Dictionary<int, int>();

        foreach (var unit in unitDefinitions)
        {
            var node = CreateUnitNode(unit.Blueprint.Id, Vector3.Zero, unit.Color);
            node.Position = ResolveSpawnPosition(unit, spawnCursorByTeam);

            var state = CreateUnitState(unit.Blueprint);
            _battleManager.RegisterUnit(node, state);
            AssignUnitNode(state.Id, node);
            _unitTeams[state.Id] = unit.Team;
            _unitAbilities[state.Id] = unit.Blueprint.Abilities.ToList();
            AttachHealthBar(node, state);
        }

        AdvanceTurnAndFocus();
        UpdateTurnOrderUi();
    }

    public override void _Process(double delta)
    {
        if (_battleEnded) return;
        ProcessQte(delta);
        if (_phase == BattlePhase.Idle)
        {
            HandleUnitInput();
            ProcessAiTurn();
        }
        UpdateLight();
        UpdatePathPreview();
        UpdateHealthBarFacing();
        TryAdvanceAfterMovement();
        UpdatePhaseLabel();
    }

    private void HandleUnitInput()
    {
        if (_battleEnded) return;
        if (_battleManager is null) return;
        if (_phase != BattlePhase.Idle) return;

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
        if (_battleManager is null || _cursor is null) return;
        if (_battleEnded) return;

        var activeNode = _pathOwnerNode;
        var attackerId = _pathOwnerId ?? _activeUnitId;
        if (activeNode is null || string.IsNullOrEmpty(attackerId))
        {
            if (!TryGetActiveNode(out activeNode))
                return;
            attackerId = activeNode.Name;
            _activeUnitId = attackerId;
        }
        var targetId = attackerId == PlayerId ? EnemyId : PlayerId;
        var cursorPos = _cursor.GetSelectedTile();
        var targetNode = GetNodeAtPosition(cursorPos, excludeId: attackerId);

        if (targetNode != null)
        {
            HideAbilityPanel();
            // Tile occupied: show abilities/skills
            ShowAbilityPanel(attackerId, targetNode.Name);
        }
        else
        {
            StartMoveAlongPath(attackerId, activeNode, cursorPos);
            return; // movement handles turn advance when done
        }

        UpdateTurnOrderUi();
    }

    private void StartMoveAlongPath(string unitId, Node3D node, Vector3 destination)
    {
        if (_pathfinding is null) return;
        var destCell = _pathfinding.WorldToCell(destination);
        if (IsCellOccupied(destCell, unitId))
        {
            GD.Print("Destination occupied.");
            return;
        }

        var path = GetElevatedPath(node.GlobalPosition, destination);
        if (path.Length < 2)
        {
            ClearPathVisualization();
            GD.Print("No path found.");
            return;
        }

        _isMoving = true;
        _pathOwnerNode = null;
        _pathOwnerId = null;
        _pathMesh = _pathfinding.VisualizePath(path, this, _pathMesh);
        _gimbal?.BeginFollow(node);
        _phase = BattlePhase.Moving;
        var tween = GetTree().CreateTween();
        const float moveSpeed = 6f; // units per second
        for (int i = 1; i < path.Length; i++)
        {
            var from = path[i - 1];
            var to = path[i];
            var segmentDist = from.DistanceTo(to);
            var duration = segmentDist / moveSpeed;
            tween.TweenProperty(node, "global_position", to, duration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
        }

        tween.TweenCallback(Callable.From(() =>
        {
            _battleManager?.ConsumeTurn(unitId);
            _gimbal?.StopFollow();
            _isMoving = false;
            _phase = BattlePhase.Idle;
            ClearPathVisualization();
            _pendingTurnAdvance = true;
        }));
    }

    private void ExecuteAttack(string attackerId, string targetId, TimingBarInput input)
    {
        if (_battleManager is null) return;
        var result = _battleManager.ExecuteAbility("basic_attack", attackerId, targetId, input, Facing.Front);
        UpdateHealthBar(targetId);
        HandleDeaths(targetId);
        _pendingTurnAdvance = true;
        HideAbilityPanel();
    }

    private void BeginQte(string attackerId, string targetId)
    {
        HideAbilityPanel();
        _qteActive = true;
        _qteTimer = 0f;
        _qteAttacker = attackerId;
        _qteTarget = targetId;
        _qteTargetTime = _qteDuration * 0.5f;
        _qteCritWindow = 0.1f;
        _qteGreatWindow = _qteCritWindow * 2.5f;
        _qteGoodWindow = _qteCritWindow * 4f;
        _phase = BattlePhase.Qte;
        if (_qteRoot != null) _qteRoot.Visible = true;
        if (_qteBar != null) _qteBar.Value = 0;
        if (_qteLabel != null) _qteLabel.Text = "Timing! Press Space";
        UpdateQteZoneLayout();
    }

    private void ProcessQte(double delta)
    {
        if (!_qteActive) return;
        _qteTimer += (float)delta;
        var progress = Mathf.Clamp(_qteTimer / _qteDuration, 0f, 1f);
        UpdateQteVisuals(progress);
        if (_qteBar != null)
        {
            _qteBar.MaxValue = 1;
            _qteBar.Value = progress;
        }

        if (Input.IsActionJustPressed("attack"))
        {
            CompleteQte(_qteTimer);
            return;
        }

        if (_qteTimer >= _qteDuration)
        {
            CompleteQte(_qteDuration + 0.5f);
        }
    }

    private void CompleteQte(float pressTime)
    {
        _qteActive = false;
        _qteRoot?.Hide();
        if (_qteAttacker is null || _qteTarget is null) return;

        var delta = Math.Abs(pressTime - _qteTargetTime);
        var input = new TimingBarInput(_qteTargetTime, pressTime);
        ExecuteAttack(_qteAttacker, _qteTarget, input);
        _qteAttacker = null;
        _qteTarget = null;
        _phase = BattlePhase.Idle;
        UpdateQteVisuals(0);
        HideAbilityPanel();
    }

    private void ProcessAiTurn()
    {
        if (_battleManager is null) return;
        if (_activeUnitId == string.Empty) return;
        if (!_unitTeams.TryGetValue(_activeUnitId, out var team)) return;
        const int AiTeam = 2;
        if (team != AiTeam) return;
        if (_aiPending) return;

        _aiPending = true;
        var target = SelectAiTarget(team);
        if (target == null)
        {
            _pendingTurnAdvance = true;
            return;
        }

        ExecuteAttack(_activeUnitId, target, new TimingBarInput(_qteTargetTime, _qteTargetTime));
        _aiPending = false;
    }

    private string? SelectAiTarget(int aiTeam)
    {
        var enemies = _unitTeams.Where(kvp => kvp.Value != aiTeam)
            .Select(kvp => kvp.Key)
            .Where(id => GetNodeById(id) != null)
            .ToList();
        if (enemies.Count == 0) return null;

        var selfNode = GetNodeById(_activeUnitId);
        if (selfNode is null) return enemies.First();

        return enemies
            .OrderBy(id => selfNode.GlobalPosition.DistanceTo(GetNodeById(id)!.GlobalPosition))
            .First();
    }

    private void CreateCamera()
    {
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

    private void CreatePathfinding()
    {
        _pathfinding = new AstarPathfinding();
        _pathfinding.SetupGrid(_mapSize.X, _mapSize.Y, TileSize);
        AddChild(_pathfinding);
    }

    private void BuildMap()
    {
        if (_mapLoader.TryLoad(DemoMapPath, out var mapData))
        {
            _loadedMap = mapData;
            _cellElevations = mapData.Cells.ToDictionary(c => new Vector2I(c.X, c.Y), c => c.Elevation);
            _mapSize = new Vector2I(mapData.Width, mapData.Height);
            _mapCenter = _mapBuilder.BuildFromMapData(this, mapData, TileSize, TileHeight).Center;
            return;
        }

        _cellElevations = new Dictionary<Vector2I, int>();
        GD.Print($"Falling back to flat grid because map could not be loaded from {DemoMapPath}.");
        _mapCenter = _mapBuilder.BuildFlatGrid(this, _mapSize, TileSize, TileHeight).Center;
    }

    private void EnsureInputMap()
    {
        AddActionIfMissing("cam_forward", new InputEventKey { Keycode = Key.W });
        AddActionIfMissing("cam_back", new InputEventKey { Keycode = Key.S });
        AddActionIfMissing("cam_left", new InputEventKey { Keycode = Key.A });
        AddActionIfMissing("cam_right", new InputEventKey { Keycode = Key.D });
        AddActionIfMissing("attack", new InputEventKey { Keycode = Key.Space });
    }

    private static void AddActionIfMissing(string name, InputEvent @event)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, @event);
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
        if (_cursor is null) return;
        RefreshPathVisualization();
    }

    private void RefreshPathVisualization()
    {
        if (_pathfinding is null || _cursor is null) return;
        if (!TryGetActiveNode(out var activeNode)) return;
        if (_isMoving) return;

        var targetCell = _pathfinding.WorldToCell(_cursor.GetSelectedTile());
        var occupied = IsCellOccupied(targetCell, _activeUnitId);
        _cursor.SetOccupied(occupied);
        if (occupied)
        {
            ClearPathVisualization();
            return;
        }

        var path = GetElevatedPath(activeNode.GlobalPosition, _cursor.GetSelectedTile());
        if (path.Length < 2)
        {
            ClearPathVisualization();
            return;
        }

        _pathOwnerNode = activeNode;
        _pathOwnerId = _activeUnitId;
        _pathMesh = _pathfinding.VisualizePath(path, this, _pathMesh);
    }

    private void ClearPathVisualization()
    {
        if (_pathMesh != null && _pathMesh.IsInsideTree())
            _pathMesh.QueueFree();

        _pathMesh = null;
        _pathOwnerNode = null;
        _pathOwnerId = null;
    }

    private Vector3[] GetElevatedPath(Vector3 start, Vector3 end)
    {
        if (_pathfinding is null) return System.Array.Empty<Vector3>();
        var destCell = _pathfinding.WorldToCell(end);
        if (IsCellOccupied(destCell, _activeUnitId))
            return System.Array.Empty<Vector3>();

        var rawPath = _pathfinding.GetPath(start, end);
        if (rawPath.Length == 0) return rawPath;

        var adjusted = new Vector3[rawPath.Length];
        for (int i = 0; i < rawPath.Length; i++)
        {
            var cell = _pathfinding.WorldToCell(rawPath[i]);
            adjusted[i] = ConvertSpawnToWorld(new Vector2I(cell.X, cell.Z));
        }

        // Ensure the starting point matches the current node height exactly.
        adjusted[0] = start;
        return adjusted;
    }

    private void FocusCameraOnActiveUnit()
    {
        if (_gimbal is null) return;
        var targetNode = GetNodeById(_activeUnitId);
        if (targetNode is null) return;

        _gimbal.SmoothFocus(targetNode.GlobalPosition);
    }

    private Node3D? GetNodeById(string unitId) =>
        _unitNodes.TryGetValue(unitId, out var node) ? node : null;

    private Node3D? GetNodeAtPosition(Vector3 position, string? excludeId = null)
    {
        if (_pathfinding is null) return null;
        var cell = _pathfinding.WorldToCell(position);
        foreach (var kvp in _unitNodes)
        {
            if (excludeId != null && kvp.Key == excludeId) continue;
            if (kvp.Value is null) continue;
            var nodeCell = _pathfinding.WorldToCell(kvp.Value.GlobalPosition);
            if (nodeCell.X == cell.X && nodeCell.Z == cell.Z)
                return kvp.Value;
        }

        return null;
    }

    private bool IsCellOccupied(Vector3I cell, string? ignoreId = null)
    {
        foreach (var kvp in _unitNodes)
        {
            if (ignoreId != null && kvp.Key == ignoreId) continue;
            var node = kvp.Value;
            if (node is null || _pathfinding is null) continue;
            var nodeCell = _pathfinding.WorldToCell(node.GlobalPosition);
            if (nodeCell == cell)
                return true;
        }

        return false;
    }

    private void AssignUnitNode(string id, Node3D node)
    {
        _unitNodes[id] = node;
    }

    private static UnitState CreateUnitState(UnitBlueprint blueprint) =>
        new(blueprint.Id, blueprint.BaseStats, blueprint.Affinity, blueprint.MoveRange);

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

    private static bool IsAttackJustPressed() => Input.IsActionJustPressed("attack");

    private Vector3 ResolveSpawnPosition(UnitContentLoader.UnitDefinition unit, Dictionary<int, int> spawnCursorByTeam)
    {
        if (_loadedMap == null) return GetFallbackSpawn(unit.SpawnCell);

        if (!spawnCursorByTeam.TryGetValue(unit.Team, out var index))
            index = 0;

        var spawn = _loadedMap.Spawns.Where(s => s.Team == unit.Team).Skip(index).FirstOrDefault();
        if (spawn != null)
        {
            spawnCursorByTeam[unit.Team] = index + 1;
            return ConvertSpawnToWorld(new Vector2I(spawn.X, spawn.Y));
        }

        GD.Print($"No spawn point found for team {unit.Team}; falling back to unit-specified spawn.");
        return GetFallbackSpawn(unit.SpawnCell);
    }

    private Vector3 ConvertSpawnToWorld(Vector2I cell)
    {
        var elevation = GetElevationAt(cell);
        return _mapBuilder.CellToWorld(cell, TileSize, TileHeight, elevation);
    }

    private Vector3 GetFallbackSpawn(Vector2I cell)
    {
        var elevation = GetElevationAt(cell);
        return _mapBuilder.CellToWorld(cell, TileSize, TileHeight, elevation);
    }

    private int GetElevationAt(Vector2I cell) => _cellElevations.TryGetValue(cell, out var e) ? e : 0;

    private Node3D CreateUnitNode(string id, Vector3 position, Color color)
    {
        var node = new Node3D { Name = id, Position = position };
        node.AddChild(BuildCapsuleMesh(color));
        AddChild(node);
        return node;
    }

    private void AttachHealthBar(Node3D node, UnitState state)
    {
        var label = new Label3D
        {
            Name = $"{state.Id}_Health",
            Text = $"{state.CurrentHP}/{state.Stats.MaxHP}",
            Position = new Vector3(0, 2.2f, 0),
            PixelSize = 0.01f
        };
        _healthBars[state.Id] = label;
        node.AddChild(label);
    }

    private void UpdateHealthBar(string unitId)
    {
        if (_battleManager is null) return;
        if (!_healthBars.TryGetValue(unitId, out var label)) return;
        if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) return;

        label.Text = $"{state.CurrentHP}/{state.Stats.MaxHP}";
    }

    private void UpdateHealthBarFacing()
    {
        if (_gimbal?.Camera is null) return;
        var camPos = _gimbal.Camera.GlobalTransform.Origin;
        foreach (var label in _healthBars.Values)
        {
            if (!IsInstanceValid(label)) continue;
            label.LookAt(camPos, Vector3.Up);
            label.RotateObjectLocal(Vector3.Up, Mathf.Pi); // orient text toward camera
        }
    }

    private void HandleDeaths(string unitId)
    {
        if (_battleManager is null) return;
        if (!_battleManager.TryGetUnit(unitId, out var state) || state is null) return;
        if (state.IsAlive) return;

        RemoveUnit(unitId);
        CheckBattleEnd();
    }

    private void RemoveUnit(string unitId)
    {
        var node = GetNodeById(unitId);
        node?.QueueFree();
        if (_battleManager is not null)
            _battleManager.RemoveUnit(unitId);

        if (_healthBars.TryGetValue(unitId, out var label) && IsInstanceValid(label))
        {
            label.QueueFree();
            _healthBars.Remove(unitId);
        }

        _unitTeams.Remove(unitId);
        _unitNodes.Remove(unitId);

        // If the active unit was removed, advance to next available.
        if (_activeUnitId == unitId)
            AdvanceTurnAndFocus();

        UpdateTurnOrderUi();
    }

    private void CreateBattleUi()
    {
        _uiRoot = new Control
        {
            Name = "BattleUI",
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1
        };
        AddChild(_uiRoot);

        _gameOverLabel = new Label
        {
            Name = "GameOverLabel",
            Text = "Game Over",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _gameOverLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.2f));
        _gameOverLabel.AddThemeFontSizeOverride("font_size", 36);
        _gameOverLabel.AnchorLeft = 0.5f;
        _gameOverLabel.AnchorRight = 0.5f;
        _gameOverLabel.AnchorTop = 0.5f;
        _gameOverLabel.AnchorBottom = 0.5f;
        _gameOverLabel.OffsetLeft = -150;
        _gameOverLabel.OffsetRight = 150;
        _gameOverLabel.OffsetTop = -40;
        _gameOverLabel.OffsetBottom = 0;
        _uiRoot.AddChild(_gameOverLabel);

        _restartButton = new Button
        {
            Name = "RestartButton",
            Text = "Restart",
            Visible = false,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -60,
            OffsetRight = 60,
            OffsetTop = 20,
            OffsetBottom = 60
        };
        _restartButton.Pressed += RestartBattle;
        _uiRoot.AddChild(_restartButton);

        CreateTurnOrderPanel();
        CreateQtePanel();
        CreatePhaseLabel();
        CreateAbilityPanel();
    }

    private void ShowGameOver()
    {
        _qteActive = false;
        _phase = BattlePhase.Ended;
        _qteRoot?.Hide();
        if (_gameOverLabel != null) _gameOverLabel.Visible = true;
        if (_restartButton != null) _restartButton.Visible = true;
    }

    private void UpdatePhaseLabel()
    {
        if (_phaseLabel is null) return;
        _phaseLabel.Text = $"Phase: {_phase}";
    }

    private void RestartBattle()
    {
        GetTree().ReloadCurrentScene();
    }

    private void CreateTurnOrderPanel()
    {
        const int slotCount = 6;
        var panel = new PanelContainer
        {
            Name = "TurnOrderPanel",
            AnchorRight = 1,
            AnchorLeft = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -220,
            OffsetRight = -20,
            OffsetTop = 20,
            OffsetBottom = 220
        };

        var vbox = new VBoxContainer
        {
            Name = "TurnOrderList",
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 10,
            OffsetRight = -10,
            OffsetTop = 10,
            OffsetBottom = -10
        };

        panel.AddChild(vbox);
        _uiRoot?.AddChild(panel);

        for (int i = 0; i < slotCount; i++)
        {
            var label = new Label
            {
                Text = "--",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _turnOrderLabels.Add(label);
            vbox.AddChild(label);
        }

        UpdateTurnOrderUi();
    }

    private void CreateAbilityPanel()
    {
        _abilityRoot = new PanelContainer
        {
            Name = "AbilityPanel",
            Visible = false,
            AnchorLeft = 0.35f,
            AnchorRight = 0.65f,
            AnchorTop = 0.2f,
            AnchorBottom = 0.45f
        };

        _abilityList = new VBoxContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 10,
            OffsetRight = -10,
            OffsetTop = 10,
            OffsetBottom = -10
        };

        _abilityRoot.AddChild(_abilityList);
        _uiRoot?.AddChild(_abilityRoot);
    }

    private void UpdateTurnOrderUi()
    {
        if (_battleManager is null || _turnOrderLabels.Count == 0) return;

        var predicted = _battleManager.GetPredictedTurnOrder(_turnOrderLabels.Count).ToList();
        for (int i = 0; i < _turnOrderLabels.Count; i++)
        {
            var labelIndex = _turnOrderLabels.Count - 1 - i; // active at bottom
            var label = _turnOrderLabels[labelIndex];
            if (i < predicted.Count)
            {
                var entry = predicted[i];
                var status = entry.UnitId == _activeUnitId ? " (active)" : entry.IsReady ? " (ready)" : "";
                label.Text = $"{entry.UnitId}{status}";
            }
            else
            {
                label.Text = "--";
            }
        }
    }

    private void CreateQtePanel()
    {
        _qteRoot = new PanelContainer
        {
            Name = "QTEPanel",
            Visible = false,
            AnchorLeft = 0.3f,
            AnchorRight = 0.7f,
            AnchorTop = 0.7f,
            AnchorBottom = 0.85f
        };

        var vbox = new VBoxContainer
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = 12,
            OffsetRight = -12,
            OffsetTop = 12,
            OffsetBottom = -12
        };

        _qteLabel = new Label
        {
            Text = "Timing!",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        vbox.AddChild(_qteLabel);

        _qteTrackContainer = new Control
        {
            AnchorLeft = 0,
            AnchorTop = 0,
            AnchorRight = 0,
            AnchorBottom = 0,
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(320, 24)
        };

        // Zones parented to a single container to avoid anchor/size warnings.
        _qteTrack = new ColorRect { Color = new Color(0.75f, 0.65f, 0.35f) };
        _qteGoodZone = new ColorRect { Color = new Color(0.95f, 0.8f, 0.25f, 0.9f) };
        _qteGreatZone = new ColorRect { Color = new Color(0.95f, 0.45f, 0.35f, 0.9f) };
        _qteCritZone = new ColorRect { Color = new Color(0.2f, 0.45f, 1f, 0.95f) };
        _qteIndicator = new ColorRect { Color = new Color(0.08f, 0.08f, 0.08f), CustomMinimumSize = new Vector2(10, 28) };

        _qteTrackContainer.AddChild(_qteTrack);
        _qteTrackContainer.AddChild(_qteGoodZone);
        _qteTrackContainer.AddChild(_qteGreatZone);
        _qteTrackContainer.AddChild(_qteCritZone);
        _qteTrackContainer.AddChild(_qteIndicator);

        _qteGoodLabel = CreateZoneLabel(string.Empty);
        _qteGreatLabel = CreateZoneLabel(string.Empty);
        _qteCritLabel = CreateZoneLabel(string.Empty);
        _qteTrackContainer.AddChild(_qteGoodLabel);
        _qteTrackContainer.AddChild(_qteGreatLabel);
        _qteTrackContainer.AddChild(_qteCritLabel);

        _qteTrackContainer.Resized += UpdateQteZoneLayout;
        UpdateQteZoneLayout();

        vbox.AddChild(_qteTrackContainer);

        _qteRoot.AddChild(vbox);
        _uiRoot?.AddChild(_qteRoot);
    }

    private void ClearChildren(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            if (child is Node node)
                node.QueueFree();
        }
    }

    private void UpdateQteZoneLayout()
    {
        if (_qteTrackContainer is null || _qteIndicator is null || _qteTrack is null || _qteGreatZone is null || _qteCritZone is null || _qteGoodZone is null) return;

        var width = _qteTrackContainer.Size.X;
        var height = Mathf.Max(_qteTrackContainer.Size.Y, 24f);
        if (width <= 0 || height <= 0) return;

        width = Mathf.Max(width, _qteTrackContainer.CustomMinimumSize.X);
        height = Mathf.Max(height, _qteTrackContainer.CustomMinimumSize.Y);

        var targetNorm = _qteTargetTime / _qteDuration;
        float critHalf = _qteCritWindow / _qteDuration;
        float greatHalf = _qteGreatWindow / _qteDuration;
        float goodHalf = _qteGoodWindow / _qteDuration;

        float critStart = Mathf.Clamp(targetNorm - critHalf, 0f, 1f) * width;
        float critEnd = Mathf.Clamp(targetNorm + critHalf, 0f, 1f) * width;
        float greatStart = Mathf.Clamp(targetNorm - greatHalf, 0f, 1f) * width;
        float greatEnd = Mathf.Clamp(targetNorm + greatHalf, 0f, 1f) * width;
        float goodStart = Mathf.Clamp(targetNorm - goodHalf, 0f, 1f) * width;
        float goodEnd = Mathf.Clamp(targetNorm + goodHalf, 0f, 1f) * width;

        _qteTrack.CustomMinimumSize = new Vector2(_qteTrack.CustomMinimumSize.X, height);
        SetDeferredRect(_qteGoodZone, goodStart, goodEnd, height);
        SetDeferredRect(_qteGreatZone, greatStart, greatEnd, height);
        SetDeferredRect(_qteCritZone, critStart, critEnd, height);

        PositionZoneLabel(_qteGoodLabel, goodStart, goodEnd, height);
        PositionZoneLabel(_qteGreatLabel, greatStart, greatEnd, height);
        PositionZoneLabel(_qteCritLabel, critStart, critEnd, height);
    }

    private void UpdateQteVisuals(float progress)
    {
        if (_qteIndicator is null || _qteTrackContainer is null) return;
        var width = _qteTrackContainer.Size.X;
        var indicatorWidth = _qteIndicator.Size.X > 0 ? _qteIndicator.Size.X : 8;
        var x = Mathf.Clamp(progress * width - indicatorWidth * 0.5f, 0, Mathf.Max(0, width - indicatorWidth));
        _qteIndicator.SetDeferred("position", new Vector2(x, 0));
    }

    private static void SetDeferredRect(ColorRect rect, float start, float end, float height)
    {
        var size = Mathf.Max(0, end - start);
        rect.SetDeferred("position", new Vector2(start, 0));
        rect.SetDeferred("size", new Vector2(size, height));
    }

    private void PositionZoneLabel(Label? label, float start, float end, float height)
    {
        if (label is null) return;
        var zoneWidth = Mathf.Max(0, end - start);
        var labelWidth = label.GetMinimumSize().X;
        var center = start + zoneWidth * 0.5f;
        var posX = center - labelWidth * 0.5f;
        label.SetDeferred("position", new Vector2(posX, 0));
        label.SetDeferred("size", new Vector2(zoneWidth, height));
        label.SetDeferred("horizontal_alignment", (int)HorizontalAlignment.Center);
        label.SetDeferred("vertical_alignment", (int)VerticalAlignment.Center);
    }

    private static Label CreateZoneLabel(string text)
    {
        return new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = new Color(0, 0, 0, 0.85f)
        };
    }

    private void ShowAbilityPanel(string attackerId, string targetId)
    {
        if (_abilityRoot is null || _abilityList is null) return;
        if (!_unitAbilities.TryGetValue(attackerId, out var abilities)) return;

        ClearChildren(_abilityList);
        foreach (var abilityId in abilities)
        {
            var button = new Button { Text = abilityId };
            button.Pressed += () => OnAbilitySelected(attackerId, targetId, abilityId);
            _abilityList.AddChild(button);
        }

        _pendingAttackerId = attackerId;
        _pendingTargetId = targetId;
        _abilityRoot.Visible = true;
        _abilityPanelOpen = true;
    }

    private void HideAbilityPanel()
    {
        if (_abilityRoot != null) _abilityRoot.Visible = false;
        _abilityPanelOpen = false;
        _pendingAttackerId = null;
        _pendingTargetId = null;
    }

    private void OnAbilitySelected(string attackerId, string targetId, string abilityId)
    {
        HideAbilityPanel();
        var isPlayerControlled = _unitTeams.TryGetValue(attackerId, out var team) && team <= 1;
        if (isPlayerControlled)
        {
            BeginQte(attackerId, targetId);
        }
        else
        {
            ExecuteAttack(attackerId, targetId, new TimingBarInput(_qteTargetTime, _qteTargetTime));
        }
    }

    private void CreatePhaseLabel()
    {
        _phaseLabel = new Label
        {
            Name = "PhaseLabel",
            Text = "Phase: Idle",
            AnchorLeft = 0.4f,
            AnchorRight = 0.6f,
            AnchorTop = 0.02f,
            AnchorBottom = 0.1f,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _uiRoot?.AddChild(_phaseLabel);
    }

    private void CheckBattleEnd()
    {
        var aliveTeams = _unitTeams.Values.Distinct().ToList();
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
        if (_isMoving || _phase == BattlePhase.Moving || _phase == BattlePhase.Qte) return;
        if (!_pendingTurnAdvance) return;

        _pendingTurnAdvance = false;
        AdvanceTurnAndFocus();
    }

    private MeshInstance3D BuildCapsuleMesh(Color color)
    {
        var mesh = new CapsuleMesh
        {
            Radius = 0.5f,
            Height = 1.6f
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = color
        };

        return new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Position = new Vector3(0, 0.8f, 0)
        };
    }
}
