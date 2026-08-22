using Godot;
using SubTerra.Core.Board;
using SubTerra.Core.Tiles;

namespace SubTerra.Presentation;

/// <summary>
/// Draws a <see cref="TempleBoard"/> as carved rock: a floor slab per tile and thick
/// walls wherever the passage is closed. This is the only place that knows how a
/// <see cref="Cell"/> maps to world space — the rules engine never deals in metres.
/// </summary>
/// <remarks>
/// Everything here is built from primitives. When modelled tiles arrive, the seam to
/// replace is <see cref="AddTile"/>: instantiate a scene per <see cref="TileKind"/>
/// instead of assembling boxes, and keep the rest.
/// </remarks>
public partial class BoardView : Node3D
{
    /// <summary>Edge length of one tile, in metres.</summary>
    public const float TileSize = 2f;

    private const float FloorThickness = 0.15f;
    private const float WallHeight = 1.5f;
    private const float WallThickness = 0.34f;

    private readonly Dictionary<TileKind, StandardMaterial3D> _floorMaterials = [];
    private StandardMaterial3D? _wallMaterial;

    public static Vector3 ToWorld(Cell cell) => new(cell.Column * TileSize, 0f, cell.Row * TileSize);

    /// <summary>Clears and redraws the whole board. Cheap enough at this scale.</summary>
    public void Render(TempleBoard board)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (cell, tile) in board.Tiles)
        {
            AddTile(cell, tile);
        }
    }

    private void AddTile(Cell cell, PlacedTile tile)
    {
        var root = new Node3D { Name = $"Tile_{cell.Column}_{cell.Row}", Position = ToWorld(cell) };
        AddChild(root);

        root.AddChild(new MeshInstance3D
        {
            Name = "Floor",
            Mesh = new BoxMesh { Size = new Vector3(TileSize, FloorThickness, TileSize) },
            MaterialOverride = FloorMaterial(tile.Kind),
            Position = new Vector3(0f, -FloorThickness / 2f, 0f),
        });

        foreach (var direction in DirectionExtensions.All)
        {
            if (!tile.IsOpen(direction))
            {
                AddWall(root, direction);
            }
        }

        AddFurniture(root, cell, tile.Kind);
    }

    private void AddWall(Node3D tile, Direction direction)
    {
        var alongZ = direction is Direction.North or Direction.South;
        var offset = TileSize / 2f - WallThickness / 2f;
        var sign = direction is Direction.North or Direction.West ? -1f : 1f;

        tile.AddChild(new MeshInstance3D
        {
            Name = $"Wall_{direction}",
            Mesh = new BoxMesh
            {
                Size = alongZ
                    ? new Vector3(TileSize, WallHeight, WallThickness)
                    : new Vector3(WallThickness, WallHeight, TileSize),
            },
            MaterialOverride = WallMaterial(),
            Position = alongZ
                ? new Vector3(0f, WallHeight / 2f, sign * offset)
                : new Vector3(sign * offset, WallHeight / 2f, 0f),
        });
    }

    /// <summary>What tells one kind of tile from another at a glance.</summary>
    private static void AddFurniture(Node3D tile, Cell cell, TileKind kind)
    {
        switch (kind)
        {
            case TileKind.Lava:
                // The tile is its own light source — that is the whole point of it.
                tile.AddChild(new OmniLight3D
                {
                    Name = "Glow",
                    Position = new Vector3(0f, 0.7f, 0f),
                    LightColor = Color.Color8(255, 140, 44),
                    LightEnergy = 3.2f,
                    OmniRange = 5.5f,
                });
                break;

            case TileKind.Key:
                tile.AddChild(Glyph("Key", Color.Color8(255, 208, 96), radius: 0.34f, energy: 1.4f));
                break;

            case TileKind.Guardian:
                tile.AddChild(Glyph("GuardianSigil", Color.Color8(178, 96, 232), radius: 0.55f, energy: 1.1f));
                break;

            case TileKind.DartTrap:
                tile.AddChild(Glyph("DartMechanism", Color.Color8(214, 66, 58), radius: 0.22f, energy: 0.9f));
                break;

            case TileKind.SpikeTrap:
                AddSpikes(tile);
                break;

            case TileKind.Ruins:
                AddRubble(tile, cell);
                break;
        }
    }

    /// <summary>A flat glowing disc set into the floor.</summary>
    private static MeshInstance3D Glyph(string name, Color colour, float radius, float energy) => new()
    {
        Name = name,
        Mesh = new CylinderMesh
        {
            TopRadius = radius,
            BottomRadius = radius,
            Height = 0.04f,
            RadialSegments = 24,
        },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = colour,
            EmissionEnabled = true,
            Emission = colour,
            EmissionEnergyMultiplier = energy,
        },
        Position = new Vector3(0f, 0.02f, 0f),
    };

    private static void AddSpikes(Node3D tile)
    {
        var material = new StandardMaterial3D { AlbedoColor = Color.Color8(196, 188, 176), Metallic = 0.6f };
        var spacing = 0.34f;

        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++)
            {
                tile.AddChild(new MeshInstance3D
                {
                    Name = $"Spike_{x}_{z}",
                    Mesh = new CylinderMesh
                    {
                        TopRadius = 0f,
                        BottomRadius = 0.07f,
                        Height = 0.3f,
                        RadialSegments = 6,
                    },
                    MaterialOverride = material,
                    Position = new Vector3(x * spacing, 0.15f, z * spacing),
                });
            }
        }
    }

    /// <summary>
    /// Rubble scattered from the cell's own coordinates, so a given tile looks the
    /// same every time the board is redrawn.
    /// </summary>
    private static void AddRubble(Node3D tile, Cell cell)
    {
        var material = new StandardMaterial3D { AlbedoColor = Color.Color8(72, 62, 58), Roughness = 1f };
        var scatter = new Rng((ulong)(cell.Column * 73856093 ^ cell.Row * 19349663));

        for (var block = 0; block < 7; block++)
        {
            var size = 0.22f + scatter.NextFloat() * 0.3f;

            tile.AddChild(new MeshInstance3D
            {
                Name = $"Rubble_{block}",
                Mesh = new BoxMesh { Size = new Vector3(size, size, size) },
                MaterialOverride = material,
                Position = new Vector3(
                    (scatter.NextFloat() - 0.5f) * 1.2f,
                    size / 2f,
                    (scatter.NextFloat() - 0.5f) * 1.2f),
                Rotation = new Vector3(0f, scatter.NextFloat() * Mathf.Tau, 0f),
            });
        }
    }

    private StandardMaterial3D WallMaterial() =>
        _wallMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = Color.Color8(44, 32, 29),
            Roughness = 1f,
        };

    private StandardMaterial3D FloorMaterial(TileKind kind)
    {
        if (!_floorMaterials.TryGetValue(kind, out var material))
        {
            material = BuildFloorMaterial(kind);
            _floorMaterials[kind] = material;
        }

        return material;
    }

    private static StandardMaterial3D BuildFloorMaterial(TileKind kind)
    {
        if (kind == TileKind.Lava)
        {
            var molten = Color.Color8(226, 88, 24);
            return new StandardMaterial3D
            {
                AlbedoColor = molten,
                EmissionEnabled = true,
                Emission = Color.Color8(255, 132, 40),
                EmissionEnergyMultiplier = 1.7f,
                Roughness = 0.5f,
            };
        }

        return new StandardMaterial3D { AlbedoColor = FloorColour(kind), Roughness = 1f };
    }

    private static Color FloorColour(TileKind kind) => kind switch
    {
        TileKind.Entrance => Color.Color8(150, 128, 96),
        TileKind.Lateral => Color.Color8(88, 60, 48),
        TileKind.Guardian => Color.Color8(70, 52, 74),
        TileKind.Ruins => Color.Color8(64, 54, 50),
        TileKind.Bridge => Color.Color8(96, 62, 42),
        TileKind.Sanctuary => Color.Color8(140, 68, 30),
        _ => Color.Color8(82, 58, 46),
    };

    /// <summary>
    /// A tiny generator for decoration only. Deliberately separate from the rules
    /// engine's <see cref="SubTerra.Core.Randomness.Rng"/>: scattering rubble must
    /// never consume rolls the game is counting on.
    /// </summary>
    private sealed class Rng(ulong seed)
    {
        private ulong _state = seed | 1UL;

        public float NextFloat()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return (_state >> 40) / (float)(1 << 24);
        }
    }
}
