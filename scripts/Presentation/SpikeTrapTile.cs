using Godot;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;

namespace SubTerra.Presentation;

/// <summary>
/// The stab a spike trap gives when it goes off: the nine points punch further up
/// out of the floor and settle back, rippling west to east, so the die that just
/// decided somebody's fate has something to show for it besides a line of text.
/// The spikes are already modelled standing — this only adds the flinch.
/// </summary>
public partial class SpikeTrapTile : Node3D, ITileEventListener
{
    private const float StabLift = 0.16f;

    private const float StabDuration = 0.08f;

    private const float SettleDuration = 0.26f;

    /// <summary>Delay between one west-east row of spikes and the next.</summary>
    private const float RowStagger = 0.05f;

    public void Announce(IReadOnlyList<GameEvent> events)
    {
        foreach (var @event in events)
        {
            if (@event is TrapSprung { Trap: TileKind.SpikeTrap })
            {
                Stab();
                return;
            }
        }
    }

    private void Stab()
    {
        foreach (var child in GetChildren())
        {
            if (child is not MeshInstance3D spike || !spike.Name.ToString().StartsWith("Spike_"))
            {
                continue;
            }

            // "Spike_{row}_{col}" — row is the west-east index the tiles were laid
            // out on, and gives the ripple something to walk across.
            var row = int.Parse(spike.Name.ToString().Split('_')[1]);
            var delay = row * RowStagger;
            var rest = spike.Position.Y;

            var tween = CreateTween();
            tween.TweenInterval(delay);
            tween.TweenProperty(spike, "position:y", rest + StabLift, StabDuration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(spike, "position:y", rest, SettleDuration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
        }
    }
}
