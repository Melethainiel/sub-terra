using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// Draws what stands on the tiles — explorers, guardians, and the things left lying
/// about. Rebuilt wholesale after every command; there are never many.
/// </summary>
public partial class TokenView : Node3D
{
    /// <summary>
    /// Draws the tokens. <paramref name="throughTheEyesOf"/> names the explorer the
    /// camera is currently looking through, if any: their own meeple and whoever
    /// shares their tile are left out, since from the inside they are 1.70 m of
    /// coloured capsule half a metre from the lens and nothing else can be seen.
    /// Guardians and items stay — one filling your view is worth knowing about.
    /// </summary>
    /// <param name="stirring">
    /// A Guardian pocket being considered for an awakening: the meeple stands there
    /// while the player weighs it up, and is gone again if they settle on another.
    /// </param>
    public void Render(GameState game, ExplorerId? throughTheEyesOf = null, Cell? stirring = null)
    {
        var blind = throughTheEyesOf is { } eyes && game.Explorers[eyes.Value] is { IsPlaying: true } viewer
            ? viewer.Cell
            : (Cell?)null;

        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _explorerNodes.Clear();
        _guardianNodes.Clear();

        // Several explorers share a tile often enough to matter; fan them out so the
        // player can tell there are two of them.
        var crowd = new Dictionary<Cell, int>();

        foreach (var explorer in game.Explorers.Where(e => e.IsPlaying && e.Cell != blind))
        {
            var rank = crowd.GetValueOrDefault(explorer.Cell);
            crowd[explorer.Cell] = rank + 1;

            _explorerNodes[explorer.Id] = AddFigure(
                $"Explorer_{explorer.Id.Value}",
                FigureFor(explorer),
                Miniature.ExplorerBase,
                explorer.Cell,
                Palette.For(explorer.Id),
                offset: Fan(rank),
                lying: explorer.IsDown);

            if (explorer.IsShielded)
            {
                AddShield(explorer.Cell, Fan(rank));
            }
        }

        var guardians = stirring is { } woken ? [.. game.Guardians, woken] : game.Guardians;

        foreach (var (cell, index) in guardians.Select((cell, index) => (cell, index)))
        {
            _guardianNodes.Add((AddFigure($"Guardian_{index}", GuardianFigure, Miniature.GuardianBase, cell, livery: null, offset: Fan(4 + index), ring: Palette.Guardian), cell));
        }

        foreach (var (cell, _) in game.Board.Tiles)
        {
            foreach (var (item, index) in game.ItemsOn(cell).Select((item, index) => (item, index)))
            {
                AddToken($"Item_{cell.Column}_{cell.Row}_{index}", cell, item);
            }
        }
    }

    /// <summary>Spreads tokens around the middle of a tile instead of stacking them.</summary>
    private static Vector3 Fan(int rank)
    {
        var angle = rank * Mathf.Tau / 6f;
        // Inside a 1.5 m gallery, with room for the bases not to overlap.
        var radius = rank == 0 ? 0f : 0.5f;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private const string Figures = "res://resources/models/figures/";
    private const string ExplorerFigure = Figures + "explorer_sketch.glb";
    private const string GuardianFigure = Figures + "guardian.glb";

    /// <summary>
    /// An Explorer's own model, named after their sheet (<c>guide.glb</c>…), or the
    /// sketch while theirs is not made yet: the cast arrives one figure at a time.
    /// </summary>
    private static string FigureFor(Explorer explorer) =>
        explorer.Sheet is { } sheet && ResourceLoader.Exists($"{Figures}{sheet.Id}.glb")
            ? $"{Figures}{sheet.Id}.glb"
            : ExplorerFigure;

    private readonly Dictionary<ExplorerId, Node3D> _explorerNodes = [];

    private readonly List<(Node3D Node, Cell Cell)> _guardianNodes = [];

    /// <summary>The meeple drawn for an explorer by the last <see cref="Render"/>, if any
    /// — none for one out of the temple, or seen through their own eyes.</summary>
    public Node3D? ExplorerNode(ExplorerId explorer) => _explorerNodes.GetValueOrDefault(explorer);

    /// <summary>The Guardian meeples drawn by the last <see cref="Render"/>, with the cell
    /// each stands on.</summary>
    public IReadOnlyList<(Node3D Node, Cell Cell)> GuardianNodes => _guardianNodes;

    /// <summary>
    /// A painted miniature on its base. One that is down lies on its side, base and
    /// all, the way a knocked-over figure does on a real table.
    /// </summary>
    private Node3D AddFigure(string name, string figure, float baseRadius, Cell cell, Color? livery, Vector3 offset, bool lying = false, Color? ring = null)
    {
        var node = Miniature.Stand(figure, baseRadius, livery, ring ?? livery);
        node.Name = name;
        node.Position = BoardView.ToWorld(cell) + offset;

        if (lying)
        {
            node.RotationDegrees = new Vector3(90f, 0f, 0f);
            node.Position += new Vector3(0f, baseRadius, 0f);
        }

        AddChild(node);
        return node;
    }

    /// <summary>The Combattante's Bouclier: a halo of gold over her meeple while it holds —
    /// above the head rather than round the waist, where the rest of a crowded tile
    /// would hide it.</summary>
    private void AddShield(Cell cell, Vector3 offset) =>
        AddChild(new MeshInstance3D
        {
            Name = "Shield",
            Mesh = new TorusMesh { InnerRadius = 0.3f, OuterRadius = 0.4f, Rings = 24, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Palette.Key,
                EmissionEnabled = true,
                Emission = Palette.Key,
                EmissionEnergyMultiplier = 1.4f,
            },
            Position = BoardView.ToWorld(cell) + offset + new Vector3(0f, 2.05f, 0f),
        });

    private void AddToken(string name, Cell cell, ItemKind item)
    {
        var colour = item == ItemKind.Artefact ? Palette.Artefact : Palette.Key;

        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh { Radius = 0.32f, Height = 0.64f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 1.6f,
            },
            Position = BoardView.ToWorld(cell) + new Vector3(0f, 0.7f, 0f),
        });
    }
}
