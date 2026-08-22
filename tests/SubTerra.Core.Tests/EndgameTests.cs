using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class EndgameTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static TileDefinition Tile(TileKind kind, TileShape shape = TileShape.Crossroads) =>
        new($"{kind}-test", kind, shape);

    private static GameState Game(IEnumerable<TileDefinition> bag, ulong seed = 1,
        params (string Name, int Health)[] roster)
    {
        var sheets = roster.Length == 0 ? [("Guide", 20)] : roster;
        var explorers = sheets.Select((sheet, index) =>
            new Explorer(new ExplorerId(index), sheet.Name, sheet.Health, Start));

        return new GameState(explorers, TempleSetup.CreateBoard(), new TileBag(bag), new Rng(seed));
    }

    [Fact]
    public void TheSanctuaryAppearsWhenTheBagRunsDry()
    {
        var game = Game([Tile(TileKind.Normal)]);

        var result = game.Execute(new Reveal(Direction.South, Rotation: 0));

        Assert.Contains(result.Events, e => e is BagEmptied);
        var found = Assert.Single(result.Events.OfType<SanctuaryFound>());

        // Laid beyond the deepest tile on the table, hall then vault.
        Assert.Equal(new Cell(Start.Column, 2), found.Hall);
        Assert.Equal(new Cell(Start.Column, 3), found.Vault);
        Assert.Equal(found.Hall, game.SanctuaryHall);
        Assert.Equal(found.Vault, game.SanctuaryVault);
    }

    [Fact]
    public void TheSanctuaryIsLaidOnlyOnce()
    {
        var game = Game([Tile(TileKind.Normal), Tile(TileKind.Normal)], roster: [("A", 20), ("B", 20)]);

        game.Execute(new Explore(Direction.South, Rotation: 0));
        game.Execute(new Reveal(Direction.South, Rotation: 0));
        var hall = game.SanctuaryHall;

        game.Execute(new EndTurn());

        Assert.Equal(hall, game.SanctuaryHall);
        Assert.NotNull(hall);
    }

    /// <summary>Runs a command and says which one failed if it does.</summary>
    private static void Do(GameState game, GameCommand command)
    {
        var result = game.Execute(command);
        Assert.True(result.Accepted, $"{command} refusé : {result.Rejection}");
    }

    /// <summary>
    /// A whole miniature expedition, scripted move by move: uncover three keys, empty
    /// the bag, ferry each key into the sanctuary hall, lift the Artefact and walk it
    /// back out. The explorer is given far more hearts than the temple can take, so
    /// that the test measures the rules and not the dice.
    /// </summary>
    [Fact]
    public void ThreeKeysFreeTheArtefactAndCarryingItOutWinsTheGame()
    {
        var game = Game(
            [Tile(TileKind.Key), Tile(TileKind.Key), Tile(TileKind.Key)],
            roster: [("Increvable", 400)]);

        var corridor = new Cell(Start.Column, 1);
        var west = new Cell(Start.Column - 1, 1);
        var east = new Cell(Start.Column + 1, 1);

        // Open a row of three key pockets across the mouth of the temple.
        Do(game, new Explore(Direction.South, Rotation: 0));
        Do(game, new Reveal(Direction.East, Rotation: 0));
        Do(game, new EndTurn());

        Do(game, new Reveal(Direction.West, Rotation: 0));
        Assert.True(game.Bag.IsEmpty);

        // Emptying the bag lays the sanctuary at the far end of what is on the table.
        Assert.NotNull(game.SanctuaryHall);
        Assert.NotNull(game.SanctuaryVault);
        var hall = game.SanctuaryHall.Value;
        var vault = game.SanctuaryVault.Value;
        Assert.Equal(new Cell(west.Column, 2), hall);
        Assert.Equal(new Cell(west.Column, 3), vault);

        Do(game, new PickUpItem(ItemKind.Key));
        Do(game, new EndTurn());

        // First key.
        Do(game, new Move(Direction.West));
        Do(game, new Move(Direction.South));
        Do(game, new EndTurn());
        Do(game, new DropItem());
        Assert.Equal(1, game.KeysDeposited);
        Do(game, new Move(Direction.North));
        Do(game, new EndTurn());

        // Second key, waiting on the tile the hall opens onto.
        Do(game, new PickUpItem(ItemKind.Key));
        Do(game, new Move(Direction.South));
        Do(game, new EndTurn());
        Do(game, new DropItem());
        Assert.Equal(2, game.KeysDeposited);
        Do(game, new Move(Direction.North));
        Do(game, new EndTurn());

        // Third key, two tiles east.
        Do(game, new Move(Direction.East));
        Do(game, new Move(Direction.East));
        Assert.Equal(east, game.CurrentExplorer.Cell);
        Do(game, new EndTurn());
        Do(game, new PickUpItem(ItemKind.Key));
        Do(game, new Move(Direction.West));
        Do(game, new EndTurn());
        Do(game, new Move(Direction.West));
        Do(game, new Move(Direction.South));
        Do(game, new EndTurn());

        var unlocked = game.Execute(new DropItem());
        Assert.True(unlocked.Accepted);
        Assert.Equal(GameState.KeysToUnlock, game.KeysDeposited);
        Assert.Contains(new ArtefactRevealed(vault), unlocked.Events);
        Assert.Equal([ItemKind.Artefact], game.ItemsOn(vault));
        Assert.False(game.IsCursed);

        // Down into the vault for it.
        Do(game, new Move(Direction.South));
        Do(game, new EndTurn());
        var lifted = game.Execute(new PickUpItem(ItemKind.Artefact));
        Assert.True(lifted.Accepted);
        Assert.Contains(new CurseFell(), lifted.Events);
        Assert.True(game.IsCursed);

        // And back out the way they came, with the mountain now moving twice as fast.
        Do(game, new Move(Direction.North));
        var beforeRound = game.EruptionCountdown;
        Do(game, new EndTurn());
        Assert.Equal(beforeRound - 2, game.EruptionCountdown);
        Do(game, new Move(Direction.North));
        Do(game, new Move(Direction.East));
        Do(game, new EndTurn());
        Do(game, new Move(Direction.North));

        var escape = game.Execute(new Move(Direction.North));

        Assert.Contains(escape.Events, e => e is ExplorerEscaped { WithArtefact: true });
        Assert.Contains(new GameEnded(Outcome.Legendary), escape.Events);
        Assert.True(game.IsOver);
    }

    [Fact]
    public void WalkingOutWithoutTheArtefactLosesTheGame()
    {
        var game = Game([Tile(TileKind.Normal)]);

        var result = game.Execute(new Move(Direction.North));

        Assert.Contains(result.Events, e => e is ExplorerEscaped { WithArtefact: false });
        Assert.Contains(new GameEnded(Outcome.ForgottenForever), result.Events);
        Assert.True(game.Explorers[0].HasEscaped);
    }

    [Fact]
    public void AnExplorerWhoGotOutTakesNoFurtherPart()
    {
        var game = Game([Tile(TileKind.Normal)], roster: [("Sortie", 20), ("Restée", 20)]);

        game.Execute(new Move(Direction.North));
        Assert.True(game.Explorers[0].HasEscaped);
        Assert.False(game.IsOver);

        game.Execute(new EndTurn());
        game.Execute(new EndTurn());

        Assert.Equal(new ExplorerId(0), game.CurrentExplorer.Id);
        Assert.Equal(0, game.ActionPoints);
        Assert.False(game.Execute(new Move(Direction.South)).Accepted);
    }
}
