using Godot;
using SubTerra.Core.Game;

namespace SubTerra.Presentation;

/// <summary>
/// The rules at a glance, on F1: the goal, a turn, what the actions cost, the six
/// faces of the Peril die, and the keys. A reminder for the table, not the rulebook.
/// </summary>
public partial class Hud
{
    private Control? _help;

    private static readonly (PerilFace Face, string Effect)[] PerilEffects =
    [
        (PerilFace.Stumble, "Qui s'est dépassé ce tour perd 1 ♥ de plus."),
        (PerilFace.Lava, "Tous ceux qui sont sur la Lave perdent 1 ♥ ; au bout de la piste, elle réveille le volcan."),
        (PerilFace.Collapse, "On lance le dé : la Ruine de ce chiffre s'effondre (5 ♥ à ceux dessus)."),
        (PerilFace.Trap, "Les pièges de la tuile de l'Explorateur actif, et les fléchettes voisines, se déclenchent."),
        (PerilFace.WakeGuardian, "Un Gardien apparaît sur la tuile Gardien la plus proche."),
        (PerilFace.ActivateGuardians, "Tous les Gardiens s'activent une fois."),
    ];

    /// <summary>Opens the reminder card, or puts it away.</summary>
    public void ToggleHelp()
    {
        if (_help is not null)
        {
            _help.QueueFree();
            _help = null;
            return;
        }

        var veil = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        veil.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        veil.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                ToggleHelp();
            }
        };

        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        veil.AddChild(centre);

        var card = new PanelContainer { Material = Grain, MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", WithMargins(Paper.CardStyle(borderWidth: 3f, radius: 14f), 28, 20));
        centre.AddChild(card);

        var columns = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        columns.AddThemeConstantOverride("separation", 28);
        card.AddChild(columns);

        var left = Column(columns);
        Heading(left, "Le but");
        Line(left, "Poser toutes les tuiles du sac : le Sanctuaire apparaît au bout du Temple.");
        Line(left, "Y déposer les trois Clés : l'Artefact est libéré — et la malédiction tombe.");
        Line(left, "Ressortir par l'Entrée avec l'Artefact avant que le volcan n'engloutisse tout.");
        Heading(left, "Un tour");
        Line(left, "Chaque Explorateur, à partir du Chef : 2 PA d'actions, puis un dé de Péril (deux si maudits).");
        Line(left, "Fin de manche : les Gardiens s'activent deux fois, l'Éruption avance (double si maudits).");
        Heading(left, "Les actions");

        foreach (var card2 in ActionCards.All)
        {
            Line(left, $"{card2.Icon}  {card2.Name} — {card2.Cost}   [{card2.Key}]");
        }

        Line(left, "✦  Capacités de l'Explorateur — [1] et [2]");

        var right = Column(columns);
        Heading(right, "Le dé de Péril");

        foreach (var (face, effect) in PerilEffects)
        {
            Line(right, $"{Glyph(face)}  {face.Name()} — {effect}");
        }

        Heading(right, "Au plateau");
        Line(right, "Clic : jouer la carte en main sur une case.   Maj+clic : révéler sans entrer.   Ctrl+clic : creuser.");
        Line(right, "Molette ou R : tourner la tuile, ou changer de case, avant de trancher.");
        Line(right, "Vue du dessus : molette ou + / − pour zoomer, flèches ou clic du milieu pour déplacer la vue.");
        Line(right, "V : vue à la première personne ↔ vue du dessus.   Échap ou clic droit : annuler — sinon, le menu (sauvegarder, quitter).");
        Line(right, "F1 : cette aide.");

        AddChild(veil);
        _help = veil;
    }

    private static VBoxContainer Column(Container parent)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(420f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        parent.AddChild(column);
        return column;
    }

    private static void Heading(Container column, string text)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", Paper.Sienna);
        column.AddChild(label);
    }

    private static void Line(Container column, string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", Ink);
        column.AddChild(label);
    }
}
