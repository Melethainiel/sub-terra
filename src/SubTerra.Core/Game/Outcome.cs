namespace SubTerra.Core.Game;

/// <summary>
/// How the expedition ended. The rank counts the explorers who did not make it —
/// escaping with the Artefact is the only thing that separates a win from a loss.
/// </summary>
public enum Outcome
{
    InProgress,

    /// <summary>Everyone came back.</summary>
    Legendary,

    /// <summary>One explorer lost.</summary>
    Gold,

    /// <summary>Two explorers lost.</summary>
    Silver,

    /// <summary>Three explorers lost.</summary>
    Bronze,

    /// <summary>The Artefact never left the mountain.</summary>
    ForgottenForever,
}
