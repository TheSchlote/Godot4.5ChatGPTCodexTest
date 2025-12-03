using Godot;

namespace CodexGame.Infrastructure.Scenes;

/// <summary>
/// Simple orbit/track camera: RMB to rotate, wheel to zoom, WASD to pan relative to yaw.
/// </summary>
public partial class Gimbal : Node3D
{
    [Export] public float MoveSpeed { get; set; } = 14f;
    [Export] public float MouseSensitivity { get; set; } = 0.005f;
    [Export] public float ZoomStep { get; set; } = 2f;
    [Export] public float MinDistance { get; set; } = 8f;
    [Export] public float MaxDistance { get; set; } = 40f;
    [Export] public float FollowLerp { get; set; } = 1.2f;

    public Camera3D? Camera { get; private set; }

    private float _yaw = -Mathf.Pi / 4f;
    private float _pitch = -0.7f;
    private float _distance = 22f;
    private bool _rotating;
    private Node3D? _followTarget;

    public override void _Ready()
    {
        Camera = new Camera3D { Name = "Camera3D", Current = true };
        AddChild(Camera);
        UpdateTransform();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Right) _rotating = true;
            if (mb.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Clamp(_distance - ZoomStep, MinDistance, MaxDistance);
            if (mb.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Clamp(_distance + ZoomStep, MinDistance, MaxDistance);
        }
        else if (@event is InputEventMouseButton mbUp && !mbUp.Pressed && mbUp.ButtonIndex == MouseButton.Right)
        {
            _rotating = false;
        }

        if (_rotating && @event is InputEventMouseMotion motion)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.2f, -0.1f);
            UpdateTransform();
        }
    }

    public override void _Process(double delta)
    {
        HandleMovement(delta);
        HandleFollow(delta);
    }

    private void HandleMovement(double delta)
    {
        var basis = Basis.Identity.Rotated(Vector3.Up, _yaw);
        var forward = basis.Z;
        var right = -basis.X; // flip to make A move left, D move right
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
            GlobalPosition += move.Normalized() * MoveSpeed * (float)delta;
            _followTarget = null;
            UpdateTransform();
        }
    }

    private void HandleFollow(double delta)
    {
        if (_followTarget is null) return;
        GlobalPosition = GlobalPosition.Lerp(_followTarget.GlobalPosition, Mathf.Clamp((float)delta * FollowLerp, 0, 1));
        UpdateTransform();
    }

    public void SmoothFocus(Vector3 target)
    {
        _followTarget = null;
        GlobalPosition = target;
        UpdateTransform();
    }

    public void BeginFollow(Node3D target) => _followTarget = target;
    public void StopFollow() => _followTarget = null;

    public void UpdateTransform()
    {
        if (Camera is null) return;

        var dir = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch));

        var camPos = GlobalPosition - dir * _distance + new Vector3(0, 4f, 0);
        Camera.GlobalTransform = new Transform3D(Basis.Identity, camPos);
        Camera.LookAt(GlobalPosition, Vector3.Up);
    }
}
