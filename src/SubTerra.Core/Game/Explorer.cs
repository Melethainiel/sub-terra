using SubTerra.Core.Board;
using SubTerra.Core.Explorers;

namespace SubTerra.Core.Game;

public readonly record struct ExplorerId(int Value)
{
    public override string ToString() => $"E{Value}";
}

/// <summary>
/// One explorer in the temple. Their sheet gives a maximum of hearts; running out
/// does not kill them, it puts them on the floor, where they can only crawl until
/// someone heals them.
/// </summary>
public sealed class Explorer(ExplorerId id, string name, int maxHealth, Cell cell, ExplorerSheet? sheet = null)
{
    public ExplorerId Id { get; } = id;

    public string Name { get; } = name;

    /// <summary>
    /// The card they were chosen from, when there is one. Tests deal explorers by
    /// hand and do not need a sheet; a real party always has one.
    /// </summary>
    public ExplorerSheet? Sheet { get; } = sheet;

    public int MaxHealth { get; } = maxHealth;

    public int Health { get; private set; } = maxHealth;

    public Cell Cell { get; internal set; } = cell;

    /// <summary>Down, not dead: a single heart puts them back on their feet.</summary>
    public bool IsDown => Health == 0;

    /// <summary>What they are carrying, if anything. Never more than one thing.</summary>
    public ItemKind? Carried { get; internal set; }

    /// <summary>Out of the temple and safe. They cannot go back in.</summary>
    public bool HasEscaped { get; internal set; }

    /// <summary>Caught by the lava. Unlike being down, there is no coming back.</summary>
    public bool IsDead { get; internal set; }

    /// <summary>Whether they can still act at all.</summary>
    public bool IsPlaying => !HasEscaped && !IsDead;

    /// <summary>Returns how many hearts were actually lost.</summary>
    internal int Wound(int amount)
    {
        var lost = Math.Min(amount, Health);
        Health -= lost;
        return lost;
    }

    /// <summary>Returns how many hearts were actually regained.</summary>
    internal int Heal(int amount)
    {
        var gained = Math.Min(amount, MaxHealth - Health);
        Health += gained;
        return gained;
    }

    public override string ToString() => $"{Name} ({Health}/{MaxHealth})";
}
