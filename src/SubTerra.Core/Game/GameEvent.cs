using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Game;

/// <summary>
/// What actually happened. The presentation replays these as animations rather than
/// diffing the state, and a game is replayable from its seed and its commands.
/// </summary>
public abstract record GameEvent;

public sealed record TurnBegan(ExplorerId Explorer, int ActionPoints) : GameEvent;

public sealed record TurnEnded(ExplorerId Explorer) : GameEvent;

/// <summary>Every explorer has taken a turn; the temple now takes its own.</summary>
public sealed record RoundEnded(int Round) : GameEvent;

public sealed record ExplorerMoved(ExplorerId Explorer, Cell From, Cell To) : GameEvent;

public sealed record TileRevealed(Cell Cell, TileDefinition Tile, int Rotation) : GameEvent;

public sealed record KeyAppeared(Cell Cell) : GameEvent;

public sealed record GuardianAppeared(Cell Cell) : GameEvent;

public sealed record HealthLost(ExplorerId Explorer, int Amount, int Remaining) : GameEvent;

public sealed record ExplorerWentDown(ExplorerId Explorer) : GameEvent;

/// <summary>The bag is empty: the Sanctuary can be placed.</summary>
public sealed record BagEmptied : GameEvent;
