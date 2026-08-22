namespace SubTerra.Core.Board;

/// <summary>
/// A side of a placed tile that leads nowhere yet. The Reveal action names one of
/// these, and the drawn tile lands on the cell beyond it.
/// </summary>
public readonly record struct TempleExit(Cell From, Direction Direction)
{
    /// <summary>The empty cell the revealed tile will occupy.</summary>
    public Cell Target => From.Neighbour(Direction);

    public override string ToString() => $"{From} {Direction}";
}
