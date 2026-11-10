using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace PvZCoop;

public static class Sync
{
	private static IntPtr _lastCannon;

	private static int _lastCannonFrame;

	private static int _inputDepth;

	private static int _inputFrame;

	private static int _spDepth;

	private static (int c, int r, int t, bool free, bool eff, Vector2 puff)? _pendingPlace;

	private static readonly System.Collections.Generic.Dictionary<(int, int, int), int> _missingHere = new System.Collections.Generic.Dictionary<(int, int, int), int>();

	private static readonly System.Collections.Generic.Dictionary<(int, int, int), int> _extraHere = new System.Collections.Generic.Dictionary<(int, int, int), int>();

	private static readonly System.Collections.Generic.Dictionary<(int, int), float> _cellCooldown = new System.Collections.Generic.Dictionary<(int, int), float>();

	private static readonly System.Collections.Generic.Dictionary<(int, int), float> _recentAction = new System.Collections.Generic.Dictionary<(int, int), float>();

	private const int Confirmations = 9;

	private const float CellCooldown = 30f;

	private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static int PartnerSun = -1;

	private static int _nextZombie = 1;

	private static readonly System.Collections.Generic.Dictionary<int, Zombie> _zById = new System.Collections.Generic.Dictionary<int, Zombie>();

	private static readonly System.Collections.Generic.Dictionary<IntPtr, int> _idByZ = new System.Collections.Generic.Dictionary<IntPtr, int>();

	private static readonly System.Collections.Generic.Dictionary<int, int> _zMissing = new System.Collections.Generic.Dictionary<int, int>();

	public static int SummonDepth;


	private static int _wave;

	private static float _nextFull;

	private static float _nextZs;


	private static float _nextMowers;

	private static float _nextCd;

	private static readonly System.Collections.Generic.HashSet<int> _pzSeen = new System.Collections.Generic.HashSet<int>();

	private static string _lastCd = "";


	private static TutorManager _tutor;

	private static int _hostTutor = -1;

	private static int _sentTutor = -1;

	private const float Zoom = 1.12f;

	private const float Lift = 0.035f;

	private static float _zoomAt = -1f;

	private static float _baseSize;

	private static Vector3 _basePos;

	private static Camera _cam;

	private static bool _zoomed;

	private static bool _hostSpawn;

	private static readonly System.Collections.Generic.Dictionary<int, (int column, Vector2 target)> _bungeeTargets = new System.Collections.Generic.Dictionary<int, (int, Vector2)>();

	private static bool PlayerAction
	{
		get
		{
			if (Running && !Allow && _inputDepth > 0)
			{
				return Time.frameCount == _inputFrame;
			}
			return false;
		}
	}

	public static bool Running
	{
		get
		{
			if (Session.Active && Session.Started && Party.InParty && Party.PartnerPresent)
			{
				return Board.Instance != null;
			}
			return false;
		}
	}

	public static bool IsHostRunning
	{
		get
		{
			if (Running)
			{
				return Party.IsHost;
			}
			return false;
		}
	}

	public static bool IsClient
	{
		get
		{
			if (Running)
			{
				return !Party.IsHost;
			}
			return false;
		}
	}

	private static bool Allow
	{
		get
		{
			return Session.Allow;
		}
		set
		{
			Session.Allow = value;
		}
	}

	public static void PatchCannons(Harmony h)
	{
		HarmonyMethod prefix = new HarmonyMethod(typeof(Sync).GetMethod("CannonPrefix", BindingFlags.Static | BindingFlags.NonPublic));
		int num = 0;
		Type[] types = typeof(CobCannon).Assembly.GetTypes();
		bool isEnabled;
		foreach (Type type in types)
		{
			if (type != typeof(CobCannon) && !type.IsSubclassOf(typeof(CobCannon)))
			{
				continue;
			}
			MethodInfo method = type.GetMethod("StartShoot", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
			if (method == null)
			{
				continue;
			}
			try
			{
				h.Patch(method, prefix);
				num++;
			}
			catch (Exception ex)
			{
				ManualLogSource logger = Plugin.Logger;
				BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(15, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("cannon patch ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(type.Name);
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(": ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
				}
				logger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
		}
		ManualLogSource logger2 = Plugin.Logger;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(28, 1, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("cannon sync on ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(num);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" cannon types");
		}
		logger2.LogInfo(bepInExInfoLogInterpolatedStringHandler);
	}

	private static void CannonPrefix(CobCannon __instance)
	{
		OnCannonShoot(__instance);
	}

	private static void OnCannonShoot(Plant p)
	{
		if (PlayerAction && !(p == null) && (!(p.Pointer == _lastCannon) || Time.frameCount != _lastCannonFrame))
		{
			_lastCannon = p.Pointer;
			_lastCannonFrame = Time.frameCount;
			Vector3 cannonTarget = p.cannonTarget;
			Party.SendFast("cob", $"{p.thePlantColumn}|{p.thePlantRow}|{(int)p.thePlantType}|{F(cannonTarget.x)}|{F(cannonTarget.y)}|{F(cannonTarget.z)}", reliable: true);
		}
	}

	private static void OnCannonMessage(string[] p)
	{
		CobCannon cobCannon = FindPlant(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]))?.TryCast<CobCannon>();
		if (cobCannon == null)
		{
			Plugin.Logger.LogWarning("partner fired a cannon we do not have");
			return;
		}
		Allow = true;
		try
		{
			cobCannon.cannonTarget = new Vector3(PF(p[3]), PF(p[4]), PF(p[5]));
			cobCannon.avaliable = true;
			cobCannon.StartShoot();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("cannon: " + ex.Message);
		}
		finally
		{
			Allow = false;
		}
	}

	private static void ResetPlants()
	{
		_inputDepth = (_spDepth = 0);
		_pendingPlace = null;
		_missingHere.Clear();
		_extraHere.Clear();
		_cellCooldown.Clear();
		_recentAction.Clear();
	}

	private static void TickInput()
	{
		if (Time.frameCount != _inputFrame)
		{
			_inputDepth = 0;
			_spDepth = 0;
			_pendingPlace = null;
		}
	}

	public static void BeginAction()
	{
		if (Running && !Allow)
		{
			if (Time.frameCount != _inputFrame)
			{
				_inputDepth = 0;
				_spDepth = 0;
			}
			_inputFrame = Time.frameCount;
			_inputDepth++;
		}
	}

	public static void EndAction()
	{
		if (_inputDepth > 0)
		{
			_inputDepth--;
		}
	}

	private static void MarkAction(int c, int r)
	{
		_recentAction[(c, r)] = Time.unscaledTime + 4f;
	}

	public static void OnSetPlantPre(int c, int r, PlantType type, Vector2 puff, bool free, bool effect)
	{
		if (Running)
		{
			_spDepth++;
			if (_spDepth == 1)
			{
				_pendingPlace = (PlayerAction ? new(int, int, int, bool, bool, Vector2)?((c, r, (int)type, free, effect, puff)) : (((int, int, int, bool, bool, Vector2)?)null));
			}
		}
	}

	public static void OnSetPlantPost(Plant result)
	{
		if (Running && _spDepth != 0 && --_spDepth <= 0 && _pendingPlace.HasValue)
		{
			(int, int, int, bool, bool, Vector2) value = _pendingPlace.Value;
			_pendingPlace = null;
			if (!(result == null))
			{
				MarkAction(value.Item1, value.Item2);
				Party.SendFast("sp", $"{value.Item1}|{value.Item2}|{value.Item3}|{(value.Item4 ? 1 : 0)}|{(value.Item5 ? 1 : 0)}|{F(value.Item6.x)}|{F(value.Item6.y)}", reliable: true);
			}
		}
	}

	public static void OnPlantDie(Plant p, Plant.DieReason reason)
	{
		if (Running && !Allow && !(p == null) && _spDepth <= 0)
		{
			bool playerAction = PlayerAction;
			bool flag = Party.IsHost && (p.thePlantHealth <= 0 || reason == Plant.DieReason.Crash || reason == Plant.DieReason.CrashInWater || reason == Plant.DieReason.ByFreeze || reason == Plant.DieReason.BySteal);
			if (playerAction || flag)
			{
				MarkAction(p.thePlantColumn, p.thePlantRow);
				Party.SendFast("pd", $"{p.thePlantColumn}|{p.thePlantRow}|{(int)p.thePlantType}|{(int)reason}", reliable: true);
			}
		}
	}

	public static void OnPlantMove(Plant p, int c2, int r2)
	{
		if (PlayerAction && !(p == null) && (p.thePlantColumn != c2 || p.thePlantRow != r2))
		{
			MarkAction(p.thePlantColumn, p.thePlantRow);
			MarkAction(c2, r2);
			Party.SendFast("mv", $"{p.thePlantColumn}|{p.thePlantRow}|{(int)p.thePlantType}|{c2}|{r2}", reliable: true);
		}
	}

	private static System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Plant>> ReadLawn()
	{
		System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Plant>> dictionary = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Plant>>();
		Il2CppSystem.Collections.Generic.List<Plant> list = Board.Instance?.boardEntity?.plantArray;
		if (list == null)
		{
			return dictionary;
		}
		Il2CppSystem.Collections.Generic.List<Plant>.Enumerator enumerator = list.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Plant current = enumerator.Current;
			if (!(current == null) && !current.WasCollected && current.thePlantColumn >= 0 && current.thePlantRow >= 0)
			{
				(int, int) key = (current.thePlantColumn, current.thePlantRow);
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = (dictionary[key] = new System.Collections.Generic.List<Plant>());
				}
				value.Add(current);
			}
		}
		return dictionary;
	}

	private static Plant FindPlant(int c, int r, int type)
	{
		Il2CppSystem.Collections.Generic.List<Plant> list = Board.Instance?.boardEntity?.plantArray;
		if (list == null)
		{
			return null;
		}
		Il2CppSystem.Collections.Generic.List<Plant>.Enumerator enumerator = list.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Plant current = enumerator.Current;
			if (current != null && !current.WasCollected && current.thePlantColumn == c && current.thePlantRow == r && current.thePlantType == (PlantType)type)
			{
				return current;
			}
		}
		return null;
	}

	private static Plant Place(int c, int r, int type, bool free, bool effect, Vector2 puff)
	{
		int sharedBefore=SharedSun.BeforeReplica();
		Allow = true;
		try
		{
			return CreatePlant.Instance.SetPlant(c, r, (PlantType)type, (Plant)null, puff, free, effect, (Plant)null);
		}
		catch (Exception ex)
		{
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(13, 4, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral("place ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(type);
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" at ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(c);
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(",");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(r);
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(": ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(ex.Message);
			}
			logger.LogError(bepInExErrorLogInterpolatedStringHandler);
			return null;
		}
		finally
		{
            SharedSun.AfterReplica(sharedBefore);
			Allow = false;
		}
	}

	private static void Kill(Plant p, Plant.DieReason reason)
	{
		Allow = true;
		try
		{
			p.Die(reason);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("plant die: " + ex.Message);
		}
		finally
		{
			Allow = false;
		}
	}

	private static void Move(Plant p, int c2, int r2)
	{
		Allow = true;
		try
		{
			Vector3 position = p.transform.position;
			p.MoveBox(c2, r2);
			if ((p.transform.position - position).sqrMagnitude < 0.0001f && Mouse.Instance != null)
			{
				float boxXFromColumn = Mouse.Instance.GetBoxXFromColumn(c2);
				float landY = Mouse.Instance.GetLandY(boxXFromColumn, r2);
				p.transform.position = new Vector3(boxXFromColumn, landY, position.z);
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("plant move: " + ex.Message);
		}
		finally
		{
			Allow = false;
		}
	}

	private static void OnPlantMessage(string type, string[] p, string payload)
	{
		switch (type)
		{
		case "sp":
		{
			int num3 = int.Parse(p[0]);
			int num4 = int.Parse(p[1]);
			MarkAction(num3, num4);
			if (Place(num3, num4, int.Parse(p[2]), p[3] == "1", p[4] == "1", new Vector2(PF(p[5]), PF(p[6]))) == null)
			{
				ManualLogSource logger = Plugin.Logger;
				bool isEnabled;
				BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(44, 3, out isEnabled);
				if (isEnabled)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("partner placed ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted((PlantType)int.Parse(p[2]));
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" at ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(num3);
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(",");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(num4);
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(" but it did not fit here");
				}
				logger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
			break;
		}
		case "pd":
		{
			int c2 = int.Parse(p[0]);
			int r2 = int.Parse(p[1]);
			MarkAction(c2, r2);
			Plant plant2 = FindPlant(c2, r2, int.Parse(p[2]));
			if (plant2 != null)
			{
				Kill(plant2, (Plant.DieReason)int.Parse(p[3]));
			}
			break;
		}
		case "mv":
		{
			int c = int.Parse(p[0]);
			int r = int.Parse(p[1]);
			int num = int.Parse(p[3]);
			int num2 = int.Parse(p[4]);
			MarkAction(c, r);
			MarkAction(num, num2);
			Plant plant = FindPlant(c, r, int.Parse(p[2]));
			if (plant != null)
			{
				Move(plant, num, num2);
			}
			break;
		}
		case "lawn":
			OnFullLawn(payload);
			break;
		}
	}

	private static void SendFullLawn()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (System.Collections.Generic.KeyValuePair<(int, int), System.Collections.Generic.List<Plant>> item in ReadLawn())
		{
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(';');
			}
			stringBuilder.Append(item.Key.Item1).Append(',').Append(item.Key.Item2)
				.Append(',')
				.Append(string.Join(".", item.Value.Select((Plant x) => (int)x.thePlantType)))
				.Append(',')
				.Append(string.Join(".", item.Value.Select((Plant x) => x.thePlantHealth)));
		}
		Party.SendFast("lawn", stringBuilder.ToString(), reliable: false);
	}

	private static void OnFullLawn(string payload)
	{
		System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<(int, int)>> dictionary = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<(int, int)>>();
		string[] array = payload.Split(';', StringSplitOptions.RemoveEmptyEntries);
		foreach (string obj in array)
		{
			string[] array2 = obj.Split(',');
			System.Collections.Generic.List<int> source = array2[2].Split('.', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
			System.Collections.Generic.List<int> hps = array2[3].Split('.', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
			dictionary[(int.Parse(array2[0]), int.Parse(array2[1]))] = source.Select((int item2, int num3) => (t: item2, (num3 < hps.Count) ? hps[num3] : (-1))).ToList();
		}
		System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Plant>> dictionary2 = ReadLawn();
		float unscaledTime = Time.unscaledTime;
		System.Collections.Generic.HashSet<(int, int, int)> seenMissing = new System.Collections.Generic.HashSet<(int, int, int)>();
		System.Collections.Generic.HashSet<(int, int, int)> seenExtra = new System.Collections.Generic.HashSet<(int, int, int)>();
		foreach (var item2 in dictionary.Keys.Union(dictionary2.Keys).ToList())
		{
			dictionary.TryGetValue(item2, out var value);
			if (value == null)
			{
				value = new System.Collections.Generic.List<(int, int)>();
			}
			dictionary2.TryGetValue(item2, out var value2);
			if (value2 == null)
			{
				value2 = new System.Collections.Generic.List<Plant>();
			}
			float value3;
			bool flag = _recentAction.TryGetValue(item2, out value3) && unscaledTime < value3;
			System.Collections.Generic.List<Plant> list = value2.ToList();
			bool isEnabled;
			foreach (var item3 in value)
			{
				int t = item3.Item1;
				int item = item3.Item2;
				Plant plant = list.FirstOrDefault((Plant x) => x.thePlantType == (PlantType)t);
				if (plant != null)
				{
					list.Remove(plant);
					if (item >= 0 && item < plant.thePlantHealth)
					{
						Allow = true;
						try
						{
							plant.TakeDamage(plant.thePlantHealth - item, null, DamageType.RealDamage, PlantType.Nothing, fix: true);
						}
						catch (Exception ex)
						{
							Plugin.Logger.LogWarning("plant damage sync: " + ex.Message);
						}
						finally
						{
							Allow = false;
						}
					}
					if (item >= 0 && !plant.WasCollected)
					{
						plant.thePlantHealth = item;
					}
				}
				else
				{
					if (flag)
					{
						continue;
					}
					(int, int, int) tuple = (item2.Item1, item2.Item2, t);
					seenMissing.Add(tuple);
					int value4;
					int i = (_missingHere[tuple] = ((!_missingHere.TryGetValue(tuple, out value4)) ? 1 : (value4 + 1)));
					if (i >= 9 && (!_cellCooldown.TryGetValue(item2, out var value5) || !(unscaledTime < value5)))
					{
						_cellCooldown[item2] = unscaledTime + 30f;
						_missingHere.Remove(tuple);
						ManualLogSource logger = Plugin.Logger;
						BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(21, 2, out isEnabled);
						if (isEnabled)
						{
							bepInExInfoLogInterpolatedStringHandler.AppendLiteral("lawn fix: adding ");
							bepInExInfoLogInterpolatedStringHandler.AppendFormatted((PlantType)t);
							bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" at ");
							bepInExInfoLogInterpolatedStringHandler.AppendFormatted(item2);
						}
						logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
						Plant plant2 = Place(item2.Item1, item2.Item2, t, free: true, effect: false, Vector2.zero);
						if (plant2 != null && item > 0)
						{
							plant2.thePlantHealth = item;
						}
					}
				}
			}
			if (flag)
			{
				continue;
			}
			foreach (Plant item4 in list)
			{
				(int, int, int) tuple2 = (item2.Item1, item2.Item2, (int)item4.thePlantType);
				seenExtra.Add(tuple2);
				int value6;
				int i = (_extraHere[tuple2] = ((!_extraHere.TryGetValue(tuple2, out value6)) ? 1 : (value6 + 1)));
				if (i >= 9)
				{
					_extraHere.Remove(tuple2);
					ManualLogSource logger2 = Plugin.Logger;
					BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(23, 2, out isEnabled);
					if (isEnabled)
					{
						bepInExInfoLogInterpolatedStringHandler.AppendLiteral("lawn fix: removing ");
						bepInExInfoLogInterpolatedStringHandler.AppendFormatted(item4.thePlantType);
						bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" at ");
						bepInExInfoLogInterpolatedStringHandler.AppendFormatted(item2);
					}
					logger2.LogInfo(bepInExInfoLogInterpolatedStringHandler);
					Kill(item4, Plant.DieReason.Hid);
				}
			}
		}
		foreach (var item5 in _missingHere.Keys.Where(((int, int, int) k) => !seenMissing.Contains(k)).ToList())
		{
			_missingHere.Remove(item5);
		}
		foreach (var item6 in _extraHere.Keys.Where(((int, int, int) k) => !seenExtra.Contains(k)).ToList())
		{
			_extraHere.Remove(item6);
		}
	}

	private static string F(float v)
	{
		return v.ToString("0.###", Inv);
	}

	private static float PF(string s)
	{
		return float.Parse(s, Inv);
	}

	public static void OnPause(bool paused)
	{
		if (Running && !Allow)
		{
			Party.SendFast(paused ? "pause" : "resume", "", reliable: true);
		}
	}

	public static void Init()
	{
		Party.OnMessage += OnMessage;
	}

	public static void Reset()
	{
		_zById.Clear();
		_idByZ.Clear();
		_zMissing.Clear();
		_pzSeen.Clear();
		_bungeeTargets.Clear();
		_hostSpawn = false;
		ResetPlants();
		_nextZombie = 1;
		SummonDepth = 0;
		PartnerSun = -1;
		_lastCd = "";
		_hostTutor = (_sentTutor = -1);
		_tutor = null;
        ResetCamera();
        PartnerBank.Clear();
        PlacementPreview.Reset();
	}

	public static void OnBattleStart()
	{
		Reset();
		if (PvP.Enabled)
		{
			PvP.OnBattleStart();
			return;
		}
		PartnerBank.Build();
		_zoomAt = Time.unscaledTime + 4f;
	}

	private static TutorManager Tutor()
	{
		if (_tutor == null || _tutor.WasCollected)
		{
			_tutor = UnityEngine.Object.FindObjectOfType<TutorManager>();
		}
		return _tutor;
	}

	private static void TickTutor()
	{
		if (Party.IsHost)
		{
			if (Time.frameCount % 15 == 0)
			{
				TutorManager tutorManager = Tutor();
				if (tutorManager != null && tutorManager.progress != _sentTutor)
				{
					_sentTutor = tutorManager.progress;
					Party.SendFast("tut", tutorManager.progress.ToString(), reliable: true);
				}
			}
		}
		else if (_hostTutor >= 0)
		{
			TutorManager tutorManager2 = Tutor();
			if (tutorManager2 != null && tutorManager2.progress != _hostTutor)
			{
				tutorManager2.progress = _hostTutor;
			}
		}
	}

	private static void TickCamera()
	{
		if (_zoomAt < 0f || Time.unscaledTime < _zoomAt || !PartnerBank.Visible)
		{
			return;
		}
		if (!_zoomed)
		{
			_cam = Camera.main;
			if (_cam == null || !_cam.orthographic)
			{
				_zoomAt = -1f;
				return;
			}
			_baseSize = _cam.orthographicSize;
			_basePos = _cam.transform.position;
			_zoomed = true;
		}
		if (!(_cam == null) && !_cam.WasCollected)
		{
			float num = _baseSize * 1.12f;
			if (Mathf.Abs(_cam.orthographicSize - num) > 0.001f)
			{
				_cam.orthographicSize = num;
				_cam.transform.position = _basePos - new Vector3(0f, 0.07f * num, 0f);
			}
		}
	}

	private static void ResetCamera()
	{
		if (_zoomed && _cam != null && !_cam.WasCollected)
		{
			_cam.orthographicSize = _baseSize;
			_cam.transform.position = _basePos;
		}
		_zoomed = false;
		_zoomAt = -1f;
		_cam = null;
	}

	public static void Tick()
	{
		if (!Running)
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		TickInput();
		if (Party.IsHost)
		{
			if (unscaledTime >= _nextFull)
			{
				_nextFull = unscaledTime + 0.5f;
				SendFullLawn();
			}
			if (unscaledTime >= _nextZs)
			{
				_nextZs = unscaledTime + 0.3f;
				SendZombieSnapshot();
			}
			if (unscaledTime >= _nextMowers)
			{
				_nextMowers = unscaledTime + 1f;
				SendMowers();
			}
		}
		PvP.Tick();
		if (PvP.Enabled)
		{
			TickTutor();
			return;
		}

		if (unscaledTime >= _nextCd)
		{
			_nextCd = unscaledTime + 0.2f;
			string text = PartnerBank.ReadMyCooldowns();
			if (text != _lastCd)
			{
				_lastCd = text;
				Party.SendFast("cd", text, reliable: false);
			}
		}
		PartnerBank.Tick();
		TickTutor();
        // Keep the game camera entirely under game control.
	}

	private static int RegisterZombie(Zombie z, int id)
	{
		_zById[id] = z;
		_idByZ[z.Pointer] = id;
		return id;
	}

	private static void ForgetZombie(int id)
	{
		if (_zById.TryGetValue(id, out var value) && value != null)
		{
			_idByZ.Remove(value.Pointer);
		}
		_zById.Remove(id);
		_zMissing.Remove(id);
	}

	public static bool IsSynced(Zombie z)
	{
		if (z != null)
		{
			return _idByZ.ContainsKey(z.Pointer);
		}
		return false;
	}

	public static void OnSummonStart(int wave)
	{
		SummonDepth++;
		_wave = wave;
	}

	public static void OnSummonEnd()
	{
		if (SummonDepth > 0)
		{
			SummonDepth--;
		}
	}

	public static void OnZombieCreated(Zombie z, int row, ZombieType type, float x, bool mind)
	{
		if (PvP.Placing && Running && z != null && !Allow)
		{
			PvP.OnZombiePlaced();
			if (Party.IsHost)
			{
				int value = RegisterZombie(z, _nextZombie++);
				Party.SendFast("zn", $"{value}|{row}|{(int)type}|{F(x)}|0", reliable: true);
			}
			else
			{
				int value2 = RegisterZombie(z, PvP.NextTempId());
				Party.SendFast("pz", $"{value2}|{row}|{(int)type}|{F(x)}", reliable: true);
			}
		}
		else
		{
			if (!IsHostRunning || z == null || Allow)
			{
				return;
			}
			int value3 = RegisterZombie(z, _nextZombie++);
			Party.SendFast("zn", $"{value3}|{row}|{(int)type}|{F(x)}|{(mind ? 1 : 0)}", reliable: true);
		}
	}

	public static bool BlockLocalZombie()
	{
		if (IsClient && !_hostSpawn)
		{
			return !PvP.Placing;
		}
		return false;
	}

	public static void OnBungeeTarget(BungiZombie b)
	{
		if (b == null)
		{
			return;
		}
		if (IsHostRunning)
		{
			if (_idByZ.TryGetValue(b.Pointer, out var value))
			{
				Party.SendFast("bt", $"{value}|{b.targetColumn}|{F(b.target.x)}|{F(b.target.y)}", reliable: true);
			}
		}
		else if (IsClient)
		{
			ApplyBungeeTarget(b);
		}
	}

	private static void ApplyBungeeTarget(Zombie z)
	{
		BungiZombie bungiZombie = z.TryCast<BungiZombie>();
		if (!(bungiZombie == null) && _idByZ.TryGetValue(z.Pointer, out var value) && _bungeeTargets.TryGetValue(value, out (int, Vector2) value2))
		{
			(bungiZombie.targetColumn, bungiZombie.target) = value2;
			if (bungiZombie.bungeeTarget != null)
			{
				Vector3 position = bungiZombie.bungeeTarget.transform.position;
				bungiZombie.bungeeTarget.transform.position = new Vector3(value2.Item2.x, value2.Item2.y, position.z);
			}
		}
	}

	public static void OnZombieDie(Zombie z, int reason)
	{
		if (IsHostRunning && !(z == null) && _idByZ.TryGetValue(z.Pointer, out var value))
		{
			Party.SendFast("zd", $"{value}|{(int)reason}", reliable: true);
			ForgetZombie(value);
		}
	}

	private static void SendZombieSnapshot()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (var (num2, zombie2) in _zById.ToList())
		{
			if (zombie2 == null || zombie2.WasCollected)
			{
				Party.SendFast("zd", $"{num2}|0", reliable: true);
				ForgetZombie(num2);
				continue;
			}
			Vector3 position = zombie2.transform.position;
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(';');
			}
			stringBuilder.Append(num2).Append(',').Append(F(position.x))
				.Append(',')
				.Append(F(position.y))
				.Append(',')
				.Append(zombie2.theZombieRow)
				.Append(',')
				.Append(zombie2.theHealth)
				.Append(',')
				.Append(zombie2.theFirstArmorHealth)
				.Append(',')
				.Append(zombie2.theSecondArmorHealth);
		}
		Party.SendFast("zs", stringBuilder.ToString(), reliable: false);
	}

	private static void OnZombieSnapshot(string payload)
	{
		System.Collections.Generic.HashSet<int> hashSet = new System.Collections.Generic.HashSet<int>();
		string[] array = payload.Split(';', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(',');
			int num = int.Parse(array2[0]);
			hashSet.Add(num);
			if (!_zById.TryGetValue(num, out var value) || value == null || value.WasCollected)
			{
				continue;
			}
			float num2 = PF(array2[1]);
			float num3 = PF(array2[2]);
			Vector3 position = value.transform.position;
			if (Mathf.Abs(position.x - num2) > 0.3f || Mathf.Abs(position.y - num3) > 0.3f)
			{
				value.transform.position = new Vector3(Mathf.Lerp(position.x, num2, 0.5f), Mathf.Lerp(position.y, num3, 0.5f), position.z);
			}
			int num4 = int.Parse(array2[3]);
			if (value.theZombieRow != num4)
			{
				value.theZombieRow = num4;
			}
			long num5 = long.Parse(array2[4]);
			int num6 = int.Parse(array2[5]);
			int num7 = int.Parse(array2[6]);
			long num8 = value.theHealth + Math.Max(0, value.theFirstArmorHealth) + Math.Max(0, value.theSecondArmorHealth);
			long num9 = num5 + Math.Max(0, num6) + Math.Max(0, num7);
			if (num8 > num9)
			{
				Allow = true;
				try
				{
					value.TakeDamage((int)Math.Min(num8 - num9, 2147483647L), null, DamageType.Normal, PlantType.Nothing, fix: true);
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogWarning("zombie damage sync: " + ex.Message);
				}
				finally
				{
					Allow = false;
				}
			}
			if (!value.WasCollected)
			{
				value.theHealth = num5;
				value.theFirstArmorHealth = num6;
				value.theSecondArmorHealth = num7;
			}
		}
		foreach (int item in _zById.Keys.ToList())
		{
			if (hashSet.Contains(item))
			{
				_zMissing.Remove(item);
				continue;
			}
			_zMissing[item] = ((!_zMissing.TryGetValue(item, out var value2)) ? 1 : (value2 + 1));
			if (_zMissing[item] >= 3)
			{
				KillLocal(item, 0);
			}
		}
	}

	private static void KillLocal(int id, int reason)
	{
		if (_zById.TryGetValue(id, out var value) && value != null && !value.WasCollected)
		{
			Allow = true;
			try
			{
				value.Die(reason);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("zombie die: " + ex);
			}
			finally
			{
				Allow = false;
			}
		}
		ForgetZombie(id);
	}

	public static void OnCoinCreated(GameObject go)
	{
		if (!Running || go == null || Allow)
		{
			return;
		}
		CoinSun component = go.GetComponent<CoinSun>();
		if (component == null)
		{
			if (PvP.IsAttacker)
			{
				UnityEngine.Object.Destroy(go);
			}
		}
		else if (PvP.Enabled)
		{
			if (!PvP.KeepCoin(component))
			{
				UnityEngine.Object.Destroy(go);
			}
		}
		else if (IsClient)
		{
			UnityEngine.Object.Destroy(go);
		}
	}

    public static bool OnGetSun(ref float count,bool save)
    {
        if(PvP.Enabled){count=PvP.ScaleIncome(count,Allow);return true;}
        return count<=0 || SharedSun.CanReceiveIncome;
    }

	private static void OnMessage(string type, string payload)
	{
        if(type=="bal"||type=="sg")return;
		if (!Running && type != "bal")
		{
			return;
		}
		string[] array = payload.Split('|');
		if (type == null)
		{
			return;
		}
		switch (type.Length)
		{
		case 2:
			switch (type[1])
			{
			default:
				return;
			case 'p':
				if (!(type == "sp"))
				{
					return;
				}
				break;
			case 'd':
				switch (type)
				{
				default:
					return;
				case "pd":
					break;
				case "zd":
					if (!Party.IsHost)
					{
						KillLocal(int.Parse(array[0]), int.Parse(array[1]));
					}
					return;
				case "cd":
					PartnerBank.SetCooldowns(payload);
					return;
				}
				break;
			case 'v':
				if (!(type == "mv"))
				{
					return;
				}
				break;
			case 'z':
				if (!(type == "pz") || !Party.IsHost || !_pzSeen.Add(int.Parse(array[0])))
				{
					return;
				}
				Allow = true;
				try
				{
					Zombie zombie = CreateZombie.Instance.SetZombie(int.Parse(array[1]), (ZombieType)int.Parse(array[2]), PF(array[3]), false);
					if (zombie != null)
					{
						int value = RegisterZombie(zombie, _nextZombie++);
						Party.SendFast("zn", $"{value}|{array[1]}|{array[2]}|{array[3]}|0|{array[0]}", reliable: true);
					}
					return;
				}
				catch (Exception ex5)
				{
					Plugin.Logger.LogError("pz: " + ex5);
					return;
				}
				finally
				{
					Allow = false;
				}
			case 'n':
			{
				if (!(type == "zn") || Party.IsHost)
				{
					return;
				}
				if (array.Length > 5 && _zById.TryGetValue(int.Parse(array[5]), out var value3) && value3 != null)
				{
					ForgetZombie(int.Parse(array[5]));
					RegisterZombie(value3, int.Parse(array[0]));
					return;
				}
				Allow = true;
				_hostSpawn = true;
				try
				{
					Zombie zombie2 = CreateZombie.Instance.SetZombie(int.Parse(array[1]), (ZombieType)int.Parse(array[2]), PF(array[3]), array[4] == "1");
					if (zombie2 != null)
					{
						RegisterZombie(zombie2, int.Parse(array[0]));
					}
					return;
				}
				catch (Exception ex6)
				{
					Plugin.Logger.LogError("zn: " + ex6);
					return;
				}
				finally
				{
					Allow = false;
					_hostSpawn = false;
				}
			}
			case 't':
				if (type == "bt" && !Party.IsHost)
				{
					int key = int.Parse(array[0]);
					_bungeeTargets[key] = (int.Parse(array[1]), new Vector2(PF(array[2]), PF(array[3])));
					if (_zById.TryGetValue(key, out var value2) && value2 != null && !value2.WasCollected)
					{
						ApplyBungeeTarget(value2);
					}
				}
				return;
			case 's':
				if (type == "zs" && !Party.IsHost)
				{
					OnZombieSnapshot(payload);
				}
				return;
			case 'g':
				if (!(type == "sg") || Party.IsHost)
				{
					return;
				}
				Allow = true;
				try
				{
					Board.Instance.GetSun(PF(array[0]), array.Length > 1 && array[1] == "1");
					return;
				}
				catch (Exception ex4)
				{
					Plugin.Logger.LogError("sg: " + ex4);
					return;
				}
				finally
				{
					Allow = false;
				}
			}
			goto IL_0258;
		case 4:
			switch (type[1])
			{
			default:
				return;
			case 'a':
				break;
			case 'o':
				if (!(type == "lose") || Party.IsHost)
				{
					return;
				}
				Allow = true;
				try
				{
					GameLose gameLose = UnityEngine.Object.FindObjectOfType<GameLose>();
					if (gameLose != null)
					{
						gameLose.HandleGameLose(Board.Instance);
					}
					else
					{
						UIMgr.EnterLoseMenu();
					}
					return;
				}
				catch (Exception ex2)
				{
					Plugin.Logger.LogError("lose: " + ex2);
					return;
				}
				finally
				{
					Allow = false;
				}
			}
			if (!(type == "lawn") || Party.IsHost)
			{
				break;
			}
			goto IL_0258;
		case 3:
			switch (type[2])
			{
			case 'b':
				if (type == "cob")
				{
					OnCannonMessage(array);
				}
				break;
			case 'l':
				if (type == "bal")
				{
					PartnerSun = int.Parse(array[0]);
				}
				break;
			case 't':
				if (type == "tut" && !Party.IsHost)
				{
					_hostTutor = int.Parse(array[0]);
				}
				break;
			case 'w':
				if (type == "mow" && !Party.IsHost)
				{
					StartMower(int.Parse(array[0]));
				}
				break;
			case 's':
				if (type == "mws" && !Party.IsHost)
				{
					string[] array2 = array[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
					for (int i = 0; i < array2.Length; i++)
					{
						StartMower(int.Parse(array2[i]));
					}
				}
				break;
			case 'n':
				if (!(type == "win") || Party.IsHost)
				{
					break;
				}
				Allow = true;
				try
				{
					Board.Instance.boardVicotry.Win();
				}
				catch (Exception ex7)
				{
					Plugin.Logger.LogError("win: " + ex7);
				}
				finally
				{
					Allow = false;
				}
				Session.ShowToast("Victory!");
				break;
			}
			break;
		case 5:
			if (!(type == "pause"))
			{
				break;
			}
			Allow = true;
			try
			{
				UIMgr.EnterPauseMenu();
			}
			catch (Exception ex3)
			{
				Plugin.Logger.LogError("pause: " + ex3);
			}
			finally
			{
				Allow = false;
			}
			Session.ShowToast(Party.PartnerName + " paused the game.");
			break;
		case 6:
			{
				if (!(type == "resume"))
				{
					break;
				}
				Allow = true;
				try
				{
					UIMgr.BackToGame();
					break;
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogError("resume: " + ex);
					break;
				}
				finally
				{
					Allow = false;
				}
			}
			IL_0258:
			OnPlantMessage(type, array, payload);
			break;
		}
	}

	public static void OnMowerStart(Mower m)
	{
		if (m != null && !m.started)
		{
			Party.SendFast("mow", m.theMowerRow.ToString(), reliable: true);
		}
	}

	private static void SendMowers()
	{
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
		Il2CppSystem.Collections.Generic.List<Mower> list2 = Board.Instance?.mowerArray;
		if (list2 != null)
		{
			Il2CppSystem.Collections.Generic.List<Mower>.Enumerator enumerator = list2.GetEnumerator();
			while (enumerator.MoveNext())
			{
				Mower current = enumerator.Current;
				if (current != null && current.started)
				{
					list.Add(current.theMowerRow);
				}
			}
		}
		Party.SendFast("mws", string.Join(",", list), reliable: false);
	}

	private static void StartMower(int row)
	{
		Il2CppSystem.Collections.Generic.List<Mower> list = Board.Instance?.mowerArray;
		if (list == null)
		{
			return;
		}
		Il2CppSystem.Collections.Generic.List<Mower>.Enumerator enumerator = list.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Mower current = enumerator.Current;
			if (current == null || current.WasCollected || current.started || current.theMowerRow != row)
			{
				continue;
			}
			Allow = true;
			try
			{
				current.StartMove();
				break;
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("mower: " + ex);
				break;
			}
			finally
			{
				Allow = false;
			}
		}
	}

	public static void SendResult(bool win)
	{
		Party.SendFast(win ? "win" : "lose", "", reliable: true);
	}
}


