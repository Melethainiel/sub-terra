using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Setup;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Entry point. For now it builds the setup board — Entrance plus both Laterals —
/// and frames it, which is enough to prove the rules engine and the 3D view agree
/// on where things are.
/// </summary>
public partial class AppRoot : Node3D
{
    private BoardView _boardView = null!;

    public override void _Ready()
    {
        _boardView = new BoardView { Name = "BoardView" };
        AddChild(_boardView);

        var board = TempleSetup.CreateBoard();
        _boardView.Render(board);

        FrameBoard(board);
        GD.Print($"Sub Terra II — {board.Tiles.Count} tuiles de mise en place affichées.");
    }

    /// <summary>Points the camera down at the middle of what is currently on the table.</summary>
    private void FrameBoard(TempleBoard board)
    {
        if (GetNodeOrNull<Camera3D>("Camera3D") is not { } camera || board.Tiles.Count == 0)
        {
            return;
        }

        var centre = Vector3.Zero;
        foreach (var cell in board.Tiles.Keys)
        {
            centre += BoardView.ToWorld(cell);
        }

        centre /= board.Tiles.Count;

        // High and slightly to the south, so the temple reads as sinking away from us.
        camera.LookAtFromPosition(centre + new Vector3(0f, 11f, 7f), centre, Vector3.Up);
    }
}
