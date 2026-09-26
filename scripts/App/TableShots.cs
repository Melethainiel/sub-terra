using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Setup;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Dev tool: sits down at a solo table, switches to the first-person view, looks
/// about, comes back up, reveals a tile, turns it, lays it down, and passes turns
/// until a Guardian stirs, shooting each step.
/// The only way to see the game without playing it by hand, and it earns its keep
/// — it is what caught the camera standing inside the party's own meeples.
/// <code>
/// SUBTERRA_SHOT=/tmp/table godot --path . res://scenes/_table_shots.tscn
/// </code>
/// </summary>
public partial class TableShots : Node
{
    public override async void _Ready()
    {
        Session.Party = [
            new Session.Seat("guerisseuse", Session.HostPeer),
            new Session.Seat("pretre", Session.HostPeer),
            new Session.Seat("guide", Session.HostPeer),
        ];
        Session.IsOnline = false;
        Session.Seed = 7;

        // Not ChangeSceneToFile: that frees this node, and every await below
        // with it. The table hangs under the driver instead.
        var table = GD.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate();
        AddChild(table);

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

        // Reveal without stepping in: arm the Révéler card, then point it at the
        // cell. The tile comes out of the bag and the table asks which way round it
        // goes, with the first answer already laid out.
        var south = TempleSetup.EntranceCrossing.Neighbour(Direction.South);
        Press(Key.R);
        await Settle(2);
        ClickCell(table, south);
        await Settle(10);
        await Shoot("orientation");

        // A notch of the wheel turns it where it lies, and that is what a click on the
        // cell then lays down.
        Turn(table, south);
        await Settle(6);
        await Shoot("orientation_tournee");

        // Clicking the cell again takes the orientation currently on show.
        ClickCell(table, south);
        await Settle(10);
        await Shoot("posee");

        // Then let the temple take its own turns until it wakes a Guardian and has to
        // ask where: both lateral pockets are the same distance from the crossing, so
        // a party that never left it always ties.
        for (var turn = 0; turn < 40 && !Asked(table).Contains("Gardien s'éveille"); turn++)
        {
            // Any other tie in the way is settled with its first answer.
            if (Answers(table).FirstOrDefault() is { } answer)
            {
                answer.EmitSignal(BaseButton.SignalName.Pressed);
            }
            else
            {
                Press(Key.Space);
            }

            await Settle(4);
        }

        if (Asked(table).Contains("Gardien s'éveille"))
        {
            await Shoot("gardien");

            // The same wheel: the meeple stands in the other pocket instead.
            Turn(table, TempleSetup.EntranceCrossing);
            await Settle(6);
            await Shoot("gardien_autre");
        }
        else
        {
            GD.PushWarning("Aucun Gardien ne s'est éveillé en quarante tours.");
        }

        GD.Print($"mode souris final : {Input.MouseMode}");
        GetTree().Quit();
    }

    /// <summary>What the HUD is asking the table, if anything.</summary>
    private static string Asked(Node table) =>
        table.GetNode<Label>("Hud/Centre/Decision/Box/Prompt") is { Visible: true } prompt ? prompt.Text : string.Empty;

    /// <summary>The HUD's buttons for the answers on offer, in the order it shows them.</summary>
    private static IEnumerable<Button> Answers(Node table) =>
        table.GetNode("Hud/Centre/Decision/Box/Options").GetChildren().OfType<Button>();

    /// <summary>Clicks a cell of the temple — whatever card is armed plays against it.</summary>
    private static void ClickCell(Node table, Cell cell) => Push(table, cell, MouseButton.Left);

    /// <summary>A notch of the wheel over a cell: turns the tile waiting to be laid.</summary>
    private static void Turn(Node table, Cell cell) => Push(table, cell, MouseButton.WheelUp);

    /// <summary>
    /// Presses a mouse button over a cell, wherever the camera happens to put it on
    /// screen. Pushed straight into the viewport in its own coordinates: that is the
    /// space the camera answers in, and it saves guessing at the window the desktop
    /// gave us.
    /// </summary>
    private static void Push(Node table, Cell cell, MouseButton button)
    {
        var at = table.GetNode<Camera3D>("Camera3D").UnprojectPosition(BoardView.ToWorld(cell));

        foreach (var pressed in new[] { true, false })
        {
            table.GetViewport().PushInput(
                new InputEventMouseButton
                {
                    ButtonIndex = button,
                    Pressed = pressed,
                    Position = at,
                    GlobalPosition = at,
                },
                inLocalCoords: true);
        }
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
