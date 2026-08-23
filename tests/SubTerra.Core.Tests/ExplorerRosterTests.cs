using SubTerra.Core.Explorers;
using SubTerra.Core.Game;

namespace SubTerra.Core.Tests;

public class ExplorerRosterTests
{
    [Fact]
    public void TheBoxHoldsTenExplorers() => Assert.Equal(10, ExplorerRoster.All.Count);

    [Fact]
    public void EveryExplorerIsTellableFromTheOthers()
    {
        Assert.Equal(ExplorerRoster.All.Count, ExplorerRoster.All.Select(sheet => sheet.Id).Distinct().Count());
        Assert.Equal(ExplorerRoster.All.Count, ExplorerRoster.All.Select(sheet => sheet.Name).Distinct().Count());
    }

    [Theory]
    [InlineData("guide", 3)]
    [InlineData("gredin", 3)]
    [InlineData("guerisseuse", 3)]
    [InlineData("combattante", 7)]
    [InlineData("pretre", 5)]
    public void TheHeartsAreTheOnesPrintedOnTheCard(string id, int hearts) =>
        Assert.Equal(hearts, ExplorerRoster.Find(id)!.MaxHealth);

    [Fact]
    public void EveryDomainIsRepresented() =>
        Assert.Equal(
            Enum.GetValues<Domain>().Length,
            ExplorerRoster.All.Select(sheet => sheet.Domain).Distinct().Count());

    [Fact]
    public void SomebodyWhoIsNotInTheBoxIsNotFound() => Assert.Null(ExplorerRoster.Find("le-plombier"));

    [Fact]
    public void APartyKeepsItsSheetsAndTheirHearts()
    {
        var party = new[] { "guide", "combattante", "pretre" }.Select(id => ExplorerRoster.Find(id)!).ToList();

        var game = GameState.NewGame(party, seed: 1);

        Assert.Equal(["Le Guide", "La Combattante chevronnée", "Le Prêtre"],
            game.Explorers.Select(explorer => explorer.Name));
        Assert.Equal([3, 7, 5], game.Explorers.Select(explorer => explorer.MaxHealth));
        Assert.Equal("agile", game.Explorers[0].Sheet!.First.Id);

        // The party is dealt in order, and the first seat holds the medallion.
        Assert.Equal("Le Guide", game.Leader.Name);
    }
}
