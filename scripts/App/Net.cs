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

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += Arrived;
    }

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
