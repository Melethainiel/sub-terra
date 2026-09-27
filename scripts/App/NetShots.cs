using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Game;

namespace SubTerra.App;

/// <summary>
/// Dev tool: one end of a networked game, driven by script, for checking two machines
/// play the same game — a client joining partway included. Run a host, then a client:
/// <code>
/// SUBTERRA_NET=host   SUBTERRA_PORT=27999 godot --headless --path . res://scenes/_net_shots.tscn
/// SUBTERRA_NET=client SUBTERRA_PORT=27999 godot --headless --path . res://scenes/_net_shots.tscn
/// </code>
/// With <c>SUBTERRA_RUSH=1</c> the host plays on while the client is still loading.
/// Each prints <c>HOST|CLIENT n fingerprint</c> after every command it settles; the
/// last line of each must agree. The driver hangs off the root, since the table has
/// to sit at /root/Main on both machines for their messages to find each other.
/// </summary>
public partial class NetShots : Node
{
    public override void _Ready()
    {
        // A save round trip writes to its own folder (SUBTERRA_SAVES), never the player's.
        SaveGame.Enabled = OS.GetEnvironment("SUBTERRA_NET") is "save" or "resume";
        CallDeferred(nameof(Start));
    }

    private void Start()
    {
        var driver = new NetDriver { Name = "NetDriver", Role = OS.GetEnvironment("SUBTERRA_NET") };
        GetTree().Root.AddChild(driver);
    }
}

public partial class NetDriver : Node
{
    public string Role { get; set; } = "host";

    private int _printed = -1;

    private static readonly GameCommand[] Before =
    [
        new Reveal(Direction.South), new Move(Direction.South), new EndTurn(), new EndTurn(), new EndTurn(),
    ];

    private static readonly GameCommand[] After =
    [
        new Reveal(Direction.South), new EndTurn(), new Overexert(), new EndTurn(), new EndTurn(), new EndTurn(),
    ];

    public override async void _Ready()
    {
        var port = int.TryParse(OS.GetEnvironment("SUBTERRA_PORT"), out var chosen) ? chosen : 27999;
        var peer = new ENetMultiplayerPeer();

        if (Role is "save" or "resume")
        {
            Session.IsOnline = false;
            Session.IsHost = true;
            Session.LocalPeer = Session.HostPeer;

            if (Role == "resume")
            {
                Session.Resume = SaveGame.Read();
                GD.Print($"RESUME read {Session.Resume?.Commands.Count} commands");
                Session.Party = [.. Session.Resume!.Party.Select(sheet => new Session.Seat(sheet, Session.HostPeer))];
            }
            else
            {
                Session.Party = [new("guide", Session.HostPeer), new("pretre", Session.HostPeer), new("combattante", Session.HostPeer)];
                Session.Seed = 11;
            }

            GetTree().ChangeSceneToFile("res://scenes/app/Main.tscn");
            await Wait(2f);

            if (Role == "save")
            {
                await Play(Before);
            }

            await Wait(1f);
            GD.Print($"{Role.ToUpperInvariant()} FINAL {Table?.Record.Commands.Count} {Table?.Game.Fingerprint}");
            GetTree().Quit();
            return;
        }

        if (Role == "host")
        {
            peer.CreateServer(port, 4);
            Multiplayer.MultiplayerPeer = peer;
            Session.Party = [new("guide", Session.HostPeer), new("pretre", Session.HostPeer), new("combattante", Session.HostPeer)];
            Session.Seed = 11;
            Session.IsOnline = true;
            Session.IsHost = true;
            Session.LocalPeer = Session.HostPeer;
            GetTree().ChangeSceneToFile("res://scenes/app/Main.tscn");

            await Wait(2f);
            // Seated alone: nobody else is expected yet.
            await Play(Before);
            GD.Print("HOST waiting for a player");

            for (var i = 0; i < 300 && Multiplayer.GetPeers().Length == 0; i++)
            {
                await Wait(0.1f);
            }

            // Time for them to replay the game handed to them and sit down — unless the
            // point is to play on while they load (SUBTERRA_RUSH): the commands sent
            // meanwhile find no table there, and the host must notice and hand the game
            // over again when they knock.
            await Wait(OS.GetEnvironment("SUBTERRA_RUSH") == "1" ? 0.05f : 4f);
            await Play(After);
            await Wait(3f);
            GD.Print($"HOST FINAL {Table?.Record.Commands.Count} {Table?.Game.Fingerprint}");
            GetTree().Quit();
            return;
        }

        peer.CreateClient("127.0.0.1", port);
        Multiplayer.MultiplayerPeer = peer;
        Multiplayer.ServerDisconnected += () =>
        {
            GD.Print($"CLIENT FINAL {Table?.Record.Commands.Count} {Table?.Game.Fingerprint}");
            GetTree().Quit();
        };
    }

    private static AppRoot? Table => Net.Instance?.Table;

    public override void _Process(double delta)
    {
        if (Table is not { } table || table.Record.Commands.Count == _printed)
        {
            return;
        }

        _printed = table.Record.Commands.Count;
        GD.Print($"{Role.ToUpperInvariant()} {_printed} {table.Game.Fingerprint}");
    }

    private async System.Threading.Tasks.Task Play(IEnumerable<GameCommand> commands)
    {
        foreach (var command in commands)
        {
            if (Table is not { } table)
            {
                return;
            }

            table.Submit(command);

            // Whatever the table is asked along the way, the first answer.
            for (var guard = 0; guard < 10 && table.Game.Pending is not null; guard++)
            {
                table.Submit(new Decide(0));
            }

            await Wait(0.4f);
        }
    }

    private async System.Threading.Tasks.Task Wait(float seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
