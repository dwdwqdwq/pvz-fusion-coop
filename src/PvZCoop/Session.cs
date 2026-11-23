using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppSystem.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PvZCoop;

public static class Session
{
	public static bool Active;

	public static bool PartnerInLevel { get=>Party.RemotePlayers.Length>0&&Party.RemotePlayers.All(id=>TeamState.For(id).InLevel); set {if(Party.SenderId>0)TeamState.For(Party.SenderId).InLevel=value;else foreach(int id in Party.RemotePlayers)TeamState.For(id).InLevel=value;} }

	public static bool MyReady;

	public static bool PartnerReady { get=>TeamState.AllReady; set {if(Party.SenderId>0)TeamState.For(Party.SenderId).Ready=value;else foreach(int id in Party.RemotePlayers)TeamState.For(id).Ready=value;} }

	public static bool Started;
    public static bool BattleLive {get;private set;}
    static bool loaded;
    public static System.Collections.Generic.List<int> MySlotKeys=new();
    public static System.Collections.Generic.List<int> PartnerSlotKeys {get=>TeamState.For(Party.PartnerPlayer).Slots;set=>TeamState.For(Party.SenderId>0?Party.SenderId:Party.PartnerPlayer).Slots=value;}

	public static System.Collections.Generic.List<(int type, int cost)> MyDeck = new System.Collections.Generic.List<(int, int)>();

	public static System.Collections.Generic.List<(int type, int cost)> PartnerDeck {get=>TeamState.For(Party.PartnerPlayer).Deck;set=>TeamState.For(Party.SenderId>0?Party.SenderId:Party.PartnerPlayer).Deck=value;}

	public static LevelType Level;

	public static int LevelNumber;
    public static int LevelId;
    public static string LevelName="";
    static (LevelType type,int number,int id,string name)? restart;
    static float restartAt;

	public static bool Allow;

	private static string _toast = "";

	private static float _toastUntil;

	private static bool _startedByGame;

	private static GameObject _hiddenStart;

	public static Rect ReadyButtonRect;

	private static float _nextStartSearch;

	private static Canvas _startCanvas;

	public static bool Holding;

	private static float _fakeReadyAt = -1f;

	private static string[] _pendingLevel;

	private static float _nextPendingCheck;

	public static string Toast
	{
		get
		{
			if (!(Time.unscaledTime < _toastUntil))
			{
				return "";
			}
			return _toast;
		}
	}

	private static bool Coop
	{
		get
		{
			if (Party.InParty)
			{
				return Party.PartnerPresent;
			}
			return false;
		}
	}

	public static bool PendingLevel => _pendingLevel != null;

	public static void ShowToast(string s, float secs = 4f)
	{
		_toast = s;
		_toastUntil = Time.unscaledTime + secs;
		Plugin.Logger.LogInfo("[toast] " + s);
	}

	public static void Init()
	{
		Party.OnMessage += OnMessage;
        Party.OnMessage += WaveSync.Receive;
	}

	public static void Reset()
	{
		RestoreStartButton();
        BattleLive=loaded=false;
        TeamState.Reset();
        DeckSharing.Reset();SharedSun.Reset();SunVisuals.Reset();
        VictorySync.Reset();WaveSync.ResetProgress();
		Active = (PartnerInLevel = (MyReady = (PartnerReady = (Started = false))));
		if (Holding)
		{
			Holding = false;
			Time.timeScale = 1f;
		}
		MyDeck.Clear();MySlotKeys.Clear();PartnerSlotKeys.Clear();
		PartnerDeck.Clear();
		_startedByGame = false;
		Sync.Reset();
	}

	public static void LeaveLevel()
	{
		Time.timeScale = 1f;
		GameObject gameObject = ((Board.Instance != null) ? Board.Instance.gameObject : null);
        // BackToMainMenu can return to the previous mode selector. A network
        // transition needs the actual root menu, with no intervening selection UI.
        if(GameAPP.UIManager!=null)GameAPP.UIManager.PopAll();
        UIMgr.EnterMainMenu();
		if (gameObject != null && !gameObject.WasCollected)
		{
			Plugin.Logger.LogInfo("leaving level: removing the lawn");
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	public static bool OnEnterGame(LevelType type, int number, int id, string name)
	{
        if (!Allow && (Party.Busy || (Party.InParty && !Party.PartnerPresent)))
        { ShowToast("Wait for teammates to connect before choosing a level."); return false; }
		if (Allow || !Coop)
		{
			return true;
		}
        if(Active&&Started&&type==Level&&number==LevelNumber){RequestRestart();return false;}
		if (!Party.IsHost)
		{
			ShowToast("Only the party host can choose the level.");
			return false;
		}
		Reset();
		Active = true;
		Level = type;
		LevelNumber = number;LevelId=id;LevelName=name??"";
		PvP.OnLevelOpened();
		Unlocks.SendNow();
		Party.Send("level", $"{(int)type}|{number}|{id}|{name}|{CampaignProgress.Encode()}");
		ShowToast($"Starting {type} {number} with {Party.PartnerName}...");
		return true;
	}

    public static void RequestRestart(){
        if(!Active||!Started||!Party.PartnerPresent)return;
        if(!Party.IsHost){Party.Send("restart_request","");ShowToast("Restart requested.",2);return;}
        if(restart.HasValue)return;
        var target=(Level,LevelNumber,LevelId,LevelName);
        Party.Send("menu","");
        Allow=true;
        try{LeaveLevel();}finally{Allow=false;}
        Reset();
        SaveMgr.ClearBoard(target.LevelNumber,target.LevelId);
        restart=target;restartAt=Time.unscaledTime+.4f;
    }
    static void TickRestart(){
        if(!restart.HasValue)return;
        if(!Party.IsHost||!Party.PartnerPresent){restart=null;return;}
        if(Time.unscaledTime<restartAt||!OnMainMenu())return;
        var level=restart.Value;restart=null;
        UIMgr.EnterGame(level.type,level.number,level.id,level.name);
    }

	public static void OnBackToMenu()
	{
		if (!Allow && Active)
		{
            Party.Send(Party.IsHost?"menu":"level_exit", "");
			Reset();
		}
	}

	private static void TickStartButton()
	{
		if (!Active || Started)
		{
			ReadyButtonRect = default(Rect);
		}
		else if (_hiddenStart != null && !_hiddenStart.WasCollected)
		{
			ReadyButtonRect = PartnerBank.ToScreen(_hiddenStart.GetComponent<RectTransform>(), _startCanvas);
		}
		else
		{
			if (Time.unscaledTime < _nextStartSearch)
			{
				return;
			}
			_nextStartSearch = Time.unscaledTime + 0.25f;
			GameObject gameObject = FindStartButton();
			if (!(gameObject == null))
			{
				_startCanvas = gameObject.GetComponentInParent<Canvas>();
				gameObject.SetActive(value: false);
				_hiddenStart = gameObject;
				ManualLogSource logger = Plugin.Logger;
				bool isEnabled;
				BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(42, 1, out isEnabled);
				if (isEnabled)
				{
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral("co-op: replaced start button '");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(gameObject.name);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral("' with Ready");
				}
				logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			}
		}
	}

	private static GameObject FindStartButton()
	{
		foreach (TextMeshProUGUI item in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
		{
			string text = item.text;
			if (string.IsNullOrEmpty(text) || !item.gameObject.activeInHierarchy || (text.IndexOf("rock", StringComparison.OrdinalIgnoreCase) < 0 && !text.Contains("摇滚")))
			{
				continue;
			}
			Transform transform = item.transform;
			while (transform != null)
			{
				if (transform.GetComponent<UIButton>() != null || transform.GetComponent<TheButton>() != null || transform.GetComponent<InGameBtn>() != null)
				{
					return transform.gameObject;
				}
				transform = transform.parent;
			}
			return (item.transform.parent != null) ? item.transform.parent.gameObject : item.gameObject;
		}
		return null;
	}

	public static void ToggleReady()
	{
		if (Active && !Started)
		{
			SetReady(!MyReady);
		}
	}

	private static void RestoreStartButton()
	{
		if (!Started && _hiddenStart != null && !_hiddenStart.WasCollected)
		{
			_hiddenStart.SetActive(value: true);
		}
		_hiddenStart = null;
		ReadyButtonRect = default(Rect);
	}

	public static bool OnStartClicked(GameObject button, Action replay, string via)
	{
		if (Allow || !Active || Started)
		{
			return true;
		}
		if (button != null && !IsStartButton(button))
		{
			return true;
		}
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(58, 1, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("Start reached the game via ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(via);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("; freezing until both are ready");
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		_startedByGame = true;
		if (!MyReady)
		{
			SetReady(ready: true);
		}
		if (!Started)
		{
			Holding = true;
		}
		return true;
	}

	private static bool IsStartButton(GameObject go)
	{
		InGameUI instance = InGameUI.Instance;
		if (instance == null || instance.StartBattle == null || go == null)
		{
			return false;
		}
		Transform transform = instance.StartBattle.transform;
		Transform transform2 = go.transform;
		while (transform2 != null)
		{
			if (transform2 == transform)
			{
				return true;
			}
			transform2 = transform2.parent;
		}
		return false;
	}

    public static void SelectionChanged()
    {
        if(MyReady){MyReady=false;Party.Send("ready","0|");}
    }
	private static void SetReady(bool ready)
	{
		if (ready)
		{
            DeckSharing.RefreshNow();
            if(!DeckSharing.Ready){ShowToast("Finishing card selection sync. Press READY again.",2);return;}
			string text = PvP.CheckDeck();
			if (text != null)
			{
				ShowToast(text, 3f);
				return;
			}
		}
		MyReady = ready;
		MyDeck = (ready ? ReadDeck() : new System.Collections.Generic.List<(int, int)>());
		MySlotKeys=InGameUI.Instance==null?new():InGameUI.Instance.Cards.ToArray().Where(c=>c!=null).Select(DeckSharing.Key).ToList();
        Party.Send("ready", (ready ? "1" : "0") + "|" + string.Join(",", MyDeck.Select((d,i) => $"{d.type}:{d.cost}:{(i<MySlotKeys.Count?MySlotKeys[i]:d.type*2)}")));
		if (ready)
		{
			TryStart();
		}
	}

	public static void Tick()
	{
		if (_fakeReadyAt > 0f && Time.unscaledTime >= _fakeReadyAt)
		{
			_fakeReadyAt = -1f;
			OnMessage("ready", "1|0:100,1:50,2:150,3:50");
		}
        TickRestart();
        TickLoading();
		TickPendingLevel();
		TickStartButton();
		if (Active && !Started)
		{
			PvP.TickSeedScreen();
		}
		if (Holding)
		{
			Time.timeScale = 0f;
		}
	}

    static void TickLoading(){
        if(!Active||!Started||BattleLive)return;
        if(!loaded&&Board.Instance!=null&&GameAPP.theGameStatus==GameStatus.InGame){loaded=true;Party.Send("battle_loaded","");}
        TryLive();
    }
    static void TryLive(){
        if(!Party.IsHost||!loaded||BattleLive||Party.RemotePlayers.Any(id=>!TeamState.For(id).Loaded))return;
        BattleLive=true;Party.Send("battle_live","");ShowToast("All players loaded - battle started!",2);
    }

	private static System.Collections.Generic.List<(int, int)> ReadDeck()
	{
		System.Collections.Generic.List<(int, int)> list = new System.Collections.Generic.List<(int, int)>();
		InGameUI instance = InGameUI.Instance;
		if (instance?.Cards == null)
		{
			return list;
		}
		Il2CppSystem.Collections.Generic.List<CardUI>.Enumerator enumerator = instance.Cards.GetEnumerator();
		while (enumerator.MoveNext())
		{
			CardUI current = enumerator.Current;
			if (current != null)
			{
				list.Add(((int)current.thePlantType, current.theSeedCost));
			}
		}
		return list;
	}

	private static bool TryStart()
	{
        DeckSharing.RefreshNow();
		if (!Party.IsHost || !MyReady || !PartnerReady || Started || !DeckSharing.CanStart())
		{
			return false;
		}
		Party.Send("go", "");
		Go();
		return true;
	}

	private static void Go()
	{
		if (!Started)
		{
			Started = true;
			Holding = false;
			Time.timeScale = 1f;
			Sync.OnBattleStart();
			_hiddenStart = null;
			ReadyButtonRect = default(Rect);
            if (!_startedByGame && InGameUI.Instance != null)
            {
                Allow=true;
                try { if(SeedLibrary.Instance != null) SeedLibrary.Instance.OnStartGame(); else InGameUI.Instance.OnStartBattleButtonClick(); if(InitBoard.Instance != null) InitBoard.Instance.ready=true; GameAPP.theGameStatus=GameStatus.InInterlude; if(SeedLibrary.Instance != null) SeedLibrary.Instance.gameObject.SetActive(false); }
                finally { Allow=false; }
            }
			SharedSun.Start();
			ShowToast("All players ready - go!");
		}
	}

	private static bool OnMainMenu()
	{
		MainMenu mainMenu = UnityEngine.Object.FindObjectOfType<MainMenu>();
		if (mainMenu != null)
		{
			return mainMenu.isActiveAndEnabled;
		}
		return false;
	}

	private static void OpenHostLevel(string[] p)
	{
		Reset();
		Active = true;
		PvP.OnLevelOpened();
		TeamState.For(1).InLevel=true;
		Level = (LevelType)int.Parse(p[0]);
		LevelNumber = int.Parse(p[1]);LevelId=int.Parse(p[2]);LevelName=p.Length>3?p[3]:"";
        if(Level==LevelType.Advanture&&p.Length>4)CampaignProgress.Apply(p[4]);
		ShowToast($"{Party.PartnerName} started {Level} {LevelNumber}");
		Allow = true;
		try
		{
			UIMgr.EnterGame(Level, LevelNumber, int.Parse(p[2]), (p.Length > 3) ? p[3] : "");
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("EnterGame from host: " + ex);
			ShowToast("Could not open the host's level.");
		}
		finally
		{
			Allow = false;
		}
		Party.Send("inlevel", "");
	}

	private static void TickPendingLevel()
	{
		if (_pendingLevel != null && !(Time.unscaledTime < _nextPendingCheck))
		{
			_nextPendingCheck = Time.unscaledTime + 0.25f;
			if (OnMainMenu())
			{
				string[] pendingLevel = _pendingLevel;
				_pendingLevel = null;
				OpenHostLevel(pendingLevel);
			}
		}
	}

	private static void OnMessage(string type, string payload)
	{
		string[] array = payload.Split('|');
		switch (type)
		{
        case "battle_loaded":
            TeamState.For(Party.SenderId).Loaded=true;TryLive();break;
        case "battle_live":
            if(!Party.IsHost&&Started&&loaded)BattleLive=true;break;
        case "restart_request":
            if(Party.IsHost)RequestRestart();
            break;
		case "level":
			if (Party.IsHost)
			{
				break;
			}
			if (!OnMainMenu())
			{
				_pendingLevel = array;
				Plugin.Logger.LogInfo("host started a level while we are still in one: leaving it first");
				Allow = true;
				try
				{
					LeaveLevel();
					break;
				}
				catch (Exception ex2)
				{
					Plugin.Logger.LogError("leave for new level: " + ex2);
					break;
				}
				finally
				{
					Allow = false;
				}
			}
			OpenHostLevel(array);
			break;
		case "inlevel":
			PartnerInLevel = true;
			break;
		case "ready":
            PartnerSlotKeys=array.Length>1&&array[1].Length>0?array[1].Split(',').Select(x=>x.Split(':')).Select(p=>p.Length>2?int.Parse(p[2]):int.Parse(p[0])*2).ToList():new();
			PartnerReady = array[0] == "1";
			PartnerDeck = ((array.Length > 1 && array[1].Length > 0) ? (from s in array[1].Split(',')
				select s.Split(':') into a
				select (int.Parse(a[0]), (a.Length > 1) ? int.Parse(a[1]) : 0)).ToList() : new System.Collections.Generic.List<(int, int)>());
			ShowToast(Party.PartnerName + (TeamState.For(Party.SenderId).Ready ? " is ready" : " is not ready"), 2.5f);
            if(Started&&Party.SenderId==Party.PartnerPlayer){PartnerBank.Build();PartnerBank.SetCooldowns(TeamState.For(Party.SenderId).Cooldowns);}
			TryStart();
			break;
		case "go":
			if (!Party.IsHost)
			{
				Go();
			}
			break;
        case "level_exit":
            if(!Party.IsHost || !Active)break;
            Party.Send("menu", "");Reset();
            Allow=true;try{LeaveLevel();}finally{Allow=false;}
            break;
		case "menu":
			if (Party.IsHost || !Active)
			{
				break;
			}
			Reset();
			ShowToast(Party.PartnerName + " went back to the menu.");
			Allow = true;
			try
			{
				LeaveLevel();
				break;
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("EnterMainMenu: " + ex);
				break;
			}
			finally
			{
				Allow = false;
			}
		}
	}

	public static void FakePartnerReply(string type, string payload)
	{
		if (type == "level")
		{
			OnMessage("inlevel", "");
		}
		if (type == "ready" && payload.StartsWith("1"))
		{
			_fakeReadyAt = Time.unscaledTime + 10f;
		}
	}
}


