using SubTerra.Core.Board;

namespace SubTerra.Core.Game;

/// <summary>
/// Which of the rulebook's ambiguities is on the table. The rules resolve most of
/// them with "le Chef d'Expédition choisit", so the engine stops and asks rather
/// than quietly picking for the players.
/// </summary>
public enum DecisionKind
{
    /// <summary>Several explorers share the guardian's tile: which one it strikes.</summary>
    GuardianTarget,

    /// <summary>Several first steps are equally close to the prey.</summary>
    GuardianStep,

    /// <summary>Several piles of rubble are within reach.</summary>
    GuardianDig,

    /// <summary>Several Guardian tiles are equally near the active explorer.</summary>
    GuardianAwakening,

    /// <summary>Several tiles at the far end of the temple could take the Sanctuary.</summary>
    SanctuarySite,

    /// <summary>The drawn tile joins up more than one way round: which way it is laid.</summary>
    TileOrientation,

    /// <summary>L'Archéologue may pay a heart to roll the die that just fell again.</summary>
    Reroll,

    /// <summary>L'Archéologue drew two tiles: which one is laid, the other going back.</summary>
    TileChoice,
}

/// <summary>
/// One of the answers on offer. <paramref name="Cell"/> and <paramref name="Explorer"/>
/// let the interface point at the board instead of showing a line of text, and
/// <paramref name="Tile"/> lets it lay the tile down for a look before it is committed.
/// </summary>
public sealed record DecisionOption(
    string Label,
    Cell? Cell = null,
    ExplorerId? Explorer = null,
    PlacedTile? Tile = null);

/// <summary>
/// A question the game is waiting on. Nothing else happens until it is answered with
/// a <see cref="Decide"/> command.
/// </summary>
public sealed record PendingDecision(
    DecisionKind Kind,
    string Prompt,
    ExplorerId Chooser,
    IReadOnlyList<DecisionOption> Options);
