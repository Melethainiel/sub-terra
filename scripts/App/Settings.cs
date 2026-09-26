using Godot;

namespace SubTerra.App;

/// <summary>
/// What a player set on the Réglages screen, kept from one launch to the next. Only
/// what actually does something lives here: no volume sliders while the game has no
/// sound to turn down, no daltonism toggle while nothing yet paints a pattern for it.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.cfg";

    private const string Section = "display";

    private static bool _loaded;

    private static bool _fullscreen;

    /// <summary>Whether a fresh table opens looking down from above, or through an
    /// Explorer's own eyes — the same two views <c>V</c> switches between in play.</summary>
    private static bool _defaultViewIsFps;

    public static bool Fullscreen
    {
        get { Load(); return _fullscreen; }
        set
        {
            Load();
            _fullscreen = value;
            Save();
            Apply();
        }
    }

    public static bool DefaultViewIsFps
    {
        get { Load(); return _defaultViewIsFps; }
        set { Load(); _defaultViewIsFps = value; Save(); }
    }

    /// <summary>Puts the window in the state the setting already names — called once
    /// at startup, and again every time <see cref="Fullscreen"/> changes.</summary>
    public static void Apply()
    {
        Load();
        DisplayServer.WindowSetMode(_fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        var file = new ConfigFile();

        if (file.Load(Path) is not Error.Ok)
        {
            // No file yet, or it didn't parse — the defaults above stand.
            return;
        }

        _fullscreen = (bool)file.GetValue(Section, "fullscreen", _fullscreen);
        _defaultViewIsFps = (bool)file.GetValue(Section, "default_view_fps", _defaultViewIsFps);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.SetValue(Section, "fullscreen", _fullscreen);
        file.SetValue(Section, "default_view_fps", _defaultViewIsFps);
        file.Save(Path);
    }
}
