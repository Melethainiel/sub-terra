using Godot;
using SubTerra.Core.Board;

namespace SubTerra.Presentation;

/// <summary>
/// The thin layer of light over the board that says what a click would do. It knows
/// nothing about the rules — <see cref="SubTerra.Core.Game.GameState"/> is asked what
/// is legal, and this only paints the answer.
/// </summary>
public partial class HighlightView : Node3D
{
    /// <summary>What clicking a cell would mean.</summary>
    public enum Hint
    {
        /// <summary>Walk onto a tile that is already there.</summary>
        Step,

        /// <summary>Lay a tile where there is nothing yet.</summary>
        Unknown,

        /// <summary>Dig the rock out.</summary>
        Rubble,

        /// <summary>An answer the game is waiting for.</summary>
        Choice,
    }

    /// <summary>Just above the floor slab, so the patch reads as light on the stone.</summary>
    private const float Height = 0.09f;

    private static readonly QuadMesh Patch = new()
    {
        Size = new Vector2(BoardView.TileSize * 0.86f, BoardView.TileSize * 0.86f),
        Orientation = PlaneMesh.OrientationEnum.Y,
    };

    public void Render(IReadOnlyDictionary<Cell, Hint> hints, Cell? hovered)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (cell, hint) in hints)
        {
            AddPatch(cell, hint, lit: cell == hovered);
        }
    }

    private void AddPatch(Cell cell, Hint hint, bool lit)
    {
        var colour = hint switch
        {
            Hint.Unknown => Palette.Unknown,
            Hint.Rubble => Palette.Rubble,
            Hint.Choice => Palette.Choice,
            _ => Palette.Step,
        };

        AddChild(new MeshInstance3D
        {
            Name = $"Hint_{cell.Column}_{cell.Row}",
            Mesh = Patch,
            Position = BoardView.ToWorld(cell) + new Vector3(0f, Height, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour with { A = lit ? 0.5f : 0.14f },
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = lit ? 1.3f : 0.25f,
            },
        });
    }
}
