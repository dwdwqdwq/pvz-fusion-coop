using System;
using System.Collections.Generic;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public static class PvP
{
	public enum Team
	{
		None,
		Defense,
		Attack
	}

	public static bool Enabled;

	public static int TimeLimit = 300;

	public static Team MyTeam;

	public static Team PartnerTeam;

	public static Team Winner;

	public static float Remaining = -1f;

	public static bool Placing;

	public static int Placing_Plant;

	public const int RedLineColumn = 8;

	private const float StartCooldown = 30f;

	private const int StartBrains = 200;

	private const int BrainsPerBite = 5;

	private const float FallingSunShare = 1f;

	public static readonly int[] TimeChoices = new int[4] { 180, 300, 600, 0 };

	private static float _startedAt = -100f;

	private static float _nextIncome;

	private static float _nextTimeSend;

	private static float _nextBrainSend;

	private static int _pendingBrains;

	private static int _tempId;

	private static bool _zombiePageDone;

	public static int LevelNumber = 1;

	private static readonly System.Collections.Generic.Dictionary<IntPtr, int> _origCost = new System.Collections.Generic.Dictionary<IntPtr, int>();

	private static readonly System.Collections.Generic.Dictionary<int, (float readyAt, float length)> _cooldown = new System.Collections.Generic.Dictionary<int, (float, float)>();

	public static readonly Color RedSun = new Color(1f, 0.25f, 0.2f);

	private static int _placingType = -1;

	private static IZECard _placingCard;

	private static bool _placedThisClick;

	public static bool IsAttacker
	{
		get
		{
			if (Enabled)
			{
				return MyTeam == Team.Attack;
			}
			return false;
		}
	}

	public static bool IsDefender
	{
		get
		{
			if (Enabled)
			{
				return MyTeam == Team.Defense;
			}
			return false;
		}
	}

	public static bool CanStart
	{
		get
		{
			if (Party.IsHost && Party.PartnerPresent && MyTeam != Team.None && PartnerTeam != Team.None)
			{
				return MyTeam != PartnerTeam;
			}
			return false;
		}
	}

	public static string ResultText
	{
		get
		{
			if (Winner != Team.None)
			{
				return ((Winner == MyTeam) ? "YOU WIN!" : "YOU LOSE") + "\n" + ((Winner == Team.Attack) ? "A zombie got into the house." : "The plants held out until the end.");
			}
			return "";
		}
	}

	public static void Init()
	{
		Party.OnMessage += OnMessage;
	}

	private static string Code(Team t)
	{
		return t switch
		{
			Team.Defense => "D", 
			Team.Attack => "A", 
			_ => "N", 
		};
	}

	private static Team FromCode(string s)
	{
		if (!(s == "A"))
		{
			if (!(s == "D"))
			{
				return Team.None;
			}
			return Team.Defense;
		}
		return Team.Attack;
	}

	public static string Name(Team t)
	{
		return t switch
		{
			Team.Defense => "Defense (plants)", 
			Team.Attack => "Attack (zombies)", 
			_ => "not chosen", 
		};
	}

	public static string TimeName(int s)
	{
		if (s != 0)
		{
			return $"{s / 60} min";
		}
		return "Endless";
	}

	public static void ChooseTeam(Team t)
	{
		MyTeam = t;
		Party.Send("team", Code(t));
	}

	public static void ChooseTime(int seconds)
	{
		if (Party.IsHost)
		{
			TimeLimit = seconds;
			Party.Send("plimit", seconds.ToString());
		}
	}

	public static void Start()
	{
		if (!CanStart)
		{
			return;
		}
		(LevelType, int) tuple = FindDayLevel();
		LevelNumber = tuple.Item2;
		WipeSavedLawn();
		Enabled = true;
		Winner = Team.None;
		_startedAt = Time.unscaledTime;
		Party.Send("pvp", $"{TimeLimit}|{Code(MyTeam)}|{tuple.Item2}");
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(27, 4, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PvP: starting on ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(tuple.Item1);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(tuple.Item2);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(", ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(TimeName(TimeLimit));
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(", I am ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(MyTeam);
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		try
		{
			UIMgr.EnterGame(tuple.Item1, tuple.Item2, -1, "");
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("PvP start: " + ex);
			Enabled = false;
			Session.ShowToast("Could not start the PvP level.");
		}
	}

	private static (LevelType type, int number) FindDayLevel()
	{
		for (int i = 1; i <= 30; i++)
		{
			try
			{
				if (UIMgr.GetSceneType(LevelType.Survival, i) == SceneType.Day)
				{
					return (type: LevelType.Survival, number: i);
				}
			}
			catch
			{
			}
		}
		return (type: LevelType.Survival, number: 1);
	}

	public static void WipeSavedLawn()
	{
		try
		{
			SaveMgr.ClearBoard(LevelNumber);
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(36, 1, out isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PvP: cleared saved lawn of survival ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(LevelNumber);
			}
			logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning("PvP clear lawn: " + ex.Message);
		}
	}

	public static void OnMainMenu()
	{
		if (!(Time.unscaledTime - _startedAt < 8f) && !Session.PendingLevel && Enabled)
		{
			Enabled = false;
			Winner = Team.None;
			Remaining = -1f;
			Placing = false;
			if (Time.timeScale == 0f)
			{
				Time.timeScale = 1f;
			}
			RestorePrices();
			WipeSavedLawn();
		}
	}

	public static void OnLevelOpened()
	{
		_zombiePageDone = false;
	}

	public static void TickSeedScreen()
	{
		if (!IsAttacker || _zombiePageDone || Session.Started)
		{
			return;
		}
		SeedLibrary instance = SeedLibrary.Instance;
		if (instance == null || instance.buttons == null || instance.buttons.Count == 0)
		{
			return;
		}
		_zombiePageDone = true;
		try
		{
			Il2CppSystem.Collections.Generic.List<ZombieType> list = new Il2CppSystem.Collections.Generic.List<ZombieType>();
			foreach (ZombieType item in AttackerZombies())
			{
				list.Add(item);
			}
			System.Collections.Generic.List<string> list2 = new System.Collections.Generic.List<string>();
			Il2CppSystem.Collections.Generic.Dictionary<string, UIButton>.Enumerator enumerator2 = instance.buttons.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				Il2CppSystem.Collections.Generic.KeyValuePair<string, UIButton> current2 = enumerator2.Current;
				list2.Add(current2.Key);
			}
			instance.CreateNewTypeofCards("PvPZombies", list, createButton: true, "Zombies");
			foreach (string item2 in list2)
			{
				instance.HideCards(item2);
				if (instance.buttons.ContainsKey(item2) && instance.buttons[item2] != null)
				{
					instance.buttons[item2].gameObject.SetActive(value: false);
				}
			}
			instance.ShowCards("PvPZombies");
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(51, 3, out isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PvP: zombie page with ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(list.Count);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" zombies, hid ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(list2.Count);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" plant pages (");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(string.Join(", ", list2));
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(")");
			}
			logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("PvP zombie page: " + ex);
		}
	}

	private static System.Collections.Generic.IEnumerable<ZombieType> AttackerZombies()
	{
		foreach (ZombieType value in Enum.GetValues(typeof(ZombieType)))
		{
			if (value != ZombieType.Nothing)
			{
				ZombieDataManager.ZombieData zombieData = null;
				try
				{
					zombieData = ZombieDataManager.GetZombieData(value);
				}
				catch
				{
				}
				if (zombieData != null)
				{
					yield return value;
				}
			}
		}
	}

	public static void PriceZombie(ZombieDataManager.ZombieData d)
	{
		if (Enabled && d != null && d.cost <= 0)
		{
			_origCost[d.Pointer] = d.cost;
			float num = Math.Max(0, d.theMaxHealth) + Math.Max(0, d.theFirstArmorMaxHealth) + Math.Max(0, d.theSecondArmorMaxHealth);
			d.cost = Mathf.Clamp(Mathf.RoundToInt(num / 270f * 50f / 25f) * 25, 50, 1000);
		}
	}

	private static void RestorePrices()
	{
		foreach (ZombieType value2 in Enum.GetValues(typeof(ZombieType)))
		{
			ZombieDataManager.ZombieData zombieData = null;
			try
			{
				zombieData = ZombieDataManager.GetZombieData(value2);
			}
			catch
			{
			}
			if (zombieData != null && _origCost.TryGetValue(zombieData.Pointer, out var value))
			{
				zombieData.cost = value;
			}
		}
		_origCost.Clear();
	}

	private static float CooldownFor(ZombieType t, int cost)
	{
		float? num = Prices.ZombieCooldown(t);
		if (num.HasValue)
		{
			return Math.Max(0.1f, num.Value);
		}
		float num2 = 0f;
		try
		{
			num2 = ZombieDataManager.GetZombieData(t)?.cd ?? 0f;
		}
		catch
		{
		}
		return Mathf.Max(num2, (float)cost / 20f, 5f);
	}

	public static float CooldownLeft(ZombieType t)
	{
		if (!_cooldown.TryGetValue((int)t, out (float, float) value))
		{
			return 0f;
		}
		return Mathf.Clamp01((value.Item1 - Time.time) / value.Item2);
	}

	public static bool CanPickZombieCard(IZECard card)
	{
		if (!IsAttacker || card == null || CooldownLeft(card.theZombieType) <= 0f)
		{
			return true;
		}
		Session.ShowToast("That zombie is still recharging.", 1.5f);
		return false;
	}

	public static string CheckDeck()
	{
		if (!Enabled)
		{
			return null;
		}
		InGameUI instance = InGameUI.Instance;
		if (instance?.Cards == null)
		{
			return null;
		}
		int num = 0;
		int num2 = 0;
		Il2CppSystem.Collections.Generic.List<CardUI>.Enumerator enumerator = instance.Cards.GetEnumerator();
		while (enumerator.MoveNext())
		{
			CardUI current = enumerator.Current;
			if (!(current == null))
			{
				if (current.TryCast<IZECard>() != null)
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
		}
		if (IsAttacker && num2 > 0)
		{
			return "Attackers can only bring zombie cards.";
		}
		if (IsDefender && num > 0)
		{
			return "Defenders can only bring plant cards.";
		}
		if (num + num2 == 0)
		{
			return "Pick some cards first.";
		}
		return null;
	}

	public static void OnBattleStart()
	{
		if (!Enabled)
		{
			return;
		}
		Winner = Team.None;
		Remaining = ((TimeLimit > 0) ? TimeLimit : (-1));
		_pendingBrains = 0;
		_tempId = 0;
		_cooldown.Clear();
		if (IsAttacker && Board.Instance != null)
		{
			Board.Instance.theSun = 200;
			_nextIncome = Time.time + UnityEngine.Random.Range(20f, 25f);
			TintSunCounter();
			if (InGameUI.Instance?.Cards != null)
			{
				Il2CppSystem.Collections.Generic.List<CardUI>.Enumerator enumerator = InGameUI.Instance.Cards.GetEnumerator();
				while (enumerator.MoveNext())
				{
					IZECard iZECard = enumerator.Current?.TryCast<IZECard>();
					if (!(iZECard == null))
					{
						_cooldown[(int)iZECard.theZombieType] = (Time.time + 30f, 30f);
						iZECard.fullCD = 30f;
						iZECard.CD = 0f;
						iZECard.isAvailable = false;
					}
				}
			}
		}
		Session.ShowToast(IsAttacker ? "You attack! You pay with red suns. Place zombies right of the red line." : ("You defend! Survive" + ((TimeLimit > 0) ? (" for " + TimeName(TimeLimit) + ".") : " as long as you can.")), 6f);
	}

	private static void HideTools()
	{
		InGameUI instance = InGameUI.Instance;
		if (instance == null)
		{
			return;
		}
		GameObject[] array = new GameObject[2] { instance.ShovelBank, instance.GloveBank };
		foreach (GameObject gameObject in array)
		{
			if (gameObject != null && gameObject.activeSelf)
			{
				gameObject.SetActive(value: false);
			}
		}
	}

	private static void ShowTools()
	{
		InGameUI instance = InGameUI.Instance;
		if (instance == null)
		{
			return;
		}
		GameObject[] array = new GameObject[2] { instance.ShovelBank, instance.GloveBank };
		foreach (GameObject gameObject in array)
		{
			if (gameObject != null && !gameObject.activeSelf)
			{
				gameObject.SetActive(value: true);
			}
		}
	}

	public static void Tick()
	{
		if (IsAttacker && Session.Started)
		{
			HideTools();
		}
		if (IsDefender && Session.Started)
		{
			ShowTools();
		}
		if (!Enabled || !Sync.Running || Winner != Team.None)
		{
			return;
		}
		if (IsAttacker && Time.time >= _nextIncome)
		{
			_nextIncome = Time.time + UnityEngine.Random.Range(20f, 25f);
			AddBrains(UnityEngine.Random.Range(100, 151));
		}
		if (!Party.IsHost)
		{
			return;
		}
		if (_pendingBrains > 0 && Time.unscaledTime >= _nextBrainSend)
		{
			_nextBrainSend = Time.unscaledTime + 0.5f;
			Party.SendFast("brain", _pendingBrains.ToString(), reliable: true);
			_pendingBrains = 0;
		}
		if (Remaining > 0f)
		{
			Remaining -= Time.deltaTime;
			if (Time.unscaledTime >= _nextTimeSend)
			{
				_nextTimeSend = Time.unscaledTime + 1f;
				Party.SendFast("ptime", ((int)Remaining).ToString(), reliable: false);
			}
			if (Remaining <= 0f)
			{
				Finish(Team.Defense);
			}
		}
	}

	private static void AddBrains(int n)
	{
		if (Board.Instance == null)
		{
			return;
		}
		Session.Allow = true;
		try
		{
			Board.Instance.GetSun(n, save: false);
		}
		catch
		{
			Board.Instance.theSun += n;
		}
		finally
		{
			Session.Allow = false;
		}
	}

	private static void TintSunCounter()
	{
		GameObject gameObject = InGameUI.Instance?.SeedBank;
		if (gameObject == null)
		{
			return;
		}
		int num = 0;
		foreach (Image componentsInChild in gameObject.GetComponentsInChildren<Image>(includeInactive: true))
		{
			if (!(componentsInChild.GetComponentInParent<CardUI>() != null) && ((componentsInChild.sprite != null) ? componentsInChild.sprite.name : "").IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				componentsInChild.color = RedSun;
				num++;
			}
		}
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(26, 1, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PvP: tinted ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(num);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" sun icons red");
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
	}

	public static float ScaleIncome(float count, bool ours)
	{
		if (!IsAttacker || ours)
		{
			return count;
		}
		return Mathf.Max(5f, Mathf.Round(count * 1f));
	}

	public static void OnBite()
	{
		if (Enabled && Party.IsHost && Winner == Team.None)
		{
			if (MyTeam == Team.Attack)
			{
				AddBrains(5);
			}
			else
			{
				_pendingBrains += 5;
			}
		}
	}

	public static bool KeepCoin(CoinSun sun)
	{
		if (!IsAttacker)
		{
			return true;
		}
		if (sun.theMoveType == 0)
		{
			return false;
		}
		foreach (SpriteRenderer componentsInChild in sun.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
		{
			componentsInChild.color = RedSun;
		}
		return true;
	}

	public static bool OnPlaceZombie(Mouse m)
	{
		if (!Enabled)
		{
			return true;
		}
		if (!IsAttacker)
		{
			return false;
		}
		if (m.theMouseColumn < 8)
		{
			Session.ShowToast("Place zombies right of the red line.", 2f);
			return false;
		}
		IZECard theIZECardOnMouse = m.theIZECardOnMouse;
		if (theIZECardOnMouse != null && CooldownLeft(theIZECardOnMouse.theZombieType) > 0f)
		{
			Session.ShowToast("That zombie is still recharging.", 1.5f);
			return false;
		}
		_placingType = (int)((theIZECardOnMouse != null) ? theIZECardOnMouse.theZombieType : ZombieType.Nothing);
		_placingCard = theIZECardOnMouse;
		Placing = true;
		return true;
	}

	public static void AfterPlaceZombie(int sunBefore, int cost, bool wasPlacing)
	{
		bool num = wasPlacing && _placedThisClick;
		Placing = false;
		_placedThisClick = false;
		if (!num || Board.Instance == null)
		{
			return;
		}
		int theSun = Board.Instance.theSun;
		if (theSun >= sunBefore && cost > 0)
		{
			Board.Instance.theSun = Math.Max(0, theSun - cost);
		}
		if (_placingType >= 0)
		{
			float num2 = CooldownFor((ZombieType)_placingType, cost);
			_cooldown[_placingType] = (Time.time + num2, num2);
			if (_placingCard != null && !_placingCard.WasCollected)
			{
				_placingCard.fullCD = num2;
				_placingCard.CD = 0f;
				_placingCard.isAvailable = false;
			}
		}
	}

	public static void OnZombiePlaced()
	{
		_placedThisClick = true;
	}

	public static int NextTempId()
	{
		return --_tempId;
	}

	public static void Finish(Team winner)
	{
		if (Party.IsHost && Winner == Team.None)
		{
			Party.SendFast("pend", Code(winner), reliable: true);
			ShowResult(winner);
		}
	}

	private static void ShowResult(Team winner)
	{
		Winner = winner;
		Remaining = 0f;
		Time.timeScale = 0f;
		Plugin.Logger.LogInfo("PvP: " + winner.ToString() + " wins");
	}

	public static void BackToMenu()
	{
		Time.timeScale = 1f;
		Winner = Team.None;
		try
		{
			Session.LeaveLevel();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("PvP menu: " + ex);
		}
	}

	private static void OnMessage(string type, string payload)
	{
		string[] array = payload.Split('|');
		switch (type)
		{
		case "team":
			PartnerTeam = FromCode(array[0]);
			break;
		case "plimit":
			if (!Party.IsHost)
			{
				TimeLimit = int.Parse(array[0]);
			}
			break;
		case "pvp":
			if (!Party.IsHost)
			{
				TimeLimit = int.Parse(array[0]);
				PartnerTeam = FromCode(array[1]);
				MyTeam = ((PartnerTeam == Team.Attack) ? Team.Defense : Team.Attack);
				if (array.Length > 2)
				{
					LevelNumber = int.Parse(array[2]);
				}
				WipeSavedLawn();
				Enabled = true;
				Winner = Team.None;
				_startedAt = Time.unscaledTime;
			}
			break;
		case "brain":
			if (IsAttacker)
			{
				AddBrains(int.Parse(array[0]));
			}
			break;
		case "ptime":
			if (!Party.IsHost)
			{
				Remaining = int.Parse(array[0]);
			}
			break;
		case "pend":
			if (!Party.IsHost)
			{
				ShowResult(FromCode(array[0]));
			}
			break;
		}
	}
}
