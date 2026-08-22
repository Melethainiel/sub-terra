using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

public class EruptionTests
{
    private static GameState Game(Difficulty difficulty, int explorers, ulong seed = 1) =>
        new(
            Enumerable.Range(0, explorers).Select(index =>
                new Explorer(new ExplorerId(index), $"E{index}", 400, TempleSetup.EntranceCrossing)),
            TempleSetup.CreateBoard(),
            new TileBag([new TileDefinition("Normal", TileKind.Normal, TileShape.Crossroads)]),
            new Rng(seed),
            difficulty);

    [Theory]
    [InlineData(Difficulty.Beginner, 3, 27)]
    [InlineData(Difficulty.Beginner, 6, 20)]
    [InlineData(Difficulty.Normal, 4, 21)]
    [InlineData(Difficulty.Advanced, 5, 16)]
    [InlineData(Difficulty.Expert, 6, 12)]
    public void TheMarkerStartsWhereTheRulebookSaysItDoes(Difficulty difficulty, int party, int expected)
    {
        Assert.Equal(expected, EruptionTrack.StartingCount(difficulty, party));
    }

    [Fact]
    public void ASmallPartyIsReadOffTheThreeExplorerColumn()
    {
        // The rules never let you control fewer than three, so nothing below it exists.
        Assert.Equal(
            EruptionTrack.StartingCount(Difficulty.Normal, 3),
            EruptionTrack.StartingCount(Difficulty.Normal, 1));
    }

    [Fact]
    public void TheMarkerMovesOneStepPerRound()
    {
        var game = Game(Difficulty.Expert, explorers: 1);
        var start = game.EruptionCountdown;

        var result = game.Execute(new EndTurn());

        Assert.Equal(start - 1, game.EruptionCountdown);
        Assert.Contains(new EruptionAdvanced(start - 1), result.Events);
    }

    [Fact]
    public void TheMarkerStandsStillMidRound()
    {
        var game = Game(Difficulty.Expert, explorers: 2);
        var start = game.EruptionCountdown;

        game.Execute(new EndTurn());

        Assert.Equal(start, game.EruptionCountdown);
    }

    [Fact]
    public void ReachingZeroPutsTheMountainOnTheBrink()
    {
        var game = Game(Difficulty.Expert, explorers: 1);
        GameEvent? announcement = null;

        while (!game.IsVolcanoReady && !game.IsOver)
        {
            announcement = game.Execute(new EndTurn()).Events.LastOrDefault(e => e is VolcanoReady)
                ?? announcement;
        }

        Assert.True(game.IsVolcanoReady);
        Assert.Equal(0, game.EruptionCountdown);
        Assert.IsType<VolcanoReady>(announcement);
        Assert.False(game.HasErupted);
    }

    [Fact]
    public void TheFlameAfterTheCountdownEndsTheExpeditionIfTheArtefactIsStillInside()
    {
        var game = Game(Difficulty.Expert, explorers: 1);

        while (!game.IsVolcanoReady && !game.IsOver)
        {
            game.Execute(new EndTurn());
        }

        // Keep taking turns until the Peril die shows a flame.
        for (var turn = 0; turn < 400 && !game.IsOver; turn++)
        {
            game.Execute(new EndTurn());
        }

        Assert.True(game.HasErupted);
        Assert.Equal(Outcome.ForgottenForever, game.Outcome);
        Assert.True(game.IsOver);
    }
}
