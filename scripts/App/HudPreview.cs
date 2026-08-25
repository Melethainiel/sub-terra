using Godot;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Dev tool: renders the Hud on its own, over a plain cave-dark backdrop, with a
/// freshly-dealt game behind it — the same trick <c>scenes/_preview.gd</c> uses for
/// one tile, but for the 2D overlay instead of a 3D scene.
/// Usage: SUBTERRA_SHOT=/tmp/hud.png godot --headless res://scenes/_preview_hud.tscn
/// </summary>
public partial class HudPreview : Node
{
    public override async void _Ready()
    {
        var hud = GetNode<Hud>("Hud");

        var party = ExplorerRoster.All.Take(4);
        var game = GameState.NewGame(party, seed: 1);

        hud.Show(
            game,
            mine: true,
            notice: "Le sac se vide : plus que 14 tuiles avant que le Sanctuaire ne puisse être posé.",
            armed: null,
            canArm: _ => true);
        hud.Say("L'Archéologue a exploré vers le Nord — une tuile Ruines révélée.");

        // A few idle frames so every StyleBox and the grain shader have actually
        // drawn once before the viewport is read back.
        for (var i = 0; i < 4; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var shot = OS.GetEnvironment("SUBTERRA_SHOT");
        GetViewport().GetTexture().GetImage().SavePng(shot);
        GetTree().Quit();
    }
}
