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

        // Several explorers share a tile often enough to matter; fan them out so the
        // player can tell there are two of them.
        var crowd = new Dictionary<Cell, int>();

        foreach (var explorer in game.Explorers.Where(e => e.IsPlaying && e.Cell != blind))
        {
            var rank = crowd.GetValueOrDefault(explorer.Cell);
            crowd[explorer.Cell] = rank + 1;

            AddMeeple(
                $"Explorer_{explorer.Id.Value}",
                explorer.Cell,
                Palette.For(explorer.Id),
                height: explorer.IsDown ? 0.5f : 1.7f,
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
            AddMeeple($"Guardian_{index}", cell, Palette.Guardian, height: 2.1f, offset: Fan(4 + index));
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
        var radius = rank == 0 ? 0f : 0.55f;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void AddMeeple(string name, Cell cell, Color colour, float height, Vector3 offset, bool lying = false)
    {
        var node = new MeshInstance3D
        {
            Name = name,
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = height, RadialSegments = 12 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 0.35f,
            },
            Position = BoardView.ToWorld(cell) + offset + new Vector3(0f, height / 2f, 0f),
        };

        if (lying)
        {
            node.RotationDegrees = new Vector3(90f, 0f, 0f);
            node.Position = BoardView.ToWorld(cell) + offset + new Vector3(0f, 0.16f, 0f);
        }

        AddChild(node);
    }

    /// <summary>The Combattante's Bouclier: a ring of gold around her meeple while it holds.</summary>
    private void AddShield(Cell cell, Vector3 offset) =>
        AddChild(new MeshInstance3D
        {
            Name = "Shield",
            Mesh = new TorusMesh { InnerRadius = 0.36f, OuterRadius = 0.44f, Rings = 24, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = Palette.Key,
                EmissionEnabled = true,
                Emission = Palette.Key,
                EmissionEnergyMultiplier = 1.4f,
            },
            Position = BoardView.ToWorld(cell) + offset + new Vector3(0f, 0.9f, 0f),
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
