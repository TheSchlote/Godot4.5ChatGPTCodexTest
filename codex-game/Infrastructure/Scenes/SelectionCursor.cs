using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Grid-snapped cursor inspired by the provided mock; operates on a simple rectangular grid.
/// </summary>
public partial class SelectionCursor : Node3D
{
    [Export] public float MoveSpeed { get; set; } = 10f;
    [Export] public float SnapDelay { get; set; } = 0.2f;
    [Export] public float SnapSpeed { get; set; } = 0.2f;
    [Export] public float HeightTransitionSpeed { get; set; } = 0.1f;
    [Export] public Camera3D? Camera { get; set; }
    [Export] public Vector2I MapSize { get; set; } = new(8, 8);
    [Export] public float TileSize { get; set; } = 2f;

    private Vector3 _velocity = Vector3.Zero;
    private Vector3 _targetGridPosition;
    private float _snapTimer;
    private bool _shouldSnap;
    private bool _followCamera = true;
    private bool _manualInput;
    private MeshInstance3D? _indicatorMesh;
    private Color _normalColor = new Color(0.9f, 0.9f, 0.2f);
    private Color _occupiedColor = new Color(0.35f, 0.8f, 1.0f);

    public bool InputEnabled { get; set; } = true;
    public void SyncToCameraLook()
    {
        if (Camera is null) return;
        FollowCameraLook(Camera);
    }

    public override void _Ready()
    {
        _targetGridPosition = Position;
    }

    public override void _Process(double delta)
    {
        if (!InputEnabled) return;
        HandleInput(delta);
        MoveCursor(delta);

        if (Input.IsActionJustPressed("ui_accept"))
            GD.Print("Cursor is on tile: " + PositionToCell(Position));
    }

    public void RegisterIndicatorMesh(MeshInstance3D mesh, Color normalColor, Color occupiedColor)
    {
        _indicatorMesh = mesh;
        _normalColor = normalColor;
        _occupiedColor = occupiedColor;
        ApplyIndicatorColor(_normalColor);
    }

    public void SetOccupied(bool occupied) => ApplyIndicatorColor(occupied ? _occupiedColor : _normalColor);

    private void HandleInput(double delta)
    {
        if (Camera is null) return;

        var inputDirection = GetMovementDirection();
        _manualInput = inputDirection != Vector3.Zero;
        if (inputDirection == Vector3.Zero)
        {
            _velocity = Vector3.Zero;
            HandleSnapTimer(delta);
            if (_followCamera)
                FollowCameraLook(Camera);
            return;
        }

        var targetCell = PositionToCell(Position + inputDirection * TileSize);
        targetCell = ClampToMap(targetCell);
        _targetGridPosition = CellToPosition(targetCell);

        _velocity = inputDirection * MoveSpeed;
        _shouldSnap = true;
        _snapTimer = 0f;
    }

    private Vector3 GetMovementDirection()
    {
        if (Camera is null) return Vector3.Zero;

        var basis = Camera.GlobalTransform.Basis;
        var forward = -basis.Z.Normalized();
        var right = basis.X.Normalized();

        var horiz = Input.GetActionStrength("ui_right") - Input.GetActionStrength("ui_left");
        var vert = Input.GetActionStrength("ui_up") - Input.GetActionStrength("ui_down");

        var dir = right * horiz + forward * vert;
        dir.Y = 0;
        return dir.LengthSquared() < 0.001f ? Vector3.Zero : dir.Normalized();
    }

    private void HandleSnapTimer(double delta)
    {
        if (_shouldSnap)
        {
            _snapTimer += (float)delta;
            if (_snapTimer >= SnapDelay)
            {
                SnapToGrid();
                _shouldSnap = false;
            }
        }
    }

    private void MoveCursor(double delta)
    {
        if (_velocity.Length() > 0.01f)
        {
            Position += _velocity * (float)delta;
            UpdateCursorHeight();
        }
    }

    private void UpdateCursorHeight()
    {
        var targetY = 0f;
        if (!Mathf.IsEqualApprox(Position.Y, targetY))
        {
            var tween = GetTree().CreateTween();
            tween.TweenProperty(this, "position:y", targetY, HeightTransitionSpeed)
                 .SetTrans(Tween.TransitionType.Sine)
                 .SetEase(Tween.EaseType.InOut);
        }
    }

    private void SnapToGrid()
    {
        var closestTile = PositionToCell(Position);
        closestTile = ClampToMap(closestTile);
        var snappedPosition = CellToPosition(closestTile);
        _targetGridPosition = snappedPosition;

        var tween = GetTree().CreateTween();
        tween.TweenProperty(this, "position", snappedPosition, SnapSpeed)
             .SetTrans(Tween.TransitionType.Sine)
             .SetEase(Tween.EaseType.InOut);
    }

    private Vector3I PositionToCell(Vector3 position)
    {
        return new Vector3I(
            Mathf.RoundToInt(position.X / TileSize),
            0,
            Mathf.RoundToInt(position.Z / TileSize));
    }

    private Vector3 CellToPosition(Vector3I cell) => new(cell.X * TileSize, 0, cell.Z * TileSize);

    private Vector3I ClampToMap(Vector3I cell)
    {
        var clampedX = Mathf.Clamp(cell.X, 0, MapSize.X - 1);
        var clampedZ = Mathf.Clamp(cell.Z, 0, MapSize.Y - 1);
        return new Vector3I(clampedX, 0, clampedZ);
    }

    private void FollowCameraLook(Camera3D cam)
    {
        var origin = cam.GlobalTransform.Origin;
        var dir = -cam.GlobalTransform.Basis.Z;
        if (Mathf.IsZeroApprox(dir.Y))
            dir.Y = -0.0001f;

        var t = -origin.Y / dir.Y;
        if (t < 0) return;

        var hitPoint = origin + dir * t;
        var cell = PositionToCell(hitPoint);
        cell = ClampToMap(cell);
        var snapped = CellToPosition(cell);
        Position = snapped;
        _targetGridPosition = snapped;
    }

    public Vector3 GetSelectedTile() => _targetGridPosition;

    private void ApplyIndicatorColor(Color color)
    {
        if (_indicatorMesh?.MaterialOverride is StandardMaterial3D mat)
        {
            mat.AlbedoColor = color;
        }
    }
}
