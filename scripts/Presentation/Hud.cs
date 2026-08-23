using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>The state of play in words, until there is a proper interface.</summary>
public partial class Hud : CanvasLayer
{
    private Label _status = null!;
    private Label _log = null!;

    public override void _Ready()
    {
        _status = GetNode<Label>("%Status");
        _log = GetNode<Label>("%Log");
    }

    public void Show(GameState game)
    {
        var explorer = game.CurrentExplorer;
        var carried = explorer.Carried is { } item ? $" — porte : {item}" : string.Empty;
        var medallion = game.Leader.Id == explorer.Id ? " ★" : string.Empty;

        _status.Text = string.Join('\n',
            $"Manche {game.Round + 1}   Chef : {game.Leader.Name}",
            $"À {explorer.Name}{medallion} — {explorer.Health}/{explorer.MaxHealth} ♥   {game.ActionPoints} action(s){carried}",
            $"Sac : {game.Bag.Count}   Éruption : {game.EruptionCountdown}   Clés déposées : {game.KeysDeposited}/{GameState.KeysToUnlock}",
            "Clic : avancer / explorer   ·   Maj+clic : révéler sans entrer   ·   Ctrl+clic : creuser",
            "A attaquer   H soigner   P ramasser   D poser   O se dépasser   Espace finir le tour");
    }

    /// <summary>The last thing that happened, or the reason nothing did.</summary>
    public void Say(string message) => _log.Text = message;
}
