using SubTerra.Core.Explorers;
using SubTerra.Core.Game;

namespace SubTerra.App;

/// <summary>
/// What the lobby agreed on, handed over to the table. A scene change is all that
/// separates the two, and there is exactly one game per process — so this is static
/// rather than an autoload with a node behind it.
/// </summary>
public static class Session
{
    /// <summary>The port a host listens on unless told otherwise.</summary>
    public const int DefaultPort = 27015;

    /// <summary>Godot's id for the host. Everyone else gets a random one.</summary>
    public const long HostPeer = 1;

    /// <summary>A chosen Explorer and the player who runs them.</summary>
    public sealed record Seat(string SheetId, long Peer);

    /// <summary>Which door of the lobby the accueil was opened by — Solo hides the
    /// host/join row entirely and seats the one machine at its own table; Coop shows
    /// it, since a shared table needs one before anyone can be seated at all.</summary>
    public enum LobbyMode
    {
        Solo,
        Coop,
    }

    /// <summary>Set by the accueil right before it opens the lobby.</summary>
    public static LobbyMode RequestedMode { get; set; } = LobbyMode.Solo;

    public static IReadOnlyList<Seat> Party { get; set; } = [];

    public static ulong Seed { get; set; } = 42;

    public static Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Whether there is a network at all. A solo game has none.</summary>
    public static bool IsOnline { get; set; }

    public static bool IsHost { get; set; } = true;

    /// <summary>This machine's peer id, or <see cref="HostPeer"/> when playing alone.</summary>
    public static long LocalPeer { get; set; } = HostPeer;

    public static IEnumerable<ExplorerSheet> Sheets =>
        Party.Select(seat => ExplorerRoster.Find(seat.SheetId)).OfType<ExplorerSheet>();

    /// <summary>Whether this machine is the one that plays that Explorer.</summary>
    public static bool Owns(ExplorerId explorer) =>
        !IsOnline || SeatOf(explorer)?.Peer == LocalPeer;

    /// <summary>Which player runs that Explorer, or <c>null</c> if the seat is empty.</summary>
    public static Seat? SeatOf(ExplorerId explorer) =>
        explorer.Value >= 0 && explorer.Value < Party.Count ? Party[explorer.Value] : null;

    /// <summary>
    /// A player who drops out leaves their Explorers behind; rather than freeze the
    /// expedition, the host picks them up.
    /// </summary>
    public static void HandOver(long peer) =>
        Party = [.. Party.Select(seat => seat.Peer == peer ? seat with { Peer = HostPeer } : seat)];

    /// <summary>The party on the wire: "guide:1;pretre:34;combattante:1".</summary>
    public static string Encode(IEnumerable<Seat> party) =>
        string.Join(';', party.Select(seat => $"{seat.SheetId}:{seat.Peer}"));

    public static IReadOnlyList<Seat> Decode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var party = new List<Seat>();

        foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            // A seat naming an Explorer that is not in the box is dropped rather than
            // trusted: this arrives over the network.
            if (entry.Split(':') is [var sheet, var peer]
                && ExplorerRoster.Find(sheet) is not null
                && long.TryParse(peer, out var owner))
            {
                party.Add(new Seat(sheet, owner));
            }
        }

        return party;
    }
}
