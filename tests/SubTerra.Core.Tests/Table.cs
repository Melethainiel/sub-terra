using SubTerra.Core.Game;

namespace SubTerra.Core.Tests;

/// <summary>
/// Drives a game the way a table of players would: a command, then whatever
/// arbitrations it raises. A test that does not care which way a tie falls takes the
/// first option on offer; one that does calls <see cref="GameState.Execute"/> itself
/// and answers with its own <see cref="Decide"/>.
/// </summary>
internal static class Table
{
    public static CommandResult Play(this GameState game, GameCommand command)
    {
        var result = game.Execute(command);

        if (!result.Accepted)
        {
            return result;
        }

        var events = new List<GameEvent>(result.Events);

        while (game.Pending is not null)
        {
            events.AddRange(game.Execute(new Decide(0)).Events);
        }

        return new CommandResult(true, null, events);
    }
}
