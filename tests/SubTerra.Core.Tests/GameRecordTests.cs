using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;

namespace SubTerra.Core.Tests;

/// <summary>A game is its record: saved, sent to a latecomer, replayed — always the same game.</summary>
public class GameRecordTests
{
    private static readonly string[] Party = ["guide", "pretre", "combattante"];

    /// <summary>A few turns of a real game, recorded as they are played.</summary>
    private static (GameState Game, GameRecord Record) Played(ulong seed = 7)
    {
        var game = GameState.NewGame(Party.Select(id => ExplorerRoster.Find(id)!), seed);
        var record = GameRecord.Start(seed, Difficulty.Normal, Party);

        void Play(GameCommand command)
        {
            if (game.Execute(command).Accepted)
            {
                record = record.With(command);
            }

            // Whatever the table is asked along the way is answered, and recorded too.
            while (game.Pending is not null)
            {
                game.Execute(new Decide(0));
                record = record.With(new Decide(0));
            }
        }

        Play(new Reveal(Direction.South));
        Play(new Move(Direction.South));
        Play(new EndTurn());
        Play(new Overexert());
        Play(new EndTurn());
        Play(new EndTurn());
        Play(new Move(Direction.North));
        Play(new EndTurn());

        return (game, record);
    }

    [Fact]
    public void ReplayingARecordRebuildsTheSameGame()
    {
        var (game, record) = Played();

        var replayed = record.Replay();

        Assert.NotNull(replayed);
        Assert.Equal(game.Fingerprint, replayed.Fingerprint);
    }

    [Fact]
    public void ARecordSurvivesBeingWrittenAndReadBack()
    {
        var (game, record) = Played();

        var back = GameRecord.Decode(record.Encode());

        Assert.NotNull(back);
        Assert.Equal(record.Commands, back.Commands);
        Assert.Equal(game.Fingerprint, back.Replay()!.Fingerprint);
    }

    [Fact]
    public void ANewRecordReplaysIntoAFreshGame()
    {
        var record = GameRecord.Start(3, Difficulty.Expert, Party);
        var fresh = GameState.NewGame(Party.Select(id => ExplorerRoster.Find(id)!), 3, Difficulty.Expert);

        Assert.Equal(fresh.Fingerprint, record.Replay()!.Fingerprint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("subterra-record 999\nseed 1\ndifficulty Normal\nparty guide,pretre,combattante\n")]
    [InlineData("subterra-record 1\nseed -1\ndifficulty Normal\nparty guide,pretre,combattante\n")]
    [InlineData("subterra-record 1\nseed 1\ndifficulty Impossible\nparty guide,pretre,combattante\n")]
    [InlineData("subterra-record 1\nseed 1\ndifficulty Normal\nparty guide,pretre,combattante\nfly:North\n")]
    public void WhatIsNotARecordIsRefused(string text) => Assert.Null(GameRecord.Decode(text));

    [Fact]
    public void ARecordThatNoLongerPlaysDoesNotReplay()
    {
        // Reads fine, but nothing has been laid south of the Entrance to walk onto.
        var record = GameRecord.Start(1, Difficulty.Normal, Party).With(new Move(Direction.South));

        Assert.Null(record.Replay());
    }

    [Fact]
    public void AnUnknownExplorerDoesNotReplay()
    {
        Assert.Null(GameRecord.Start(1, Difficulty.Normal, ["guide", "pretre", "fantome"]).Replay());
    }
}
