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
    public void Render(GameState game)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        // Several explorers share a tile often enough to matter; fan them out so the
        // player can tell there are two of them.
        var crowd = new Dictionary<Cell, int>();

        foreach (var explorer in game.Explorers.Where(e => e.IsPlaying))
        {
            var rank = crowd.GetValueOrDefault(explorer.Cell);
            crowd[explorer.Cell] = rank + 1;

            AddMeeple(
                $"Explorer_{explorer.Id.Value}",
                explorer.Cell,
                Palette.For(explorer.Id),
                height: explorer.IsDown ? 0.18f : 0.5f,
                offset: Fan(rank),
                lying: explorer.IsDown);
        }

        foreach (var (cell, index) in game.Guardians.Select((cell, index) => (cell, index)))
        {
            AddMeeple($"Guardian_{index}", cell, Palette.Guardian, height: 0.62f, offset: Fan(4 + index));
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
        var radius = rank == 0 ? 0f : 0.28f;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private void AddMeeple(string name, Cell cell, Color colour, float height, Vector3 offset, bool lying = false)
    {
        var node = new MeshInstance3D
        {
            Name = name,
            Mesh = new CapsuleMesh { Radius = 0.16f, Height = height, RadialSegments = 12 },
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

    private void AddToken(string name, Cell cell, ItemKind item)
    {
        var colour = item == ItemKind.Artefact ? Palette.Artefact : Palette.Key;

        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new SphereMesh { Radius = 0.14f, Height = 0.28f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 1.6f,
            },
            Position = BoardView.ToWorld(cell) + new Vector3(0f, 0.3f, 0f),
        });
    }
}
