using Godot;

namespace SubTerra.App;

/// <summary>
/// What a player set on the Réglages screen, kept from one launch to the next. Only
/// what actually does something lives here: no daltonism toggle while nothing yet
/// paints a pattern for it.
/// </summary>
public static class Settings
{
    private const string Path = "user://settings.cfg";

    private const string Section = "display";

    private const string AudioSection = "audio";

    private static bool _loaded;

    private static bool _fullscreen;

    /// <summary>Whether a fresh table opens looking down from above, or through an
    /// Explorer's own eyes — the same two views <c>V</c> switches between in play.</summary>
    private static bool _defaultViewIsFps;

    /// <summary>The audio buses a player can turn up or down, and how loud each starts.</summary>
    public static readonly (string Bus, string Label, float Default)[] Buses =
    [
        ("Master", "Général", 0.8f),
        ("Music", "Musique", 0.6f),
        ("Ambience", "Ambiance", 0.7f),
        ("Sfx", "Effets", 0.9f),
    ];

    private static readonly Dictionary<string, float> _volumes = Buses.ToDictionary(b => b.Bus, b => b.Default);

    /// <summary>A bus's volume, from 0 (silent) to 1.</summary>
    public static float Volume(string bus)
    {
        Load();
        return _volumes.GetValueOrDefault(bus, 1f);
    }

    public static void SetVolume(string bus, float volume)
    {
        Load();
        _volumes[bus] = Mathf.Clamp(volume, 0f, 1f);
        Save();
        ApplyAudio();
    }

    /// <summary>Sets every bus to the volume the settings name.</summary>
    public static void ApplyAudio()
    {
        Load();

        foreach (var (bus, _, _) in Buses)
        {
            var index = AudioServer.GetBusIndex(bus);

            if (index >= 0)
            {
                var volume = _volumes[bus];
                AudioServer.SetBusVolumeDb(index, volume <= 0.001f ? -80f : Mathf.LinearToDb(volume));
            }
        }
    }

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

        foreach (var (bus, _, fallback) in Buses)
        {
            _volumes[bus] = (float)file.GetValue(AudioSection, bus, fallback);
        }
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.SetValue(Section, "fullscreen", _fullscreen);
        file.SetValue(Section, "default_view_fps", _defaultViewIsFps);

        foreach (var (bus, volume) in _volumes)
        {
            file.SetValue(AudioSection, bus, volume);
        }
        file.Save(Path);
    }
}
