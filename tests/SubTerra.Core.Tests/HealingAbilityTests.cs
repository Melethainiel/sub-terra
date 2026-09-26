using SubTerra.Core.Board;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Core.Randomness;
using SubTerra.Core.Setup;
using SubTerra.Core.Tiles;

namespace SubTerra.Core.Tests;

/// <summary>Guérir and Ranimer: healing someone else, from a distance, in sight.</summary>
public class HealingAbilityTests
{
    private static readonly Cell Start = TempleSetup.EntranceCrossing;

    private static PlacedTile Tile(Sides openSides)
    {
        var layout = TileShapeExtensions.Match(openSides)
            ?? throw new ArgumentException($"No tile shape lays out {openSides}.", nameof(openSides));

        return new PlacedTile(new TileDefinition("test", TileKind.Normal, layout.Shape), layout.Rotation);
    }

    /// <summary>
    /// The setup board, plus a bend south of the Entrance that turns east: the tile at
    /// its end is two steps away but out of any straight line from the Entrance.
    /// </summary>
    private static TempleBoard BoardWithABend()
    {
        var board = TempleSetup.CreateBoard();
        board.PlaceFixed(new Cell(Start.Column, 1), Tile(Sides.North | Sides.East));
        board.PlaceFixed(new Cell(Start.Column + 1, 1), Tile(Sides.West));
        return board;
    }

    /// <summary>
    /// The patient plays first and stands on <paramref name="patientCell"/>; the
    /// healer, holding <paramref name="healerSheet"/>, stands on the Entrance.
    /// </summary>
    private static GameState Game(string healerSheet, Cell patientCell, int patientHealth = 5, TempleBoard? board = null)
    {
        var sheet = ExplorerRoster.Find(healerSheet)!;

        return new GameState(
            [new Explorer(new ExplorerId(0), "Patient", patientHealth, patientCell),
             new Explorer(new ExplorerId(1), sheet.Name, sheet.MaxHealth, Start, sheet)],
            board ?? BoardWithABend(),
            new TileBag([]),
            new Rng(1));
    }

    private static Explorer Patient(GameState game) => game.Explorers[0];

    /// <summary>
    /// The patient spends a heart on each of their next <paramref name="rounds"/>
    /// turns, then the turn comes round to the healer.
    /// </summary>
    private static void Hurt(GameState game, int rounds)
    {
        for (var round = 0; round < rounds; round++)
        {
            PassUntil(game, Patient(game).Id);
            game.Play(new Overexert());
            game.Play(new EndTurn());
        }

        PassUntil(game, game.Explorers[1].Id);
    }

    private static void PassUntil(GameState game, ExplorerId who)
    {
        for (var turn = 0; turn < 10 && game.CurrentExplorer.Id != who; turn++)
        {
            game.Play(new EndTurn());
        }

        Assert.Equal(who, game.CurrentExplorer.Id);
    }

    private static int Regained(CommandResult result, ExplorerId who) =>
        result.Events.OfType<HealthRegained>().Single(e => e.Explorer == who).Amount;

    [Fact]
    public void GuerirGivesTwoHeartsToSomeoneInSightTwoTilesAway()
    {
        var game = Game("guerisseuse", patientCell: new Cell(Start.Column - 2, 0));
        Hurt(game, rounds: 2);
        var before = Patient(game).Health;

        var result = game.Play(new UseAbility(AbilityIds.Guerir, Patient(game).Id));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Contains(new AbilityUsed(game.Explorers[1].Id, AbilityIds.Guerir), result.Events);
        Assert.Equal(Math.Min(2, 5 - before), Regained(result, Patient(game).Id));
        Assert.Equal(GameState.ActionsPerTurn - 1, game.ActionPoints);
    }

    [Fact]
    public void GuerirDoesNotReachThreeTilesAway()
    {
        var game = Game("guerisseuse", patientCell: new Cell(Start.Column - 3, 0));
        Hurt(game, rounds: 1);

        Assert.Contains("deux tuiles", game.Play(new UseAbility(AbilityIds.Guerir, Patient(game).Id)).Rejection);
    }

    [Fact]
    public void GuerirDoesNotSeeRoundACorner()
    {
        var game = Game("guerisseuse", patientCell: new Cell(Start.Column + 1, 1));
        Hurt(game, rounds: 1);

        Assert.Contains("visible", game.Play(new UseAbility(AbilityIds.Guerir, Patient(game).Id)).Rejection);
    }

    [Fact]
    public void GuerirIsForSomeoneElse()
    {
        var game = Game("guerisseuse", patientCell: Start);
        PassUntil(game, game.Explorers[1].Id);
        game.Play(new Overexert());

        Assert.Contains("autre", game.Play(new UseAbility(AbilityIds.Guerir, game.CurrentExplorer.Id)).Rejection);
    }

    [Fact]
    public void AnAbilityOffSomeoneElsesSheetIsRefused()
    {
        var game = Game("pretre", patientCell: Start);
        Hurt(game, rounds: 1);

        var result = game.Play(new UseAbility(AbilityIds.Guerir, Patient(game).Id));

        Assert.Contains("n'a pas cette capacité", result.Rejection);
    }

    [Fact]
    public void RanimerCostsThreeActionsSoThePriestMustOverexert()
    {
        var game = Game("pretre", patientCell: new Cell(Start.Column - 3, 0), patientHealth: 1);
        Hurt(game, rounds: 1);
        Assert.True(Patient(game).IsDown);

        Assert.Contains("3 points", game.Play(new UseAbility(AbilityIds.Ranimer, Patient(game).Id)).Rejection);

        game.Play(new Overexert());
        var result = game.Play(new UseAbility(AbilityIds.Ranimer, Patient(game).Id));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(0, game.ActionPoints);
        // Down: a single heart, and back on their feet — however far away they lie.
        Assert.Equal(1, Regained(result, Patient(game).Id));
        Assert.Contains(new ExplorerStoodUp(Patient(game).Id), result.Events);
    }

    [Fact]
    public void RanimerGivesMoreToSomeoneStillStanding()
    {
        var game = Game("pretre", patientCell: new Cell(Start.Column + 3, 0));
        Hurt(game, rounds: 2);
        var missing = 5 - Patient(game).Health;
        Assert.False(Patient(game).IsDown);

        game.Play(new Overexert());
        var result = game.Play(new UseAbility(AbilityIds.Ranimer, Patient(game).Id));

        Assert.True(result.Accepted, result.Rejection);
        Assert.Equal(Math.Min(3, missing), Regained(result, Patient(game).Id));
    }

    [Fact]
    public void HealTargetsNamesWhoEachHealCanReach()
    {
        var game = Game("guerisseuse", patientCell: new Cell(Start.Column - 1, 0));
        Hurt(game, rounds: 1);

        Assert.Equal([Patient(game).Id], game.HealTargets(AbilityIds.Guerir));
        // Plain Soigner stays on one's own tile.
        Assert.DoesNotContain(Patient(game).Id, game.HealTargets());
    }
}
