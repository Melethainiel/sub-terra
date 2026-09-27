using Godot;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// What the table sounds like. The temple breathes under everything — air, water, a
/// mountain turning over below, louder as the eruption nears — and each thing that
/// happens is heard when the playback shows it: a step as the meeple steps, a die
/// each time it strikes the table. Everything goes through <see cref="Audio"/>.
/// </summary>
public partial class Soundscape : Node
{
    private const string Cave = "res://resources/audio/ambience/cave.ogg";

    private const string Volcano = "res://resources/audio/ambience/volcano.ogg";

    private AudioStreamPlayer _cave = null!;

    private AudioStreamPlayer _volcano = null!;

    private Tween? _swell;

    /// <summary>Where the eruption track stood when the game began: the far end of the swell.</summary>
    private int _eruptionStart;

    public override void _Ready()
    {
        _cave = new AudioStreamPlayer { Bus = "Ambience", Stream = Audio.Looping(Cave), VolumeDb = -80f };
        _volcano = new AudioStreamPlayer { Bus = "Ambience", Stream = Audio.Looping(Volcano), VolumeDb = -80f };
        AddChild(_cave);
        AddChild(_volcano);
        _cave.Play();
        _volcano.Play();

        var fadeIn = CreateTween();
        fadeIn.TweenProperty(_cave, "volume_db", 0f, 3f);

        Audio.Instance?.Table();
    }

    /// <summary>Tunes the mountain to the state of the game: a far murmur at first, a
    /// roar once it is ready to blow, and all of it once it has.</summary>
    public void Mood(GameState game)
    {
        _eruptionStart = Math.Max(_eruptionStart, game.EruptionCountdown);

        var progress = _eruptionStart == 0 ? 1f : 1f - (float)game.EruptionCountdown / _eruptionStart;
        var target = game switch
        {
            { HasErupted: true } => 2f,
            { IsVolcanoReady: true } => -2f,
            _ => Mathf.Lerp(-32f, -8f, progress) + (game.IsCursed ? 4f : 0f),
        };

        if (Mathf.Abs(_volcano.VolumeDb - target) < 0.5f)
        {
            return;
        }

        _swell?.Kill();
        _swell = CreateTween();
        _swell.TweenProperty(_volcano, "volume_db", target, 4f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>
    /// Plays what an event sounds like, <paramref name="at"/> seconds into the playback
    /// — the moment the <see cref="Choreographer"/> shows it happening.
    /// </summary>
    public void At(GameEvent @event, float at)
    {
        foreach (var (sound, offset, volumeDb) in SoundsOf(@event))
        {
            var when = at + offset;

            if (when <= 0.001f)
            {
                Audio.Instance?.Play(sound, volumeDb);
                continue;
            }

            GetTree().CreateTimer(when).Timeout += () => Audio.Instance?.Play(sound, volumeDb);
        }
    }

    /// <summary>What each event sounds like: which sound, how long after its moment, how loud.</summary>
    private static IEnumerable<(string Sound, float Offset, float VolumeDb)> SoundsOf(GameEvent @event)
    {
        switch (@event)
        {
            case ExplorerMoved:
                yield return ("step", 0.02f, -6f);
                yield return ("step", 0.15f, -8f);
                break;

            case PerilRolled or DieRolled:
                // The throw's own bounces (see Dice): a strike on each, then the rattle.
                yield return ("dice_hit", Dice.ThrowTime * 0.45f, -2f);
                yield return ("dice_hit", Dice.ThrowTime * 0.75f, -7f);
                yield return ("dice_settle", Dice.ThrowTime * 0.92f, -10f);
                break;

            case TileRevealed:
                yield return ("tile_place", 0f, -3f);
                break;

            case TrapSprung { Trap: TileKind.SpikeTrap }:
                yield return ("spikes", 0f, 0f);
                break;

            case TrapSprung:
                yield return ("darts", 0f, -2f);
                break;

            case RuinsCollapsed:
                yield return ("collapse", 0f, 0f);
                break;

            case WallDemolished:
                yield return ("collapse", 0f, -8f);
                break;

            case GuardianAppeared:
                yield return ("guardian_wake", 0f, -2f);
                break;

            case GuardianStepped:
                yield return ("guardian_step", 0.1f, -4f);
                break;

            case GuardianAttacked:
                yield return ("guardian_strike", 0f, -2f);
                break;

            case GuardianEliminated:
                yield return ("tile_place", 0f, -6f);
                break;

            case HealthLost:
                yield return ("hurt", 0f, -5f);
                break;

            case ExplorerWentDown:
                yield return ("hurt", 0.1f, 0f);
                break;

            case HealthRegained { Amount: > 0 }:
                yield return ("heal", 0f, -6f);
                break;

            case RubbleCleared or GuardianClearedRubble:
                yield return ("dig", 0f, -4f);
                break;

            case ItemPickedUp:
                yield return ("key_pickup", 0f, -4f);
                break;

            case ItemDropped:
                yield return ("item_drop", 0f, -4f);
                break;

            case KeyDeposited:
                yield return ("key_deposit", 0f, -2f);
                break;

            case ArtefactRevealed:
                yield return ("artefact", 0f, -2f);
                break;

            case CurseFell:
                yield return ("curse", 0f, 0f);
                break;

            case VolcanoErupted:
                yield return ("eruption", 0f, 0f);
                break;

            case TilesFlooded:
                yield return ("lava_flood", 0f, -3f);
                break;

            case AbilityUsed or ShieldRaised:
                yield return ("ability", 0f, -6f);
                break;

            case TileConsolidated:
                yield return ("tile_place", 0f, -6f);
                break;

            case ExplorerEscaped:
                yield return ("heal", 0f, -4f);
                break;

            case TurnBegan:
                yield return ("turn_begin", 0.2f, -14f);
                break;

            case DecisionRequired:
                yield return ("ui_card", 0f, -8f);
                break;

            case GameEnded { Outcome: Outcome.ForgottenForever }:
                yield return ("defeat", 0.2f, 0f);
                break;

            case GameEnded:
                yield return ("victory", 0.2f, 0f);
                break;
        }
    }
}
