using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// A tile scene that has something to show for what just happened on its own cell —
/// a trap that sprang, a ruin that came down. <see cref="BoardView"/> hands it only
/// the events that named its cell, from the command that was just settled; a tile
/// drawn for any other reason (a preview, a redraw after someone else's turn) gets
/// nothing, so nothing replays on its own.
/// </summary>
public interface ITileEventListener
{
    void Announce(IReadOnlyList<GameEvent> events);
}
