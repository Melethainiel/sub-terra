using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Setup;

/// <summary>
/// Lays out the fixed pieces: the Entrance on the edge of the table with a Lateral
/// arm either side. Everything else is drawn from the bag during play.
/// </summary>
/// <remarks>
/// Shapes and make-up are dictated in docs/tuiles.md. The Lateral arms are not tiles
/// of their own — each is two Normal T tiles ending in a Guardian dead end, and no
/// Guardian stands there at setup.
/// </remarks>
public static class TempleSetup
{
    /// <summary>Where every explorer starts. A crossroads: the temple opens south of it.</summary>
    public static readonly Cell EntranceCrossing = new(3, 0);

    /// <summary>Step onto this cell to leave the Temple for good.</summary>
    public static readonly Cell EntranceExit = new(3, -1);

    public static readonly IReadOnlyList<Cell> WestLateral = [new(0, 0), new(1, 0), new(2, 0)];

    public static readonly IReadOnlyList<Cell> EastLateral = [new(4, 0), new(5, 0), new(6, 0)];

    public static TempleBoard CreateBoard(BoardBounds? bounds = null)
    {
        var board = new TempleBoard(bounds);

        board.PlaceFixed(EntranceExit, Fixed("Entrance-Exit", TileKind.Entrance, Sides.South));
        board.PlaceFixed(EntranceCrossing, Fixed("Entrance-Crossing", TileKind.Entrance, Sides.All));

        PlaceLateral(board, WestLateral, "West");
        PlaceLateral(board, EastLateral, "East");

        return board;
    }

    private static void PlaceLateral(TempleBoard board, IReadOnlyList<Cell> cells, string side)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var eastward = RunsOn(cells, cell, Direction.East);
            var westward = RunsOn(cells, cell, Direction.West);

            // The arm ends where it stops running: a Guardian pocket facing back in.
            var tile = (eastward, westward) switch
            {
                (true, false) => Fixed($"Lateral-{side}-{index}", TileKind.Guardian, Sides.East),
                (false, true) => Fixed($"Lateral-{side}-{index}", TileKind.Guardian, Sides.West),
                _ => Fixed($"Lateral-{side}-{index}", TileKind.Normal, Sides.East | Sides.West | Sides.South),
            };

            board.PlaceFixed(cell, tile);
        }
    }

    /// <summary>
    /// Whether the arm continues that way — another of its cells, or the Entrance
    /// crossing. Beyond its ends there is nothing.
    /// </summary>
    private static bool RunsOn(IReadOnlyList<Cell> cells, Cell cell, Direction direction)
    {
        var neighbour = cell.Neighbour(direction);
        return cells.Contains(neighbour) || neighbour == EntranceCrossing;
    }

    /// <summary>
    /// Builds a setup piece from the passages it must offer, working back to the
    /// printed shape and the turn that lays it out that way.
    /// </summary>
    private static PlacedTile Fixed(string id, TileKind kind, Sides openSides)
    {
        if (TileShapeExtensions.Match(openSides) is not { } layout)
        {
            throw new ArgumentException($"No tile shape lays out {openSides}.", nameof(openSides));
        }

        return new PlacedTile(new TileDefinition(id, kind, layout.Shape), layout.Rotation);
    }
}
