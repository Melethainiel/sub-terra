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

/// <summary>
/// The rules have run into a tie and handed it to a player. Play is suspended until a
/// <see cref="Decide"/> command answers it.
/// </summary>
public sealed record DecisionRequired(PendingDecision Decision) : GameEvent;

/// <summary>The tie was settled, and the consequences pick up where they stopped.</summary>
public sealed record DecisionMade(PendingDecision Decision, int Option) : GameEvent
{
    public DecisionOption Chosen => Decision.Options[Option];
}

public sealed record ExplorerMoved(ExplorerId Explorer, Cell From, Cell To) : GameEvent;

public sealed record TileRevealed(Cell Cell, TileDefinition Tile, int Rotation) : GameEvent;

public sealed record ItemAppeared(Cell Cell, ItemKind Item) : GameEvent;

public sealed record GuardianAppeared(Cell Cell) : GameEvent;

public sealed record PerilRolled(PerilFace Face) : GameEvent;

public sealed record GuardianAttacked(Cell Cell, ExplorerId Explorer) : GameEvent;

public sealed record GuardianStepped(Cell From, Cell To) : GameEvent;

public sealed record GuardianClearedRubble(Cell Guardian, Cell Cleared) : GameEvent;

public sealed record GuardianEliminated(Cell Cell) : GameEvent;

public sealed record RubbleAppeared(Cell Cell) : GameEvent;

public sealed record RubbleCleared(Cell Cell) : GameEvent;

public sealed record RuinsCollapsed(Cell Cell) : GameEvent;

public sealed record ItemPickedUp(ExplorerId Explorer, ItemKind Item, Cell Cell) : GameEvent;

public sealed record ItemDropped(ExplorerId Explorer, ItemKind Item, Cell Cell) : GameEvent;

/// <summary>A die was rolled in the open, and everyone saw the face.</summary>
public sealed record DieRolled(int Face) : GameEvent;

public sealed record TrapSprung(Cell Cell, TileKind Trap) : GameEvent;

/// <summary>An Explorer played one of their own abilities; its effects follow.</summary>
public sealed record AbilityUsed(ExplorerId Explorer, string Ability) : GameEvent;

/// <summary>Démolir: the wall on that side of the tile is gone for the rest of the game.</summary>
public sealed record WallDemolished(Cell From, Direction Direction) : GameEvent;

/// <summary>Érudite: the tile she did not keep goes back in the bag.</summary>
public sealed record TileReturned(TileDefinition Tile) : GameEvent;

/// <summary>Se préparer: the Bouclier goes up until her next turn.</summary>
public sealed record ShieldRaised(ExplorerId Explorer) : GameEvent;

/// <summary>Her turn has come round again: the Bouclier comes down.</summary>
public sealed record ShieldLowered(ExplorerId Explorer) : GameEvent;

/// <summary>A Consolidation marker: that tile is a Normal one for the rest of the game.</summary>
public sealed record TileConsolidated(Cell Cell) : GameEvent;

public sealed record HealthRegained(ExplorerId Explorer, int Amount, int Total) : GameEvent;

public sealed record ExplorerStoodUp(ExplorerId Explorer) : GameEvent;

public sealed record HealthLost(ExplorerId Explorer, int Amount, int Remaining) : GameEvent;

public sealed record ExplorerWentDown(ExplorerId Explorer) : GameEvent;

/// <summary>The bag is empty: the Sanctuary can be placed.</summary>
public sealed record BagEmptied : GameEvent;

public sealed record SanctuaryFound(Cell Hall, Cell Vault) : GameEvent;

public sealed record KeyDeposited(Cell Cell, int Total) : GameEvent;

/// <summary>The third key turned: the Artefact sits on its pedestal.</summary>
public sealed record ArtefactRevealed(Cell Cell) : GameEvent;

/// <summary>Someone lifted the Artefact. The mountain noticed.</summary>
public sealed record CurseFell : GameEvent;

public sealed record ExplorerEscaped(ExplorerId Explorer, bool WithArtefact) : GameEvent;

public sealed record GameEnded(Outcome Outcome) : GameEvent;

public sealed record EruptionAdvanced(int Remaining) : GameEvent;

/// <summary>The marker reached zero. The next flame face blows the mountain open.</summary>
public sealed record VolcanoReady : GameEvent;

public sealed record VolcanoErupted : GameEvent;

/// <summary>Tiles turned over to their volcano face. Nothing walks there again.</summary>
public sealed record TilesFlooded(IReadOnlyList<Cell> Cells) : GameEvent;

public sealed record ExplorerKilled(ExplorerId Explorer, Cell Cell) : GameEvent;
