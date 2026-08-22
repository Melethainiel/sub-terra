using SubTerra.Core.Board;

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
public sealed class Explorer(ExplorerId id, string name, int maxHealth, Cell cell)
{
    public ExplorerId Id { get; } = id;

    public string Name { get; } = name;

    public int MaxHealth { get; } = maxHealth;

    public int Health { get; private set; } = maxHealth;

    public Cell Cell { get; internal set; } = cell;

    /// <summary>Down, not dead: a single heart puts them back on their feet.</summary>
    public bool IsDown => Health == 0;

    /// <summary>What they are carrying, if anything. Never more than one thing.</summary>
    public ItemKind? Carried { get; internal set; }

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
