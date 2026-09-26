namespace SubTerra.Core.Game;

/// <summary>A common action an ability lets an Explorer take without paying for it.</summary>
public enum GrantedAction
{
    Move,
    Reveal,
    Dig,
}

/// <summary>
/// What Illuminer, Sprinter or Excaver bought: that many of one common action, free
/// of action points. They are meant to be taken there and then — playing anything
/// else, or ending the turn, lets whatever is left go.
/// </summary>
public readonly record struct GrantedActions(GrantedAction Action, int Remaining);
