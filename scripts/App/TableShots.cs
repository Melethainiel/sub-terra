using Godot;

namespace SubTerra.App;

/// <summary>
/// Dev tool: sits down at a solo table, switches to the first-person view, looks
/// about, comes back up, and shoots each step. The only way to see the game
/// without playing it by hand, and it earns its keep — it is what caught the
/// camera standing inside the party's own meeples.
/// <code>
/// SUBTERRA_SHOT=/tmp/table godot --path . res://scenes/_table_shots.tscn
/// </code>
/// </summary>
public partial class TableShots : Node
{
    public override async void _Ready()
    {
        Session.Party = [
            new Session.Seat("archeologue", Session.HostPeer),
            new Session.Seat("aventuriere", Session.HostPeer),
            new Session.Seat("guide", Session.HostPeer),
        ];
        Session.IsOnline = false;
        Session.Seed = 7;

        // Not ChangeSceneToFile: that frees this node, and every await below
        // with it. The table hangs under the driver instead.
        AddChild(GD.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate());

        await Settle(20);
        await Shoot("overview");

        Press(Key.V);
        await Settle(10);
        await Shoot("fps");

        Look(new Vector2(140f, -30f));
        await Settle(6);
        await Shoot("fps_tourne");

        Press(Key.Escape);
        await Settle(10);
        await Shoot("retour");

        GD.Print($"mode souris final : {Input.MouseMode}");
        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Settle(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
    }

    private async System.Threading.Tasks.Task Shoot(string name)
    {
        var path = $"{OS.GetEnvironment("SUBTERRA_SHOT")}_{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"shot {path}");
        await Settle(1);
    }

    private static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private static void Look(Vector2 relative) =>
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = relative });
}
