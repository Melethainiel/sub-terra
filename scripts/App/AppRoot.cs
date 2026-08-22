using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Entry point. It builds the setup board and, for now, deals a few tiles onto it so
/// the draw is visible — the real Reveal action, driven by a player spending an
/// action point, replaces this.
/// </summary>
public partial class AppRoot : Node3D
{
    private BoardView _boardView = null!;

    /// <summary>Same seed, same temple. Change it in the inspector to deal another one.</summary>
    [Export]
    public int Seed { get; set; } = 42;

    [Export(PropertyHint.Range, "0,30,1")]
    public int PreviewTileCount { get; set; } = 14;

    public override void _Ready()
    {
        _boardView = new BoardView { Name = "BoardView" };
        AddChild(_boardView);

        var board = TempleSetup.CreateBoard();
        var bag = TileBag.Temple();
        var rng = new Rng((ulong)Seed);

        DealPreview(board, bag, rng, PreviewTileCount);

        _boardView.Render(board);
        FrameBoard(board);

        GD.Print($"Sub Terra II — graine {Seed} : {board.Tiles.Count} tuiles posées, {bag.Count} restantes dans le sac.");
    }

    /// <summary>
    /// Draws tiles and drops them on random open exits. A stand-in for play: it makes
    /// no attempt to be a good temple, only a legal one.
    /// </summary>
    private static void DealPreview(TempleBoard board, TileBag bag, Rng rng, int count)
    {
        for (var dealt = 0; dealt < count && !bag.IsEmpty; dealt++)
        {
            var exits = board.OpenExits().ToList();
            if (exits.Count == 0)
            {
                return;
            }

            var exit = exits[rng.Next(exits.Count)];
            var tile = new PlacedTile(bag.Draw(rng), rng.Next(4));

            if (board.CanPlace(exit.Target, tile, exit.From))
            {
                board.Place(exit.Target, tile, exit.From);
            }
            else
            {
                // Rule of the "totally blocked" temple: back in the bag, draw again.
                bag.Return(tile.Definition);
            }
        }
    }

    /// <summary>Points the camera down at the middle of what is currently on the table.</summary>
    private void FrameBoard(TempleBoard board)
    {
        if (GetNodeOrNull<Camera3D>("Camera3D") is not { } camera || board.Tiles.Count == 0)
        {
            return;
        }

        var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
        var max = new Vector3(float.MinValue, 0f, float.MinValue);

        foreach (var cell in board.Tiles.Keys)
        {
            var position = BoardView.ToWorld(cell);
            min = new Vector3(Mathf.Min(min.X, position.X), 0f, Mathf.Min(min.Z, position.Z));
            max = new Vector3(Mathf.Max(max.X, position.X), 0f, Mathf.Max(max.Z, position.Z));
        }

        var centre = (min + max) / 2f;
        var extent = Mathf.Max(max.X - min.X, max.Z - min.Z) + BoardView.TileSize;

        // High and slightly to the south, backed off far enough to hold the whole
        // temple in frame, so the board reads as sinking away from us.
        var distance = Mathf.Max(extent * 0.9f, 8f);
        camera.LookAtFromPosition(centre + new Vector3(0f, distance, distance * 0.6f), centre, Vector3.Up);
    }
}
