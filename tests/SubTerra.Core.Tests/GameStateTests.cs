using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class GameStateTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static GameState Game(params (string Name, int MaxHealth)[] roster) =>
        GameState.NewGame(roster.Length == 0 ? [("Guide", 3)] : roster, seed: 1);

    /// <summary>A game whose bag holds exactly the tiles named, drawn in that order.</summary>
    private static GameState GameWithBag(params TileDefinition[] tiles)
    {
        var board = TempleSetup.CreateBoard();
        var explorers = new[] { new Explorer(new ExplorerId(0), "Guide", 3, Start) };
        return new GameState(explorers, board, new TileBag(tiles), new Rng(1));
    }

    private static TileDefinition Tile(TileKind kind, TileShape shape) =>
        new($"{kind}-test", kind, shape);

    [Fact]
    public void EveryoneStartsOnTheEntranceCrossingWithTwoActions()
    {
        var game = Game(("Guide", 3), ("Prêtre", 5));

        Assert.All(game.Explorers, explorer => Assert.Equal(Start, explorer.Cell));
        Assert.Equal(GameState.ActionsPerTurn, game.ActionPoints);
        Assert.Equal("Guide", game.CurrentExplorer.Name);
    }

    [Fact]
    public void MovingOntoAConnectedTileSpendsAnAction()
    {
        var game = Game();

        var result = game.Play(new Move(Direction.West));

        Assert.True(result.Accepted);
        Assert.Equal(Start.Neighbour(Direction.West), game.CurrentExplorer.Cell);
        Assert.Equal(1, game.ActionPoints);
        Assert.Contains(result.Events, e => e is ExplorerMoved);
    }

    [Fact]
    public void MovingIntoBareRockIsRefused()
    {
        var game = Game();

        // Nothing has been revealed south of the entrance yet.
        var result = game.Play(new Move(Direction.South));

        Assert.False(result.Accepted);
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn, game.ActionPoints);
    }

    [Fact]
    public void AnExplorerCannotActWithoutActionPoints()
    {
        var game = Game();
        game.Play(new Move(Direction.West));
        game.Play(new Move(Direction.East));

        Assert.Equal(0, game.ActionPoints);
        Assert.False(game.Play(new Move(Direction.West)).Accepted);
    }

    [Fact]
    public void RevealingLaysTheDrawnTileAndSpendsAnAction()
    {
        var game = GameWithBag(Tile(TileKind.Normal, TileShape.Junction));

        var result = game.Play(new Reveal(Direction.South, Rotation: 0));

        Assert.True(result.Accepted);
        Assert.True(game.Board.IsOccupied(Start.Neighbour(Direction.South)));
        Assert.Equal(1, game.ActionPoints);
        Assert.Contains(result.Events, e => e is TileRevealed);
    }

    [Fact]
    public void ATileThatWouldNotConnectGoesStraightBackInTheBag()
    {
        var game = GameWithBag(Tile(TileKind.Normal, TileShape.DeadEnd));

        // Turned away from the entrance, its single opening faces south.
        var result = game.Play(new Reveal(Direction.South, Rotation: 2));

        Assert.False(result.Accepted);
        Assert.Equal(1, game.Bag.Count);
        Assert.Equal(GameState.ActionsPerTurn, game.ActionPoints);
    }

    [Fact]
    public void AKeyTileBringsItsKeyWithIt()
    {
        var game = GameWithBag(Tile(TileKind.Key, TileShape.DeadEnd));
        var target = Start.Neighbour(Direction.South);

        // Its one opening faces north, back towards the entrance it is revealed from.
        var result = game.Play(new Reveal(Direction.South, Rotation: 0));

        Assert.True(result.Accepted);
        Assert.Contains(new ItemAppeared(target, ItemKind.Key), result.Events);
        Assert.Equal([ItemKind.Key], game.ItemsOn(target));
    }

    [Fact]
    public void AGuardianTileWakesAGuardian()
    {
        var game = GameWithBag(Tile(TileKind.Guardian, TileShape.Corner));
        var target = Start.Neighbour(Direction.South);

        var result = game.Play(new Reveal(Direction.South, Rotation: 0));

        Assert.True(result.Accepted);
        Assert.Contains(new GuardianAppeared(target), result.Events);
        Assert.Equal([target], game.Guardians);
    }

    [Fact]
    public void EmptyingTheBagIsAnnouncedOnce()
    {
        var game = GameWithBag(Tile(TileKind.Normal, TileShape.Junction));

        var result = game.Play(new Reveal(Direction.South, Rotation: 0));

        Assert.Contains(result.Events, e => e is BagEmptied);
        Assert.False(game.Play(new Reveal(Direction.South, Rotation: 0)).Accepted);
    }

    [Fact]
    public void OverexertingBuysAnActionWithAHeart()
    {
        var game = Game();

        var result = game.Play(new Overexert());

        Assert.True(result.Accepted);
        Assert.Equal(3, game.ActionPoints);
        Assert.Equal(2, game.CurrentExplorer.Health);
        Assert.Contains(result.Events, e => e is HealthLost);
    }

    [Fact]
    public void AnExplorerOverexertsOnlyOncePerTurn()
    {
        var game = Game();
        game.Play(new Overexert());

        Assert.False(game.Play(new Overexert()).Accepted);
    }

    [Fact]
    public void OverexertingIsAllowedAgainNextTurn()
    {
        var game = Game(("Prêtre", 5), ("Guide", 3));
        game.Play(new Overexert());
        game.Play(new EndTurn());
        PassTo(game, "Prêtre");

        Assert.True(game.Play(new Overexert()).Accepted);
    }

    [Fact]
    public void RunningOutOfHeartsPutsAnExplorerDownAndEndsTheirTurn()
    {
        // A companion still standing, so this tests the turn ending and not the game.
        var game = Game(("Fragile", 1), ("Prêtre", 5));

        var result = game.Play(new Overexert());

        Assert.True(game.CurrentExplorer.IsDown);
        Assert.Equal(0, game.ActionPoints);
        Assert.Contains(result.Events, e => e is ExplorerWentDown);
    }

    [Fact]
    public void ADownedExplorerGetsOneActionAndCanOnlyCrawl()
    {
        var game = Game(("Fragile", 1), ("Prêtre", 5));
        game.Play(new Overexert());
        game.Play(new EndTurn());
        PassTo(game, "Fragile");

        Assert.True(game.CurrentExplorer.IsDown);
        Assert.Equal(1, game.ActionPoints);
        Assert.False(game.Play(new Overexert()).Accepted);
        Assert.False(game.Play(new Reveal(Direction.South, 0)).Accepted);
        Assert.True(game.Play(new Move(Direction.West)).Accepted);
    }

    [Fact]
    public void TurnsGoRoundAndTheRoundEndsWhenEveryoneHasPlayed()
    {
        var game = Game(("Guide", 3), ("Prêtre", 5));
        Assert.Equal("Guide", game.Leader.Name);

        var first = game.Play(new EndTurn());
        Assert.Equal("Prêtre", game.CurrentExplorer.Name);
        Assert.DoesNotContain(first.Events, e => e is RoundEnded);

        var second = game.Play(new EndTurn());
        Assert.Contains(new RoundEnded(1), second.Events);
        Assert.Equal(1, game.Round);
    }

    [Fact]
    public void TheMedallionStaysWhereItWasDealtAndEveryRoundOpensOnIt()
    {
        var game = Game(("Guide", 3), ("Prêtre", 5), ("Sapeur", 5));

        for (var round = 0; round < 3; round++)
        {
            foreach (var _ in game.Explorers)
            {
                game.Play(new EndTurn());
            }

            Assert.Equal("Guide", game.Leader.Name);
            Assert.Equal("Guide", game.CurrentExplorer.Name);
        }

        Assert.Equal(3, game.Round);
    }

    /// <summary>Hands the turn on until the named explorer is playing again.</summary>
    private static void PassTo(GameState game, string name)
    {
        for (var turn = 0; turn < 20 && game.CurrentExplorer.Name != name && !game.IsOver; turn++)
        {
            game.Play(new EndTurn());
        }

        Assert.Equal(name, game.CurrentExplorer.Name);
    }

    [Fact]
    public void ABridgeTakesOneExplorerAtATime()
    {
        var game = GameWithBag(Tile(TileKind.Bridge, TileShape.Corridor));
        var bridge = Start.Neighbour(Direction.South);

        game.Play(new Reveal(Direction.South, Rotation: 0));
        Assert.True(game.Play(new Move(Direction.South)).Accepted);
        Assert.Equal(bridge, game.CurrentExplorer.Cell);

        // A second explorer standing at the entrance may not follow them on.
        var crowd = new GameState(
            [new Explorer(new ExplorerId(0), "A", 3, bridge), new Explorer(new ExplorerId(1), "B", 3, Start)],
            game.Board,
            game.Bag,
            new Rng(1));
        crowd.Play(new EndTurn());

        var blocked = crowd.Play(new Move(Direction.South));

        Assert.False(blocked.Accepted);
        Assert.Contains("Pont", blocked.Rejection);
    }
}
