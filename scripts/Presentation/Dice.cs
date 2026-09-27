using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// Dice thrown onto the table. The engine has already rolled — it is the authority,
/// and a replay must land the same — so nothing here is physics: the die follows a
/// scripted throw that looks free, tumbling and bouncing, while its turning is made
/// to settle exactly on the face the engine rolled.
/// </summary>
public static class Dice
{
    public const string D6 = "res://resources/models/props/die_d6.glb";

    public const string Peril = "res://resources/models/props/die_peril.glb";

    /// <summary>
    /// Across, on the table: a 16 mm die beside a 28 mm miniature is over half its
    /// height, and seen from above the whole temple is in frame — so, 90 cm.
    /// </summary>
    public const float Size = 0.9f;

    /// <summary>From leaving the hand to lying still.</summary>
    public const float ThrowTime = 1.15f;

    /// <summary>How long it lies there, read, before it is picked back up.</summary>
    private const float RestTime = 1.4f;

    /// <summary>
    /// Which way up each face is, in the model — the contract with
    /// tools/blender/props.py (Blender's +Z is Godot's +Y, its -Y is Godot's +Z).
    /// </summary>
    public static Vector3 FaceOf(int face) => face switch
    {
        1 => Vector3.Up,
        6 => Vector3.Down,
        2 => Vector3.Right,
        5 => Vector3.Left,
        3 => Vector3.Back,
        _ => Vector3.Forward,
    };

    public static Vector3 FaceOf(PerilFace face) => face switch
    {
        PerilFace.Stumble => Vector3.Up,
        PerilFace.Lava => Vector3.Down,
        PerilFace.Collapse => Vector3.Right,
        PerilFace.Trap => Vector3.Left,
        PerilFace.WakeGuardian => Vector3.Back,
        _ => Vector3.Forward,
    };

    /// <summary>
    /// Throws a die onto <paramref name="landing"/>, <paramref name="at"/> seconds into
    /// the playback, to come to rest with <paramref name="up"/> — one of its faces, in
    /// its own frame — facing the ceiling. <paramref name="seed"/> varies the throw.
    /// </summary>
    /// <param name="toward">Horizontal direction the throw comes from — the players'
    /// side of the table, wherever the camera stands.</param>
    /// <param name="size">Across, on the table; <see cref="Size"/> unless the view calls
    /// for another — through an Explorer's eyes the die is seen at their scale.</param>
    public static void Throw(Node3D parent, string model, Vector3 up, Vector3 landing, Vector3 toward, float at, int seed, float size = Size)
    {
        var die = Miniature.Piece(model);
        die.Scale = Vector3.One * size;
        die.Visible = false;
        parent.AddChild(die);

        // Its own light, so a dark die still stands out of dark rock: embers for the
        // Peril die, a warm white for the other.
        die.AddChild(new OmniLight3D
        {
            LightColor = model == Peril ? new Color(1f, 0.45f, 0.12f) : new Color(1f, 0.9f, 0.75f),
            LightEnergy = model == Peril ? 1.6f : 0.9f,
            OmniRange = 2.2f / size,
            Position = new Vector3(0f, 1.2f, 0f),
        });

        var rng = new RandomNumberGenerator { Seed = (ulong)seed };

        // Turned so the rolled face looks up, then spun about the vertical at random.
        var settle = new Quaternion(Vector3.Up, rng.RandfRange(0f, Mathf.Tau)) * Lift(up);
        var axis = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(-1f, 1f)).Normalized();
        var spin = Mathf.Tau * rng.RandfRange(2.5f, 3.5f);

        // From the players' side of the table, a little to one side, down onto it.
        var side = toward.Rotated(Vector3.Up, rng.RandfRange(0.45f, 0.75f) * (rng.Randf() < 0.5f ? -1f : 1f));
        var from = landing + side * rng.RandfRange(1.5f, 2f) * (size / Size);
        var rest = size / 2f;

        void Pose(float t)
        {
            var glide = 1f - (1f - t) * (1f - t);
            var position = from.Lerp(landing, glide);
            position.Y = rest + Height(t);
            die.Position = position;

            // The spin runs out as the die comes to rest; at the end it is exactly `settle`.
            var remaining = 1f - (1f - Mathf.Pow(1f - t, 3f));
            die.Quaternion = settle * new Quaternion(axis, spin * remaining);
        }

        Pose(0f);

        var tween = die.CreateTween();
        tween.TweenInterval(at);
        tween.TweenCallback(Callable.From(() => die.Visible = true));
        tween.TweenMethod(Callable.From<float>(Pose), 0f, 1f, ThrowTime);
        tween.TweenInterval(RestTime);
        tween.TweenProperty(die, "scale", Vector3.Zero, 0.25f).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(die.QueueFree));
    }

    /// <summary>The turn that brings a face from pointing <paramref name="up"/> to pointing at the ceiling.</summary>
    private static Quaternion Lift(Vector3 up) =>
        up.Dot(Vector3.Up) < -0.99f
            ? new Quaternion(Vector3.Right, Mathf.Pi)
            : up.Dot(Vector3.Up) > 0.99f
                ? Quaternion.Identity
                : new Quaternion(up, Vector3.Up);

    /// <summary>A drop from the hand, then two bounces dying away.</summary>
    private static float Height(float t) => t switch
    {
        < 0.45f => 2.2f * (1f - (t / 0.45f) * (t / 0.45f)),
        < 0.75f => Bounce((t - 0.45f) / 0.3f, 0.45f),
        < 0.92f => Bounce((t - 0.75f) / 0.17f, 0.12f),
        _ => 0f,
    };

    private static float Bounce(float u, float height) => 4f * height * u * (1f - u);
}
