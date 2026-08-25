using Godot;
using SubTerra.Core.Game;
using SubTerra.Core.Tiles;

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

    /// <summary>An action card that draws blood — Attaquer.</summary>
    public static readonly Color Combat = Color.Color8(214, 72, 58);

    /// <summary>An action card spent on another Explorer, or on an object — Soigner,
    /// Ramasser, Poser.</summary>
    public static readonly Color Support = Color.Color8(102, 182, 176);

    /// <summary>An action card that isn't about the temple at all — Se dépasser,
    /// Finir le tour.</summary>
    public static readonly Color Meta = Color.Color8(196, 168, 128);

    public static readonly Color Ink = Color.Color8(244, 224, 204);

    public static readonly Color Faded = Color.Color8(150, 138, 128);

    /// <summary>Warm torchlight — what an ordinary passage glows by.</summary>
    public static readonly Color Torchlight = Color.Color8(255, 176, 110);

    /// <summary>The heat off a run of Lava.</summary>
    public static readonly Color LavaGlow = Color.Color8(255, 130, 40);

    public static Color For(ExplorerId explorer) => Explorers[explorer.Value % Explorers.Length];

    /// <summary>
    /// What a tile lights its own passage with. Reuses the sigil colours where a
    /// tile already has one, so the glow and the marker on the floor read as the
    /// same thing rather than two unrelated choices.
    /// </summary>
    public static Color TileGlow(TileKind kind) => kind switch
    {
        TileKind.Guardian => Guardian,
        TileKind.Key or TileKind.Sanctuary => Key,
        TileKind.Lava => LavaGlow,
        _ => Torchlight,
    };

    /// <summary>
    /// How hard the tile's own torch should shine, relative to an ordinary passage.
    /// A run of Lava already lights itself — an equally strong torch on top of that
    /// glowing floor just blows it out white, so it gets a much dimmer accent instead.
    /// </summary>
    public static float TileGlowEnergyScale(TileKind kind) => kind switch
    {
        TileKind.Lava => 0.3f,
        _ => 1f,
    };
}
