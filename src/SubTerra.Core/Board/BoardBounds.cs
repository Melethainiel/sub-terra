namespace SubTerra.Core.Board;

/// <summary>
/// The region tiles may be drawn into. The rules only say tiles may go neither
/// behind nor beyond the Lateral tiles, which bounds the columns and forbids
/// anything north of the Lateral row.
/// </summary>
/// <remarks>
/// The column span is read off the setup diagram (rulebook p. 8) and is still to be
/// confirmed against the physical components — see docs/regles.md §13.2.
/// </remarks>
public sealed record BoardBounds(int MinColumn, int MaxColumn, int MinRow)
{
    /// <summary>Seven playable columns, the Entrance sitting on the middle one.</summary>
    public static readonly BoardBounds Default = new(MinColumn: 0, MaxColumn: 6, MinRow: 1);

    public bool Contains(Cell cell) =>
        cell.Column >= MinColumn && cell.Column <= MaxColumn && cell.Row >= MinRow;
}
