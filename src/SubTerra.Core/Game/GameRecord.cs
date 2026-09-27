using System.Globalization;
using SubTerra.Core.Explorers;

namespace SubTerra.Core.Game;

/// <summary>
/// A whole game on a few lines of text: its seed, its difficulty, the party in order
/// of play, and every command settled so far. Because the engine is deterministic,
/// that is all a save needs — and all a player joining a game already under way
/// needs to catch up: replaying it rebuilds the very same state.
/// </summary>
/// <remarks>
/// Text, like <see cref="CommandCodec"/>, so a save can be read and a desync studied
/// by eye. A version line heads it; a record from another version is refused rather
/// than replayed into a game it no longer describes.
/// </remarks>
public sealed record GameRecord(ulong Seed, Difficulty Difficulty, IReadOnlyList<string> Party, IReadOnlyList<string> Commands)
{
    /// <summary>Bumped whenever the rules change what a command does: an old record
    /// would replay into a different game.</summary>
    public const int Version = 1;

    private const string Header = "subterra-record";

    /// <summary>The record of a game about to begin: no command yet.</summary>
    public static GameRecord Start(ulong seed, Difficulty difficulty, IEnumerable<string> party) =>
        new(seed, difficulty, [.. party], []);

    /// <summary>The same record, one command further on.</summary>
    public GameRecord With(GameCommand command) => this with { Commands = [.. Commands, CommandCodec.Encode(command)] };

    /// <summary>
    /// Builds the game this record describes and plays every command into it, or
    /// returns <c>null</c> if the record does not hold together — an unknown sheet, a
    /// command the engine refuses, a line it cannot read.
    /// </summary>
    public GameState? Replay()
    {
        var sheets = Party.Select(ExplorerRoster.Find).ToList();

        if (sheets.Count < ExplorerRoster.SmallestParty || sheets.Any(sheet => sheet is null))
        {
            return null;
        }

        var game = GameState.NewGame(sheets!, Seed, Difficulty);

        foreach (var line in Commands)
        {
            if (CommandCodec.Decode(line) is not { } command || !game.Execute(command).Accepted)
            {
                return null;
            }
        }

        return game;
    }

    public string Encode() => string.Join('\n', new[]
    {
        $"{Header} {Version}",
        $"seed {Seed.ToString(CultureInfo.InvariantCulture)}",
        $"difficulty {Difficulty}",
        $"party {string.Join(',', Party)}",
    }.Concat(Commands)) + "\n";

    /// <summary>Reads a record back, or <c>null</c> if it is not one of this version.</summary>
    public static GameRecord? Decode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length < 4
            || lines[0] != $"{Header} {Version}"
            || !lines[1].StartsWith("seed ", StringComparison.Ordinal)
            || !ulong.TryParse(lines[1][5..], NumberStyles.None, CultureInfo.InvariantCulture, out var seed)
            || !lines[2].StartsWith("difficulty ", StringComparison.Ordinal)
            || !Enum.TryParse<Difficulty>(lines[2][11..], out var difficulty)
            || !Enum.IsDefined(difficulty)
            || !lines[3].StartsWith("party ", StringComparison.Ordinal))
        {
            return null;
        }

        var party = lines[3][6..].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var commands = lines[4..];

        // Every command must at least read as one; whether it plays is Replay's to say.
        return commands.All(line => CommandCodec.Decode(line) is not null)
            ? new GameRecord(seed, difficulty, party, commands)
            : null;
    }
}
