using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public static class Screens
{
	private enum View
	{
		Main,
		Friends
	}

	public static bool Built;

	public static bool PanelOpen;

	public static string CodeInput = "";
    public static string PortInput = "27650";
    private static bool _editingPort;
    private static TextMeshProUGUI _portField;

	private static View _view = View.Main;

	private static Btn _menuBtn;

	private static RectTransform _panel;

	private static RectTransform _vOut;

	private static RectTransform _vIn;

	private static RectTransform _vFriends;

	private static TextMeshProUGUI _outTitle;

	private static TextMeshProUGUI _signedIn;

	private static TextMeshProUGUI _codeField;

	private static TextMeshProUGUI _status;

	private static Btn _create;

	private static Btn _join;

	private static Btn _paste;

	private static Btn _clear;

	private static TextMeshProUGUI _codeLabel;

	private static Btn _copy;

	private static Btn _leave;

	private static Btn _invite;

	private static AvatarView _av1;

	private static AvatarView _av2;

	private static TextMeshProUGUI _name1;


	private static TextMeshProUGUI _name2;

	private static TextMeshProUGUI _role1;

	private static TextMeshProUGUI _role2;

	private static RectTransform _pvpBox;




	private static readonly List<Btn> _timeBtns = new List<Btn>();


	private const int FriendsPerPage = 6;

	private static readonly List<(RectTransform row, AvatarView av, TextMeshProUGUI name, Btn btn)> _friendRows = new List<(RectTransform, AvatarView, TextMeshProUGUI, Btn)>();

	private static List<(CSteamID id, string name, bool inGame)> _friends = new List<(CSteamID, string, bool)>();

	private static int _page;

	private static Btn _prev;

	private static Btn _next;

	private static TextMeshProUGUI _friendsEmpty;

	private static readonly Dictionary<ulong, float> _invited = new Dictionary<ulong, float>();

	private const float InviteTimeout = 60f;

	private static RectTransform _invitePopup;

	private static AvatarView _inviteAv;

	private static TextMeshProUGUI _inviteText;

	private static RectTransform _seedStatus;
    private static RectTransform _seedRows;
    private static Image _seedFrame;
    private static TextMeshProUGUI[] _teamNumbers=new TextMeshProUGUI[4];

	private static AvatarView _sAv1;

	private static AvatarView _sAv2;

	private static Image _dot1;

	private static Image _dot2;

	private static Btn _ready;

	private static Image _toastBg;

	private static TextMeshProUGUI _toastText;

	private static Image _tagMe;

	private static Image _tagPartner;

	private static TextMeshProUGUI _tagMeText;

	private static TextMeshProUGUI _tagPartnerText;

	private static readonly List<Image> _shade = new List<Image>();

	private static RectTransform _clock;

	private static TextMeshProUGUI _clockText;

	private static Image _redLine;

	private static RectTransform _result;

	private static TextMeshProUGUI _resultTitle;

	private static TextMeshProUGUI _resultReason;

	private static MainMenu _blockedMenu;
    private static TextMeshProUGUI _rosterText,_teamReadyText;
    private static readonly TextMeshProUGUI[] _lobbyNames=new TextMeshProUGUI[4],_lobbyNumbers=new TextMeshProUGUI[4],_lobbyRoles=new TextMeshProUGUI[4];
    private static readonly Image[] _lobbySeparators=new Image[4];
    private static TextMeshProUGUI[] _teamNames=new TextMeshProUGUI[4];
    private static Image[] _teamDots=new Image[4];
    private static Image[] _teamSeparators=new Image[4];

	private static Transform R => Ui.Root;

	public static bool TryBuild()
	{
		if (Built)
		{
			return true;
		}
		if (Ui.Sprite("dialog_topleft") == null || Ui.Font == null)
		{
			return false;
		}
		Art.Build();
		BuildMenu();
		BuildSeedScreen();
		BuildOverlays();
		Built = true;
		return true;
	}

	private static RectTransform TopCenter(RectTransform rt, float y, float w, float h)
	{
		Vector2 anchorMin = (rt.anchorMax = new Vector2(0.5f, 1f));
		rt.anchorMin = anchorMin;
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, 0f - y);
		rt.sizeDelta = new Vector2(w, h);
		return rt;
	}

	private static void BuildMenu()
	{
		_menuBtn = Ui.Button(R, new Rect(0f, 0f, 300f, 68f), "CO-OP", delegate
		{
			PanelOpen = true;
			_view = View.Main;
		});
		TopCenter(_menuBtn.Rt, 10f, 300f, 68f);
		_panel = Ui.Dialog(R, new Rect(0f, 0f, 680f, 660f));
		TopCenter(_panel, 95f, 680f, 660f);
		Ui.Hotspot(Ui.Text(_panel, new Rect(588f, 12f, 64f, 64f), "X", 62f, Ui.Red).rectTransform, delegate
		{
			if (_view == View.Friends)
			{
				_view = View.Main;
			}
			else
			{
				PanelOpen = false;
			}
		});
		_status = Ui.Text(_panel, new Rect(50f, 476f, 580f, 66f), "", 22f, Ui.Pale);
		_vOut = Ui.Node(_panel, "out", new Rect(0f, 0f, 680f, 660f));
		_outTitle = Ui.Text(_vOut, new Rect(60f, 30f, 520f, 60f), "CO-OP", 46f, Ui.Gold, TextAlignmentOptions.Left);
		_signedIn = Ui.Text(_vOut, new Rect(60f, 100f, 560f, 36f), "", 26f, Ui.Pale, TextAlignmentOptions.Left);
        Ui.Text(_vOut, new Rect(60f, 155f, 560f, 36f), "IP ADDRESS", 24f, Ui.Pale, TextAlignmentOptions.Left);
        Ui.Img(_vOut, new Rect(60f, 205f, 350f, 70f), Ui.Sprite("InputFieldBackground"), new Color(0.12f, 0.1f, 0.08f, 0.9f), sliced: true);
        _codeField = Ui.Text(_vOut, new Rect(60f, 205f, 350f, 70f), "", 28f, Ui.Gold);
        Ukrainian.MarkInput(_codeField);
        Ui.Hotspot(_codeField.rectTransform, delegate { _editingPort = false; });
        Ui.Text(_vOut, new Rect(435f, 155f, 185f, 36f), "TCP PORT", 24f, Ui.Pale);
        Ui.Img(_vOut, new Rect(435f, 205f, 185f, 70f), Ui.Sprite("InputFieldBackground"), new Color(0.12f, 0.1f, 0.08f, 0.9f), sliced: true);
        _portField = Ui.Text(_vOut, new Rect(435f, 205f, 185f, 70f), "", 30f, Ui.Gold);
        Ukrainian.MarkInput(_portField);
        Ui.Hotspot(_portField.rectTransform, delegate { _editingPort = true; });
        _paste = Ui.Button(_vOut, new Rect(60f, 295f, 200f, 55f), "PASTE", delegate {
            if (_editingPort) PortInput = GUIUtility.systemCopyBuffer.Trim(); else SetCode(GUIUtility.systemCopyBuffer);
        }, "buttonsmall", 26f);
        _clear = Ui.Button(_vOut, new Rect(275f, 295f, 160f, 55f), "CLEAR", delegate {
            if (_editingPort) PortInput = ""; else CodeInput = "";
        }, "buttonsmall", 26f);
        _create = Ui.Button(_vOut, new Rect(60f, 385f, 270f, 76f), "CREATE GAME", delegate {
            if (!ApplyPort()) return;
            Party.BindAddress = string.IsNullOrWhiteSpace(CodeInput) ? "0.0.0.0" : CodeInput.Trim();
            Party.Create();
        }, "buttonbig", 30f);
        _join = Ui.Button(_vOut, new Rect(350f, 385f, 270f, 76f), "JOIN GAME", JoinAddress, "buttonbig", 32f);
		_vIn = Ui.Node(_panel, "in", new Rect(0f, 0f, 680f, 660f));
		_codeLabel = Ui.Text(_vIn, new Rect(55f, 30f, 370f, 60f), "", 26f, Color.white, TextAlignmentOptions.Left);
		_copy = Ui.Button(_vIn, new Rect(430f, 36f, 145f, 50f), "COPY IP", delegate
		{
			GUIUtility.systemCopyBuffer = Party.Code;
		}, "buttonsmall", 24f);
		_rosterText=Ui.Text(_vIn,new Rect(60,105,560,180),"",28,Ui.Pale,TextAlignmentOptions.Left);Ukrainian.MarkInput(_rosterText);_rosterText.enableAutoSizing=true;_rosterText.fontSizeMin=16;_rosterText.fontSizeMax=28;_rosterText.enableWordWrapping=false;_rosterText.overflowMode=TextOverflowModes.Ellipsis;
        Ui.Show(_rosterText,false);
        for(int i=0;i<4;i++){
            _lobbyNumbers[i]=Ui.Text(_vIn,new Rect(60,105+i*45,35,39),"",26,Ui.Pale,TextAlignmentOptions.Left);
            _lobbyNames[i]=Ui.Text(_vIn,new Rect(104,105+i*45,307,39),"",30,Ui.Pale,TextAlignmentOptions.Left);
            Ukrainian.MarkInput(_lobbyNames[i]);_lobbyNames[i].enableAutoSizing=true;_lobbyNames[i].fontSizeMin=18;_lobbyNames[i].fontSizeMax=30;_lobbyNames[i].enableWordWrapping=false;_lobbyNames[i].overflowMode=TextOverflowModes.Ellipsis;
            _lobbyRoles[i]=Ui.Text(_vIn,new Rect(419,105+i*45,201,39),"",20,Ui.Pale,TextAlignmentOptions.Right);
            _lobbyRoles[i].enableAutoSizing=true;_lobbyRoles[i].fontSizeMin=16;_lobbyRoles[i].fontSizeMax=20;_lobbyRoles[i].enableWordWrapping=false;
            _lobbySeparators[i]=Ui.Img(_vIn,new Rect(60,148+i*45,560,1),Ui.White,new Color(.8f,.8f,.7f,.2f));
        }
		_av1 = new AvatarView(_vIn, new Rect(60f, 105f, 54f, 54f));
		_name1 = Ui.Text(_vIn, new Rect(126f, 105f, 360f, 54f), "", 34f, Color.white, TextAlignmentOptions.Left);
		_role1 = Ui.Text(_vIn, new Rect(480f, 105f, 140f, 54f), "", 24f, Ui.Gold, TextAlignmentOptions.Right);
		_av2 = new AvatarView(_vIn, new Rect(60f, 168f, 54f, 54f));
		_name2 = Ui.Text(_vIn, new Rect(126f, 168f, 360f, 54f), "", 34f, Color.white, TextAlignmentOptions.Left);
		_role2 = Ui.Text(_vIn, new Rect(480f, 168f, 140f, 54f), "", 24f, Ui.Gold, TextAlignmentOptions.Right);
		_pvpBox = Ui.Node(_vIn, "pvp", new Rect(0f, 300f, 680f, 180f));
        Ui.Text(_pvpBox, new Rect(60f, 10f, 560f, 160f), "READY TO PLAY\n\nConnect all players before choosing a level.\nEveryone selects cards, then READY.\nYou defend the same lawn together.", 24f, Ui.Pale);
		_leave = Ui.Button(_vIn, new Rect(55f, 552f, 230f, 68f), "LEAVE", delegate
		{
			Party.Leave();
		}, "buttonbig", 34f);
		_invite = Ui.Button(_vIn, new Rect(395f, 552f, 230f, 68f), "INVITE", OpenFriends, "buttonbig", 34f);
		_vFriends = Ui.Node(_panel, "friends", new Rect(0f, 0f, 680f, 660f));
		Ui.Text(_vFriends, new Rect(60f, 30f, 520f, 60f), "INVITE A FRIEND", 42f, Ui.Gold, TextAlignmentOptions.Left);
		for (int num2 = 0; num2 < 6; num2++)
		{
			int idx = num2;
			RectTransform rectTransform = Ui.Node(_vFriends, "row", new Rect(55f, 100 + num2 * 66, 570f, 60f));
			AvatarView item2 = new AvatarView(rectTransform, new Rect(0f, 4f, 52f, 52f));
			TextMeshProUGUI item3 = Ui.Text(rectTransform, new Rect(66f, 0f, 360f, 60f), "", 30f, Color.white, TextAlignmentOptions.Left);
			Btn item4 = Ui.Button(rectTransform, new Rect(430f, 6f, 140f, 50f), "INVITE", delegate
			{
				InviteRow(idx);
			}, "buttonsmall", 24f);
			_friendRows.Add((rectTransform, item2, item3, item4));
		}
		_friendsEmpty = Ui.Text(_vFriends, new Rect(60f, 200f, 560f, 120f), "None of your Steam friends are online.", 28f, Ui.Pale);
		_prev = Ui.Button(_vFriends, new Rect(55f, 560f, 150f, 56f), "< PREV", delegate
		{
			_page--;
		}, "buttonsmall", 24f);
		_next = Ui.Button(_vFriends, new Rect(475f, 560f, 150f, 56f), "NEXT >", delegate
		{
			_page++;
		}, "buttonsmall", 24f);
		Ui.Button(_vFriends, new Rect(240f, 556f, 200f, 62f), "BACK", delegate
		{
			_view = View.Main;
		});
		_invitePopup = Ui.Dialog(R, new Rect(0f, 0f, 560f, 250f));
		TopCenter(_invitePopup, 90f, 560f, 250f);
		_inviteAv = new AvatarView(_invitePopup, new Rect(50f, 46f, 64f, 64f));
		_inviteText = Ui.Text(_invitePopup, new Rect(126f, 40f, 380f, 80f), "", 28f, Color.white, TextAlignmentOptions.Left);
		Ui.Button(_invitePopup, new Rect(60f, 140f, 200f, 64f), "JOIN", Party.AcceptInvite, "buttonbig", 32f);
		Ui.Button(_invitePopup, new Rect(300f, 140f, 200f, 64f), "DECLINE", Party.DeclineInvite, "buttonbig", 30f, Ui.Red);
	}

	private static Image TeamCard(Rect r, string label, Sprite built, string art, PvP.Team team)
	{
		Image result = Ui.Img(_pvpBox, new Rect(r.x - 7f, r.y - 7f, r.width + 14f, r.height + 14f), Ui.White, Ui.Gold);
		Image image;
		if (built != null)
		{
			image = Ui.Img(_pvpBox, r, built);
		}
		else
		{
			image = Ui.Img(_pvpBox, r, Ui.Sprite("SeedPacket_Default"));
			Ui.Img(image.rectTransform, new Rect(14f, 22f, r.width - 28f, r.height * 0.52f), Ui.Sprite(art)).preserveAspect = true;
		}
		Ui.Text(image.rectTransform, new Rect(8f, r.height * 0.79f, r.width - 16f, r.height * 0.17f), label.ToUpperInvariant(), 26f, new Color(0.2f, 0.15f, 0.05f), TextAlignmentOptions.Center, outline: false);
		Ui.Hotspot(image.rectTransform, delegate
		{
			PvP.ChooseTeam(team);
		});
		return result;
	}

	private static void BuildSeedScreen()
	{
		_seedStatus = Ui.Dialog(R, new Rect(0f, 0f, 300f, 200f), header: false, compact: true);
        _seedFrame=_seedStatus.GetChild(0).GetComponent<Image>();
        _seedRows=Ui.Node(_seedStatus,"ReadinessRows",new Rect(36,28,228,144));
        _seedRows.gameObject.AddComponent<RectMask2D>();
		RectTransform seedStatus = _seedStatus;
		Vector2 anchorMin = (_seedStatus.anchorMax = new Vector2(1f, 0.5f));
		seedStatus.anchorMin = anchorMin;
		_seedStatus.pivot = new Vector2(1f, 0.5f);
		_seedStatus.anchoredPosition = new Vector2(-20f, 0f);
		_teamReadyText=Ui.Text(_seedStatus,new Rect(20,25,190,190),"",20,Ui.Pale,TextAlignmentOptions.Left);
        for(int player=0;player<4;player++){
            _teamNumbers[player]=Ui.Text(_seedRows,new Rect(6,player*48,30,42),"",34,Ui.Pale,TextAlignmentOptions.Left);
            _teamNames[player]=Ui.Text(_seedRows,new Rect(48,player*48,150,42),"",34,Ui.Pale,TextAlignmentOptions.Left);
            foreach(var label in new[]{_teamNames[player],_teamNumbers[player]}){Ukrainian.MarkInput(label);label.enableAutoSizing=false;label.fontSize=34;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;}
            _teamDots[player]=Ui.Img(_seedRows,new Rect(210,12+player*48,18,18),Ui.Circle,Ui.Dim);
            _teamSeparators[player]=Ui.Img(_seedRows,new Rect(6,46+player*48,222,1.5f),Ui.White,new Color(.9f,.82f,.6f,.22f));
        }

		_sAv1 = new AvatarView(_seedStatus, new Rect(50f, 36f, 66f, 66f));
		_dot1 = Ui.Img(_seedStatus, new Rect(134f, 54f, 30f, 30f), Ui.Circle, Ui.Dim);
		_sAv2 = new AvatarView(_seedStatus, new Rect(50f, 112f, 66f, 66f));
		_dot2 = Ui.Img(_seedStatus, new Rect(134f, 130f, 30f, 30f), Ui.Circle, Ui.Dim);
		_ready = Ui.Button(R, new Rect(0f, 0f, 10f, 10f), "READY!", Session.ToggleReady, "SeedChooser_Button", 40f, Ui.Gold);
	}

	private static void BuildOverlays()
	{
		_toastBg = Ui.Plate(R, new Rect(0f, 0f, 820f, 58f));
		TopCenter(_toastBg.rectTransform, 160f, 820f, 58f);
		_toastText = Ui.Text(_toastBg.rectTransform, new Rect(14f, 0f, 792f, 58f), "", 28f, Ui.Gold);
		_tagMe = Ui.Img(R,new Rect(278f,150f,248f,46f),CoopArt.Badge);
        _tagMeText=Ui.Text(_tagMe.rectTransform,new Rect(13,3,225,24),"",19,Ui.Gold,TextAlignmentOptions.Left);
        Ukrainian.MarkInput(_tagMeText);_tagMeText.enableAutoSizing=true;_tagMeText.fontSizeMin=11;_tagMeText.fontSizeMax=19;_tagMeText.enableWordWrapping=false;
        Ui.Text(_tagMe.rectTransform,new Rect(13,25,225,18),"YOUR CARDS",14,Ui.Pale,TextAlignmentOptions.Left);
		_tagPartner = Ui.Plate(R, new Rect(0f, 0f, 200f, 28f));
		_tagPartnerText = Ui.Text(_tagPartner.rectTransform, new Rect(10f, 0f, 150f, 28f), "", 20f, Color.white, TextAlignmentOptions.Left);
		_redLine = Ui.Img(R, new Rect(0f, 0f, 6f, 10f), Ui.White, new Color(0.95f, 0.1f, 0.1f, 0.85f));
		for (int i = 0; i < 16; i++)
		{
			Image image = Ui.Img(R, new Rect(0f, 0f, 10f, 10f), Ui.White, new Color(0f, 0f, 0f, 0.55f));
			Ui.Show(image, on: false);
			_shade.Add(image);
		}
		_clock = Ui.Img(R, new Rect(0f, 0f, 240f, 66f), Ui.Sprite("buttonbig")).rectTransform;
		RectTransform clock = _clock;
		Vector2 anchorMin = (_clock.anchorMax = new Vector2(1f, 1f));
		clock.anchorMin = anchorMin;
		_clock.pivot = new Vector2(1f, 1f);
		_clock.anchoredPosition = new Vector2(-14f, -10f);
		_clockText = Ui.Text(_clock, new Rect(6f, 2f, 228f, 60f), "", 42f, Ui.Green);
		_result = Ui.Dialog(R, new Rect(0f, 0f, 660f, 340f), header: true);
		RectTransform result = _result;
		anchorMin = (_result.anchorMax = new Vector2(0.5f, 0.5f));
		result.anchorMin = anchorMin;
		_result.pivot = new Vector2(0.5f, 0.5f);
		_result.anchoredPosition = Vector2.zero;
		_resultTitle = Ui.Text(_result, new Rect(40f, 50f, 580f, 90f), "", 72f, Ui.Gold);
		_resultReason = Ui.Text(_result, new Rect(40f, 140f, 580f, 70f), "", 30f, Ui.Pale);
		Ui.Button(_result, new Rect(180f, 230f, 300f, 72f), "BACK TO MENU", PvP.BackToMenu, "buttonbig", 32f);
	}

	public static void Tick(MainMenu menu, bool onMainMenu)
	{
		if (Built || TryBuild())
		{
			TickMenu(menu, onMainMenu);
			TickSeedScreen();
			TickOverlays();
			Ui.Update();
		}
	}

	private static void TickMenu(MainMenu menu, bool onMain)
	{
        if(onMain&&menu!=null)foreach(var nativeMenu in UnityEngine.Object.FindObjectsOfType<BaseMenu>())if(nativeMenu.isActiveAndEnabled&&nativeMenu.Pointer!=menu.Pointer){onMain=false;break;}
		bool flag = onMain && PanelOpen;
        bool block = flag || (onMain && LanguageSelection.Open);
		if (block && menu != null && _blockedMenu == null)
		{
			menu.Interactable = false;
			_blockedMenu = menu;
		}
		if (!block && _blockedMenu != null)
		{
			if (!_blockedMenu.WasCollected)
			{
				_blockedMenu.Interactable = true;
			}
			_blockedMenu = null;
		}
		Ui.Show(_menuBtn.Rt, onMain && !PanelOpen && !LanguageSelection.Open);
		_menuBtn.Text = Party.InParty ? "CO-OP  "+Party.PlayerCount+" / 4" : "CO-OP (2-4)";
		Ui.Show(_invitePopup, onMain && Party.InviteLobby != CSteamID.Nil);
		if (Party.InviteLobby != CSteamID.Nil)
		{
			string text = (Party.SteamReady ? SteamFriends.GetFriendPersonaName(Party.InviteFrom) : "");
			_inviteText.text = text + "\ninvited you to a party";
			_inviteAv.Set(Party.InviteFrom, text, Ui.Gold);
		}
		Ui.Show(_panel, flag);
		if (!flag)
		{
			return;
		}
		_status.text = Party.Status ?? "";
		bool flag2 = _view == View.Friends && Party.InParty;
		Ui.Show(_vFriends, flag2);
		Ui.Show(_vOut, !flag2 && !Party.InParty);
		Ui.Show(_vIn, !flag2 && Party.InParty);
		if (flag2)
		{
			TickFriends();
			return;
		}
		if (!Party.InParty)
		{
			bool flag3 = Party.SteamReady || Party.Local;
			_signedIn.text = (flag3 ? ("Play together on the same Wi-Fi") : Party.Status);
			_paste.Text = _editingPort ? "PASTE PORT" : "PASTE IP";
            _codeField.text = (CodeInput.Length == 0 ? (LanguageSelection.UkrainianEnabled ? "IP (порожньо — усі адаптери)" : "IP (blank = all adapters)") : CodeInput) + (!_editingPort ? "_" : "");
            _codeField.enableAutoSizing=true;_codeField.fontSizeMax=28;_codeField.fontSizeMin=14;
            _codeField.color = _editingPort ? Ui.Pale : Ui.Gold;
            _portField.text = PortInput + (_editingPort ? "_" : "");
            _portField.color = _editingPort ? Ui.Gold : Ui.Pale;
			Btn[] array = new Btn[4] { _create, _join, _paste, _clear };
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Enabled = flag3 && !Party.Busy;
			}
			return;
		}
		_codeLabel.text = "IP: " + Party.Code;
		Ui.Show(_copy.Rt, true);
		bool isHost = Party.IsHost;
		string text2 = (isHost ? Party.MyName : Party.PartnerName);
		string text3 = (isHost ? Party.PartnerName : Party.MyName);
		CSteamID id = (isHost ? Party.MyId : Party.PartnerId);
		CSteamID id2 = (isHost ? Party.PartnerId : Party.MyId);
		_av1.Set(id, text2, Ui.Red);
		Ukrainian.MarkInput(_name1);_name1.enableAutoSizing=true;_name1.fontSizeMin=14;_name1.fontSizeMax=34;_name1.text=text2;
		_role1.text = "SERVER";
		bool partnerPresent = Party.PartnerPresent;
		_av2.Show(partnerPresent);
		if (partnerPresent)
		{
			_av2.Set(id2, text3, Ui.Gold);
		}
		Ukrainian.MarkInput(_name2);_name2.enableAutoSizing=true;_name2.fontSizeMin=14;_name2.fontSizeMax=34;_name2.text=partnerPresent?text3:Ukrainian.Translate("Waiting for teammate...");
		_name2.color = (partnerPresent ? Color.white : Ui.Dim);
		_role2.text = partnerPresent ? "CONNECTED" : "";
		Ui.Show(_pvpBox, partnerPresent);
        Ui.Show(_invite.Rt, false);
        _av1.Show(false);_av2.Show(false);Ui.Show(_name1,false);Ui.Show(_name2,false);Ui.Show(_role1,false);Ui.Show(_role2,false);
        var members=Party.Members;
        for(int i=0;i<4;i++){
            bool show=i<members.Length;Ui.Show(_lobbyNumbers[i],show);Ui.Show(_lobbyNames[i],show);Ui.Show(_lobbyRoles[i],show);Ui.Show(_lobbySeparators[i],show&&i<members.Length-1);
            if(!show)continue;int player=members[i];
            _lobbyNumbers[i].text=player+".";_lobbyNames[i].text=Party.DisplayName(player);
            string role=player==1?(LanguageSelection.UkrainianEnabled?"ХОСТ":"HOST"):(LanguageSelection.UkrainianEnabled?"ПІДКЛЮЧЕНО":"CONNECTED");
            _lobbyRoles[i].text=role+(player==Party.PlayerId?(LanguageSelection.UkrainianEnabled?" · ВИ":" · YOU"):"");
        }
	}

	private static string TeamTag(PvP.Team t)
	{
		return t switch
		{
			PvP.Team.Attack => "\n<size=80%><color=#f88>ATTACK</color></size>", 
			PvP.Team.Defense => "\n<size=80%><color=#8f8>DEFENSE</color></size>", 
			_ => "", 
		};
	}

	private static void OpenFriends()
	{
		_friends = Party.OnlineFriends();
		_page = 0;
		_view = View.Friends;
	}

	private static void InviteRow(int row)
	{
		int num = _page * 6 + row;
		if (num >= 0 && num < _friends.Count && Party.Invite(_friends[num].id))
		{
			_invited[_friends[num].id.m_SteamID] = Time.unscaledTime;
		}
	}

	private static void TickFriends()
	{
		int num = Math.Max(1, (_friends.Count + 6 - 1) / 6);
		_page = Mathf.Clamp(_page, 0, num - 1);
		for (int i = 0; i < 6; i++)
		{
			int num2 = _page * 6 + i;
			(RectTransform, AvatarView, TextMeshProUGUI, Btn) tuple = _friendRows[i];
			bool flag = num2 < _friends.Count;
			Ui.Show(tuple.Item1, flag);
			if (flag)
			{
				(CSteamID, string, bool) tuple2 = _friends[num2];
				tuple.Item2.Set(tuple2.Item1, tuple2.Item2, Ui.Gold);
				tuple.Item3.text = tuple2.Item2 + (tuple2.Item3 ? "  <size=70%><color=#9f9>in game</color></size>" : "");
				float value;
				bool flag2 = _invited.TryGetValue(tuple2.Item1.m_SteamID, out value) && Time.unscaledTime - value < 60f && !Party.PartnerPresent;
				tuple.Item4.Text = (flag2 ? "SENT" : "INVITE");
				tuple.Item4.Enabled = !flag2;
			}
		}
		Ui.Show(_friendsEmpty, _friends.Count == 0);
		_prev.Enabled = _page > 0;
		_next.Enabled = _page < num - 1;
	}

	private static void TickSeedScreen()
	{
		bool flag = Session.Active && !Session.Started && Party.InParty;
		Ui.Show(_seedStatus, flag);
		Rect readyButtonRect = Session.ReadyButtonRect;
		bool flag2 = flag && readyButtonRect.width > 1f;
		Ui.Show(_ready.Rt, flag2);
		if (flag2)
		{
			Ui.Place(_ready.Rt, Ui.FromScreen(readyButtonRect));
			_ready.Text = (Session.MyReady ? "NOT READY" : "READY");
			_ready.SetColor(Session.MyReady ? Ui.Red : Ui.Gold);
			_ready.Label.fontSize = Ui.FromScreen(readyButtonRect).height * 0.5f;
		}
		if (flag)
		{
			bool isHost = Party.IsHost;
            
            
			_sAv1.Set(isHost ? Party.MyId : Party.PartnerId, isHost ? Party.MyName : Party.PartnerName, Ui.Red);
			_sAv2.Set(isHost ? Party.PartnerId : Party.MyId, isHost ? Party.PartnerName : Party.MyName, Ui.Gold);
			bool flag3 = (isHost ? Session.MyReady : Session.PartnerReady);
			bool flag4 = (isHost ? Session.PartnerReady : Session.MyReady);
			_dot1.color = (flag3 ? Ui.Green : Ui.Dim);
			_dot2.color = (flag4 ? Ui.Green : Ui.Dim);
            _sAv1.Show(false);_sAv2.Show(false);Ui.Show(_dot1,false);Ui.Show(_dot2,false);
            Ui.Show(_teamReadyText,false);
            var members=Party.Members;
            float nameWidth=100;
            foreach(int id in members)nameWidth=Mathf.Max(nameWidth,_teamNames[0].GetPreferredValues(Party.DisplayName(id)).x+12);
            nameWidth=Mathf.Min(nameWidth,240);
            float contentWidth=48+nameWidth+18+18+8;
            float height=72+Mathf.Max(1,members.Length)*48,width=72+contentWidth;
            _seedStatus.sizeDelta=new Vector2(width,height);
            if(_seedFrame!=null)Ui.Place(_seedFrame.rectTransform,new Rect(0,0,width,height));
            Ui.Place(_seedRows,new Rect(36,28,contentWidth,height-72));
            for(int i=0;i<4;i++){
                bool present=i<members.Length;
                Ui.Show(_teamNumbers[i],present);Ui.Show(_teamNames[i],present);Ui.Show(_teamDots[i],present);Ui.Show(_teamSeparators[i],present&&i<members.Length-1);
                if(!present)continue;int id=members[i];
                _teamNumbers[i].text=id+".";_teamNames[i].text=Party.DisplayName(id);
                Ui.Place(_teamNumbers[i].rectTransform,new Rect(6,i*48,30,42));
                Ui.Place(_teamNames[i].rectTransform,new Rect(48,i*48,nameWidth,42));
                Ui.Place(_teamDots[i].rectTransform,new Rect(48+nameWidth+8,12+i*48,18,18));
                Ui.Place(_teamSeparators[i].rectTransform,new Rect(6,46+i*48,contentWidth-12,1.5f));
                bool ready=id==Party.PlayerId?Session.MyReady:TeamState.For(id).Ready;
                _teamDots[i].color=ready?Ui.Green:Ui.Dim;
            }

		}
	}

	private static void TickOverlays()
	{
		string toast = Session.Toast;
		Ui.Show(_toastBg, toast.Length > 0);
		if (toast.Length > 0 && _toastText.text != toast)
		{
			_toastText.text = toast;
		}
		int used = 0;
		bool flag = Sync.Running && !PvP.Enabled && !Sync.ResultSent && !Sync.ResultApplied;
		Ui.Show(_tagMe, false);
		Ui.Show(_tagPartner, false);
		if (flag)
		{
			Rect rect = Ui.FromScreen(PartnerBank.MyScreenRect);
			Ui.Place(_tagMe.rectTransform,new Rect(Party.RemotePlayers.Length>1?30+Party.RemotePlayers.Length*170:278,150f,248f,46f));
            _tagMeText.text=Party.MyName;
			if (PartnerBank.Visible)
			{
				Rect rect2 = Ui.FromScreen(PartnerBank.ScreenRect);
				Ui.Place(_tagPartner.rectTransform, new Rect(rect2.x + 6f, rect2.y - 25f, 170f, 24f));
				_tagPartnerText.text = "TEAMMATE";
			}
		}
		bool flag2 = PvP.Enabled && Session.Started;
		Ui.Show(_clock, flag2);
		Ui.Show(_result, flag2 && PvP.Winner != PvP.Team.None);
		bool flag3 = false;
		if (flag2)
		{
			int num = Mathf.Max(0, (int)PvP.Remaining);
			_clockText.text = ((PvP.TimeLimit == 0) ? "ENDLESS" : $"{num / 60}:{num % 60:00}");
			_clockText.color = ((PvP.TimeLimit != 0 && num <= 30) ? Ui.Red : Ui.Green);
			Camera main = Camera.main;
			if (main != null && Mouse.Instance != null)
			{
				float boxXFromColumn = Mouse.Instance.GetBoxXFromColumn(8);
				float boxXFromColumn2 = Mouse.Instance.GetBoxXFromColumn(7);
				float x = boxXFromColumn + (boxXFromColumn - boxXFromColumn2) / 2f;
				Vector3 vector = main.WorldToScreenPoint(new Vector3(x, Mouse.Instance.GetBoxYFromRow(0) + 0.6f, 0f));
				Vector3 vector2 = main.WorldToScreenPoint(new Vector3(x, Mouse.Instance.GetBoxYFromRow(4) - 0.6f, 0f));
				Ui.Place(_redLine.rectTransform, Ui.FromScreen(Rect.MinMaxRect(vector.x - 3f, (float)Screen.height - vector.y, vector.x + 3f, (float)Screen.height - vector2.y)));
				flag3 = true;
			}
			if (PvP.Winner != PvP.Team.None)
			{
				bool flag4 = PvP.Winner == PvP.MyTeam;
				_resultTitle.text = (flag4 ? "YOU WIN!" : "YOU LOSE");
				_resultTitle.color = (flag4 ? Ui.Gold : Ui.Red);
				_resultReason.text = ((PvP.Winner == PvP.Team.Attack) ? "A zombie got into the house." : "The plants held out until the end.");
			}
		}
		Ui.Show(_redLine, flag3);
		for (int i = used; i < _shade.Count; i++)
		{
			Ui.Show(_shade[i], on: false);
		}
	}

	private static void Shade(ref int used, Rect r)
	{
		if (used < _shade.Count)
		{
			Image image = _shade[used++];
			Ui.Place(image.rectTransform, r);
			Ui.Show(image, on: true);
		}
	}

    private static bool ApplyPort()
    {
        if (!int.TryParse(PortInput, out int port) || port < 1 || port > 65535)
        { Party.Status = "Port must be a number from 1 to 65535."; return false; }
        Party.Port = port; return true;
    }
    private static void JoinAddress()
    {
        if (ApplyPort()) Party.Join(CodeInput.Trim() + ":" + Party.Port);
    }
    public static void SetCode(string text)
    {
        text = (text ?? "").Trim();
        int colon = text.LastIndexOf(':');
        if (colon >= 0) { PortInput = text.Substring(colon + 1); text = text.Substring(0, colon); }
        var clean = new StringBuilder();
        foreach (char c in text) if ((char.IsLetterOrDigit(c) || c == '.' || c == '-') && clean.Length < 253) clean.Append(c);
        CodeInput = clean.ToString();
    }
    public static void ReadTyping(bool onMain)
    {
        if (!onMain || !PanelOpen || Party.InParty || Party.Busy) return;
        foreach (char c in Input.inputString)
        {
            if (c == '\t') { _editingPort = !_editingPort; continue; }
            if (c == '\r' || c == '\n') { JoinAddress(); continue; }
            if (c == '\b')
            {
                if (_editingPort) { if (PortInput.Length > 0) PortInput = PortInput.Substring(0, PortInput.Length - 1); }
                else if (CodeInput.Length > 0) CodeInput = CodeInput.Substring(0, CodeInput.Length - 1);
                continue;
            }
            if (_editingPort) { if (char.IsDigit(c) && PortInput.Length < 5) PortInput += c; }
            else if ((char.IsLetterOrDigit(c) || c == '.' || c == '-') && CodeInput.Length < 253) CodeInput += c;
        }
    }
}

