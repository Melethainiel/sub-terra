using Godot;
using FileAccess = Godot.FileAccess;
using SubTerra.Core.Game;

namespace SubTerra.App;

/// <summary>
/// The game in progress, written after every command it settles, so that closing the
/// window — on purpose or not — loses nothing. A save is only a <see cref="GameRecord"/>:
/// seed, difficulty, party, commands.
/// </summary>
public static class SaveGame
{
    /// <summary>Where saves go: the player's own folder, unless a dev tool points
    /// elsewhere (SUBTERRA_SAVES) so as not to touch it.</summary>
    private static string Folder =>
        OS.GetEnvironment("SUBTERRA_SAVES") is { Length: > 0 } elsewhere ? elsewhere : "user://saves";

    private static string Path => Folder + "/autosave.sav";

    /// <summary>Off for the dev tools that play games by script: they must not write over
    /// the player's own save.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>Writes the record in place of the last one — through a temporary file
    /// and a rename, so a crash mid-write leaves the previous save rather than half of one.</summary>
    public static void Write(GameRecord record)
    {
        if (!Enabled)
        {
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(Folder);
        var temporary = Path + ".tmp";

        using (var file = FileAccess.Open(temporary, FileAccess.ModeFlags.Write))
        {
            if (file is null)
            {
                GD.PushWarning($"Sauvegarde impossible : {FileAccess.GetOpenError()}");
                return;
            }

            file.StoreString(record.Encode());
        }

        DirAccess.RenameAbsolute(temporary, Path);
    }

    /// <summary>The saved game, if there is one that still replays.</summary>
    public static GameRecord? Read()
    {
        if (!FileAccess.FileExists(Path))
        {
            return null;
        }

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        var record = GameRecord.Decode(file?.GetAsText());
        return record?.Replay() is not null ? record : null;
    }

    /// <summary>A finished game is not one to resume.</summary>
    public static void Delete()
    {
        if (!Enabled)
        {
            return;
        }

        if (FileAccess.FileExists(Path))
        {
            DirAccess.RemoveAbsolute(Path);
        }
    }
}
