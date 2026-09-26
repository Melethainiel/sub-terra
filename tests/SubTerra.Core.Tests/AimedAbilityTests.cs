using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>
/// The abilities aimed by pointing at a tile — Lunette de visée, Tir de précision,
/// Grenade, Purifier, Démolir, Rechercher — and Érudite, which changes how a tile is drawn.
/// </summary>
public class AimedAbilityTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static Cell Below(int rows) => new(Start.Column, Start.Row + rows);

    private static PlacedTile Laid(Sides openSides)
    {
        var layout = TileShapeExtensions.Match(openSides)
            ?? throw new ArgumentException($"No tile shape lays out {openSides}.", nameof(openSides));

        return new PlacedTile(new TileDefinition("test", TileKind.Normal, layout.Shape), layout.Rotation);
    }

    private static TileDefinition Tile(TileKind kind, TileShape shape = TileShape.Crossroads) =>
        new($"{kind}-test", kind, shape);

    private static Explorer Holding(int id, string sheetId, Cell? at = null)
    {
        var sheet = ExplorerRoster.Find(sheetId)!;
        return new Explorer(new ExplorerId(id), sheet.Name, sheet.MaxHealth, at ?? Start, sheet);
    }

    private static Explorer Plain(int id, Cell? at = null) => new(new ExplorerId(id), $"Autre {id}", 5, at ?? Start);

    /// <summary>The setup board with a straight north-south gallery of <paramref name="length"/> tiles below the Entrance.</summary>
    private static TempleBoard Gallery(int length)
    {
        var board = TempleSetup.CreateBoard();

        for (var row = 1; row <= length; row++)
        {
            board.PlaceFixed(Below(row), Laid(Sides.North | Sides.South));
        }

        return board;
    }

    private static GameState Game(Explorer[] explorers, TempleBoard? board = null, TileDefinition[]? bag = null, ulong seed = 1) =>
        new(explorers, board ?? TempleSetup.CreateBoard(), new TileBag(bag ?? []), new Rng(seed));

    // ── Lunette de visée ───────────────────────────────────────────────────

    [Fact]
    public void LunetteRevealsTheFarEndOfALineInSight()
    {
        var game = Game([Holding(0, "tireuse")], Gallery(2), [Tile(TileKind.Normal)]);
        Assert.Contains(Below(3), game.AbilityCells(AbilityIds.Lunette));

        var result = game.Play(game.AbilityAt(AbilityIds.Lunette, Below(3))!);

        Assert.True(result.Accepted, result.Rejection);
        Assert.True(game.Board.IsOccupied(Below(3)));
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
    }

    [Fact]
    public void LunetteSeesNoFurtherThanThreeTiles()
    {
        var game = Game([Holding(0, "tireuse")], Gallery(3), [Tile(TileKind.Normal)]);

        Assert.DoesNotContain(Below(4), game.AbilityCells(AbilityIds.Lunette));
        Assert.False(game.Play(new UseAbility(AbilityIds.Lunette, Direction: Direction.South)).Accepted);
    }

    // ── Tir de précision ───────────────────────────────────────────────────

    [Fact]
    public void TirDePrecisionShootsAnEnemyInSight()
    {
        var game = Game([Holding(0, "tireuse")], bag: [Tile(TileKind.Guardian)]);
        game.Play(new Reveal(Direction.South));
        Assert.Equal([Below(1)], game.Guardians);

        var result = game.Play(new UseAbility(AbilityIds.TirDePrecision, Cell: Below(1)));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(game.Guardians);
    }

    [Fact]
    public void TirDePrecisionDoesNotShootOnHerOwnTile()
    {
        var game = Game([Holding(0, "tireuse")], bag: [Tile(TileKind.Guardian)]);
        game.Play(new Reveal(Direction.South));
        game.Play(new Move(Direction.South));
        game.Play(new Overexert());

        Assert.Empty(game.AbilityCells(AbilityIds.TirDePrecision));
        Assert.False(game.Play(new UseAbility(AbilityIds.TirDePrecision, Cell: Below(1))).Accepted);
    }

    // ── Grenade ────────────────────────────────────────────────────────────

    [Fact]
    public void AGrenadeClearsANeighbouringTileAndHurtsWhoeverIsOnIt()
    {
        // Someone is already standing where the Guardian's tile is about to be laid.
        var game = Game([Holding(0, "sapeur"), Plain(1, Below(1))], bag: [Tile(TileKind.Guardian)]);
        game.Play(new Reveal(Direction.South));

        var result = game.Play(new UseAbility(AbilityIds.Grenade, Cell: Below(1)));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(game.Guardians);
        Assert.Equal(4, game.Explorers[1].Health);
        Assert.Equal(5, game.Explorers[0].Health);
    }

    // ── Purifier ───────────────────────────────────────────────────────────

    [Fact]
    public void PurifierClearsAnyOtherTile()
    {
        // A seed whose Peril die, after the one draw from the bag, leaves the Guardian where it is.
        ulong seed = 1;

        while (true)
        {
            var rng = new Rng(seed);
            rng.Next(1);

            if (rng.RollPeril() is PerilFace.Stumble or PerilFace.Lava)
            {
                break;
            }

            seed++;
        }

        var game = Game([Plain(0), Holding(1, "pretre")], bag: [Tile(TileKind.Guardian)], seed: seed);
        game.Play(new Reveal(Direction.South));
        game.Play(new EndTurn());
        Assert.Equal([Below(1)], game.Guardians);

        game.Play(new Overexert());
        var result = game.Play(new UseAbility(AbilityIds.Purifier, Cell: Below(1)));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(game.Guardians);
        Assert.Equal(0, game.ActionPoints);
    }

    // ── Démolir ────────────────────────────────────────────────────────────

    [Fact]
    public void DemolirOpensAWallTheTempleCanBeRevealedThrough()
    {
        var board = TempleSetup.CreateBoard();
        board.PlaceFixed(Below(1), Laid(Sides.North | Sides.East));
        var game = Game([Holding(0, "sapeur", Below(1))], board, [Tile(TileKind.Normal)]);
        var west = Below(1).Neighbour(Direction.West);

        Assert.DoesNotContain(Direction.West, game.Exits());
        Assert.Contains(west, game.AbilityCells(AbilityIds.Demolir));

        var result = game.Play(new UseAbility(AbilityIds.Demolir, Direction: Direction.West));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Contains(new WallDemolished(Below(1), Direction.West), result.Events);
        Assert.Contains(Direction.West, game.Exits());
        Assert.Equal(2, game.UsesLeft(game.CurrentExplorer.Id, AbilityIds.Demolir));
        Assert.True(game.Play(new Reveal(Direction.West)).Accepted);
    }

    [Fact]
    public void AnOpenSideIsNoWallToDemolish()
    {
        var game = Game([Holding(0, "sapeur")]);

        // The Entrance crossing is open on every side.
        Assert.Empty(game.AbilityCells(AbilityIds.Demolir));
        Assert.False(game.Play(new UseAbility(AbilityIds.Demolir, Direction: Direction.South)).Accepted);
    }

    // ── Rechercher ─────────────────────────────────────────────────────────

    [Fact]
    public void RechercherLaysAJournalAgainstAnyTileOfTheTemple()
    {
        var game = Game([Holding(0, "aristocrate")]);
        var farSide = new Cell(1, 1);
        Assert.Contains(farSide, game.AbilityCells(AbilityIds.Rechercher));

        var result = game.Play(game.AbilityAt(AbilityIds.Rechercher, farSide)!);

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(TileKind.Journal, game.Board.TileAt(farSide)!.Kind);
        Assert.True(game.Board.AreConnected(new Cell(1, 0), farSide));
        Assert.Equal(0, game.ActionPoints);
        Assert.Equal(2, game.UsesLeft(game.CurrentExplorer.Id, AbilityIds.Rechercher));
    }

    // ── Érudite ────────────────────────────────────────────────────────────

    [Fact]
    public void TheArcheologistDrawsTwoAndPutsOneBack()
    {
        var game = Game([Holding(0, "archeologue")], bag: [Tile(TileKind.Lava), Tile(TileKind.Key, TileShape.DeadEnd)]);

        game.Execute(new Reveal(Direction.South));
        Assert.Equal(DecisionKind.TileChoice, game.Pending?.Kind);
        var kept = game.Pending!.Options[1].Tile!.Definition;
        var other = game.Pending.Options[0].Tile!.Definition;

        var result = game.Execute(new Decide(1));
        while (game.Pending is not null)
        {
            game.Execute(new Decide(0));
        }

        Assert.Contains(new TileReturned(other), result.Events);
        Assert.Equal(kept, game.Board.TileAt(Below(1))!.Definition);
        Assert.Equal([other], game.Bag.Remaining);
    }

    [Fact]
    public void WithOneTileLeftThereIsNothingToChoose()
    {
        var game = Game([Holding(0, "archeologue")], bag: [Tile(TileKind.Lava)]);

        game.Execute(new Reveal(Direction.South));

        Assert.NotEqual(DecisionKind.TileChoice, game.Pending?.Kind);
        Assert.True(game.Board.IsOccupied(Below(1)));
    }
}
