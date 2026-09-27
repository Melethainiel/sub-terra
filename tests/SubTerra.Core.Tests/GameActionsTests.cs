using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class GameActionsTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static TileDefinition Tile(TileKind kind, TileShape shape = TileShape.Crossroads) =>
        new($"{kind}-test", kind, shape);

    private static GameState GameWith(TempleBoard board, TileBag? bag = null, ulong seed = 1,
        params (string Name, int MaxHealth)[] roster)
    {
        var sheets = roster.Length == 0 ? [("Guide", 3)] : roster;
        var explorers = sheets.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.MaxHealth, Start));

        return new GameState(explorers, board, bag ?? new TileBag([]), new Rng(seed));
    }

    /// <summary>The setup board with a straight run of open tiles heading south.</summary>
    private static TempleBoard CorridorSouth(int length, TileKind kind = TileKind.Normal)
    {
        var board = TempleSetup.CreateBoard();

        for (var row = 1; row <= length; row++)
        {
            board.PlaceFixed(new Cell(Start.Column, row), new PlacedTile(Tile(kind)));
        }

        return board;
    }

    [Fact]
    public void ExploringRevealsATileAndStepsOntoItForOneAction()
    {
        var game = GameWith(TempleSetup.CreateBoard(), new TileBag([Tile(TileKind.Normal, TileShape.Junction)]));

        var result = game.Play(new Explore(Direction.South));

        Assert.True(result.Accepted);
        Assert.Equal(new Cell(Start.Column, 1), game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
        Assert.Contains(result.Events, e => e is TileRevealed);
        Assert.Contains(result.Events, e => e is ExplorerMoved);
    }

    [Fact]
    public void ExploringOntoAnOccupiedBridgeLeavesTheTileButNotTheExplorer()
    {
        // The second explorer already stands where the bridge is about to land.
        var landing = new Cell(Start.Column, 1);
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 3, Start),
             new Explorer(new ExplorerId(1), "Prêtre", 5, landing)],
            TempleSetup.CreateBoard(),
            new TileBag([Tile(TileKind.Bridge, TileShape.Corridor)]),
            new Rng(1));

        var result = game.Play(new Explore(Direction.South));

        Assert.True(result.Accepted);
        Assert.True(game.Board.IsOccupied(landing));
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.DoesNotContain(result.Events, e => e is ExplorerMoved);
    }

    [Fact]
    public void RunningCoversThreeTilesForTwoActions()
    {
        var game = GameWith(CorridorSouth(3));

        var result = game.Play(new Run([Direction.South, Direction.South, Direction.South]));

        Assert.True(result.Accepted);
        Assert.Equal(new Cell(Start.Column, 3), game.CurrentExplorer.Cell);
        Assert.Equal(0, game.ActionPoints);
    }

    [Fact]
    public void ARouteThatBreaksPartWayIsRefusedWhole()
    {
        var game = GameWith(CorridorSouth(2));

        var result = game.Play(new Run([Direction.South, Direction.South, Direction.South]));

        Assert.False(result.Accepted);
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn, game.ActionPoints);
    }

    [Fact]
    public void RunningNeedsBothActionPoints()
    {
        var game = GameWith(CorridorSouth(3));
        game.Play(new Move(Direction.South));

        Assert.False(game.Play(new Run([Direction.South])).Accepted);
    }

    [Fact]
    public void RunningCoversAtMostThreeTiles()
    {
        var game = GameWith(CorridorSouth(4));

        var result = game.Play(new Run([.. Enumerable.Repeat(Direction.South, 4)]));

        Assert.False(result.Accepted);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(20260823UL)]
    [InlineData(99UL)]
    public void SteppingOntoSpikesIsSafeOnFourOrBetter(ulong seed)
    {
        var game = GameWith(CorridorSouth(1, TileKind.SpikeTrap), seed: seed, roster: [("Guide", 7)]);

        var result = game.Play(new Move(Direction.South));
        var roll = Assert.Single(result.Events.OfType<DieRolled>()).Face;

        if (roll >= GameState.SpikeTrapSafeRoll)
        {
            Assert.Equal(7, game.CurrentExplorer.Health);
            Assert.DoesNotContain(result.Events, e => e is TrapSprung);
        }
        else
        {
            Assert.Equal(7 - GameState.SpikeTrapDamage, game.CurrentExplorer.Health);
            Assert.Contains(result.Events, e => e is TrapSprung);
        }
    }

    [Fact]
    public void BothOutcomesOfTheSpikesAreReachable()
    {
        var rolls = Enumerable.Range(1, 40)
            .Select(seed => GameWith(CorridorSouth(1, TileKind.SpikeTrap), seed: (ulong)seed, roster: [("Guide", 7)]))
            .Select(game => game.Play(new Move(Direction.South)).Events.OfType<DieRolled>().Single().Face)
            .ToList();

        Assert.Contains(rolls, roll => roll >= GameState.SpikeTrapSafeRoll);
        Assert.Contains(rolls, roll => roll < GameState.SpikeTrapSafeRoll);
    }

    [Fact]
    public void SpikesDoNotKnockDownAnExplorerAlreadyOnTheFloor()
    {
        var sprung = 0;

        for (var seed = 1UL; seed <= 40; seed++)
        {
            var game = GameWith(CorridorSouth(1, TileKind.SpikeTrap), seed: seed, roster: [("Fragile", 1), ("Prêtre", 5)]);
            game.Play(new Overexert());
            game.Play(new EndTurn());

            for (var turn = 0; turn < 10 && game.CurrentExplorer.Id != game.Explorers[0].Id; turn++)
            {
                game.Play(new EndTurn());
            }

            if (game.IsOver || !game.CurrentExplorer.IsDown || game.CurrentExplorer.Cell != Start)
            {
                continue;
            }

            var result = game.Play(new Move(Direction.South));

            if (!result.Events.Any(e => e is TrapSprung))
            {
                continue;
            }

            sprung++;
            Assert.DoesNotContain(result.Events, e => e is ExplorerWentDown or HealthLost);
            Assert.True(game.Explorers[0].IsDown);
        }

        Assert.True(sprung > 0);
    }

    [Fact]
    public void HealingGivesBackAHeartOnYourOwnTile()
    {
        var game = GameWith(TempleSetup.CreateBoard(), roster: [("Guide", 3)]);
        game.Play(new Overexert());

        var result = game.Play(new Heal(game.CurrentExplorer.Id));

        Assert.True(result.Accepted);
        Assert.Equal(3, game.CurrentExplorer.Health);
        Assert.Contains(result.Events, e => e is HealthRegained);
    }

    [Fact]
    public void HealingAtFullHealthIsRefused()
    {
        var game = GameWith(TempleSetup.CreateBoard());

        Assert.False(game.Play(new Heal(game.CurrentExplorer.Id)).Accepted);
    }

    [Fact]
    public void HealingReachesOnlyYourOwnTile()
    {
        var game = GameWith(CorridorSouth(1), roster: [("Guide", 3), ("Prêtre", 5)]);

        // The priest spends a heart, walks off, and hands the turn back.
        game.Play(new EndTurn());
        game.Play(new Overexert());
        game.Play(new Move(Direction.South));

        for (var turn = 0; turn < 10 && game.CurrentExplorer.Id != game.Explorers[0].Id; turn++)
        {
            game.Play(new EndTurn());
        }

        Assert.Equal(game.Explorers[0].Id, game.CurrentExplorer.Id);
        Assert.Equal(4, game.Explorers[1].Health);
        Assert.NotEqual(game.CurrentExplorer.Cell, game.Explorers[1].Cell);
        Assert.False(game.Play(new Heal(game.Explorers[1].Id)).Accepted);
    }

    [Fact]
    public void AHealedExplorerGetsBackOnTheirFeet()
    {
        var game = GameWith(TempleSetup.CreateBoard(), roster: [("Fragile", 1), ("Prêtre", 5)]);
        game.Play(new Overexert());
        Assert.True(game.Explorers[0].IsDown);

        game.Play(new EndTurn());
        var result = game.Play(new Heal(game.Explorers[0].Id));

        Assert.True(result.Accepted);
        Assert.False(game.Explorers[0].IsDown);
        Assert.Contains(new ExplorerStoodUp(game.Explorers[0].Id), result.Events);
    }

    [Fact]
    public void AnExplorerPicksUpTheKeyLyingOnTheirTile()
    {
        var game = GameWith(TempleSetup.CreateBoard(), new TileBag([Tile(TileKind.Key, TileShape.DeadEnd)]));
        game.Play(new Explore(Direction.South));

        var result = game.Play(new PickUpItem(ItemKind.Key));

        Assert.True(result.Accepted);
        Assert.Equal(ItemKind.Key, game.CurrentExplorer.Carried);
        Assert.Empty(game.ItemsOn(game.CurrentExplorer.Cell));
    }

    [Fact]
    public void AnExplorerCarriesOneThingAtATime()
    {
        var game = GameWith(TempleSetup.CreateBoard(), new TileBag([Tile(TileKind.Key, TileShape.DeadEnd)]));
        game.Play(new Explore(Direction.South));
        game.Play(new PickUpItem(ItemKind.Key));
        game.Play(new EndTurn());
        game.Play(new DropItem());

        // Two keys on the tile now would still only let them hold one.
        Assert.Null(game.CurrentExplorer.Carried);
        Assert.Equal([ItemKind.Key], game.ItemsOn(game.CurrentExplorer.Cell));
        Assert.True(game.Play(new PickUpItem(ItemKind.Key)).Accepted);
        Assert.False(game.Play(new PickUpItem(ItemKind.Key)).Accepted);
    }

    [Fact]
    public void PickingUpWhatIsNotThereIsRefused()
    {
        var game = GameWith(TempleSetup.CreateBoard());

        Assert.False(game.Play(new PickUpItem(ItemKind.Key)).Accepted);
    }

    [Fact]
    public void DroppingNothingIsRefused()
    {
        var game = GameWith(TempleSetup.CreateBoard());

        Assert.False(game.Play(new DropItem()).Accepted);
    }
}
