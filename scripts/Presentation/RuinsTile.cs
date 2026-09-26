using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// A Ruins tile's two faces: buried under the debris it arrived with, or dug clear.
/// Both are the same "Debris_*" chunks scattered in the scene — this only ever
/// shows or hides them to match what the board says right now, and animates the
/// moment it actually happens under our own eyes: a fall when a Peril roll brings
/// more of the ceiling down, a clearing when someone digs it out.
/// </summary>
public partial class RuinsTile : Node3D, ITileEventListener, IRubbleAwareTile
{
    /// <summary>Roughly where the vault runs — see <see cref="BoardView"/> — so a
    /// falling chunk reads as coming out of the ceiling rather than from nowhere.</summary>
    private const float FallHeight = 1.9f;

    private const float FallDuration = 0.34f;

    private const float ClearDuration = 0.3f;

    /// <summary>Delay between one chunk and the next, so the room doesn't come down
    /// or clear as a single rigid slab.</summary>
    private const float ChunkStagger = 0.05f;

    private List<MeshInstance3D>? _chunks;

    private List<MeshInstance3D> Chunks => _chunks ??=
        [.. GetChildren().OfType<MeshInstance3D>().Where(n => n.Name.ToString().StartsWith("Debris_"))];

    /// <summary>Snaps straight to the resting look — no fall, no fade — for a redraw
    /// that isn't the moment the state actually changed.</summary>
    public void SetBuried(bool buried)
    {
        foreach (var chunk in Chunks)
        {
            chunk.Visible = buried;
            chunk.Scale = Vector3.One;
        }
    }

    public void Announce(IReadOnlyList<GameEvent> events)
    {
        foreach (var @event in events)
        {
            switch (@event)
            {
                case RuinsCollapsed:
                    Collapse();
                    return;

                case RubbleCleared:
                case GuardianClearedRubble:
                    Clear();
                    return;
            }
        }
    }

    /// <summary>More of the ceiling lets go: each chunk drops in from above and
    /// settles where it was always going to end up.</summary>
    private void Collapse()
    {
        var rng = new RandomNumberGenerator();

        foreach (var (chunk, index) in Chunks.Select((chunk, index) => (chunk, index)))
        {
            var rest = chunk.Position;
            chunk.Visible = true;
            chunk.Position = rest + new Vector3(0f, FallHeight, 0f);
            chunk.RotationDegrees = new Vector3(rng.RandfRange(-90f, 90f), chunk.RotationDegrees.Y, rng.RandfRange(-90f, 90f));

            var tween = CreateTween();
            tween.TweenInterval(index * ChunkStagger);
            tween.TweenProperty(chunk, "position", rest, FallDuration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(
                chunk, "rotation_degrees", new Vector3(0f, chunk.RotationDegrees.Y, 0f), FallDuration);
        }
    }

    /// <summary>Someone's cleared it: every chunk shrinks away rather than trying to
    /// show it being carried off piece by piece.</summary>
    private void Clear()
    {
        foreach (var (chunk, index) in Chunks.Select((chunk, index) => (chunk, index)))
        {
            var tween = CreateTween();
            tween.TweenInterval(index * ChunkStagger);
            tween.TweenProperty(chunk, "scale", Vector3.Zero, ClearDuration)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() => chunk.Visible = false));
        }
    }
}
