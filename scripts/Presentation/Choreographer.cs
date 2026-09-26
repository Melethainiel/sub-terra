using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// Plays back what a command did. The board and the tokens are always drawn as the
/// engine now stands; this takes the events that got them there and walks the
/// pieces through them one after another — a meeple steps from where it was, a tile
/// drops into place, a Guardian rises, a heart flies off — so the table can follow
/// the consequences in the order the engine settled them, not just see the result.
/// </summary>
/// <remarks>
/// Only ever animates towards what is already drawn, so a redraw in the middle of a
/// playback — the next command, a card picked up — simply lands everything where it
/// belongs. It never decides anything and never touches the engine.
/// </remarks>
public partial class Choreographer : Node3D
{
    private const float StepTime = 0.26f;
    private const float GuardianStepTime = 0.34f;
    private const float RiseTime = 0.4f;
    private const float TileDropTime = 0.32f;
    private const float TileDropHeight = 2.4f;
    private const float FallTime = 0.3f;
    private const float FloatTime = 1.1f;

    /// <summary>A die on the table gets this long to be read before what it caused follows.</summary>
    private const float DieTime = 0.7f;

    /// <summary>A little room between one consequence and the next.</summary>
    private const float Beat = 0.12f;

    /// <summary>
    /// Walks the pieces through <paramref name="events"/>. <paramref name="cue"/> is
    /// told about each die and each ending at the moment it lands in the playback,
    /// for whatever shows them outside the board. Returns how long the whole playback lasts.
    /// </summary>
    public float Play(IReadOnlyList<GameEvent> events, BoardView board, TokenView tokens, Action<GameEvent>? cue = null)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var clock = 0f;
        var walks = new Dictionary<ExplorerId, List<(float At, Cell From, Cell To)>>();
        var guardians = new List<GuardianTrail>();
        var cues = CreateTween();
        var cueClock = 0f;
        var cued = false;

        void Cue(GameEvent @event, float at)
        {
            if (cue is null)
            {
                return;
            }

            cues.TweenInterval(Math.Max(0f, at - cueClock));
            cues.TweenCallback(Callable.From(() => cue(@event)));
            cueClock = at;
            cued = true;
        }

        foreach (var @event in events)
        {
            switch (@event)
            {
                case ExplorerMoved moved:
                    if (!walks.TryGetValue(moved.Explorer, out var walk))
                    {
                        walks[moved.Explorer] = walk = [];
                    }

                    walk.Add((clock, moved.From, moved.To));
                    clock += StepTime;
                    break;

                case GuardianAppeared appeared:
                    guardians.Add(new GuardianTrail(appeared.Cell, clock));
                    clock += RiseTime;
                    break;

                case GuardianStepped stepped:
                    var trail = guardians.FirstOrDefault(g => g.End == stepped.From && !g.Gone)
                        ?? AddTrail(guardians, stepped.From);
                    trail.Steps.Add((clock, stepped.To));
                    clock += GuardianStepTime;
                    break;

                case GuardianEliminated eliminated:
                    // Whoever was tracked there is gone; a ghost stands in to fall.
                    if (guardians.FirstOrDefault(g => g.End == eliminated.Cell && !g.Gone) is { } fallen)
                    {
                        fallen.Gone = true;
                    }

                    Ghost(eliminated.Cell, clock);
                    clock += FallTime;
                    break;

                case TileRevealed revealed when board.TileNode(revealed.Cell) is { } tile:
                    Drop(tile, clock);
                    clock += TileDropTime;
                    break;

                case HealthLost lost:
                    Float(tokens, lost.Explorer, $"−{lost.Amount} ♥", Palette.Combat, clock);
                    clock += Beat;
                    break;

                case HealthRegained regained when regained.Amount > 0:
                    Float(tokens, regained.Explorer, $"+{regained.Amount} ♥", Palette.Support, clock);
                    clock += Beat;
                    break;

                case ExplorerWentDown down when tokens.ExplorerNode(down.Explorer) is { } meeple:
                    FallOver(meeple, clock);
                    clock += FallTime;
                    break;

                case PerilRolled or DieRolled:
                    Cue(@event, clock);
                    clock += DieTime;
                    break;

                case GameEnded:
                    Cue(@event, clock + Beat);
                    break;
            }
        }

        foreach (var (explorer, walk) in walks)
        {
            if (tokens.ExplorerNode(explorer) is { } meeple)
            {
                Walk(meeple, walk);
            }
        }

        // A trail ends where its Guardian is drawn now; pair each with one of those.
        var unclaimed = tokens.GuardianNodes.ToList();

        foreach (var trail in guardians.Where(g => !g.Gone))
        {
            var index = unclaimed.FindIndex(g => g.Cell == trail.End);

            if (index >= 0)
            {
                Follow(unclaimed[index].Node, trail);
                unclaimed.RemoveAt(index);
            }
        }

        // A tween with nothing to do is an error in Godot; one that cues at the very
        // start (a die rolled first thing) still has work, hence a flag, not the clock.
        if (!cued)
        {
            cues.Kill();
        }

        return clock;
    }

    /// <summary>One Guardian's part in the playback: where it came from, or rose.</summary>
    private sealed class GuardianTrail(Cell start, float? risesAt = null)
    {
        public Cell Start { get; } = start;

        public float? RisesAt { get; } = risesAt;

        public List<(float At, Cell To)> Steps { get; } = [];

        public bool Gone { get; set; }

        public Cell End => Steps.Count > 0 ? Steps[^1].To : Start;
    }

    private static GuardianTrail AddTrail(List<GuardianTrail> trails, Cell start)
    {
        var trail = new GuardianTrail(start);
        trails.Add(trail);
        return trail;
    }

    /// <summary>
    /// Puts a meeple back where its walk began and steps it along, square by square,
    /// keeping whatever offset its tile gave it now so it lands on its own spot.
    /// </summary>
    private static void Walk(Node3D meeple, List<(float At, Cell From, Cell To)> walk)
    {
        var offset = meeple.Position - BoardView.ToWorld(walk[^1].To);
        meeple.Position = BoardView.ToWorld(walk[0].From) + offset;

        var tween = meeple.CreateTween();
        var time = 0f;

        foreach (var (at, _, to) in walk)
        {
            if (at > time)
            {
                tween.TweenInterval(at - time);
            }

            tween.TweenProperty(meeple, "position", BoardView.ToWorld(to) + offset, StepTime)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            time = at + StepTime;
        }
    }

    private static void Follow(Node3D meeple, GuardianTrail trail)
    {
        var offset = meeple.Position - BoardView.ToWorld(trail.End);
        var rest = meeple.Position;
        var tween = meeple.CreateTween();
        var time = 0f;

        if (trail.RisesAt is { } rises)
        {
            // Up out of the floor of the tile that woke it.
            meeple.Position = BoardView.ToWorld(trail.Start) + offset + new Vector3(0f, -2.2f, 0f);
            meeple.Visible = false;
            tween.TweenInterval(rises);
            tween.TweenCallback(Callable.From(() => meeple.Visible = true));
            tween.TweenProperty(meeple, "position", BoardView.ToWorld(trail.Start) + offset, RiseTime)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            time = rises + RiseTime;
        }
        else
        {
            meeple.Position = BoardView.ToWorld(trail.Start) + offset;
        }

        foreach (var (at, to) in trail.Steps)
        {
            if (at > time)
            {
                tween.TweenInterval(at - time);
            }

            tween.TweenProperty(meeple, "position", BoardView.ToWorld(to) + offset, GuardianStepTime)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            time = at + GuardianStepTime;
        }

        if (trail.Steps.Count == 0 && trail.RisesAt is null)
        {
            meeple.Position = rest;
        }
    }

    /// <summary>A tile just out of the bag falls into its place from above.</summary>
    private static void Drop(Node3D tile, float at)
    {
        var rest = tile.Position;
        tile.Position = rest + new Vector3(0f, TileDropHeight, 0f);
        tile.Visible = false;

        var tween = tile.CreateTween();
        tween.TweenInterval(at);
        tween.TweenCallback(Callable.From(() => tile.Visible = true));
        tween.TweenProperty(tile, "position", rest, TileDropTime)
            .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    /// <summary>A meeple drawn lying down is stood back up, then goes over at its moment.</summary>
    private static void FallOver(Node3D meeple, float at)
    {
        var lying = meeple.RotationDegrees;
        var restingAt = meeple.Position;
        meeple.RotationDegrees = Vector3.Zero;
        meeple.Position = restingAt + new Vector3(0f, 0.6f, 0f);

        var tween = meeple.CreateTween();
        tween.TweenInterval(at);
        tween.SetParallel();
        tween.TweenProperty(meeple, "rotation_degrees", lying, FallTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(meeple, "position", restingAt, FallTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>A Guardian that is no more: a stand-in sinks into the floor and fades.</summary>
    private void Ghost(Cell cell, float at)
    {
        var colour = Palette.Guardian;
        var material = new StandardMaterial3D
        {
            AlbedoColor = colour,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = colour,
            EmissionEnergyMultiplier = 0.5f,
        };

        var ghost = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = 2.1f, RadialSegments = 12 },
            MaterialOverride = material,
            Position = BoardView.ToWorld(cell) + new Vector3(0f, 1.05f, 0f),
        };
        AddChild(ghost);

        var tween = ghost.CreateTween();
        tween.TweenInterval(at);
        tween.SetParallel();
        tween.TweenProperty(ghost, "position:y", -0.6f, FallTime * 2f).SetEase(Tween.EaseType.In);
        tween.TweenProperty(material, "albedo_color:a", 0f, FallTime * 2f);
        tween.Chain().TweenCallback(Callable.From(ghost.QueueFree));
    }

    /// <summary>Hearts lost or regained, rising off whoever they belong to.</summary>
    private void Float(TokenView tokens, ExplorerId explorer, string text, Color colour, float at)
    {
        if (tokens.ExplorerNode(explorer) is not { } meeple)
        {
            return;
        }

        var label = new Label3D
        {
            Text = text,
            Modulate = colour,
            OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
            OutlineSize = 10,
            FontSize = 48,
            // The same size on screen however far the camera stands: from above the
            // whole temple is in frame, and a heart the size of the meeple is a dot.
            FixedSize = true,
            PixelSize = 0.0009f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false,
            Position = meeple.Position + new Vector3(0f, 2.1f, 0f),
        };
        AddChild(label);

        var tween = label.CreateTween();
        tween.TweenInterval(at);
        tween.TweenCallback(Callable.From(() => label.Visible = true));
        tween.SetParallel();
        tween.TweenProperty(label, "position:y", label.Position.Y + 0.9f, FloatTime).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, FloatTime).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
