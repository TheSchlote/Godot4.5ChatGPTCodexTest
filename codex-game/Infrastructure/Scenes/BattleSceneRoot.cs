using CodexGame.Application.Battle;
using CodexGame.Domain.QTE;
using CodexGame.Domain.Stats;
using CodexGame.Domain.Units;
using Godot;
using CodexGame.Infrastructure.Pathfinding;
using CodexGame.Infrastructure.Scenes;
using System;
using System.Linq;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Minimal scene entry that wires Godot nodes to the domain battle manager.
/// </summary>
public partial class BattleSceneRoot : Node3D
{
    private BattleManager? _battleManager;
    private SelectionCursor? _cursor;
    private AstarPathfinding? _pathfinding;
    private MeshInstance3D? _pathMesh;
    private Node3D? _pathOwnerNode;
    private string? _pathOwnerId;
    private Node3D? _playerNode;
    private Node3D? _enemyNode;
    private DirectionalLight3D? _light;
    private Node3D? _followNode;
    private Gimbal? _gimbal;
    private bool _isMoving;
    private string _activeUnitId = "Player";
    private Vector3 _mapCenter = Vector3.Zero;
    private Vector2I _mapSize = new(8, 8);
    private float _moveCooldown;
    private const float MoveRepeatDelay = 0.18f;
    private const float TileSize = 2f;

    private Node3D? _cameraRig;
    private Camera3D? _camera;
    private float _yaw = -Mathf.Pi / 4f;
    private float _pitch = -0.7f;
    private float _camDistance = 22f;
    private const float MinDistance = 8f;
    private const float MaxDistance = 40f;
    private const float CamMoveSpeed = 14f;
    private const float CamFollowLerp = 6f;
    private bool _rotatingCamera;

    public override void _Ready()
    {
        EnsureInputMap();
        _battleManager = new BattleManager();
        AddChild(_battleManager);

        BuildDemoMap();
        CreateCamera();
        CreateLight();
        CreatePathfinding();
        SpawnDemoUnits();
        CreateCursor();
    }

    private void SpawnDemoUnits()
    {
        if (_battleManager is null) return;

        _playerNode = new Node3D { Name = "Player", Position = new Vector3(2, 0, 2) };
        _playerNode.AddChild(BuildCapsuleMesh(new Color(0.2f, 0.7f, 1f)));
        AddChild(_playerNode);
        _enemyNode = new Node3D { Name = "Enemy", Position = new Vector3(8, 0, 6) };
        _enemyNode.AddChild(BuildCapsuleMesh(new Color(1f, 0.4f, 0.2f)));
        AddChild(_enemyNode);

        var player = new UnitState("Player", new StatBlock(30, 10, 15, 5, 8, 6, 12), Element.Fire, moveRange: 4);
        var enemy = new UnitState("Enemy", new StatBlock(25, 8, 10, 4, 7, 5, 10), Element.Water, moveRange: 3);

        _battleManager.RegisterUnit(_playerNode, player);
        _battleManager.RegisterUnit(_enemyNode, enemy);
        FocusCameraOnActiveUnit();
        DebugPrintTurnOrder();
    }

    public override void _Process(double delta)
    {
        _moveCooldown = Math.Max(0, _moveCooldown - (float)delta);
        HandleUnitInput();
        UpdateLight();
        UpdateCursorFollow();
        UpdateCameraFollow(delta);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Right)
                _rotatingCamera = true;
            if (mb.ButtonIndex == MouseButton.WheelUp)
                _camDistance = Mathf.Clamp(_camDistance - 2f, MinDistance, MaxDistance);
            if (mb.ButtonIndex == MouseButton.WheelDown)
                _camDistance = Mathf.Clamp(_camDistance + 2f, MinDistance, MaxDistance);
        }
        else if (@event is InputEventMouseButton mbRelease && !mbRelease.Pressed && mbRelease.ButtonIndex == MouseButton.Right)
        {
            _rotatingCamera = false;
        }

        if (_rotatingCamera && @event is InputEventMouseMotion motion)
        {
            _yaw -= motion.Relative.X * 0.005f;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * 0.005f, -1.2f, -0.1f);
            UpdateCameraTransform();
        }
    }

    private void HandleUnitInput()
    {
        if (_battleManager is null || _playerNode is null || _enemyNode is null) return;

        if (IsTabJustPressed())
        {
            _activeUnitId = _activeUnitId == "Player" ? "Enemy" : "Player";
            FocusCameraOnActiveUnit();
            DebugPrintTurnOrder();
        }

        if (IsAttackJustPressed())
            HandleActionAtCursor();
    }

    private void HandleCameraMovement(double delta)
    {
        if (_cameraRig is null) return;

        var basis = Basis.Identity.Rotated(Vector3.Up, _yaw);
        var forward = -basis.Z;
        var right = basis.X;
        forward.Y = 0;
        right.Y = 0;
        forward = forward.Normalized();
        right = right.Normalized();

        var move = Vector3.Zero;
        if (Input.IsActionPressed("cam_forward") || Input.IsActionPressed("ui_up")) move += forward;
        if (Input.IsActionPressed("cam_back") || Input.IsActionPressed("ui_down")) move -= forward;
        if (Input.IsActionPressed("cam_left") || Input.IsActionPressed("ui_left")) move -= right;
        if (Input.IsActionPressed("cam_right") || Input.IsActionPressed("ui_right")) move += right;

        if (move != Vector3.Zero)
        {
            _cameraRig.GlobalPosition += move.Normalized() * CamMoveSpeed * (float)delta;
            _followNode = null; // manual control overrides follow
            UpdateCameraTransform();
        }
    }

    private bool TryGetActiveNode(out Node3D node)
    {
        node = _activeUnitId == "Player" ? _playerNode! : _enemyNode!;
        return node is not null;
    }

    private void HandleActionAtCursor()
    {
        if (_battleManager is null || _cursor is null) return;

        var activeNode = _pathOwnerNode;
        var attackerId = _pathOwnerId ?? _activeUnitId;
        if (activeNode is null || string.IsNullOrEmpty(attackerId))
        {
            if (!TryGetActiveNode(out activeNode))
                return;
            attackerId = activeNode.Name;
            _activeUnitId = attackerId;
        }
        var targetId = attackerId == "Player" ? "Enemy" : "Player";
        var targetNode = targetId == "Player" ? _playerNode : _enemyNode;
        var cursorPos = _cursor.GetSelectedTile();

        if (targetNode != null && cursorPos.DistanceTo(targetNode.GlobalPosition) < 0.6f)
        {
            var result = _battleManager.ExecuteAbility("basic_attack", attackerId, targetId, new TimingBarInput(0, 0), Facing.Front);
            GD.Print($"{attackerId} hit {targetId} for {result.Damage} damage.");
        }
        else
        {
            StartMoveAlongPath(attackerId, activeNode, cursorPos);
            return; // movement handles turn advance when done
        }

        AdvanceTurnAndFocus();
        DebugPrintTurnOrder();
    }

    private void StartMoveAlongPath(string unitId, Node3D node, Vector3 destination)
    {
        if (_pathfinding is null) return;
        var path = _pathfinding.GetPath(node.GlobalPosition, destination);
        if (path.Length < 2)
        {
            GD.Print("No path found.");
            return;
        }

        _followNode = node;
        _isMoving = true;
        _pathOwnerNode = null;
        _pathOwnerId = null;
        _pathMesh = _pathfinding.VisualizePath(path, this, _pathMesh);
        _gimbal?.BeginFollow(node);
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
            _followNode = null;
            _gimbal?.StopFollow();
            _isMoving = false;
            if (_pathMesh != null && _pathMesh.IsInsideTree())
            {
                _pathMesh.QueueFree();
                _pathMesh = null;
            }
            AdvanceTurnAndFocus();
            DebugPrintTurnOrder();
        }));
    }

    private void BuildDemoMap()
    {
        var tileMesh = new BoxMesh
        {
            Size = new Vector3(TileSize, 0.2f, TileSize)
        };

        for (var x = 0; x < _mapSize.X; x++)
        {
            for (var z = 0; z < _mapSize.Y; z++)
            {
                var tile = new MeshInstance3D
                {
                    Mesh = tileMesh,
                    Position = new Vector3(x * TileSize, -0.1f, z * TileSize),
                    Name = $"Tile_{x}_{z}"
                };
                AddChild(tile);
            }
        }

        _mapCenter = new Vector3((_mapSize.X - 1) * TileSize * 0.5f, 0, (_mapSize.Y - 1) * TileSize * 0.5f);
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
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.2f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoTexture = null };
        _cursor.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = mat });
    }

    private void CreatePathfinding()
    {
        _pathfinding = new AstarPathfinding();
        _pathfinding.SetupGrid(_mapSize.X, _mapSize.Y, TileSize);
        AddChild(_pathfinding);
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

    private void UpdateCursorFollow()
    {
        if (_cursor is null) return;
        RefreshPathVisualization();
    }

    private void RefreshPathVisualization()
    {
        if (_pathfinding is null || _cursor is null) return;
        if (!TryGetActiveNode(out var activeNode)) return;
        if (_isMoving) return;

        var path = _pathfinding.GetPath(activeNode.GlobalPosition, _cursor.GetSelectedTile());
        if (path.Length < 2)
        {
            if (_pathMesh != null && _pathMesh.IsInsideTree())
                _pathMesh.QueueFree();
            _pathMesh = null;
            _pathOwnerNode = null;
            _pathOwnerId = null;
            return;
        }

        _pathOwnerNode = activeNode;
        _pathOwnerId = _activeUnitId;
        _pathMesh = _pathfinding.VisualizePath(path, this, _pathMesh);
    }

    private void UpdateCameraFollow(double delta)
    {
        if (_followNode is null || _gimbal is null) return;
        _gimbal.BeginFollow(_followNode);
    }

    private void FocusCameraOnActiveUnit()
    {
        if (_gimbal is null) return;
        var targetNode = _activeUnitId == "Player" ? _playerNode : _enemyNode;
        if (targetNode is null) return;

        _gimbal.SmoothFocus(targetNode.GlobalPosition);
        _followNode = null;
    }

    private void SelectActiveByCursor()
    {
        // Intentionally left empty; active unit is determined by turn order, not cursor proximity.
    }

    private void DebugPrintTurnOrder()
    {
        if (_battleManager is null) return;
        var snapshot = _battleManager.GetTurnOrderSnapshot();
        var output = string.Join(", ",
            snapshot.Select(s =>
            {
                var pos = GetNodePosition(s.UnitId);
                var tag = s.UnitId == _activeUnitId ? "(active)" : s == snapshot.Skip(1).FirstOrDefault() ? "(next)" : "";
                return $"{s.UnitId}{tag}@{pos}: {s.TurnValue:0.0}{(s.IsReady ? "*" : "")}";
            }));
        GD.Print($"Turn Order -> {output}");
    }

    private string GetNodePosition(string unitId)
    {
        var node = unitId == "Player" ? _playerNode : _enemyNode;
        if (node == null) return "(null)";
        return $"({node.GlobalPosition.X:0.0},{node.GlobalPosition.Y:0.0},{node.GlobalPosition.Z:0.0})";
    }

    private void AdvanceTurnAndFocus()
    {
        if (_battleManager is null) return;

        var ready = _battleManager.AdvanceToNextReady();
        if (!string.IsNullOrEmpty(ready))
        {
            _activeUnitId = ready;
            FocusCameraOnActiveUnit();
        }
    }

    private static bool IsTabJustPressed() => Input.IsKeyPressed(Key.Tab) || Input.IsActionJustPressed("ui_select");
    private static bool IsAttackJustPressed() => Input.IsActionJustPressed("attack");

    private void UpdateCameraTransform()
    {
        if (_cameraRig is null || _camera is null) return;

        var dir = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch));

        var camPos = _cameraRig.GlobalPosition - dir * _camDistance + new Vector3(0, 4f, 0);
        _camera.GlobalTransform = new Transform3D(Basis.Identity, camPos);
        _camera.LookAt(_cameraRig.GlobalPosition, Vector3.Up);
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
