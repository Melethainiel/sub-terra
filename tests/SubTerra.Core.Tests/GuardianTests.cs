using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class GuardianTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static TileDefinition Tile(TileKind kind, TileShape shape = TileShape.Crossroads) =>
        new($"{kind}-test", kind, shape);

    /// <summary>
    /// The first seed whose d6 shows a face the test cares about, once the rolls the
    /// game makes beforehand are out of the way — drawing from the bag spends one.
    /// </summary>
    private static ulong SeedWhereDieIs(Func<int, bool> wanted, int rollsFirst = 0)
    {
        for (ulong seed = 1; seed < 5_000; seed++)
        {
            var rng = new Rng(seed);

            for (var skip = 0; skip < rollsFirst; skip++)
            {
                rng.Next(1);
            }

            if (wanted(rng.RollDie()))
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Aucune graine ne convient.");
    }

    private static GameState Game(TempleBoard board, ulong seed = 1, params (string Name, int Health, Cell Cell)[] roster)
    {
        var sheets = roster.Length == 0 ? [("Guide", 7, Start)] : roster;
        var explorers = sheets.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.Health, sheet.Cell));

        return new GameState(explorers, board, new TileBag([]), new Rng(seed));
    }

    /// <summary>Puts a guardian in play by revealing the pocket that carries it.</summary>
    private static (GameState Game, Cell Pocket) GameWithGuardianNextDoor(ulong seed = 1, int health = 7)
    {
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", health, Start),
             new Explorer(new ExplorerId(1), "Prêtre", 5, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([Tile(TileKind.Guardian)]),
            new Rng(seed));

        return (game, new Cell(Start.Column, 1));
    }

    [Fact]
    public void AGuardianArrivesWithItsPocket()
    {
        var (game, pocket) = GameWithGuardianNextDoor();
        game.Execute(new Reveal(Direction.South, Rotation: 0));

        Assert.Equal([pocket], game.Guardians);
    }

    [Fact]
    public void GuardiansCloseInWhenTheRoundEnds()
    {
        var (game, pocket) = GameWithGuardianNextDoor();
        game.Execute(new Reveal(Direction.South, Rotation: 0));

        game.Execute(new EndTurn());
        var round = game.Execute(new EndTurn());

        Assert.Contains(round.Events, e => e is RoundEnded);

        // Two activations: one step onto the explorers, then a strike.
        Assert.Contains(new GuardianStepped(pocket, Start), round.Events);
        Assert.Contains(round.Events, e => e is GuardianAttacked);
    }

    [Fact]
    public void AGuardianOnYourTileStrikesRatherThanWalks()
    {
        var (game, _) = GameWithGuardianNextDoor();
        game.Execute(new Reveal(Direction.South, Rotation: 0));
        game.Execute(new EndTurn());
        var round = game.Execute(new EndTurn());

        // First activation walks it onto the explorers, second one strikes.
        var struck = round.Events.OfType<GuardianAttacked>().Single();
        Assert.Equal(Start, struck.Cell);
        Assert.Equal(6, game.Explorers.Single(e => e.Id == struck.Explorer).Health);
    }

    [Fact]
    public void LeavingAGuardianCostsAHeart()
    {
        var (game, pocket) = GameWithGuardianNextDoor();

        // Explore reveals the pocket and steps into it for a single action, leaving
        // one to walk back out with.
        game.Execute(new Explore(Direction.South, Rotation: 0));
        Assert.Equal(pocket, game.CurrentExplorer.Cell);

        var before = game.CurrentExplorer.Health;
        var result = game.Execute(new Move(Direction.North));

        Assert.True(result.Accepted);
        Assert.Equal(before - GameState.GuardianDamage, game.CurrentExplorer.Health);
        Assert.Equal(Start, game.CurrentExplorer.Cell);
    }

    [Fact]
    public void AnExplorerFleeingOnTheirLastHeartStillReachesTheNextTile()
    {
        var (game, pocket) = GameWithGuardianNextDoor(health: 1);

        game.Execute(new Explore(Direction.South, Rotation: 0));
        Assert.Equal(pocket, game.CurrentExplorer.Cell);

        var result = game.Execute(new Move(Direction.North));

        Assert.True(result.Accepted);
        Assert.True(game.CurrentExplorer.IsDown);
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Contains(result.Events, e => e is ExplorerWentDown);
    }

    [Fact]
    public void AttackingKillsAGuardianOnFourOrBetter()
    {
        // The reveal draws from the bag, which spends a roll before the attack's.
        var seed = SeedWhereDieIs(roll => roll >= GameState.AttackSuccessRoll, rollsFirst: 1);
        var (game, _) = GameWithGuardianNextDoor(seed);

        game.Execute(new Explore(Direction.South, Rotation: 0));
        var result = game.Execute(new Attack());

        Assert.True(result.Accepted);
        Assert.Contains(result.Events, e => e is GuardianEliminated);
        Assert.Empty(game.Guardians);
    }

    [Fact]
    public void AttackingMissesBelowFour()
    {
        var seed = SeedWhereDieIs(roll => roll < GameState.AttackSuccessRoll, rollsFirst: 1);
        var (game, _) = GameWithGuardianNextDoor(seed);

        game.Execute(new Explore(Direction.South, Rotation: 0));
        var result = game.Execute(new Attack());

        Assert.True(result.Accepted);
        Assert.DoesNotContain(result.Events, e => e is GuardianEliminated);
        Assert.Single(game.Guardians);
    }

    [Fact]
    public void AttackingThinAirIsRefused()
    {
        var game = Game(TempleSetup.CreateBoard());

        Assert.False(game.Execute(new Attack()).Accepted);
    }

    [Fact]
    public void RuinsArriveBuriedAndCannotBeWalkedInto()
    {
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([Tile(TileKind.Ruins)]),
            new Rng(1));

        var revealed = game.Execute(new Reveal(Direction.South, Rotation: 0));
        var ruins = new Cell(Start.Column, 1);

        Assert.Contains(new RubbleAppeared(ruins), revealed.Events);
        Assert.Contains(ruins, game.Rubble);
        Assert.False(game.Execute(new Move(Direction.South)).Accepted);
    }

    [Fact]
    public void DiggingClearsTheRubbleForTwoActions()
    {
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start), new Explorer(new ExplorerId(1), "Prêtre", 5, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([Tile(TileKind.Ruins)]),
            new Rng(1));

        var ruins = new Cell(Start.Column, 1);
        game.Execute(new Reveal(Direction.South, Rotation: 0));
        game.Execute(new EndTurn());

        var result = game.Execute(new Dig(ruins));

        Assert.True(result.Accepted);
        Assert.DoesNotContain(ruins, game.Rubble);
        // Digging is the whole turn: nothing left to walk in with.
        Assert.Equal(0, game.ActionPoints);
        Assert.False(game.Execute(new Move(Direction.South)).Accepted);
    }

    [Fact]
    public void DiggingOutOfReachIsRefused()
    {
        var game = Game(TempleSetup.CreateBoard());

        Assert.False(game.Execute(new Dig(new Cell(0, 5))).Accepted);
    }
}
