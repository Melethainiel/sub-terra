namespace SubTerra.Core.Game;

/// <summary>
/// The outcome of a command. A rejection carries a reason rather than throwing:
/// players ask for illegal things all the time, and that is not exceptional.
/// </summary>
public sealed record CommandResult(bool Accepted, string? Rejection, IReadOnlyList<GameEvent> Events)
{
    public static CommandResult Reject(string reason) => new(false, reason, []);

    public static CommandResult Accept(params GameEvent[] events) => new(true, null, events);
}
