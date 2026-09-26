namespace SubTerra.Core.Board;

/// <summary>
/// The tiles on the table and how they join up. Purely geometric: whose meeple
/// stands where, and which markers sit on a tile, belong to the game state.
/// </summary>
public sealed class TempleBoard
{
    private readonly Dictionary<Cell, PlacedTile> _tiles = [];
    private readonly HashSet<CellEdge> _demolished = [];

    public TempleBoard(BoardBounds? bounds = null) => Bounds = bounds ?? BoardBounds.Default;

    public BoardBounds Bounds { get; }

    public IReadOnlyDictionary<Cell, PlacedTile> Tiles => _tiles;

    public PlacedTile? TileAt(Cell cell) => _tiles.GetValueOrDefault(cell);

    public bool IsOccupied(Cell cell) => _tiles.ContainsKey(cell);

    /// <summary>
    /// Places a tile without checking the drawing rules. Used for the fixed setup
    /// pieces (Entrance, Laterals), which sit outside <see cref="Bounds"/>.
    /// </summary>
    public void PlaceFixed(Cell cell, PlacedTile tile)
    {
        if (_tiles.ContainsKey(cell))
        {
            throw new InvalidOperationException($"Cell {cell} already holds a tile.");
        }

        _tiles[cell] = tile;
    }

    /// <summary>
    /// Whether a revealed tile may legally land on <paramref name="cell"/> in this
    /// rotation: inside the bounds, on empty ground, and connecting to the tile the
    /// Explorer is revealing from.
    /// </summary>
    public bool CanPlace(Cell cell, PlacedTile tile, Cell from)
    {
        if (!Bounds.Contains(cell) || IsOccupied(cell) || !IsOccupied(from))
        {
            return false;
        }

        return Connects(from, cell, tile);
    }

    public void Place(Cell cell, PlacedTile tile, Cell from)
    {
        if (!CanPlace(cell, tile, from))
        {
            throw new InvalidOperationException(
                $"{tile.Kind} cannot be placed on {cell} from {from} at rotation {tile.Rotation}.");
        }

        _tiles[cell] = tile;
    }

    /// <summary>
    /// Every way out of the placed tiles that has nothing on the far side yet — an
    /// open side, or a demolished wall, giving onto empty ground inside the bounds.
    /// Revealing means picking one of these.
    /// </summary>
    public IEnumerable<TempleExit> OpenExits()
    {
        // Ordered rather than left in dictionary order: anything that picks among the
        // exits must do so reproducibly for a game to replay from its seed.
        foreach (var (cell, tile) in _tiles.OrderBy(entry => entry.Key.Row).ThenBy(entry => entry.Key.Column))
        {
            foreach (var direction in DirectionExtensions.All)
            {
                var target = cell.Neighbour(direction);

                if (!Bounds.Contains(target) || IsOccupied(target))
                {
                    continue;
                }

                if (tile.IsOpen(direction) || IsDemolished(cell, target))
                {
                    yield return new TempleExit(cell, direction);
                }
            }
        }
    }

    /// <summary>Knocks down the wall between two adjacent cells (Sapper's Demolition).</summary>
    public void Demolish(Cell a, Cell b) => _demolished.Add(new CellEdge(a, b));

    public bool IsDemolished(Cell a, Cell b) => _demolished.Contains(new CellEdge(a, b));

    /// <summary>
    /// Two tiles are connected when they are adjacent and no wall separates them.
    /// An open side laid against a neighbour's wall does not connect: the wall counts.
    /// </summary>
    public bool AreConnected(Cell a, Cell b)
    {
        var tile = TileAt(b);
        return tile is not null && Connects(a, b, tile);
    }

    public IEnumerable<Cell> ConnectedNeighbours(Cell cell)
    {
        foreach (var direction in DirectionExtensions.All)
        {
            var neighbour = cell.Neighbour(direction);
            if (AreConnected(cell, neighbour))
            {
                yield return neighbour;
            }
        }
    }

    /// <summary>
    /// Length in tiles of the shortest path of connected tiles, or <c>null</c> if
    /// <paramref name="target"/> cannot be reached. This is the measure the rules use
    /// for "nearest Guardian tile" and "nearest active Explorer".
    /// </summary>
    public int? Distance(Cell origin, Cell target, Func<Cell, bool>? passable = null)
    {
        if (origin == target)
        {
            return 0;
        }

        var visited = new HashSet<Cell> { origin };
        var frontier = new Queue<(Cell Cell, int Steps)>();
        frontier.Enqueue((origin, 0));

        while (frontier.Count > 0)
        {
            var (cell, steps) = frontier.Dequeue();
            foreach (var neighbour in ConnectedNeighbours(cell))
            {
                if (!visited.Add(neighbour))
                {
                    continue;
                }

                if (neighbour == target)
                {
                    return steps + 1;
                }

                if (passable is null || passable(neighbour))
                {
                    frontier.Enqueue((neighbour, steps + 1));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The way from <paramref name="origin"/> to the nearest tile that satisfies
    /// <paramref name="isTarget"/>, walking only connected tiles, or <c>null</c> if
    /// none can be reached. The origin is not included; the target is, even if
    /// <paramref name="passable"/> would refuse to route through it.
    /// </summary>
    /// <remarks>
    /// Ties are broken by the order <see cref="ConnectedNeighbours"/> yields, which is
    /// north, east, south, west — deterministic, so a replay follows the same path.
    /// </remarks>
    public IReadOnlyList<Cell>? ShortestPath(
        Cell origin,
        Func<Cell, bool> isTarget,
        Func<Cell, bool>? passable = null)
    {
        var cameFrom = new Dictionary<Cell, Cell>();
        var visited = new HashSet<Cell> { origin };
        var frontier = new Queue<Cell>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue();

            foreach (var neighbour in ConnectedNeighbours(cell))
            {
                if (!visited.Add(neighbour))
                {
                    continue;
                }

                cameFrom[neighbour] = cell;

                if (isTarget(neighbour))
                {
                    return Retrace(cameFrom, origin, neighbour);
                }

                if (passable is null || passable(neighbour))
                {
                    frontier.Enqueue(neighbour);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The tiles one sees from <paramref name="origin"/> in a straight line, at most
    /// <paramref name="range"/> tiles away: the origin itself first, then each of the
    /// four lines outward in north, east, south, west order, nearest first.
    /// </summary>
    /// <remarks>
    /// A line runs on while each tile connects to the next, so a wall stops it and a
    /// demolished one lets it through. A tile <paramref name="opaque"/> names — one
    /// buried under rubble — is not seen, and nothing beyond it is either. The origin
    /// is always seen: whoever stands there is looking out of it, whatever lies on it.
    /// </remarks>
    public IEnumerable<Cell> VisibleFrom(Cell origin, int range, Func<Cell, bool>? opaque = null)
    {
        yield return origin;

        foreach (var direction in DirectionExtensions.All)
        {
            var cell = origin;

            for (var distance = 1; distance <= range; distance++)
            {
                var next = cell.Neighbour(direction);

                if (!AreConnected(cell, next) || (opaque is not null && opaque(next)))
                {
                    break;
                }

                yield return next;
                cell = next;
            }
        }
    }

    private static List<Cell> Retrace(Dictionary<Cell, Cell> cameFrom, Cell origin, Cell target)
    {
        var path = new List<Cell>();

        for (var cell = target; cell != origin; cell = cameFrom[cell])
        {
            path.Add(cell);
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Whether <paramref name="tile"/>, sitting on <paramref name="cell"/>, would join
    /// up with whatever occupies <paramref name="from"/>.
    /// </summary>
    private bool Connects(Cell from, Cell cell, PlacedTile tile)
    {
        if (from.DirectionTo(cell) is not { } direction)
        {
            return false;
        }

        if (IsDemolished(from, cell))
        {
            return true;
        }

        var source = TileAt(from);
        return source is not null
            && source.IsOpen(direction)
            && tile.IsOpen(direction.Opposite());
    }
}
