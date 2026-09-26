using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>Agile, Vigilance, Survivante and Aventurière: always on, never a card.</summary>
public class PassiveAbilityTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static readonly Cell South = Start.Neighbour(Direction.South);

    private static Explorer Holding(int id, string sheetId, int? health = null)
    {
        var sheet = ExplorerRoster.Find(sheetId)!;
        return new Explorer(new ExplorerId(id), sheet.Name, health ?? sheet.MaxHealth, Start, sheet);
    }

    private static Explorer Plain(int id, string name = "Sans capacité") =>
        new(new ExplorerId(id), name, 5, Start);

    private static GameState Game(IEnumerable<Explorer> explorers, TileKind? inTheBag = null, ulong seed = 1) =>
        new(
            explorers,
            TempleSetup.CreateBoard(),
            new TileBag(inTheBag is { } kind ? [new TileDefinition($"{kind}-test", kind, TileShape.Crossroads)] : []),
            new Rng(seed));

    /// <summary>The first seed whose opening roll of the Peril die shows <paramref name="face"/>.</summary>
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

    // ── Agile ──────────────────────────────────────────────────────────────

    [Fact]
    public void TheGuideWalksOverRubble()
    {
        var game = Game([Holding(0, "guide")], TileKind.Ruins);
        game.Play(new Reveal(Direction.South));
        Assert.Contains(South, game.Rubble);

        Assert.True(game.Play(new Move(Direction.South)).Accepted);
        Assert.Equal(South, game.CurrentExplorer.Cell);
        // Walked over, not dug out.
        Assert.Contains(South, game.Rubble);
    }

    [Fact]
    public void TheGuideExploresStraightIntoRuins()
    {
        var game = Game([Holding(0, "guide")], TileKind.Ruins);

        game.Play(new Explore(Direction.South));

        Assert.Equal(South, game.CurrentExplorer.Cell);
    }

    [Fact]
    public void RubbleStillStopsEveryoneElse()
    {
        var game = Game([Plain(0)], TileKind.Ruins);

        game.Play(new Explore(Direction.South));

        Assert.Equal(Start, game.CurrentExplorer.Cell);
    }

    // ── Vigilance ──────────────────────────────────────────────────────────

    [Fact]
    public void TheGredinStepsOnSpikesWithoutRollingForThem()
    {
        var game = Game([Holding(0, "gredin")], TileKind.SpikeTrap);

        var result = game.Play(new Explore(Direction.South));

        Assert.Equal(South, game.CurrentExplorer.Cell);
        Assert.DoesNotContain(result.Events, e => e is DieRolled or TrapSprung);
    }

    [Fact]
    public void AnyoneElseDoesRollForThem()
    {
        var game = Game([Plain(0)], TileKind.SpikeTrap);

        var result = game.Play(new Explore(Direction.South));

        Assert.Contains(result.Events, e => e is DieRolled);
    }

    [Fact]
    public void JoiningTheGredinOnSpikesIsSafeToo()
    {
        var game = Game([Holding(0, "gredin"), Plain(1)], TileKind.SpikeTrap);
        game.Play(new Explore(Direction.South));
        game.Play(new EndTurn());

        for (var turn = 0; turn < 10 && game.CurrentExplorer.Id.Value != 1; turn++)
        {
            game.Play(new EndTurn());
        }

        var result = game.Play(new Move(Direction.South));

        Assert.True(result.Accepted, result.Rejection);
        Assert.DoesNotContain(result.Events, e => e is DieRolled or TrapSprung);
    }

    [Fact]
    public void ThePerilDiesTrapFaceSetsNothingOffAroundTheGredin()
    {
        // Laying the spike trap draws from the bag once; the Peril die comes next.
        ulong seed = 1;

        while (true)
        {
            var rng = new Rng(seed);
            rng.Next(1);

            if (rng.RollPeril() == PerilFace.Trap)
            {
                break;
            }

            seed++;
        }

        var game = Game([Holding(0, "gredin"), Plain(1)], TileKind.SpikeTrap, seed);
        game.Play(new Explore(Direction.South));

        var result = game.Play(new EndTurn());

        Assert.Contains(new PerilRolled(PerilFace.Trap), result.Events);
        Assert.DoesNotContain(result.Events, e => e is TrapSprung or HealthLost);
    }

    // ── Survivante ─────────────────────────────────────────────────────────

    [Fact]
    public void AStumbleGivesTheHealerAHeartBack()
    {
        var game = Game([Holding(0, "guerisseuse"), Plain(1)], seed: SeedWherePerilIs(PerilFace.Stumble));
        game.Play(new Overexert());
        Assert.Equal(2, game.Explorers[0].Health);

        var result = game.Play(new EndTurn());

        Assert.Contains(new PerilRolled(PerilFace.Stumble), result.Events);
        Assert.Contains(new AbilityUsed(new ExplorerId(0), AbilityIds.Survivante), result.Events);
        Assert.Equal(3, game.Explorers[0].Health);
    }

    [Fact]
    public void AStumbleStillCostsAnyoneElseWhoOverexerted()
    {
        var game = Game([Plain(0), Plain(1)], seed: SeedWherePerilIs(PerilFace.Stumble));
        game.Play(new Overexert());

        game.Play(new EndTurn());

        Assert.Equal(3, game.Explorers[0].Health);
    }

    // ── Aventurière ────────────────────────────────────────────────────────

    [Fact]
    public void TheArcheologistIsOfferedARerollOfHerOwnPerilDie()
    {
        var game = Game([Holding(0, "archeologue"), Plain(1)]);

        var result = game.Execute(new EndTurn());

        Assert.Contains(result.Events, e => e is PerilRolled);
        Assert.Equal(DecisionKind.Reroll, game.Pending?.Kind);
        Assert.Equal(new ExplorerId(0), game.Pending?.Chooser);
        Assert.StartsWith("Le dé de Péril montre ", game.Pending?.Prompt);
        Assert.DoesNotContain("Stumble", game.Pending?.Prompt);
    }

    [Fact]
    public void ARerollCostsAHeartAndRollsAgainUntilSheKeepsIt()
    {
        var game = Game([Holding(0, "archeologue"), Plain(1)]);
        game.Execute(new EndTurn());

        var rerolled = game.Execute(new Decide(1));

        Assert.Contains(new AbilityUsed(new ExplorerId(0), AbilityIds.Aventuriere), rerolled.Events);
        Assert.Contains(rerolled.Events, e => e is HealthLost { Amount: 1 });
        Assert.Contains(rerolled.Events, e => e is PerilRolled);
        Assert.Equal(DecisionKind.Reroll, game.Pending?.Kind);

        game.Execute(new Decide(0));

        Assert.NotEqual(DecisionKind.Reroll, game.Pending?.Kind);
        // One heart for the one reroll; nothing on this bare board hurts her after.
        Assert.Equal(4, game.Explorers[0].Health);
    }

    [Fact]
    public void SheIsNotAskedAgainOnceTheRerollPutsHerDown()
    {
        var game = Game([Holding(0, "archeologue", health: 1), Plain(1)]);
        game.Execute(new EndTurn());

        game.Execute(new Decide(1));

        Assert.True(game.Explorers[0].IsDown);
        Assert.NotEqual(DecisionKind.Reroll, game.Pending?.Kind);
    }

    [Fact]
    public void SheIsNotOfferedSomeoneElsesDie()
    {
        var game = Game([Plain(0), Holding(1, "archeologue")]);

        game.Execute(new EndTurn());

        Assert.NotEqual(DecisionKind.Reroll, game.Pending?.Kind);
    }
}
