using Godot;
using SubTerra.Core.Explorers;
using SubTerra.Core.Game;

namespace SubTerra.App;

/// <summary>
/// Where a party is put together: alone, or with others over the network. The host
/// owns the truth — claims are asked of it, and it hands the agreed party back to
/// everyone before the temple opens.
/// </summary>
public partial class Lobby : Control
{
    private readonly List<Session.Seat> _party = [];

    private Button _solo = null!;
    private Button _host = null!;
    private Button _join = null!;
    private Button _start = null!;
    private LineEdit _address = null!;
    private LineEdit _seed = null!;
    private OptionButton _difficulty = null!;
    private Label _status = null!;
    private Label _hint = null!;
    private GridContainer _roster = null!;
    private VBoxContainer _partyList = null!;

    /// <summary>Whether a link has been opened at all. Before that, everything is local.</summary>
    private bool _online;

    public override void _Ready()
    {
        _solo = GetNode<Button>("%Solo");
        _host = GetNode<Button>("%Host");
        _join = GetNode<Button>("%Join");
        _start = GetNode<Button>("%Start");
        _address = GetNode<LineEdit>("%Address");
        _seed = GetNode<LineEdit>("%Seed");
        _difficulty = GetNode<OptionButton>("%Difficulty");
        _status = GetNode<Label>("%Status");
        _hint = GetNode<Label>("%Hint");
        _roster = GetNode<GridContainer>("%Roster");
        _partyList = GetNode<VBoxContainer>("%Party");

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            _difficulty.AddItem(Say(difficulty), (int)difficulty);
        }

        _difficulty.Selected = (int)Difficulty.Normal;

        _solo.Pressed += PlayAlone;
        _host.Pressed += Host;
        _join.Pressed += Join;
        _start.Pressed += Start;
        _difficulty.ItemSelected += _ => Publish();
        _seed.TextChanged += _ => Publish();

        Multiplayer.PeerConnected += WelcomePeer;
        Multiplayer.PeerDisconnected += SeeOffPeer;
        Multiplayer.ConnectedToServer += OnConnected;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnHostGone;

        BuildRoster();
        Refresh();
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
        var error = peer.CreateClient(_address.Text.Trim(), Session.DefaultPort);

        if (error is not Error.Ok)
        {
            Note($"Connexion impossible : {error}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        _online = true;
        Note($"Connexion à {_address.Text.Trim()}…");
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
            Rpc(nameof(SyncTable), Session.Encode(_party), _difficulty.Selected, _seed.Text);
        }

        Refresh();
    }

    [Rpc(CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SyncTable(string party, int difficulty, string seed)
    {
        _party.Clear();
        _party.AddRange(Session.Decode(party));
        _difficulty.Selected = difficulty;
        _seed.Text = seed;
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

        var seed = ulong.TryParse(_seed.Text.Trim(), out var chosen)
            ? chosen
            : (ulong)Random.Shared.NextInt64(1, long.MaxValue);

        if (_online)
        {
            Rpc(nameof(Enter), Session.Encode(_party), _difficulty.Selected, seed.ToString());
        }

        Enter(Session.Encode(_party), _difficulty.Selected, seed.ToString());
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

    // ---------------------------------------------------------------- the view

    private void BuildRoster()
    {
        foreach (var sheet in ExplorerRoster.All)
        {
            var id = sheet.Id;

            var card = new Button
            {
                Name = sheet.Id,
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0f, 64f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = $"{sheet.First.Name} ({sheet.First.Cost}) — {sheet.First.Text}\n\n"
                    + $"{sheet.Second.Name} ({sheet.Second.Cost}) — {sheet.Second.Text}",
            };

            card.Pressed += () => Take(id);
            _roster.AddChild(card);
        }
    }

    private void Refresh()
    {
        foreach (var sheet in ExplorerRoster.All)
        {
            var card = _roster.GetNode<Button>(sheet.Id);
            var seat = _party.FirstOrDefault(seat => seat.SheetId == sheet.Id);

            card.Text = string.Join('\n',
                sheet.Name,
                $"{new string('♥', sheet.MaxHealth)}   {Say(sheet.Domain)}",
                seat is null ? " " : Who(seat.Peer));

            // A card someone else holds is not yours to take, but you can always let
            // go of your own.
            card.Disabled = seat is not null && seat.Peer != Me;
        }

        foreach (var child in _partyList.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (seat, index) in _party.Select((seat, index) => (seat, index)))
        {
            var sheet = ExplorerRoster.Find(seat.SheetId)!;
            var badge = index == 0 ? "★ " : string.Empty;

            _partyList.AddChild(new Label
            {
                Text = $"{badge}{sheet.Name}   {new string('♥', sheet.MaxHealth)}   {Who(seat.Peer)}",
            });
        }

        _start.Disabled = !IsHost || _party.Count < ExplorerRoster.SmallestParty;
        _seed.Editable = IsHost;
        _difficulty.Disabled = !IsHost;

        _hint.Text = _party.Count < ExplorerRoster.SmallestParty
            ? $"Le premier Explorateur de la liste est Chef d'Expédition. Il en faut {ExplorerRoster.SmallestParty} au minimum — en solo, prends-en trois."
            : "Le premier Explorateur de la liste est Chef d'Expédition ; l'ordre de la liste est l'ordre du tour.";
    }

    private void Note(string message) => _status.Text = message;

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
}
