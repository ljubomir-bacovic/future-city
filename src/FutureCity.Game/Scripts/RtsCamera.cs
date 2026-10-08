using Godot;

namespace FutureCity.Game;

/// <summary>
/// Strategy-game camera: keyboard and screen-edge panning, middle-mouse drag, and wheel zoom toward the cursor.
/// Works while the game is paused.
/// </summary>
public partial class RtsCamera : Camera2D
{
    [Export] public float PanSpeed { get; set; } = 900f;   // screen pixels per second
    [Export] public int EdgeMargin { get; set; } = 12;     // pixels from the window edge that start scrolling
    [Export] public float MinZoom { get; set; } = 0.35f;
    [Export] public float MaxZoom { get; set; } = 3f;
    [Export] public float ZoomStep { get; set; } = 1.15f;

    /// <summary>Whether moving the mouse to the window edge scrolls the view.</summary>
    public bool EdgeScrollEnabled { get; set; } = true;

    private Rect2 _bounds;

    /// <summary>Limits the camera center to <paramref name="bounds"/> and centers the view on it.</summary>
    public void SetBounds(Rect2 bounds)
    {
        _bounds = bounds;
        Position = bounds.GetCenter();
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector(InputActions.CameraLeft, InputActions.CameraRight, InputActions.CameraUp, InputActions.CameraDown);
        if (direction == Vector2.Zero && EdgeScrollEnabled)
            direction = EdgeScrollDirection();
        if (direction != Vector2.Zero)
            Position += direction * PanSpeed * (float)delta / Zoom.X;
        ClampToBounds();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp } wheel:
                ZoomAt(Zoom.X * ZoomStep, wheel.Position);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown } wheel:
                ZoomAt(Zoom.X / ZoomStep, wheel.Position);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Middle) != 0:
                Position -= motion.Relative / Zoom.X;
                ClampToBounds();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    private void ZoomAt(float zoom, Vector2 screenPoint)
    {
        zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        // Keep the world point under the cursor fixed while zooming.
        var offset = screenPoint - GetViewportRect().Size / 2;
        var worldPoint = Position + offset / Zoom.X;
        Zoom = new Vector2(zoom, zoom);
        Position = worldPoint - offset / zoom;
        ClampToBounds();
    }

    private Vector2 EdgeScrollDirection()
    {
        if (!DisplayServer.WindowIsFocused()) return Vector2.Zero;
        var size = GetViewportRect().Size;
        var mouse = GetViewport().GetMousePosition();
        if (mouse.X < 0 || mouse.Y < 0 || mouse.X > size.X || mouse.Y > size.Y) return Vector2.Zero;

        var direction = Vector2.Zero;
        if (mouse.X <= EdgeMargin) direction.X -= 1;
        else if (mouse.X >= size.X - EdgeMargin) direction.X += 1;
        if (mouse.Y <= EdgeMargin) direction.Y -= 1;
        else if (mouse.Y >= size.Y - EdgeMargin) direction.Y += 1;
        return direction.Normalized();
    }

    private void ClampToBounds()
    {
        if (_bounds.Size == Vector2.Zero) return;
        Position = new Vector2(
            Mathf.Clamp(Position.X, _bounds.Position.X, _bounds.End.X),
            Mathf.Clamp(Position.Y, _bounds.Position.Y, _bounds.End.Y));
    }
}
