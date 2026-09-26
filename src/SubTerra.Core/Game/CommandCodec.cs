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
        Reveal reveal => $"reveal:{reveal.Direction}",
        Explore explore => $"explore:{explore.Direction}",
        Heal heal => $"heal:{heal.Target.Value}",
        UseAbility use => string.Join(':', new[]
        {
            "ability",
            use.Ability,
            use.Target is { } target ? $"e{target.Value}" : null,
            use.Direction is { } direction ? $"d{direction}" : null,
        }.OfType<string>()),
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
            ["reveal", var direction] when Way(direction) is { } way => new Reveal(way),
            ["explore", var direction] when Way(direction) is { } way => new Explore(way),
            ["heal", var target] when Number(target) is { } value => new Heal(new ExplorerId(value)),
            ["ability", var ability, .. var aims] when IsName(ability) => Ability(ability, aims),
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

    private static Cell? Where(string text)
    {
        var parts = text.Split(',');

        return parts is [var column, var row] && Number(column) is { } x && Number(row) is { } y
            ? new Cell(x, y)
            : null;
    }

    /// <summary>
    /// What an ability aims at, each tagged: <c>e3</c> an Explorer, <c>dSouth</c> a
    /// direction. Anything untagged, repeated or unreadable spoils the whole line.
    /// </summary>
    private static UseAbility? Ability(string ability, string[] aims)
    {
        ExplorerId? target = null;
        Direction? direction = null;

        foreach (var aim in aims)
        {
            switch (aim)
            {
                case ['e', .. var id] when target is null && Number(id) is { } value:
                    target = new ExplorerId(value);
                    break;
                case ['d', .. var way] when direction is null && Way(way) is { } parsed:
                    direction = parsed;
                    break;
                default:
                    return null;
            }
        }

        return new UseAbility(ability, target, direction);
    }

    /// <summary>Ability names are lower-case words and dashes, never anything the
    /// line format itself would choke on.</summary>
    private static bool IsName(string text) =>
        text.Length > 0 && text.All(c => c is (>= 'a' and <= 'z') or '-');

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
