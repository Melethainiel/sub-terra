namespace SubTerra.Core.Tiles;

/// <summary>
/// Every Temple tile is double-sided. Flipping to <see cref="Volcano"/> kills the
/// Explorers standing on it and seals the cell for good.
/// </summary>
public enum TileFace
{
    Temple,
    Volcano,
}
