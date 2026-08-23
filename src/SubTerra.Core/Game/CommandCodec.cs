using System.Globalization;
using SubTerra.Core.Board;

namespace SubTerra.Core.Game;

/// <summary>
/// Commands as short lines of text, for the wire and for a replay file. A game is its
/// seed and its list of commands, so this is also the whole of a save.
/// </summary>
/// <remarks>
/// Text rather than binary on purpose: a desync is read with the naked eye, and a
/// replay can be edited by hand while the rules are still moving.
/// </remarks>
public static class CommandCodec
{
    public static string Encode(GameCommand command) => command switch
    {
        Move move => $"move:{move.Direction}",
        Run run => $"run:{string.Join(',', run.Steps)}",
        Reveal reveal => $"reveal:{reveal.Direction}:{reveal.Rotation}",
        Explore explore => $"explore:{explore.Direction}:{explore.Rotation}",
        Heal heal => $"heal:{heal.Target.Value}",
        PickUpItem pickUp => $"pickup:{pickUp.Item}",
        DropItem => "drop",
        Attack => "attack",
        Dig dig => $"dig:{dig.Cell.Column},{dig.Cell.Row}",
        Overexert => "overexert",
        EndTurn => "endturn",
        Decide decide => $"decide:{decide.Option}",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Commande non sérialisable."),
    };

    /// <summary>
    /// Reads a line back, or <c>null</c> if it is not one. Anything arriving over the
    /// network is suspect: a malformed line is refused, never guessed at.
    /// </summary>
    public static GameCommand? Decode(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(':');

        return parts switch
        {
            ["move", var direction] when Way(direction) is { } way => new Move(way),
            ["run", var steps] when Path(steps) is { } path => new Run(path),
            ["reveal", var direction, var turn] when Way(direction) is { } way && Turn(turn, out var rotation) =>
                new Reveal(way, rotation),
            ["explore", var direction, var turn] when Way(direction) is { } way && Turn(turn, out var rotation) =>
                new Explore(way, rotation),
            ["heal", var target] when Number(target) is { } value => new Heal(new ExplorerId(value)),
            ["pickup", var item] when Enum.TryParse<ItemKind>(item, out var kind) => new PickUpItem(kind),
            ["drop"] => new DropItem(),
            ["attack"] => new Attack(),
            ["dig", var cell] when Where(cell) is { } target => new Dig(target),
            ["overexert"] => new Overexert(),
            ["endturn"] => new EndTurn(),
            ["decide", var option] when Number(option) is { } value => new Decide(value),
            _ => null,
        };
    }

    private static Direction? Way(string text) =>
        Enum.TryParse<Direction>(text, out var direction) ? direction : null;

    private static IReadOnlyList<Direction>? Path(string text)
    {
        var steps = new List<Direction>();

        foreach (var step in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Way(step) is not { } way)
            {
                return null;
            }

            steps.Add(way);
        }

        return steps.Count > 0 ? steps : null;
    }

    /// <summary>An empty field means "any orientation that joins up".</summary>
    private static bool Turn(string text, out int? rotation)
    {
        rotation = null;

        if (text.Length == 0)
        {
            return true;
        }

        if (Number(text) is not { } value)
        {
            return false;
        }

        rotation = value;
        return true;
    }

    private static Cell? Where(string text)
    {
        var parts = text.Split(',');

        return parts is [var column, var row] && Number(column) is { } x && Number(row) is { } y
            ? new Cell(x, y)
            : null;
    }

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
