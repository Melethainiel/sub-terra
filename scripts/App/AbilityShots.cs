using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Game;
using SubTerra.Core.Setup;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Dev tool: sits a few parties down at a solo table and plays their abilities
/// through the real interface — keys pressed, cells clicked, nothing called on the
/// engine directly — printing the HUD's log and the state that matters after each
/// step, and shooting the moments worth looking at. The engine's tests say the
/// rules are right; this says the cards, the clicks and the highlights reach them.
/// <code>
/// SUBTERRA_SHOT=/tmp/abilities godot --path . res://scenes/_ability_shots.tscn
/// </code>
/// </summary>
public partial class AbilityShots : Node
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private AppRoot _table = null!;

    public override async void _Ready()
    {
        await Scenario("soins", ["guerisseuse", "pretre", "guide"], async () =>
        {
            Press(Key.Space);
            await Settle(3);
            Press(Key.O);
            await Settle(2);
            Press(Key.Space);
            await Settle(3);
            Press(Key.Space);
            await Settle(3);
            Report("Guérisseuse de retour, Prêtre blessé");

            Press(Key.Key1);
            await Settle(3);
            await Shoot("guerir_arme");
            Click(Explorer(1).Cell);
            await Settle(4);
            Report("après Guérir sur le Prêtre");
        });

        await Scenario("illuminer", ["guide", "gredin", "contremaitre"], async () =>
        {
            Press(Key.Key2);
            await Settle(3);
            Report("Illuminer joué");
            Click(Start.Neighbour(Direction.South));
            await Settle(4);
            SettleDecisions();
            await Settle(4);
            Report("premier Révéler offert");
            await Shoot("illuminer");
        });

        await Scenario("sprinter", ["gredin", "guide", "contremaitre"], async () =>
        {
            Press(Key.Key1);
            await Settle(3);
            Report("Sprinter joué");
            Click(Start.Neighbour(Direction.West));
            await Settle(3);
            Report("premier pas offert — Se déplacer doit être de nouveau en main");
            Click(Start);
            await Settle(3);
            Report("second pas offert");
        });

        await Scenario("ordonner", ["aristocrate", "gredin", "guide"], async () =>
        {
            Press(Key.Key1);
            await Settle(3);
            Click(Start);
            await Settle(3);
            await Shoot("ordonner_qui");
            Click(Start.Neighbour(Direction.West));
            await Settle(4);
            Report("Gredin envoyé à l'ouest");
        });

        await Scenario("rechercher", ["aristocrate", "gredin", "guide"], async () =>
        {
            Press(Key.Key2);
            await Settle(3);
            await Shoot("rechercher_arme");
            Click(new Cell(1, 1));
            await Settle(4);
            SettleDecisions();
            await Settle(6);
            Report("Journal posé en (1,1)");
            await Shoot("rechercher");
        });

        await Scenario("bouclier", ["combattante", "gredin", "guide"], async () =>
        {
            Press(Key.Key2);
            await Settle(6);
            Report("Se préparer");
            await Shoot("bouclier");
        });

        await Scenario("demolir", ["sapeur", "gredin", "guide"], async () =>
        {
            Press(Key.M);
            await Settle(2);
            Click(Start.Neighbour(Direction.West));
            await Settle(3);
            Press(Key.M);
            await Settle(2);
            Click(new Cell(1, 0));
            await Settle(3);

            for (var turn = 0; turn < 3; turn++)
            {
                SettleDecisions();
                Press(Key.Space);
                await Settle(3);
            }

            SettleDecisions();
            Report("Sapeur sur le bras ouest, son tour revenu");

            // One more step, into the Guardian pocket at the arm's end: rock on its south side.
            Press(Key.M);
            await Settle(2);
            Click(new Cell(0, 0));
            await Settle(3);
            Report("Sapeur dans la poche du Gardien");
            Press(Key.Key2);
            await Settle(3);
            await Shoot("demolir_arme");
            Click(new Cell(0, 1));
            await Settle(4);
            Report("après Démolir vers le sud");
            await Shoot("demolir");
        });

        await Scenario("animations", ["guide", "gredin", "contremaitre"], async () =>
        {
            Press(Key.R);
            await Settle(2);
            Click(Start.Neighbour(Direction.South));
            await Settle(3);
            await Shoot("anim_tuile_1");
            await Settle(6);
            await Shoot("anim_tuile_2");
            SettleDecisions();
            await Settle(30);

            Press(Key.M);
            await Settle(2);
            Click(Start.Neighbour(Direction.South));
            await Settle(4);
            await Shoot("anim_pas");
            await Settle(30);

            Press(Key.Space);
            await Settle(4);
            await Shoot("anim_de_1");
            await Settle(25);
            await Shoot("anim_de_2");
            Report("après la fin du tour");
        });

        // No game is played to its end here: the HUD is simply shown the ending.
        await Scenario("fin", ["guide", "gredin", "contremaitre"], async () =>
        {
            _table.GetNode<Hud>("Hud").Cue(new GameEnded(Outcome.Gold));
            await Settle(50);
            await Shoot("fin_or");
            _table.GetNode<Hud>("Hud").Cue(new GameEnded(Outcome.ForgottenForever));
            await Settle(50);
            await Shoot("fin_oubli");
        });

        GetTree().Quit();
    }

    private async System.Threading.Tasks.Task Scenario(string name, string[] sheets, Func<System.Threading.Tasks.Task> play)
    {
        GD.Print($"── {name} ──");
        Session.Party = [.. sheets.Select(sheet => new Session.Seat(sheet, Session.HostPeer))];
        Session.IsOnline = false;
        Session.Seed = 7;

        _table = GD.Load<PackedScene>("res://scenes/app/Main.tscn").Instantiate<AppRoot>();
        AddChild(_table);
        await Settle(12);

        await play();

        _table.QueueFree();
        await Settle(2);
    }

    private Explorer Explorer(int index) => _table.Game.Explorers[index];

    /// <summary>Prints the HUD's log and what the engine now holds.</summary>
    private void Report(string step)
    {
        var game = _table.Game;
        var log = _table.FindChild("Log", recursive: true, owned: false) is Label label ? label.Text.Replace('\n', '|') : "?";

        GD.Print($"[{step}] tour de {game.CurrentExplorer.Name} · PA {game.ActionPoints}"
            + (game.Granted is { } granted ? $" · offert {granted.Action}×{granted.Remaining}" : "")
            + (game.Pending is { } pending ? $" · attend : {pending.Prompt}" : ""));
        GD.Print($"    équipe : {string.Join(", ", game.Explorers.Select(e => $"{e.Name} {e.Cell} {e.Health}♥{(e.IsShielded ? " 🛡" : "")}"))}");
        GD.Print($"    tuiles {game.Board.Tiles.Count} · brèches {game.Board.Demolished.Count} · journal : {log}");
    }

    /// <summary>Takes the first answer to anything the table is asked, the way a
    /// hurried player would.</summary>
    private void SettleDecisions()
    {
        for (var guard = 0; guard < 10 && _table.Game.Pending is not null; guard++)
        {
            _table.GetNode("Hud/Centre/Decision/Box/Options").GetChildren().OfType<Button>().FirstOrDefault()
                ?.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    private void Click(Cell cell)
    {
        var at = _table.GetNode<Camera3D>("Camera3D").UnprojectPosition(BoardView.ToWorld(cell));

        foreach (var pressed in new[] { true, false })
        {
            _table.GetViewport().PushInput(
                new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed, Position = at, GlobalPosition = at },
                inLocalCoords: true);
        }
    }

    private static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
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
        await Settle(1);
    }
}
