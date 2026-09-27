using Godot;

namespace SubTerra.App;

/// <summary>
/// The game's sound, from the first screen to the last (autoload): the menus' music,
/// carried from one screen to the next rather than restarted, a pool of voices for
/// the effects, and the tick of every button, wherever it is built. The sounds
/// themselves come out of tools/audio/synth.py.
/// </summary>
public partial class Audio : Node
{
    private const string Sfx = "res://resources/audio/sfx/";

    private const string Theme = "res://resources/audio/music/theme.ogg";

    /// <summary>How many effects can sound at once before the oldest gives way.</summary>
    private const int Voices = 16;

    private const float MusicFade = 1.5f;

    public static Audio? Instance { get; private set; }

    private readonly List<AudioStreamPlayer> _voices = [];

    private readonly Dictionary<string, AudioStream[]> _variants = [];

    private readonly RandomNumberGenerator _rng = new();

    private AudioStreamPlayer _music = null!;

    private Tween? _musicFade;

    private int _next;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        Settings.ApplyAudio();

        for (var i = 0; i < Voices; i++)
        {
            var voice = new AudioStreamPlayer { Bus = "Sfx" };
            AddChild(voice);
            _voices.Add(voice);
        }

        _music = new AudioStreamPlayer { Bus = "Music", Stream = Looping(Theme), VolumeDb = -80f };
        AddChild(_music);

        // Every button in the game ticks when pressed, however and wherever it is made.
        GetTree().NodeAdded += node =>
        {
            if (node is BaseButton button)
            {
                button.Pressed += () => Play("ui_click", -10f);
            }
        };
    }

    /// <summary>Plays an effect — one of its variants at random, a hair off pitch so a
    /// sound heard over and over never quite repeats.</summary>
    public void Play(string sound, float volumeDb = 0f, float pitchSpread = 0.04f)
    {
        if (Variants(sound) is not { Length: > 0 } streams)
        {
            return;
        }

        var voice = _voices[_next];
        _next = (_next + 1) % _voices.Count;

        voice.Stream = streams[_rng.RandiRange(0, streams.Length - 1)];
        voice.VolumeDb = volumeDb;
        voice.PitchScale = 1f + _rng.RandfRange(-pitchSpread, pitchSpread);
        voice.Play();
    }

    /// <summary>The menus' music, faded in — and carried on if it is already playing.</summary>
    public void Menu() => FadeMusic(0f);

    /// <summary>At the table the temple makes its own sound: the music makes way.</summary>
    public void Table() => FadeMusic(-80f);

    private void FadeMusic(float volumeDb)
    {
        if (!_music.Playing && volumeDb > -80f)
        {
            _music.Play();
        }

        _musicFade?.Kill();
        _musicFade = CreateTween();
        _musicFade.TweenProperty(_music, "volume_db", volumeDb, MusicFade)
            .SetTrans(volumeDb > _music.VolumeDb ? Tween.TransitionType.Sine : Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);

        if (volumeDb <= -80f)
        {
            _musicFade.TweenCallback(Callable.From(_music.Stop));
        }
    }

    /// <summary>A sound's files: <c>name.ogg</c>, or <c>name_0.ogg</c>, <c>name_1.ogg</c>…</summary>
    private AudioStream[] Variants(string sound)
    {
        if (_variants.TryGetValue(sound, out var known))
        {
            return known;
        }

        var found = new List<AudioStream>();

        if (ResourceLoader.Exists($"{Sfx}{sound}.ogg"))
        {
            found.Add(GD.Load<AudioStream>($"{Sfx}{sound}.ogg"));
        }

        for (var i = 0; ResourceLoader.Exists($"{Sfx}{sound}_{i}.ogg"); i++)
        {
            found.Add(GD.Load<AudioStream>($"{Sfx}{sound}_{i}.ogg"));
        }

        if (found.Count == 0)
        {
            GD.PushWarning($"Aucun son « {sound} ».");
        }

        return _variants[sound] = [.. found];
    }

    /// <summary>An Ogg file set to loop, for music and ambiences.</summary>
    public static AudioStream Looping(string path)
    {
        var stream = GD.Load<AudioStreamOggVorbis>(path);
        stream.Loop = true;
        return stream;
    }
}
