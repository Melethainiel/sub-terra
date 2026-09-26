using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>Anéantir, Se préparer, Consolider and Ordonner.</summary>
public class OnTheSpotAbilityTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static readonly Cell South = Start.Neighbour(Direction.South);

    private static Explorer Holding(int id, string sheetId)
    {
        var sheet = ExplorerRoster.Find(sheetId)!;
        return new Explorer(new ExplorerId(id), sheet.Name, sheet.MaxHealth, Start, sheet);
    }

    private static Explorer Plain(int id, int health = 5) => new(new ExplorerId(id), $"Autre {id}", health, Start);

    private static GameState Game(Explorer[] explorers, TileKind? inTheBag = null, ulong seed = 1) =>
        new(
            explorers,
            TempleSetup.CreateBoard(),
            new TileBag(inTheBag is { } kind ? [new TileDefinition($"{kind}-test", kind, TileShape.Crossroads)] : []),
            new Rng(seed));

    private static ulong SeedWherePerilIs(PerilFace face)
    {
        for (ulong seed = 1; seed < 5_000; seed++)
        {
            if (new Rng(seed).RollPeril() == face)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Aucune graine ne convient.");
    }

    // ── Anéantir ───────────────────────────────────────────────────────────

    [Fact]
    public void AneantirEliminatesAnEnemyOnHerTileWithoutARoll()
    {
        var game = Game([Holding(0, "combattante")], TileKind.Guardian);
        game.Play(new Reveal(Direction.South));
        game.Play(new Move(Direction.South));
        game.Play(new Overexert());
        Assert.Equal([South], game.Guardians);

        var result = game.Play(new UseAbility(AbilityIds.Aneantir));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Empty(game.Guardians);
        Assert.Contains(new GuardianEliminated(South), result.Events);
        Assert.DoesNotContain(result.Events, e => e is DieRolled);
    }

    [Fact]
    public void AneantirWithNoEnemyHereIsRefused()
    {
        var game = Game([Holding(0, "combattante")]);

        Assert.Contains("Aucun ennemi", game.Play(new UseAbility(AbilityIds.Aneantir)).Rejection);
    }

    // ── Se préparer ────────────────────────────────────────────────────────

    [Fact]
    public void BehindTheBouclierNoHeartIsLost()
    {
        var game = Game([Holding(0, "combattante"), Plain(1)], seed: SeedWherePerilIs(PerilFace.Stumble));
        game.Play(new Overexert());
        game.Play(new UseAbility(AbilityIds.SePreparer));
        Assert.True(game.Explorers[0].IsShielded);

        var result = game.Play(new EndTurn());

        // She overexerted, so Trébucher should have cost her a heart.
        Assert.Contains(new PerilRolled(PerilFace.Stumble), result.Events);
        Assert.Equal(6, game.Explorers[0].Health);
    }

    [Fact]
    public void OverexertingStillCostsAHeartBehindTheBouclier()
    {
        var game = Game([Holding(0, "combattante")]);
        game.Play(new UseAbility(AbilityIds.SePreparer));

        game.Play(new Overexert());

        Assert.Equal(6, game.Explorers[0].Health);
    }

    [Fact]
    public void TheBouclierComesDownOnHerNextTurnWhichCannotRaiseItAgain()
    {
        var game = Game([Holding(0, "combattante")]);
        game.Play(new UseAbility(AbilityIds.SePreparer));

        var next = game.Play(new EndTurn());

        Assert.Contains(new ShieldLowered(new ExplorerId(0)), next.Events);
        Assert.False(game.Explorers[0].IsShielded);
        Assert.Contains("indisponible", game.Play(new UseAbility(AbilityIds.SePreparer)).Rejection);

        game.Play(new EndTurn());

        Assert.True(game.Play(new UseAbility(AbilityIds.SePreparer)).Accepted);
    }

    // ── Consolider ─────────────────────────────────────────────────────────

    [Fact]
    public void ConsoliderTurnsHisTileIntoANormalOneForGood()
    {
        var game = Game([Holding(0, "contremaitre")], TileKind.Lava);
        game.Play(new Explore(Direction.South));
        Assert.Equal(TileKind.Lava, game.Board.TileAt(South)!.Kind);

        var result = game.Play(new UseAbility(AbilityIds.Consolider));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Contains(new TileConsolidated(South), result.Events);
        Assert.Equal(TileKind.Normal, game.Board.TileAt(South)!.Kind);
        // The printed tile is still what it was; only its rules have changed.
        Assert.Equal(TileKind.Lava, game.Board.TileAt(South)!.Definition.Kind);
        Assert.Equal(3, game.UsesLeft(game.CurrentExplorer.Id, AbilityIds.Consolider));
    }

    [Fact]
    public void ANormalTileHasNothingToConsolidate()
    {
        var game = Game([Holding(0, "contremaitre")], TileKind.Normal);
        game.Play(new Explore(Direction.South));

        Assert.Contains("Rien à consolider", game.Play(new UseAbility(AbilityIds.Consolider)).Rejection);
        Assert.Equal(4, game.UsesLeft(game.CurrentExplorer.Id, AbilityIds.Consolider));
    }

    // ── Ordonner ───────────────────────────────────────────────────────────

    [Fact]
    public void OrdonnerSendsSomeoneElseAStep()
    {
        var game = Game([Holding(0, "aristocrate"), Plain(1)]);

        var result = game.Play(new UseAbility(AbilityIds.Ordonner, new ExplorerId(1), Direction.West));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(Start.Neighbour(Direction.West), game.Explorers[1].Cell);
        Assert.Equal(Start, game.CurrentExplorer.Cell);
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
    }

    [Fact]
    public void OrderingSomeoneOutOfTheTempleDoesNotEndHerTurn()
    {
        var game = Game([Holding(0, "aristocrate"), Plain(1)]);

        game.Play(new UseAbility(AbilityIds.Ordonner, new ExplorerId(1), Direction.North));

        Assert.True(game.Explorers[1].HasEscaped);
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
    }

    [Fact]
    public void OrdonnerIsForSomeoneElseStanding()
    {
        var game = Game([Holding(0, "aristocrate"), Plain(1, health: 1)]);

        Assert.Contains("aux autres", game.Play(new UseAbility(AbilityIds.Ordonner, new ExplorerId(0), Direction.West)).Rejection);

        // Put the other one down: their own turn, overexerting their last heart.
        game.Play(new EndTurn());
        game.Play(new Overexert());
        game.Play(new EndTurn());
        Assert.True(game.Explorers[1].IsDown);
        Assert.Equal(new ExplorerId(0), game.CurrentExplorer.Id);

        Assert.Contains("ne peut pas", game.Play(new UseAbility(AbilityIds.Ordonner, new ExplorerId(1), Direction.West)).Rejection);
        Assert.Empty(game.OrderTargets());
    }

    [Fact]
    public void OrdonnerStillFollowsTheWalls()
    {
        var game = Game([Holding(0, "aristocrate"), Plain(1)]);

        // Nothing has been laid south of the Entrance yet.
        Assert.False(game.Play(new UseAbility(AbilityIds.Ordonner, new ExplorerId(1), Direction.South)).Accepted);
        Assert.Equal([new ExplorerId(1)], game.OrderTargets());
        Assert.DoesNotContain(Direction.South, game.StepsOf(new ExplorerId(1)));
    }
}
