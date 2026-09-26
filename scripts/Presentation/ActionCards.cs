using Godot;

namespace SubTerra.Presentation;

/// <summary>One column of the Actions card, and the keyboard shortcut printed under
/// it — the same fact the Réglages screen's command reference reprints, so the two
/// can never drift apart.</summary>
public sealed record ActionCardInfo(
    string Action,
    string Name,
    string Icon,
    string Cost,
    string Key,
    bool Targeted,
    Color Accent);

/// <summary>
/// Every action, in the order they sit as columns on the one Actions card — an icon
/// and a name apiece, its PA cost and its key printed underneath. Shared between the
/// HUD, which draws the card itself, and the Réglages screen, which only needs to
/// reprint the key.
/// </summary>
public static class ActionCards
{
    public static readonly IReadOnlyList<ActionCardInfo> All =
    [
        new("move", "Se déplacer", "→", "1 PA", "M", true, Palette.Step),
        new("explore", "Explorer", "◇", "1 PA", "E", true, Palette.Unknown),
        new("reveal", "Révéler", "☆", "1 PA", "R", true, Palette.Unknown),
        new("dig", "Creuser", "▼", "2 PA", "C", true, Palette.Rubble),
        new("run", "Courir", "»", "2 PA", "U", true, Palette.Step),
        new("attack", "Attaquer", "×", "1 PA", "A", false, Palette.Combat),
        new("heal", "Soigner", "♥", "1 PA", "H", false, Palette.Support),
        new("pickup", "Ramasser", "▲", "1 PA", "P", false, Palette.Support),
        new("drop", "Poser", "▽", "1 PA", "D", false, Palette.Support),
        new("overexert", "Se dépasser", "↑", "1 ♥", "O", false, Palette.Meta),
        new("endturn", "Finir le tour", "■", "—", "Espace", false, Palette.Meta),
    ];
}
