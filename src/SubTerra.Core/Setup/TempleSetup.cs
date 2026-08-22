using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Setup;

/// <summary>
/// Lays out the fixed pieces: the Entrance on the edge of the table with a Lateral
/// tile either side. Everything else is drawn from the bag during play.
/// </summary>
/// <remarks>
/// The cell coordinates come from the setup diagram (rulebook p. 8) and are still to
/// be confirmed against the physical components — see docs/regles.md §13.2.
/// </remarks>
public static class TempleSetup
{
    /// <summary>Where every Explorer starts, and the cell that joins the two Laterals.</summary>
    public static readonly Cell EntranceCrossing = new(3, 0);

    /// <summary>Step onto this cell to leave the Temple for good.</summary>
    public static readonly Cell EntranceExit = new(3, -1);

    public static readonly IReadOnlyList<Cell> WestLateral = [new(0, 0), new(1, 0), new(2, 0)];

    public static readonly IReadOnlyList<Cell> EastLateral = [new(4, 0), new(5, 0), new(6, 0)];

    public static TempleBoard CreateBoard(BoardBounds? bounds = null)
    {
        var board = new TempleBoard(bounds);

        board.PlaceFixed(EntranceExit, Fixed("Entrance-Exit", TileKind.Entrance, Sides.South));
        board.PlaceFixed(
            EntranceCrossing,
            Fixed("Entrance-Crossing", TileKind.Entrance, Sides.North | Sides.East | Sides.West));

        PlaceLateral(board, WestLateral, "West");
        PlaceLateral(board, EastLateral, "East");

        return board;
    }

    private static void PlaceLateral(TempleBoard board, IReadOnlyList<Cell> cells, string side)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            // The strip runs east-west and opens south into the temple. Its outer end
            // is a Guardian cell, though no Guardian stands there at setup.
            var isOuterEnd = cells[index].Column is 0 or 6;
            var kind = isOuterEnd ? TileKind.Guardian : TileKind.Lateral;
            var openSides = Sides.East | Sides.West | Sides.South;

            board.PlaceFixed(cells[index], Fixed($"Lateral-{side}-{index}", kind, openSides));
        }
    }

    private static PlacedTile Fixed(string id, TileKind kind, Sides openSides) =>
        new(new TileDefinition(id, kind, openSides));
}
