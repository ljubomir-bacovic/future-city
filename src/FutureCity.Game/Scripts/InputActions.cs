using Godot;

namespace FutureCity.Game;

/// <summary>
/// Input action names and their default key bindings. Registered from code so bindings live in one place
/// (rebinding arrives with the settings screen in Phase 8).
/// </summary>
public static class InputActions
{
    public const string CameraLeft = "camera_left";
    public const string CameraRight = "camera_right";
    public const string CameraUp = "camera_up";
    public const string CameraDown = "camera_down";
    public const string TogglePause = "toggle_pause";
    public const string Speed1 = "speed_1";
    public const string Speed2 = "speed_2";
    public const string Speed3 = "speed_3";
    public const string Speed4 = "speed_4";
    public const string SpeedUp = "speed_up";
    public const string SpeedDown = "speed_down";
    public const string QuickSave = "quick_save";
    public const string QuickLoad = "quick_load";
    public const string ToggleResearch = "toggle_research";
    public const string ToggleEconomy = "toggle_economy";

    /// <summary>Adds all actions to the InputMap (safe to call more than once).</summary>
    public static void Register()
    {
        Add(CameraLeft, Key.A, Key.Left);
        Add(CameraRight, Key.D, Key.Right);
        Add(CameraUp, Key.W, Key.Up);
        Add(CameraDown, Key.S, Key.Down);
        Add(TogglePause, Key.Space, Key.P);
        Add(Speed1, Key.Key1);
        Add(Speed2, Key.Key2);
        Add(Speed3, Key.Key3);
        Add(Speed4, Key.Key4);
        Add(SpeedUp, Key.Equal, Key.KpAdd);
        Add(SpeedDown, Key.Minus, Key.KpSubtract);
        Add(QuickSave, Key.F5);
        Add(QuickLoad, Key.F9);
        Add(ToggleResearch, Key.R);
        Add(ToggleEconomy, Key.E);
    }

    private static void Add(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        foreach (var key in keys)
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
