using Godot;

namespace SubTerra.App;

/// <summary>Entry point node. Owns nothing yet beyond proving the toolchain works.</summary>
public partial class AppRoot : Node3D
{
    public override void _Ready()
    {
        GD.Print($"Sub Terra II — Godot {Engine.GetVersionInfo()["string"]}");
    }
}
