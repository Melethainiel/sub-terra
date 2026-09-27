using Godot;
using SubTerra.Core.Game;

namespace SubTerra.App;

/// <summary>
/// The table's messages that must reach a player wherever they are (autoload): an RPC
/// finds its node by path, and a player joining a game already under way is still in
/// the lobby when the host answers — a message to the table would find nobody there.
/// This node sits at /root/Net in every scene, on every machine.
/// </summary>
public partial class Net : Node
{
    public static Net? Instance { get; private set; }

    /// <summary>The table this machine is playing at, if it is at one: set by the table
    /// itself, and only a host's is asked anything here.</summary>
    public AppRoot? Table { get; set; }

    /// <summary>The port this machine asked its router to forward, if it did.</summary>
    private (Upnp Router, int Port)? _forwarded;

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += Arrived;
    }

    /// <summary>What asking the router for a port came to.</summary>
    /// <param name="Address">The address a friend types to join, when the router told it.</param>
    /// <param name="Problem">Why friends from outside may not get in, or <c>null</c>.</param>
    public sealed record Forwarding(string? Address, string? Problem);

    /// <summary>
    /// Asks the router, over UPnP, to let the game's port in — so a host does not have
    /// to open it by hand. The asking blocks for a couple of seconds, so it happens off
    /// the main thread; the answer comes back on it.
    /// </summary>
    public async Task<Forwarding> Forward(int port)
    {
        if (_forwarded is { } held && held.Port == port)
        {
            return await Task.Run(() => Describe(AddressOf(held.Router)));
        }

        Unforward();

        var (router, forwarding) = await Task.Run(() => Ask(port));

        if (router is not null)
        {
            _forwarded = (router, port);
        }

        return forwarding;
    }

    private static (Upnp? Router, Forwarding Forwarding) Ask(int port)
    {
        var upnp = new Upnp();

        if (upnp.Discover() != (int)Upnp.UpnpResult.Success || upnp.GetGateway() is not { } gateway || !gateway.IsValidGateway())
        {
            return (null, new Forwarding(null, "box introuvable en UPnP"));
        }

        if (upnp.AddPortMapping(port, port, "Sub Terra", "UDP") != (int)Upnp.UpnpResult.Success)
        {
            return (null, new Forwarding(AddressOf(upnp), "la box a refusé d'ouvrir le port (UPnP désactivé ?)"));
        }

        return (upnp, Describe(AddressOf(upnp)));
    }

    private static string? AddressOf(Upnp upnp) => upnp.QueryExternalAddress() is { Length: > 0 } address ? address : null;

    private static Forwarding Describe(string? address) =>
        address is not null && IsBehindCarrierNat(address)
            ? new Forwarding(null, "la box n'a pas d'adresse publique à elle (NAT de l'opérateur)")
            : new Forwarding(address, null);

    /// <summary>
    /// A router whose own "public" address is private or in the carrier range sits behind
    /// the operator's NAT: forwarding on it opens nothing to the outside world.
    /// </summary>
    private static bool IsBehindCarrierNat(string address)
    {
        var parts = address.Split('.').Select(part => int.TryParse(part, out var value) ? value : -1).ToArray();

        return parts.Length == 4 && parts[0] switch
        {
            10 => true,
            100 => parts[1] is >= 64 and <= 127,
            172 => parts[1] is >= 16 and <= 31,
            192 => parts[1] == 168,
            _ => false,
        };
    }

    /// <summary>Hands the forwarded port back to the router.</summary>
    public void Unforward()
    {
        if (_forwarded is { } held)
        {
            held.Router.DeletePortMapping(held.Port, "UDP");
            _forwarded = null;
        }
    }

    public override void _ExitTree() => Unforward();

    /// <summary>
    /// Someone connects while this host's game is under way: they are handed the whole
    /// game to replay, and the seats of whoever dropped out, if anyone did.
    /// </summary>
    private void Arrived(long peer)
    {
        if (Table is not { } table || !Multiplayer.IsServer())
        {
            return;
        }

        if (Session.Adopt(peer))
        {
            table.SeatsChanged();
        }

        HandGame(peer, table.Record);
    }

    /// <summary>Sends a peer the game as it stands, to replay and sit down at.</summary>
    public void HandGame(long peer, GameRecord record) =>
        RpcId(peer, nameof(Resume), record.Encode(), Session.Encode(Session.Party));

    /// <summary>
    /// The host's game, handed over: sit down at the table and replay it. A player
    /// already at the table — resynchronising after a divergence — sits down afresh.
    /// </summary>
    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Resume(string record, string party)
    {
        if (GameRecord.Decode(record) is not { } game)
        {
            GD.PushError("Partie illisible reçue de l'hôte.");
            return;
        }

        Session.Resume = game;
        Session.Party = Session.Decode(party);
        Session.Seed = game.Seed;
        Session.Difficulty = game.Difficulty;
        Session.IsOnline = true;
        Session.IsHost = false;
        Session.LocalPeer = Multiplayer.GetUniqueId();

        GetTree().ChangeSceneToFile("res://scenes/app/Main.tscn");
    }

    /// <summary>A client whose game drifted from the host's asks for the host's again.</summary>
    public void AskForResync() => RpcId(Session.HostPeer, nameof(Resync));

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Resync()
    {
        if (Table is { } table && Multiplayer.IsServer())
        {
            HandGame(Multiplayer.GetRemoteSenderId(), table.Record);
        }
    }
}
