using Godot;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;
using SubTerra.Presentation;

namespace SubTerra.App;

/// <summary>
/// Where a party is put together: alone, or with others over the network. The host
/// owns the truth — claims are asked of it, and it hands the agreed party back to
/// everyone before the temple opens. Which door of the accueil it was opened by
/// (<see cref="Session.RequestedMode"/>) decides whether the host/join row shows at
/// all — a solo table has no one to host or join.
/// </summary>
public partial class Lobby : Control
{
    /// <summary>Whether this machine is hosting or trying to join, in Coop mode.
    /// Purely a display choice until a button is actually pressed — <see
    /// cref="Session.LobbyMode.Solo"/> never touches either.</summary>
    private enum NetMode
    {
        Host,
        Join,
    }

    private readonly List<Session.Seat> _party = [];
    private readonly List<ExplorerSheet> _roster = [.. ExplorerRoster.All];

    private Session.LobbyMode _mode;
    private NetMode _netMode = NetMode.Host;
    private int _focus;
    private int _difficultyIndex = (int)Difficulty.Normal;
    private string _seedText = "";
    private string _addressText = "127.0.0.1";

    /// <summary>Whether a link has been opened at all. Before that, everything is local.</summary>
    private bool _online;

    private Control _netModeRow = null!;
    private Label _statusLabel = null!;
    private Control _joinField = null!;
    private LineEdit _addressInput = null!;
    private HBoxContainer _carouselRow = null!;
    private HBoxContainer _dotsRow = null!;
    private VBoxContainer _partyList = null!;
    private Control _difficultyRow = null!;
    private LineEdit _seedInput = null!;
    private Button _startButton = null!;
    private Label _hintLabel = null!;

    public override void _Ready()
    {
        _mode = Session.RequestedMode;
        Audio.Instance?.Menu();
        _seedInput = new LineEdit();
        _addressInput = new LineEdit();

        BuildLayout();

        Multiplayer.PeerConnected += WelcomePeer;
        Multiplayer.PeerDisconnected += SeeOffPeer;
        Multiplayer.ConnectedToServer += OnConnected;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnHostGone;

        // Both branches end by calling Refresh() themselves — there is no bare
        // "nothing has happened yet" state to draw on top of.
        if (_mode == Session.LobbyMode.Solo)
        {
            PlayAlone();
        }
        else
        {
            Host();
        }
    }

    // ---------------------------------------------------------------- the link

    private void PlayAlone()
    {
        Drop();
        _online = false;
        Note("Partie solo — prends au moins trois Explorateurs.");
        Refresh();
    }

    private void Host()
    {
        Drop();

        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(Session.DefaultPort, ExplorerRoster.LargestParty);

        if (error is not Error.Ok)
        {
            Note($"Impossible d'ouvrir le port {Session.DefaultPort} : {error}");
            Refresh();
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        _online = true;
        Note($"En attente de joueurs sur le port {Session.DefaultPort}…");
        Refresh();
    }

    private void Join()
    {
        Drop();

        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(_addressText.Trim(), Session.DefaultPort);

        if (error is not Error.Ok)
        {
            Note($"Connexion impossible : {error}");
            Refresh();
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        _online = true;
        Note($"Connexion à {_addressText.Trim()}…");
        Refresh();
    }

    /// <summary>Closes any existing link, so a second click starts from a clean slate.</summary>
    private void Drop()
    {
        Multiplayer.MultiplayerPeer = null;
        _party.Clear();
        _online = false;
    }

    private bool IsHost => !_online || Multiplayer.IsServer();

    private long Me => _online ? Multiplayer.GetUniqueId() : Session.HostPeer;

    private void WelcomePeer(long peer)
    {
        if (IsHost)
        {
            Note($"Un joueur rejoint la table ({peer}).");
            Publish();
        }
    }

    private void SeeOffPeer(long peer)
    {
        if (!IsHost)
        {
            return;
        }

        // Their Explorers go back on the table rather than staying claimed by a ghost.
        _party.RemoveAll(seat => seat.Peer == peer);
        Note($"Un joueur quitte la table ({peer}).");
        Publish();
    }

    private void OnConnected() => Note("Connecté. Choisis tes Explorateurs.");

    private void OnConnectionFailed()
    {
        Drop();
        Note("L'hôte n'a pas répondu.");
        Refresh();
    }

    private void OnHostGone()
    {
        Drop();
        Note("L'hôte a fermé la table.");
        Refresh();
    }

    // ------------------------------------------------------------- the choices

    private void Take(string sheetId)
    {
        if (IsHost)
        {
            Claim(sheetId, Me);
            return;
        }

        RpcId(Session.HostPeer, nameof(AskClaim), sheetId);
    }

    /// <summary>
    /// A claim, settled by the host. Taking an Explorer you already hold gives them
    /// back — one gesture for both, since a claimed card is either yours or not yours.
    /// </summary>
    private void Claim(string sheetId, long peer)
    {
        if (ExplorerRoster.Find(sheetId) is null)
        {
            return;
        }

        if (_party.FirstOrDefault(seat => seat.SheetId == sheetId) is { } taken)
        {
            if (taken.Peer == peer)
            {
                _party.Remove(taken);
                Publish();
            }

            return;
        }

        if (_party.Count < ExplorerRoster.LargestParty)
        {
            _party.Add(new Session.Seat(sheetId, peer));
            Publish();
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AskClaim(string sheetId) => Claim(sheetId, Multiplayer.GetRemoteSenderId());

    /// <summary>The host's table is the table: it is sent out whole after every change.</summary>
    private void Publish()
    {
        if (_online && IsHost)
        {
            Rpc(nameof(SyncTable), Session.Encode(_party), _difficultyIndex, _seedText);
        }

        Refresh();
    }

    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncTable(string party, int difficulty, string seed)
    {
        _party.Clear();
        _party.AddRange(Session.Decode(party));
        _difficultyIndex = difficulty;
        _seedText = seed;
        Refresh();
    }

    // --------------------------------------------------------------- the start

    private void Start()
    {
        if (_party.Count < ExplorerRoster.SmallestParty)
        {
            Note($"Il faut au moins {ExplorerRoster.SmallestParty} Explorateurs.");
            return;
        }

        var seed = ulong.TryParse(_seedText.Trim(), out var chosen)
            ? chosen
            : (ulong)Random.Shared.NextInt64(1, long.MaxValue);

        if (_online)
        {
            Rpc(nameof(Enter), Session.Encode(_party), _difficultyIndex, seed.ToString());
        }

        Enter(Session.Encode(_party), _difficultyIndex, seed.ToString());
    }

    /// <summary>
    /// Everyone writes down the same table and walks into the same temple. Nothing of
    /// the game itself travels: the same seed and the same party rebuild it locally.
    /// </summary>
    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Enter(string party, int difficulty, string seed)
    {
        Session.Party = Session.Decode(party);
        Session.Difficulty = (Difficulty)difficulty;
        Session.Seed = ulong.TryParse(seed, out var value) ? value : 42;
        Session.IsOnline = _online;
        Session.IsHost = IsHost;
        Session.LocalPeer = Me;

        GetTree().ChangeSceneToFile("res://scenes/app/Main.tscn");
    }

    private void Note(string message)
    {
        if (_statusLabel is not null)
        {
            _statusLabel.Text = message;
        }
    }

    private string Who(long peer) =>
        peer == Me ? "— à toi" : $"— joueur {peer}";

    private static string Say(Domain domain) => domain switch
    {
        Domain.Scout => "Éclaireur",
        Domain.Connector => "Connecteur",
        Domain.Defender => "Défenseur",
        Domain.Support => "Appui",
        _ => domain.ToString(),
    };

    private static string Say(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Beginner => "Débutant",
        Difficulty.Normal => "Normal",
        Difficulty.Advanced => "Avancé",
        Difficulty.Expert => "Expert",
        _ => difficulty.ToString(),
    };

    // ---------------------------------------------------------------- the view

    private void BuildLayout()
    {
        var background = new ColorRect { Color = Paper.Field, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 44);
        margin.AddThemeConstantOverride("margin_right", 44);
        margin.AddThemeConstantOverride("margin_top", 36);
        margin.AddThemeConstantOverride("margin_bottom", 36);
        AddChild(margin);

        var stage = new VBoxContainer();
        stage.AddThemeConstantOverride("separation", 14);
        margin.AddChild(stage);

        stage.AddChild(BuildHeader());

        stage.AddChild(BuildLinkRow());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 26);
        stage.AddChild(body);

        body.AddChild(BuildLeft());
        body.AddChild(BuildRight());
    }

    private Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var back = new Button
        {
            Text = "‹ Accueil",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        back.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        back.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        back.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        back.AddThemeFontSizeOverride("font_size", 14);
        back.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.62f });
        back.AddThemeColorOverride("font_hover_color", Paper.Sienna);
        back.Pressed += () =>
        {
            Drop();
            GetTree().ChangeSceneToFile("res://scenes/app/Home.tscn");
        };

        var title = new Label
        {
            Text = _mode == Session.LobbyMode.Solo ? "Partie solo" : "Partie coopérative",
        };
        title.AddThemeColorOverride("font_color", Paper.Ink);
        title.AddThemeFontSizeOverride("font_size", 30);

        row.AddChild(back);
        row.AddChild(title);
        return row;
    }

    /// <summary>The Héberger/Rejoindre row — Coop only. A solo table has nothing to
    /// host or join, so it is never even built into the tree for that mode.</summary>
    private Control BuildLinkRow()
    {
        var row = new HBoxContainer { Visible = _mode == Session.LobbyMode.Coop };
        row.AddThemeConstantOverride("separation", 16);

        _netModeRow = new MarginContainer();
        row.AddChild(_netModeRow);

        _joinField = BuildJoinField();
        row.AddChild(_joinField);

        row.AddChild(BuildStatusBanner());

        return row;
    }

    private Control BuildJoinField()
    {
        var box = new HBoxContainer { Visible = _netMode == NetMode.Join };
        box.AddThemeConstantOverride("separation", 8);

        _addressInput.Text = _addressText;
        _addressInput.PlaceholderText = "adresse de l'hôte";
        _addressInput.CustomMinimumSize = new Vector2(170, 0);
        Paper.TextField(_addressInput);
        _addressInput.TextChanged += text => _addressText = text;

        var join = new Button
        {
            Text = "Rejoindre",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        join.AddThemeStyleboxOverride("normal", Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 2f, radius: 8f));
        join.AddThemeStyleboxOverride("hover", Paper.CardStyle(bg: Palette.Support, borderWidth: 2f, radius: 8f));
        join.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        join.AddThemeColorOverride("font_color", Paper.Ink);
        join.Pressed += Join;

        box.AddChild(_addressInput);
        box.AddChild(join);
        return box;
    }

    private Control BuildStatusBanner()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 2f, radius: 8f));

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 0);

        var stripe = new ColorRect
        {
            Color = Palette.Choice,
            CustomMinimumSize = new Vector2(4, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("margin_left", 12);
        pad.AddThemeConstantOverride("margin_right", 12);
        pad.AddThemeConstantOverride("margin_top", 8);
        pad.AddThemeConstantOverride("margin_bottom", 8);

        _statusLabel = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _statusLabel.AddThemeColorOverride("font_color", Paper.Ink);
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);

        pad.AddChild(_statusLabel);
        row.AddChild(stripe);
        row.AddChild(pad);
        panel.AddChild(row);
        return panel;
    }

    private Control BuildLeft()
    {
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 8);

        var heading = new Label { Text = "Les Explorateurs" };
        heading.AddThemeColorOverride("font_color", Paper.Ink);
        heading.AddThemeFontSizeOverride("font_size", 20);

        var rosterHint = new Label
        {
            Text = "Clique la carte du centre pour la recruter (reclique pour la rendre) · une carte voisine pour la faire défiler.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        rosterHint.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.62f });
        rosterHint.AddThemeFontSizeOverride("font_size", 13);

        var carouselArea = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };

        _carouselRow = new HBoxContainer();
        _carouselRow.AddThemeConstantOverride("separation", 18);
        carouselArea.AddChild(_carouselRow);

        _dotsRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _dotsRow.AddThemeConstantOverride("separation", 8);
        var dotsCenter = new CenterContainer();
        dotsCenter.AddChild(_dotsRow);

        col.AddChild(heading);
        col.AddChild(rosterHint);
        col.AddChild(carouselArea);
        col.AddChild(dotsCenter);
        return col;
    }

    private Control BuildRight()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
        panel.AddThemeStyleboxOverride("panel", Paper.CardStyle());
        panel.Material = Paper.Grain;

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        margin.AddChild(col);
        panel.AddChild(margin);

        var heading = new Label { Text = "L'expédition" };
        heading.AddThemeColorOverride("font_color", Paper.Ink);
        heading.AddThemeFontSizeOverride("font_size", 21);

        _partyList = new VBoxContainer { CustomMinimumSize = new Vector2(0, 90) };
        _partyList.AddThemeConstantOverride("separation", 6);

        var difficultyLabel = new Label { Text = "Difficulté" };
        difficultyLabel.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.7f });
        difficultyLabel.AddThemeFontSizeOverride("font_size", 12);

        var difficultyHolder = new VBoxContainer();
        difficultyHolder.AddThemeConstantOverride("separation", 6);
        difficultyHolder.AddChild(difficultyLabel);
        _difficultyRow = new MarginContainer();
        difficultyHolder.AddChild(_difficultyRow);

        var seedLabel = new Label { Text = "Graine" };
        seedLabel.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.7f });
        seedLabel.AddThemeFontSizeOverride("font_size", 12);

        _seedInput.PlaceholderText = "au hasard";
        Paper.TextField(_seedInput);
        _seedInput.TextChanged += text =>
        {
            _seedText = text;
            Publish();
        };

        var seedHolder = new VBoxContainer();
        seedHolder.AddThemeConstantOverride("separation", 6);
        seedHolder.AddChild(seedLabel);
        seedHolder.AddChild(_seedInput);

        _startButton = new Button
        {
            Text = "Entrer dans le Temple",
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 52),
        };
        _startButton.AddThemeFontSizeOverride("font_size", 18);
        _startButton.AddThemeColorOverride("font_color", Paper.Ink);
        _startButton.AddThemeColorOverride("font_disabled_color", Paper.Ink with { A = 0.5f });
        _startButton.Pressed += Start;

        _hintLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
        _hintLabel.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.62f });
        _hintLabel.AddThemeFontSizeOverride("font_size", 11);

        col.AddChild(heading);
        col.AddChild(_partyList);
        col.AddChild(Paper.Divider(vertical: false));
        col.AddChild(difficultyHolder);
        col.AddChild(seedHolder);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(spacer);
        col.AddChild(_startButton);
        col.AddChild(_hintLabel);

        return panel;
    }

    /// <summary>
    /// Redraws everything that can change after a claim, a difficulty pick, a
    /// carousel step, or a link opening or closing. Rebuilt wholesale, the same way
    /// the HUD redraws its own party row after every command — there are never many
    /// pieces, and it keeps a stale claim from ever lingering on screen.
    /// </summary>
    private void Refresh()
    {
        RefreshLinkRow();
        RefreshCarousel();
        RefreshPartyList();
        RefreshDifficulty();

        _seedInput.Text = _seedText;
        _seedInput.Editable = IsHost;

        var canStart = IsHost && _party.Count >= ExplorerRoster.SmallestParty;
        _startButton.Disabled = !canStart;
        _startButton.AddThemeStyleboxOverride("normal", Paper.CardStyle(bg: canStart ? Palette.Step : Paper.CardSpine, borderWidth: 3f, radius: 10f));
        _startButton.AddThemeStyleboxOverride("disabled", Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 3f, radius: 10f));
        _startButton.AddThemeStyleboxOverride("hover", Paper.CardStyle(bg: canStart ? Palette.Step : Paper.CardSpine, borderWidth: 3f, radius: 10f));
        _startButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        _hintLabel.Text = (_mode == Session.LobbyMode.Coop && !IsHost)
            ? "Seul l'hôte peut lancer l'expédition."
            : _party.Count < ExplorerRoster.SmallestParty
                ? $"Le premier Explorateur de la liste est Chef d'Expédition. Il en faut {ExplorerRoster.SmallestParty} au minimum — en solo, prends-en trois."
                : "Le premier Explorateur de la liste est Chef d'Expédition ; l'ordre de la liste est l'ordre du tour.";
    }

    private void RefreshLinkRow()
    {
        if (_mode == Session.LobbyMode.Solo)
        {
            return;
        }

        _joinField.Visible = _netMode == NetMode.Join;

        foreach (var child in _netModeRow.GetChildren())
        {
            child.QueueFree();
        }

        _netModeRow.AddChild(Paper.Segmented(["Héberger", "Rejoindre"], (int)_netMode, i =>
        {
            var chosen = (NetMode)i;

            if (chosen == _netMode)
            {
                return;
            }

            _netMode = chosen;

            if (_netMode == NetMode.Host)
            {
                Host();
            }
            else
            {
                Drop();
                Note($"Prêt à rejoindre {_addressText}…");
                Refresh();
            }
        }));
    }

    private void RefreshCarousel()
    {
        foreach (var child in _carouselRow.GetChildren())
        {
            child.QueueFree();
        }

        var count = _roster.Count;
        var prev = (_focus - 1 + count) % count;
        var next = (_focus + 1) % count;

        _carouselRow.AddChild(BuildArrow("‹", () => Step(-1)));
        _carouselRow.AddChild(BuildExplorerCard(_roster[prev], focused: false));
        _carouselRow.AddChild(BuildExplorerCard(_roster[_focus], focused: true));
        _carouselRow.AddChild(BuildExplorerCard(_roster[next], focused: false));
        _carouselRow.AddChild(BuildArrow("›", () => Step(1)));

        foreach (var child in _dotsRow.GetChildren())
        {
            child.QueueFree();
        }

        for (var i = 0; i < count; i++)
        {
            var index = i;
            var dot = new Button
            {
                CustomMinimumSize = new Vector2(9, 9),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            var style = Paper.CardStyle(bg: i == _focus ? Paper.Ink : Paper.CardShade, borderWidth: 1f, radius: 5f);
            dot.AddThemeStyleboxOverride("normal", style);
            dot.AddThemeStyleboxOverride("hover", style);
            dot.AddThemeStyleboxOverride("pressed", style);
            dot.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            dot.Pressed += () => { _focus = index; Refresh(); };
            _dotsRow.AddChild(dot);
        }
    }

    private void Step(int delta)
    {
        var count = _roster.Count;
        _focus = ((_focus + delta) % count + count) % count;
        Refresh();
    }

    private Control BuildArrow(string glyph, Action onPress)
    {
        var arrow = new Button
        {
            Text = glyph,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(44, 44),
            MouseDefaultCursorShape = CursorShape.PointingHand,
            // Round, not stretched down the whole height of the carousel.
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        arrow.AddThemeStyleboxOverride("normal", Paper.CardStyle(bg: Paper.CardSpine, borderWidth: 2f, radius: 22f));
        arrow.AddThemeStyleboxOverride("hover", Paper.CardStyle(bg: Palette.Step, borderWidth: 2f, radius: 22f));
        arrow.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        arrow.AddThemeColorOverride("font_color", Paper.Ink);
        arrow.AddThemeFontSizeOverride("font_size", 20);
        arrow.Pressed += onPress;
        return arrow;
    }

    /// <summary>
    /// One Explorer's card. The focused one — the centre of the carousel — recruits
    /// or releases on a click and prints both abilities; the two flanking it are a
    /// smaller preview that only ever changes which one is focused. The portrait is the
    /// Explorer's own miniature, rendered live (<see cref="Portrait"/>).
    /// </summary>
    private Control BuildExplorerCard(ExplorerSheet sheet, bool focused)
    {
        var order = _party.FindIndex(seat => seat.SheetId == sheet.Id);
        var isPicked = order >= 0;
        var mine = isPicked && _party[order].Peer == Me;
        var seatColor = isPicked ? Palette.SeatColor(order) : Palette.For(sheet.Domain);

        var width = focused ? 320f : 190f;

        // A panel, not a Button: a Button does not lay its children out, so the card's
        // contents got no width at all and every label wrapped after each letter.
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            Modulate = focused ? Colors.White : Colors.White with { A = 0.55f },
        };

        var borderWidth = focused ? 3f : 2f;
        var borderColor = isPicked ? seatColor : Paper.BorderLine;
        card.AddThemeStyleboxOverride("panel", Paper.CardStyle(borderWidth: borderWidth, border: borderColor, radius: 16f));
        card.Material = Paper.Grain;

        Action pick = focused ? () => Take(sheet.Id) : () => { _focus = _roster.IndexOf(sheet); Refresh(); };
        card.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                Audio.Instance?.Play("ui_card", -8f);
                pick();
            }
        };

        var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("margin_left", 14);
        pad.AddThemeConstantOverride("margin_right", 14);
        pad.AddThemeConstantOverride("margin_top", 14);
        pad.AddThemeConstantOverride("margin_bottom", 14);

        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);

        var nameRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        nameRow.AddThemeConstantOverride("separation", 6);

        if (isPicked)
        {
            var badge = new Label { Text = order == 0 ? "★" : (order + 1).ToString() };
            badge.AddThemeColorOverride("font_color", Paper.Sienna);
            badge.AddThemeFontSizeOverride("font_size", focused ? 14 : 11);
            nameRow.AddChild(badge);
        }

        var name = new Label
        {
            Text = sheet.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        name.AddThemeColorOverride("font_color", Paper.Ink);
        name.AddThemeFontSizeOverride("font_size", focused ? 20 : 14);
        nameRow.AddChild(name);
        col.AddChild(nameRow);

        if (isPicked && _mode == Session.LobbyMode.Coop)
        {
            var owner = new Label { Text = Who(_party[order].Peer), HorizontalAlignment = HorizontalAlignment.Center };
            owner.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.6f });
            owner.AddThemeFontSizeOverride("font_size", 10);
            col.AddChild(owner);
        }

        if (focused)
        {
            var pill = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            var pillStyle = Paper.CardStyle(bg: Palette.For(sheet.Domain) with { A = 0.55f }, border: Paper.Ink with { A = 0.35f }, borderWidth: 1f, radius: 8f);
            pillStyle.ContentMarginLeft = 10;
            pillStyle.ContentMarginRight = 10;
            pillStyle.ContentMarginTop = 2;
            pillStyle.ContentMarginBottom = 2;
            pill.AddThemeStyleboxOverride("panel", pillStyle);
            var pillLabel = new Label { Text = Say(sheet.Domain) };
            pillLabel.AddThemeColorOverride("font_color", Paper.Ink);
            pillLabel.AddThemeFontSizeOverride("font_size", 12);
            pill.AddChild(pillLabel);
            var pillCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            pillCenter.AddChild(pill);
            col.AddChild(pillCenter);
        }

        var figureCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        var figurePanel = new PanelContainer { CustomMinimumSize = new Vector2(0, focused ? 190 : 100) };
        figurePanel.AddThemeStyleboxOverride("panel", Paper.CardStyle(bg: seatColor with { A = isPicked ? 0.3f : 0.16f }, borderWidth: 0f, radius: 12f));

        figurePanel.AddChild(Portrait(sheet.Id, seatColor, isPicked, focused ? new Vector2(200, 200) : new Vector2(110, 110), turning: focused));
        figureCenter.AddChild(figurePanel);
        col.AddChild(figureCenter);

        var hearts = new Label
        {
            Text = new string('♥', sheet.MaxHealth),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hearts.AddThemeColorOverride("font_color", Palette.Combat);
        hearts.AddThemeFontSizeOverride("font_size", focused ? 13 : 10);
        col.AddChild(hearts);

        if (focused)
        {
            col.AddChild(BuildAbility(sheet.First));
            col.AddChild(BuildAbility(sheet.Second));
        }

        pad.AddChild(col);
        card.AddChild(pad);
        LetClicksThrough(pad);
        return card;
    }

    /// <summary>Everything inside a card lets the mouse through to the card itself —
    /// a panel stops it by default, and a click on the portrait would go nowhere.</summary>
    private static void LetClicksThrough(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
        }

        foreach (var child in node.GetChildren())
        {
            LetClicksThrough(child);
        }
    }

    /// <summary>
    /// The Explorer's own miniature, rendered live on its base and turning slowly on
    /// the card — the figure that will stand at the table, in the seat's colour once
    /// taken.
    /// </summary>
    private static Control Portrait(string sheetId, Color seatColor, bool isPicked, Vector2 size, bool turning)
    {
        var frame = new SubViewportContainer { Stretch = true, CustomMinimumSize = size };
        var view = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            Size = (Vector2I)(size * 2),
        };
        frame.AddChild(view);

        var model = $"res://resources/models/figures/{sheetId}.glb";
        var figure = Miniature.Stand(
            ResourceLoader.Exists(model) ? model : "res://resources/models/figures/explorer_sketch.glb",
            Miniature.ExplorerBase,
            isPicked ? seatColor : null,
            isPicked ? seatColor : null);
        figure.RotationDegrees = new Vector3(0f, 25f, 0f);
        view.AddChild(figure);

        if (turning)
        {
            var spin = figure.CreateTween().SetLoops();
            spin.TweenProperty(figure, "rotation:y", Mathf.Tau, 12f).AsRelative();
        }

        view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35f, 30f, 0f), LightEnergy = 1.3f, LightColor = new Color(1f, 0.92f, 0.8f) });
        view.AddChild(new OmniLight3D { Position = new Vector3(-0.6f, 1.6f, -1.2f), LightColor = new Color(0.6f, 0.7f, 1f), LightEnergy = 1.5f, OmniRange = 4f });
        view.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.9f, 0.8f, 0.7f),
                AmbientLightEnergy = 0.5f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });

        var camera = new Camera3D { Fov = 30f, Current = true };
        view.AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0f, 1.2f, 4.1f), new Vector3(0f, 0.88f, 0f), Vector3.Up);

        return frame;
    }

    private static Control BuildAbility(Ability ability)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 1);

        var rule = Paper.Divider(vertical: false);
        rule.CustomMinimumSize = new Vector2(0, 1);

        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 6);

        var name = new Label { Text = ability.Name };
        name.AddThemeColorOverride("font_color", Paper.Ink);
        name.AddThemeFontSizeOverride("font_size", 12);

        var cost = new Label { Text = $"({ability.Cost})" };
        cost.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.6f });
        cost.AddThemeFontSizeOverride("font_size", 10);

        head.AddChild(name);
        head.AddChild(cost);

        var text = new Label
        {
            Text = ability.Text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        text.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.62f });
        text.AddThemeFontSizeOverride("font_size", 10);

        box.AddChild(rule);
        box.AddChild(head);
        box.AddChild(text);
        return box;
    }

    private void RefreshPartyList()
    {
        foreach (var child in _partyList.GetChildren())
        {
            child.QueueFree();
        }

        if (_party.Count == 0)
        {
            var empty = new Label { Text = "Aucun Explorateur pour l'instant." };
            empty.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.4f });
            empty.AddThemeFontSizeOverride("font_size", 13);
            _partyList.AddChild(empty);
            return;
        }

        foreach (var (seat, index) in _party.Select((seat, index) => (seat, index)))
        {
            var sheet = ExplorerRoster.Find(seat.SheetId)!;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var swatch = new PanelContainer { CustomMinimumSize = new Vector2(12, 12) };
            swatch.AddThemeStyleboxOverride("panel", Paper.CardStyle(bg: Palette.SeatColor(index), borderWidth: 1f, radius: 6f));

            var label = new Label
            {
                Text = (index == 0 ? "★ " : "") + sheet.Name,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            label.AddThemeColorOverride("font_color", Paper.Ink);
            label.AddThemeFontSizeOverride("font_size", 13);

            var hearts = new Label { Text = new string('♥', sheet.MaxHealth) };
            hearts.AddThemeColorOverride("font_color", Palette.Combat);
            hearts.AddThemeFontSizeOverride("font_size", 11);

            row.AddChild(swatch);
            row.AddChild(label);
            row.AddChild(hearts);

            if (_mode == Session.LobbyMode.Coop)
            {
                var who = new Label { Text = Who(seat.Peer) };
                who.AddThemeColorOverride("font_color", Paper.Ink with { A = 0.55f });
                who.AddThemeFontSizeOverride("font_size", 10);
                row.AddChild(who);
            }

            _partyList.AddChild(row);
        }
    }

    private void RefreshDifficulty()
    {
        foreach (var child in _difficultyRow.GetChildren())
        {
            child.QueueFree();
        }

        var labels = Enum.GetValues<Difficulty>().Select(Say).ToArray();
        var isHost = IsHost;

        var segmented = Paper.Segmented(labels, _difficultyIndex, i =>
        {
            if (!IsHost)
            {
                return;
            }

            _difficultyIndex = i;
            Publish();
        });

        segmented.Modulate = isHost ? Colors.White : Colors.White with { A = 0.55f };
        _difficultyRow.AddChild(segmented);
    }
}
