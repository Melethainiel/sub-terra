using SubTerra.Core.Board;

namespace SubTerra.Core.Game;

/// <summary>
/// Something a player asks the game to do. The host validates and applies these;
/// the interface may grey out the impossible ones for comfort, but it is never the
/// authority on what is legal.
/// </summary>
public abstract record GameCommand;

/// <summary>Step onto the adjacent connected tile in that direction. Costs 1 action.</summary>
public sealed record Move(Direction Direction) : GameCommand;

/// <summary>
/// Draw a tile from the bag and lay it beyond one of the current tile's unconnected
/// exits, turned as the player likes. Costs 1 action.
/// </summary>
public sealed record Reveal(Direction Direction, int Rotation) : GameCommand;

/// <summary>Spend a heart for an extra action. Once per player turn.</summary>
public sealed record Overexert : GameCommand;

/// <summary>Hand over to the next explorer.</summary>
public sealed record EndTurn : GameCommand;
