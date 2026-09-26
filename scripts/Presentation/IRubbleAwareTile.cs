namespace SubTerra.Presentation;

/// <summary>
/// A tile scene whose look depends on whether its own cell is currently buried —
/// set on every redraw, not just the one where it changes, so a tile drawn fresh
/// (say, after someone else's turn) still matches what the board says is true.
/// </summary>
public interface IRubbleAwareTile
{
    void SetBuried(bool buried);
}
