using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The colours the game is drawn in. One place for them so a meeple on the board and
/// its line in the roster are unmistakably the same explorer.
/// </summary>
public static class Palette
{
    private static readonly Color[] Explorers =
    [
        Color.Color8(96, 190, 120),
        Color.Color8(226, 118, 96),
        Color.Color8(118, 158, 232),
        Color.Color8(232, 206, 108),
        Color.Color8(200, 128, 216),
        Color.Color8(120, 214, 210),
    ];

    public static readonly Color Guardian = Color.Color8(150, 86, 208);

    public static readonly Color Key = Color.Color8(255, 208, 96);

    public static readonly Color Artefact = Color.Color8(255, 122, 40);

    /// <summary>Somewhere you could walk.</summary>
    public static readonly Color Step = Color.Color8(96, 190, 120);

    /// <summary>Somewhere there is no tile yet.</summary>
    public static readonly Color Unknown = Color.Color8(232, 170, 72);

    /// <summary>Rock you could clear.</summary>
    public static readonly Color Rubble = Color.Color8(150, 160, 186);

    /// <summary>Something the game is waiting for you to point at.</summary>
    public static readonly Color Choice = Color.Color8(255, 96, 84);

    public static readonly Color Ink = Color.Color8(244, 224, 204);

    public static readonly Color Faded = Color.Color8(150, 138, 128);

    public static Color For(ExplorerId explorer) => Explorers[explorer.Value % Explorers.Length];
}
