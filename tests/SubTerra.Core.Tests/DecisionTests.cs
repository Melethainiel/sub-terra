using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>
/// The rulebook settles a dozen ties with "le Chef d'Expédition choisit". The engine
/// stops there and asks rather than picking quietly, and these say so.
/// </summary>
public class DecisionTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static ulong SeedFor(PerilFace face)
    {
        for (ulong seed = 1; seed < 5_000; seed++)
        {
            if (new Rng(seed).RollPeril() == face)
            {
                return seed;
            }
        }

        throw new InvalidOperationException($"Aucune graine ne donne {face}.");
    }

    /// <summary>A guardian pocket next door, and two explorers standing on the crossing.</summary>
    private static (GameState Game, Cell Pocket) Crowded(ulong seed = 1) =>
        (new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start),
             new Explorer(new ExplorerId(1), "Prêtre", 5, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([new TileDefinition("Guardian-test", TileKind.Guardian, TileShape.Crossroads)]),
            new Rng(seed)),
        new Cell(Start.Column, 1));

    /// <summary>Walks a guardian onto the crowd, leaving it about to strike.</summary>
    private static GameState AboutToStrike()
    {
        var (game, _) = Crowded();
        game.Execute(new Reveal(Direction.South, Rotation: 0));
        game.Execute(new EndTurn());
        game.Execute(new EndTurn());

        return game;
    }

    [Fact]
    public void AGuardianSpoiltForChoiceAsksTheLeader()
    {
        var game = AboutToStrike();

        var decision = game.Pending;

        Assert.NotNull(decision);
        Assert.Equal(DecisionKind.GuardianTarget, decision.Kind);
        Assert.Equal(game.Leader.Id, decision.Chooser);
        Assert.Equal(["Guide", "Prêtre"], decision.Options.Select(option => option.Label));
    }

    [Fact]
    public void TheOneTheLeaderNamesIsTheOneWhoBleeds()
    {
        var game = AboutToStrike();

        var struck = game.Execute(new Decide(1));

        Assert.True(struck.Accepted);
        Assert.Null(game.Pending);
        Assert.Contains(struck.Events, e => e is GuardianAttacked attack && attack.Explorer == new ExplorerId(1));
        Assert.Equal(7, game.Explorers[0].Health);
        Assert.Equal(4, game.Explorers[1].Health);
    }

    [Fact]
    public void TheDecisionIsPartOfTheRecord()
    {
        var game = AboutToStrike();
        var made = game.Execute(new Decide(1)).Events.OfType<DecisionMade>().Single();

        Assert.Equal(DecisionKind.GuardianTarget, made.Decision.Kind);
        Assert.Equal("Prêtre", made.Chosen.Label);
    }

    [Fact]
    public void NothingElseHappensUntilTheTieIsSettled()
    {
        var game = AboutToStrike();

        var blocked = game.Execute(new Move(Direction.South));

        Assert.False(blocked.Accepted);
        Assert.Contains("Guide", blocked.Rejection);
        Assert.NotNull(game.Pending);
    }

    [Fact]
    public void AnOptionThatIsNotOnTheListIsRefused()
    {
        var game = AboutToStrike();

        Assert.False(game.Execute(new Decide(7)).Accepted);
        Assert.False(game.Execute(new Decide(-1)).Accepted);
        Assert.NotNull(game.Pending);
    }

    [Fact]
    public void ThereIsNothingToDecideWhenNothingIsPending()
    {
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([]),
            new Rng(1));

        Assert.False(game.Execute(new Decide(0)).Accepted);
    }

    [Fact]
    public void ASingleVictimIsNoChoiceAtAll()
    {
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([new TileDefinition("Guardian-test", TileKind.Guardian, TileShape.Crossroads)]),
            new Rng(1));

        game.Execute(new Reveal(Direction.South, Rotation: 0));
        var round = game.Execute(new EndTurn());

        Assert.Null(game.Pending);
        Assert.DoesNotContain(round.Events, e => e is DecisionRequired);
        Assert.Contains(round.Events, e => e is GuardianAttacked);
    }

    [Fact]
    public void WakingAGuardianIsTheActiveExplorersCallNotTheLeaders()
    {
        // The two lateral arms each end in a guardian pocket, both three tiles from
        // the crossing: a tie by construction.
        var game = new GameState(
            [new Explorer(new ExplorerId(0), "Guide", 7, Start),
             new Explorer(new ExplorerId(1), "Prêtre", 5, Start)],
            TempleSetup.CreateBoard(),
            new TileBag([]),
            new Rng(SeedFor(PerilFace.WakeGuardian)));

        game.Execute(new EndTurn());

        var decision = game.Pending;

        Assert.NotNull(decision);
        Assert.Equal(DecisionKind.GuardianAwakening, decision.Kind);
        Assert.Equal(new ExplorerId(0), decision.Chooser);
        Assert.Equal(2, decision.Options.Count);

        game.Execute(new Decide(1));

        Assert.Equal(decision.Options[1].Cell, Assert.Single(game.Guardians));
    }
}
