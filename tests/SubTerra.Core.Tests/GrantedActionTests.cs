using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>Illuminer, Sprinter and Excaver: a common action, again or cheaper.</summary>
public class GrantedActionTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    /// <summary>A crossroads laid just south of the Entrance: from it, three ways lie open.</summary>
    private static readonly Cell Hub = Start.Neighbour(Direction.South);

    private static TileDefinition Tile(TileKind kind) => new($"{kind}-test", kind, TileShape.Crossroads);

    private static GameState Game(string sheetId, Cell at, params TileKind[] bag) =>
        Game(sheetId, at, [], bag);

    private static GameState Game(string sheetId, Cell at, Explorer[] others, params TileKind[] bag)
    {
        var board = TempleSetup.CreateBoard();
        board.PlaceFixed(Hub, new PlacedTile(Tile(TileKind.Normal)));

        var sheet = ExplorerRoster.Find(sheetId)!;

        return new GameState(
            [new Explorer(new ExplorerId(0), sheet.Name, sheet.MaxHealth, at, sheet), .. others],
            board,
            new TileBag([.. bag.Select(Tile)]),
            new Rng(1));
    }

    [Fact]
    public void IlluminerRevealsTwiceForOneAction()
    {
        var game = Game("guide", Hub, TileKind.Normal, TileKind.Normal);

        Assert.True(game.Play(new UseAbility(AbilityIds.Illuminer)).Accepted);
        Assert.Equal(new GrantedActions(GrantedAction.Reveal, 2), game.Granted);

        Assert.True(game.Play(new Reveal(Direction.East)).Accepted);
        Assert.True(game.Play(new Reveal(Direction.West)).Accepted);

        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
        Assert.Null(game.Granted);
        Assert.True(game.Board.IsOccupied(Hub.Neighbour(Direction.East)));
        Assert.True(game.Board.IsOccupied(Hub.Neighbour(Direction.West)));
    }

    [Fact]
    public void PlayingAnythingElseLetsWhatIsLeftGo()
    {
        var game = Game("guide", Hub, TileKind.Normal, TileKind.Normal);
        game.Play(new UseAbility(AbilityIds.Illuminer));
        game.Play(new Reveal(Direction.East));

        game.Play(new Move(Direction.North));

        Assert.Null(game.Granted);
        Assert.Equal(0, game.ActionPoints);
    }

    [Fact]
    public void ARefusedCommandLetsNothingGo()
    {
        var game = Game("guide", Hub, TileKind.Normal);
        game.Play(new UseAbility(AbilityIds.Illuminer));

        Assert.False(game.Play(new Dig(Hub)).Accepted);

        Assert.Equal(new GrantedActions(GrantedAction.Reveal, 2), game.Granted);
    }

    [Fact]
    public void EndingTheTurnLetsWhatIsLeftGo()
    {
        var game = Game("guide", Hub, [new Explorer(new ExplorerId(1), "Autre", 5, Start)], TileKind.Normal);

        game.Play(new UseAbility(AbilityIds.Illuminer));
        game.Play(new EndTurn());

        Assert.Null(game.Granted);
    }

    [Fact]
    public void IlluminerWithNothingToRevealIsRefused()
    {
        var game = Game("guide", Hub);

        var result = game.Play(new UseAbility(AbilityIds.Illuminer));

        Assert.Contains("Rien à révéler", result.Rejection);
        Assert.Equal(GameState.ActionsPerTurn, game.ActionPoints);
    }

    [Fact]
    public void SprinterMovesTwiceForOneAction()
    {
        var game = Game("gredin", Start);

        game.Play(new UseAbility(AbilityIds.Sprinter));
        Assert.True(game.Play(new Move(Direction.South)).Accepted);
        Assert.True(game.Play(new Move(Direction.North)).Accepted);

        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
    }

    [Fact]
    public void ExcaverDigsForOneActionInsteadOfTwo()
    {
        var game = Game("contremaitre", Hub, TileKind.Ruins);
        game.Play(new Reveal(Direction.East));
        var ruins = Hub.Neighbour(Direction.East);
        Assert.Contains(ruins, game.Rubble);
        Assert.Equal(1, game.ActionPoints);

        Assert.True(game.Play(new UseAbility(AbilityIds.Excaver)).Accepted);
        var dug = game.Play(new Dig(ruins));

        Assert.True(dug.Accepted, dug.Rejection);
        Assert.DoesNotContain(ruins, game.Rubble);
    }

    [Fact]
    public void OnlyTheActionThatWasBoughtIsFree()
    {
        var game = Game("gredin", Start, TileKind.Normal);
        game.Play(new UseAbility(AbilityIds.Sprinter));

        // A Reveal is not a move: it is paid for, and it lets the sprint go.
        Assert.True(game.Play(new Move(Direction.South)).Accepted);
        Assert.True(game.Play(new Reveal(Direction.East)).Accepted);

        Assert.Null(game.Granted);
        Assert.Equal(0, game.ActionPoints);
    }
}
