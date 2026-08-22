using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class PerilTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static TileDefinition Tile(TileKind kind, int? ruinsNumber = null) =>
        new($"{kind}-test", kind, TileShape.Crossroads, ruinsNumber);

    /// <summary>
    /// The first seed whose opening Peril roll shows that face. Searching beats
    /// hard-coding: the test says what it needs rather than a magic number.
    /// </summary>
    private static ulong SeedFor(PerilFace face, Func<Rng, bool>? andThen = null)
    {
        for (ulong seed = 1; seed < 5_000; seed++)
        {
            var rng = new Rng(seed);

            if (rng.RollPeril() == face && (andThen is null || andThen(rng)))
            {
                return seed;
            }
        }

        throw new InvalidOperationException($"Aucune graine ne donne {face}.");
    }

    private static GameState Game(TempleBoard board, ulong seed, params (string Name, int Health)[] roster)
    {
        var sheets = roster.Length == 0 ? [("Guide", 7)] : roster;
        var explorers = sheets.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.Health, Start));

        return new GameState(explorers, board, new TileBag([]), new Rng(seed));
    }

    private static TempleBoard BoardWith(params (Cell Cell, TileDefinition Tile)[] tiles)
    {
        var board = TempleSetup.CreateBoard();

        foreach (var (cell, tile) in tiles)
        {
            board.PlaceFixed(cell, new PlacedTile(tile));
        }

        return board;
    }

    [Fact]
    public void EveryTurnEndsWithAPerilRoll()
    {
        var game = Game(TempleSetup.CreateBoard(), seed: 3);

        var result = game.Execute(new EndTurn());

        Assert.Single(result.Events.OfType<PerilRolled>());
    }

    [Fact]
    public void StumblingOnlyHurtsThoseWhoPushedThemselves()
    {
        var seed = SeedFor(PerilFace.Stumble);

        var careful = Game(TempleSetup.CreateBoard(), seed);
        careful.Execute(new EndTurn());
        Assert.Equal(7, careful.Explorers[0].Health);

        var reckless = Game(TempleSetup.CreateBoard(), seed);
        reckless.Execute(new Overexert());
        reckless.Execute(new EndTurn());

        // One heart for the extra action, one more for stumbling on it.
        Assert.Equal(5, reckless.Explorers[0].Health);
    }

    [Fact]
    public void LavaBurnsEveryoneStandingInIt()
    {
        var lava = new Cell(Start.Column, 1);
        var game = Game(BoardWith((lava, Tile(TileKind.Lava))), SeedFor(PerilFace.Lava),
            ("Guide", 7), ("Prêtre", 5));

        game.Execute(new Move(Direction.South));
        game.Execute(new EndTurn());

        Assert.Equal(7 - GameState.LavaDamage, game.Explorers[0].Health);
        Assert.Equal(5, game.Explorers[1].Health);
    }

    [Fact]
    public void ARuinComesDownWhenItsNumberIsRolled()
    {
        // The face, then the d6 that names which ruin falls.
        var doomedNumber = 0;
        var seed = SeedFor(PerilFace.Collapse, rng =>
        {
            doomedNumber = rng.RollDie();
            return true;
        });

        var ruins = new Cell(Start.Column, 1);
        var game = Game(BoardWith((ruins, Tile(TileKind.Ruins, doomedNumber))), seed, ("Guide", 7));

        var result = game.Execute(new EndTurn());

        Assert.Contains(new RuinsCollapsed(ruins), result.Events);
        Assert.Contains(ruins, game.Rubble);
    }

    [Fact]
    public void ARuinAlreadyBuriedCannotFallAgain()
    {
        var doomedNumber = 0;
        var seed = SeedFor(PerilFace.Collapse, rng =>
        {
            doomedNumber = rng.RollDie();
            return true;
        });

        var ruins = new Cell(Start.Column, 1);
        var board = BoardWith((ruins, Tile(TileKind.Ruins, doomedNumber)));
        var game = Game(board, seed);

        // Reveal-time rubble is not simulated here, so bury it by hand via a collapse.
        game.Execute(new EndTurn());
        Assert.Contains(ruins, game.Rubble);

        var again = game.Execute(new EndTurn());
        Assert.DoesNotContain(again.Events, e => e is RuinsCollapsed);
    }

    [Fact]
    public void TrapsSpringAroundTheActiveExplorerOnly()
    {
        var darts = new Cell(Start.Column, 1);
        var game = Game(BoardWith((darts, Tile(TileKind.DartTrap))), SeedFor(PerilFace.Trap),
            ("Guide", 7), ("Prêtre", 5));

        var result = game.Execute(new EndTurn());

        // The active explorer is at the entrance, connected to the dart tile.
        Assert.Contains(result.Events, e => e is TrapSprung { Trap: TileKind.DartTrap });
        Assert.Equal(7 - GameState.DartTrapDamage, game.Explorers[0].Health);
    }

    [Fact]
    public void WakingAGuardianFillsTheNearestGuardianPocket()
    {
        var pocket = new Cell(Start.Column, 1);

        // Two explorers, so ending the first turn does not also end the round and set
        // the guardian moving before we can look at it.
        var game = Game(BoardWith((pocket, Tile(TileKind.Guardian))), SeedFor(PerilFace.WakeGuardian),
            ("Guide", 7), ("Prêtre", 5));

        var result = game.Execute(new EndTurn());

        Assert.Contains(new GuardianAppeared(pocket), result.Events);
        Assert.Equal([pocket], game.Guardians);
    }

    [Fact]
    public void TheNearestPocketWinsOverTheOnesDownTheLateralArms()
    {
        // The setup board already ends each Lateral arm in a guardian pocket, three
        // tiles away. One laid next door should take precedence.
        var next = new Cell(Start.Column, 1);
        var game = Game(BoardWith((next, Tile(TileKind.Guardian))), SeedFor(PerilFace.WakeGuardian),
            ("Guide", 7), ("Prêtre", 5));

        game.Execute(new EndTurn());

        Assert.Equal([next], game.Guardians);
    }

    [Fact]
    public void TheSixthGuardianNeverComes()
    {
        // Six guardian pockets laid one after another; the box only holds five meeples.
        var bag = new TileBag(Enumerable.Range(1, 6)
            .Select(i => new TileDefinition($"Guardian-{i}", TileKind.Guardian, TileShape.Crossroads)));

        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Increvable", 99, Start)],
            TempleSetup.CreateBoard(),
            bag,
            new Rng(4));

        while (!game.Bag.IsEmpty)
        {
            game.Execute(new Explore(Direction.South, Rotation: 0));
            game.Execute(new EndTurn());
        }

        Assert.True(game.Bag.IsEmpty);
        Assert.Equal(GameState.MaxGuardians, game.Guardians.Count);
    }
}
