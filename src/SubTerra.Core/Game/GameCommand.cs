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

/// <summary>
/// Reveal a tile and step onto it in one action — quicker than doing both, and a
/// good deal more dangerous, since you walk onto whatever you just uncovered.
/// </summary>
public sealed record Explore(Direction Direction, int Rotation) : GameCommand;

/// <summary>Up to three moves for two actions.</summary>
public sealed record Run(IReadOnlyList<Direction> Steps) : GameCommand;

/// <summary>Give an explorer on your tile a heart back — yourself included. 1 action.</summary>
public sealed record Heal(ExplorerId Target) : GameCommand;

/// <summary>Take something off your tile. 1 action.</summary>
public sealed record PickUpItem(ItemKind Item) : GameCommand;

/// <summary>Put down what you are carrying. 1 action.</summary>
public sealed record DropItem : GameCommand;

/// <summary>Roll to kill one enemy on your tile: 4 or better does it. 1 action.</summary>
public sealed record Attack : GameCommand;

/// <summary>Clear the rubble from your tile or a connected neighbour. 2 actions.</summary>
public sealed record Dig(Cell Cell) : GameCommand;

/// <summary>Spend a heart for an extra action. Once per player turn.</summary>
public sealed record Overexert : GameCommand;

/// <summary>Hand over to the next explorer.</summary>
public sealed record EndTurn : GameCommand;
