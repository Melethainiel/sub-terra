using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;

namespace SubTerra.Core.Tests;

/// <summary>
/// The fingerprint is what tells two networked peers they are still playing the same
/// game. It has to depend on the state, and it has to be the same everywhere.
/// </summary>
public class FingerprintTests
{
    private static GameState Game() =>
        GameState.NewGame(
            new[] { "guide", "gredin", "pretre" }.Select(id => ExplorerRoster.Find(id)!),
            seed: 7);

    [Fact]
    public void TheSameSeedAndTheSameCommandsGiveTheSameNumber()
    {
        var here = Game();
        var there = Game();

        foreach (var command in new GameCommand[] { new Reveal(Direction.South), new EndTurn(), new EndTurn() })
        {
            here.Execute(command);
            there.Execute(command);
        }

        Assert.Equal(here.Fingerprint, there.Fingerprint);
    }

    [Fact]
    public void ADifferentCommandGivesADifferentNumber()
    {
        var here = Game();
        var there = Game();

        here.Execute(new Reveal(Direction.South));
        there.Execute(new EndTurn());

        Assert.NotEqual(here.Fingerprint, there.Fingerprint);
    }

    [Fact]
    public void TheNumberIsTheSameInEveryProcess()
    {
        // Written down on purpose: HashCode draws a fresh seed per process, and a
        // fingerprint that did the same would agree with nobody. If the state grows a
        // field this number moves — check the change is deliberate, then update it.
        Assert.Equal(-7181684646216004230L, Game().Fingerprint);
    }
}
