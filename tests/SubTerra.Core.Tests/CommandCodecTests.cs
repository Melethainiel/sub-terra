using SubTerra.Core.Board;
using SubTerra.Core.Game;

namespace SubTerra.Core.Tests;

/// <summary>
/// Every command has to survive the trip down the wire unchanged, and nothing else
/// may come back up it.
/// </summary>
public class CommandCodecTests
{
    public static TheoryData<GameCommand> EveryCommand =>
    [
        new Move(Direction.South),
        new Run([Direction.North, Direction.East, Direction.East]),
        new Reveal(Direction.West),
        new Explore(Direction.East),
        new Heal(new ExplorerId(3)),
        new UseAbility("guerir", new ExplorerId(1)),
        new UseAbility("se-preparer"),
        new UseAbility("ordonner", new ExplorerId(2), Direction.South),
        new UseAbility("rechercher", Direction: Direction.East, Cell: new Cell(-1, 2)),
        new PickUpItem(ItemKind.Artefact),
        new DropItem(),
        new Attack(),
        new Dig(new Cell(-2, 4)),
        new Overexert(),
        new EndTurn(),
        new Decide(2),
    ];

    [Theory]
    [MemberData(nameof(EveryCommand))]
    public void ACommandComesBackTheSameOnTheOtherSide(GameCommand command)
    {
        var line = CommandCodec.Encode(command);
        var back = CommandCodec.Decode(line);

        Assert.NotNull(back);
        Assert.Equal(command.GetType(), back.GetType());

        // The wire form is what has to be stable — Run carries a list, and two equal
        // lists are not the same object.
        Assert.Equal(line, CommandCodec.Encode(back));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fly:North")]
    [InlineData("move")]
    [InlineData("move:Upwards")]
    [InlineData("move:South:2")]
    // A reveal names an exit and nothing else: how the tile is turned is settled
    // afterwards, by a Decide, once everyone has seen what came out of the bag.
    [InlineData("reveal:North:0")]
    [InlineData("run:")]
    [InlineData("dig:3")]
    [InlineData("dig:trois,4")]
    [InlineData("ability")]
    [InlineData("ability:Guérir:e1")]
    [InlineData("ability:guerir:1")]
    [InlineData("ability:guerir:e1:e2")]
    [InlineData("ability:ordonner:e1:dUpwards")]
    [InlineData("ability:rechercher:c1")]
    [InlineData("ability:rechercher:c1,2:c3,4")]
    [InlineData("heal:moi")]
    [InlineData("pickup:Sandwich")]
    [InlineData("reveal:North:gauche")]
    public void RubbishOnTheWireIsRefusedRatherThanGuessedAt(string line) =>
        Assert.Null(CommandCodec.Decode(line));
}
