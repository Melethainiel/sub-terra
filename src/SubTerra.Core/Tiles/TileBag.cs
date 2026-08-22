using SubTerra.Core.Randomness;

namespace SubTerra.Core.Tiles;

/// <summary>
/// The cloth bag the Temple tiles are drawn from. Tiles come out in no particular
/// order, and some rules put them back: the Archaeologist's <em>Erudite</em> returns
/// the tile she did not keep, and a tile that would seal the Temple off goes back in
/// before another is drawn.
/// </summary>
public sealed class TileBag
{
    private readonly List<TileDefinition> _remaining;

    public TileBag(IEnumerable<TileDefinition> tiles) => _remaining = [.. tiles];

    /// <summary>The thirty Temple tiles of a new game.</summary>
    public static TileBag Temple() => new(TileCatalog.CreateBag());

    public int Count => _remaining.Count;

    public bool IsEmpty => _remaining.Count == 0;

    /// <summary>What is still in the bag. Order carries no meaning.</summary>
    public IReadOnlyList<TileDefinition> Remaining => _remaining;

    /// <summary>Takes one tile out at random.</summary>
    /// <exception cref="InvalidOperationException">The bag is empty.</exception>
    public TileDefinition Draw(Rng rng)
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException(
                "The tile bag is empty — the Sanctuary should have been placed by now.");
        }

        var index = rng.Next(_remaining.Count);
        var tile = _remaining[index];

        // Swap with the last entry rather than shifting the tail along: nothing
        // depends on the order of a bag.
        _remaining[index] = _remaining[^1];
        _remaining.RemoveAt(_remaining.Count - 1);

        return tile;
    }

    public void Return(TileDefinition tile) => _remaining.Add(tile);
}
