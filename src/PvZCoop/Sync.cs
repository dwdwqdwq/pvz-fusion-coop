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

	private static readonly System.Collections.Generic.Dictionary<int, MotionBuffer> _motion=new();

	private static readonly System.Collections.Generic.Dictionary<int, int> _zMissing = new System.Collections.Generic.Dictionary<int, int>();

	public static int SummonDepth;


    public sealed class CharredCall {
        public Zombie Zombie;public int Id,Damage,Armor1,Armor2,Reason,Row;public Vector3 Position;public long Health;public PlantType Report;public DamageType Type;public bool Fix,Died;
    }
    static readonly System.Collections.Generic.Dictionary<int,CharredCall> _charring=new();
    static readonly System.Collections.Generic.HashSet<int> _terminalIds=new();
    static readonly System.Collections.Generic.HashSet<IntPtr> _terminalPointers=new();
    static readonly ZombieVerificationTracker _zVerification=new();
    static float _lastZombieSnapshotStamp=float.NegativeInfinity;
    public static int ReceivedCharred {get;private set;}

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
    public static bool ApplyingHostResult { get; private set; }
    public static bool ResultSent { get; private set; }
    public static bool ResultApplied { get; private set; }
    public static bool HostLossAccepted { get; private set; }
    static readonly System.Collections.Generic.Dictionary<int,float> _animationSpeeds=new();
    public static bool GuestRound => Session.Active && Party.InParty && !Party.IsHost;

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

    public static bool IsLocalPlayerAction=>PlayerAction;

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

	public static void MarkAction(int c, int r)
	{
		_recentAction[(c, r)] = Time.unscaledTime + 4f;
	}

	public static void OnSetPlantPre(int c, int r, PlantType type, Vector2 puff, bool free, bool effect, Plant target = null)
	{
		if (Running)
		{
			_spDepth++;
			if (_spDepth == 1)
			{
				_pendingPlace = ((PlayerAction || Session.BattleLive&&IsHostRunning&&!Allow&&target==null) ? new(int, int, int, bool, bool, Vector2)?((c, r, (int)type, free, effect, puff)) : (((int, int, int, bool, bool, Vector2)?)null));
                if(PlantGloveSync.Begin(c,r,type,target,PlayerAction))_pendingPlace=null;
			}
		}
	}

	public static void OnSetPlantPost(Plant result)
	{
		if (Running && _spDepth != 0 && --_spDepth <= 0)
		{
            PlantGloveSync.Finish(result);
            if(!_pendingPlace.HasValue)return;
			(int, int, int, bool, bool, Vector2) value = _pendingPlace.Value;
			_pendingPlace = null;
			if (!(result == null))
			{
				MarkAction(value.Item1, value.Item2);
				var identity=PlantIdentity.LocalCreated(result);
                if(!Party.IsHost||identity.Id>0)Party.SendFast("sp",new PlantSpawnPacket(value.Item1,value.Item2,value.Item3,value.Item4,value.Item5,value.Item6.x,value.Item6.y,identity.Operation,identity.Id).Encode(),reliable:true);
			}
		}
	}

	public static void OnPlantDie(Plant p, Plant.DieReason reason)
	{
        string identity=PlantIdentity.Reference(p);
        PlantGloveSync.RecordDeath(p,reason);
        PlantIdentity.MarkRetired(p);
		if (Running && !Allow && !(p == null) && _spDepth <= 0)
		{
			bool playerAction = PlayerAction;
			bool flag = Party.IsHost;
			if (playerAction || flag)
			{
				MarkAction(p.thePlantColumn, p.thePlantRow);
				Party.SendFast("pd", $"{p.thePlantColumn}|{p.thePlantRow}|{(int)p.thePlantType}|{(int)reason}|{identity}", reliable: true);
			}
		}
	}

	public static void OnPlantMove(Plant p, int c2, int r2)
	{
        if(PlantGloveSync.SuppressLegacyMove)return;
		if (PlayerAction && !(p == null) && (p.thePlantColumn != c2 || p.thePlantRow != r2))
		{
			MarkAction(p.thePlantColumn, p.thePlantRow);
			MarkAction(c2, r2);
			Party.SendFast("mv", $"{p.thePlantColumn}|{p.thePlantRow}|{(int)p.thePlantType}|{c2}|{r2}|{PlantIdentity.Reference(p)}", reliable: true);
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
			if(IsClient)ZombieDeathEffects.RemoveReplicaPlant(p,reason);
            else p.Die(reason);
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

    static Plant ResolvePlantReference(string[] fields,int at,int column,int row,int type)
    {
        if(fields.Length>=at+3)
        {
            if(!int.TryParse(fields[at],out int id)||id<0||!int.TryParse(fields[at+1],out int creator)||creator<0||creator>4)return null;
            if(id>0)return PlantIdentity.TryGet(id,out var bound)&&(int)bound.thePlantType==type?bound:null;
            if(creator>0)return PlantIdentity.TryOperationPlant(creator,fields[at+2],out var pending)&&(int)pending.thePlantType==type?pending:null;
        }
        // Only initial inventory may use structural matching. Runtime commands
        // without a canonical ID/operation must never target a similar plant.
        if(PlantIdentity.BootstrapComplete)return null;
        return FindPlant(column,row,type);
    }

	private static void OnPlantMessage(string type, string[] p, string payload)
	{
		switch (type)
		{
        case "sp":
        {
            if(Party.SenderId!=1){Plugin.Logger.LogWarning("Ignored guest legacy spawn; host card transaction required.");break;}
            if(!PlantSpawnPacket.TryRead(payload,out var spawn)||(Party.SenderId!=1&&spawn.EntityId!=0))
            {Plugin.Logger.LogWarning("Ignored invalid plant spawn operation");break;}
            MarkAction(spawn.Column,spawn.Row);
            var plant=PlantIdentity.ReceiveSpawn(Party.SenderId,spawn.Operation,spawn.EntityId,
                ()=>Place(spawn.Column,spawn.Row,spawn.Type,spawn.Free,spawn.Effect,new Vector2(spawn.X,spawn.Y)));
            if(plant==null)Plugin.Logger.LogWarning("Placement operation returned no plant: "+spawn.Operation);
            break;
        }
		case "pd":
		{
			int c2 = int.Parse(p[0]);
			int r2 = int.Parse(p[1]);
			MarkAction(c2, r2);
			Plant plant2 = ResolvePlantReference(p,4,c2,r2,int.Parse(p[2]));
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
			Plant plant = ResolvePlantReference(p,5,c,r,int.Parse(p[2]));
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
        // Identity-bound state and reliable lifecycle events replace the old
        // cell/type repair. It could otherwise kill or resurrect a fusion.
        if(PlantIdentity.Enabled)return;
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
		ZombieVisuals.Reset();
        ZombieDeathEffects.Reset();
        PlantGloveSync.Reset();
        MineStateSync.Reset();
        ChomperSync.Reset();
        PlantIdentity.Reset();
        PlantStateSync.Reset();
		_zById.Clear();
		_idByZ.Clear();
		_zMissing.Clear();_motion.Clear();_animationSpeeds.Clear();_charring.Clear();_terminalIds.Clear();_terminalPointers.Clear();_zVerification.Reset();_lastZombieSnapshotStamp=float.NegativeInfinity;ReceivedCharred=0;
        ApplyingHostResult=ResultSent=ResultApplied=HostLossAccepted=false;CoopPause.Reset();
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
        if(GuestRound && !ResultApplied && Board.Instance!=null)Board.Instance.over=false;
		if (!Running || ResultSent || ResultApplied)
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
				_nextZs = unscaledTime + 0.1f;
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
        // All registration paths must preserve terminal IDs and a live ID's
        // native identity. Negative temporary PvP IDs remain supported.
        if(z==null||z.WasCollected||id==0||_terminalIds.Contains(id))return 0;
        if(_zById.TryGetValue(id,out var previous)&&previous!=null&&!previous.WasCollected)
            return previous.Pointer==z.Pointer?id:0;
        _terminalPointers.Remove(z.Pointer);
		_zById[id] = z;
		_idByZ[z.Pointer] = id;
		ZombieVisuals.Register(id,z);
		return id;
	}

	private static void ForgetZombie(int id)
	{
		ZombieVisuals.Forget(id);
		if (_zById.TryGetValue(id, out var value) && value != null)
		{
			_idByZ.Remove(value.Pointer);
		}
		_zById.Remove(id);
		_zMissing.Remove(id);_zVerification.Forget(id);_motion.Remove(id);_animationSpeeds.Remove(id);
	}

	public static bool IsSynced(Zombie z)
	{
		if (z != null)
		{
			return _idByZ.ContainsKey(z.Pointer)||_terminalPointers.Contains(z.Pointer);
		}
		return false;
	}

    public static bool TryZombieId(Zombie z,out int id)=>_idByZ.TryGetValue(z.Pointer,out id);
    public static bool TryGetZombie(int id,out Zombie zombie){zombie=null;if(!_zById.TryGetValue(id,out var z)||z==null||z.WasCollected)return false;zombie=z;return true;}

    public static bool IsZombieTerminal(int id)=>_terminalIds.Contains(id);
    public static bool TryGetLivingZombie(int id,out Zombie zombie)
    {
        zombie=null;
        if(_terminalIds.Contains(id))return false;
        return _zById.TryGetValue(id,out zombie)&&zombie!=null&&!zombie.WasCollected&&
            zombie.theHealth>0&&zombie.theStatus!=ZombieStatus.Dying;
    }
    public static bool CapturingCharred(int id)=>_charring.ContainsKey(id);
    public static bool ReservePresentationDeath(int id,out Zombie zombie)
    {
        zombie=null;
        // Even an unseen lifetime stays terminal: a late zn/zs cannot spawn it.
        if(id<=0||!_terminalIds.Add(id))return false;
        return _zById.TryGetValue(id,out zombie)&&zombie!=null&&!zombie.WasCollected;
    }
    public static void CompletePresentationDeath(int id,Zombie zombie)
    {
        _terminalIds.Add(id);if(zombie!=null&&!zombie.WasCollected)_terminalPointers.Add(zombie.Pointer);
        ForgetZombie(id);
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
            if(_charring.TryGetValue(value,out var call)){call.Died=true;call.Reason=reason;return;}
            // Generic native Die finalizers publish only after the outermost
            // override has completed and actually entered a terminal state.
		}
	}

    public static CharredCall BeginCharred(Zombie z,int damage,PlantType report,bool fix,DamageType type){
        if(!IsHostRunning||z==null||!_idByZ.TryGetValue(z.Pointer,out int id)||_charring.ContainsKey(id)||ZombieDeathEffects.Capturing(id))return null;
        var call=new CharredCall{Zombie=z,Id=id,Damage=damage,Report=report,Fix=fix,Type=type,Health=z.theHealth,Armor1=z.theFirstArmorHealth,Armor2=z.theSecondArmorHealth,Position=z.transform.position,Row=z.theZombieRow};
        _charring[id]=call;return call;
    }
    public static void EndCharred(CharredCall call,Exception error=null){
        if(call==null)return;
        if(_charring.TryGetValue(call.Id,out var current)&&ReferenceEquals(current,call))_charring.Remove(call.Id);
        // A finalizer always releases capture, including native exceptions.
        // Entering Die or reaching zero HP does not prove terminal success.
        if(error!=null){Plugin.Logger.LogError("Native Charred failed; capture released without publishing: "+error);return;}
        var z=call.Zombie;bool exists=z!=null&&!z.WasCollected;
        bool terminal=ZombieVerification.NativeTerminal(exists,exists&&z.theStatus==ZombieStatus.Dying);
        if(terminal)ZombieDeathEffects.PublishTerminalPose(call.Id,call.Position,call.Row);
        Party.Send("zc",$"{call.Id}|{call.Damage}|{(int)call.Report}|{(call.Fix?1:0)}|{(int)call.Type}|{call.Health}|{call.Armor1}|{call.Armor2}|{(terminal?1:0)}");
        if(terminal){_terminalIds.Add(call.Id);if(z!=null&&!z.WasCollected)_terminalPointers.Add(z.Pointer);ForgetZombie(call.Id);}
    }
    static void ReceiveCharred(string payload){
        if(!IsClient||Party.SenderId!=1)return;var p=payload.Split('|');if(p.Length!=9)throw new FormatException("Invalid charred event");
        if(!int.TryParse(p[0],out int id)||id<=0||(p[8]!="0"&&p[8]!="1")||_terminalIds.Contains(id))return;
        // Validate the full native event before reserving an irreversible ID.
        if(!int.TryParse(p[1],out int damage)||!int.TryParse(p[2],out int report)||(p[3]!="0"&&p[3]!="1")||
            !int.TryParse(p[4],out int damageType)||!long.TryParse(p[5],out long health)||
            !int.TryParse(p[6],out int armor1)||!int.TryParse(p[7],out int armor2))return;
        bool terminal=p[8]=="1";
        if(terminal)_terminalIds.Add(id);
        if(!_zById.TryGetValue(id,out var z)||z==null||z.WasCollected)return;
        if(terminal)_terminalPointers.Add(z.Pointer);
        bool old=Allow;Allow=true;
        try{
            ZombieDeathEffects.ApplyTerminalPose(id,z);
            z.theHealth=terminal?1:health;z.theFirstArmorHealth=terminal?0:armor1;z.theSecondArmorHealth=terminal?0:armor2;
            using(ZombieDeathEffects.ReplayScope(z))
                z.Charred(damage,(PlantType)report,p[3]=="1",(DamageType)damageType);ReceivedCharred++;
        }finally{Allow=old;}
        if(terminal)ForgetZombie(id); // Native charred animation owns its remaining lifetime.
    }
    public static bool LocalZombieMotion(Zombie z)=>!IsClient||z==null||!IsSynced(z);

	private static void SendZombieSnapshot()
	{
        // Include native spawns that happened outside the initial spawn callback.
        foreach(var z in UnityEngine.Object.FindObjectsOfType<Zombie>())
            if(z!=null && !z.isPreview && z.theHealth>0 && !_terminalPointers.Contains(z.Pointer) && !_idByZ.ContainsKey(z.Pointer))RegisterZombie(z,_nextZombie++);
		StringBuilder stringBuilder = new StringBuilder();
		foreach (var (num2, zombie2) in _zById.ToList())
		{
			if (ZombieVerification.NativeTerminal(zombie2!=null&&!zombie2.WasCollected,
                zombie2!=null&&!zombie2.WasCollected&&zombie2.theStatus==ZombieStatus.Dying))
			{
                if(zombie2!=null && !zombie2.WasCollected){ZombieVisuals.Publish(num2,ZombieVisuals.Capture(zombie2),final:true);ZombieDeathEffects.PublishTerminalPose(num2,zombie2.transform.position,zombie2.theZombieRow);_terminalPointers.Add(zombie2.Pointer);}
				Party.SendFast("zd", $"{num2}|0", reliable: true);
				CompletePresentationDeath(num2,zombie2);
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
				.Append((int)zombie2.theZombieType)
                .Append(',').Append(zombie2.isMindControlled?1:0);
            var anim=zombie2.anim;
            var state=anim==null?default:anim.GetCurrentAnimatorStateInfo(0);
            stringBuilder.Append(',').Append(state.fullPathHash)
                .Append(',').Append(F(state.normalizedTime))
                .Append(',').Append(F(anim==null?1:anim.speed))
                .Append(',').Append(zombie2.isAttacking?1:0);
            var visuals=ZombieVisuals.Capture(zombie2);
            ZombieVisuals.Publish(num2,visuals);
            var effects=ZombieEffects.Capture(zombie2);
            ZombieEffects.Publish(num2,visuals.Revision,effects);
            stringBuilder.Append(',').Append(visuals.Encode()).Append(',').Append(effects.Encode());
		}
        Party.SendFast("wave",CaptureWaveState(),reliable:false);
		Party.SendFast("zs", F(Time.time)+"|"+stringBuilder.ToString(), reliable: false);
	}

    static string CaptureWaveState(bool completed=false){
        var b=Board.Instance;if(b==null)return "";
        return $"{b.theWave}|{b.theMaxWave}|{F(b.timeUntilNextWave)}|{F(b.hugeWaveCountDown)}|{(b.isHugeWave?1:0)}|{(b.nextWaveRequested?1:0)}|{(b.autoNextWaveEnabled?1:0)}|{(b.autoNextWaveForced?1:0)}|{b.nextWaveButtonWave}|{F(Time.timeScale)}|{WaveSync.CaptureProgress(completed)}";
    }
    static void ApplyHostWave(string payload){
        var wave=payload.Split('|');if(wave.Length<2||Board.Instance==null)return;
        Board.Instance.theWave=int.Parse(wave[0]);Board.Instance.theMaxWave=int.Parse(wave[1]);
        if(wave.Length>=9){var b=Board.Instance;if(wave.Length>=10&&!CoopPause.IsPaused&&Session.BattleLive){float scale=PF(wave[9]);if(scale>0)Time.timeScale=Mathf.Clamp(scale,.01f,10);}
            b.timeUntilNextWave=PF(wave[2]);b.hugeWaveCountDown=PF(wave[3]);b.isHugeWave=wave[4]=="1";b.nextWaveRequested=wave[5]=="1";b.autoNextWaveEnabled=wave[6]=="1";b.autoNextWaveForced=wave[7]=="1";b.nextWaveButtonWave=int.Parse(wave[8]);if(InGameUI.Instance!=null)InGameUI.Instance.UpdateNextWaveButtons();}
        if(Board.Instance.boardSpawner!=null)Board.Instance.boardSpawner.theWave=Board.Instance.theWave;
        WaveSync.ReadProgress(wave);WaveSync.ApplyProgress(LevelProgress.Instance);
    }

	private static void OnZombieSnapshot(string payload)
	{
        int stampEnd=payload.IndexOf('|');if(stampEnd<0)throw new FormatException("Missing snapshot timestamp");
        float stamp=PF(payload[..stampEnd]);
        if(!ZombieVerification.FreshSnapshot(stamp,_lastZombieSnapshotStamp))return;
        _lastZombieSnapshotStamp=stamp;payload=payload[(stampEnd+1)..];
		System.Collections.Generic.HashSet<int> hashSet = new System.Collections.Generic.HashSet<int>();
		string[] array = payload.Split(';', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(',');
            if(array2.Length!=11+ZombieVisualState.FieldCount || !ZombieVisualState.TryRead(array2[..^1],10,out var visuals) || !ZombieEffectState.TryRead(array2[^1],out var effects))continue;
            if(!int.TryParse(array2[0],out int num)||num<=0)continue;
			hashSet.Add(num);
            if(_terminalIds.Contains(num))continue;
			if (!_zById.TryGetValue(num, out var value) || value == null || value.WasCollected)
			{
                if(CreateZombie.Instance==null)continue;
                Allow=true;_hostSpawn=true;
                try {
                    value=CreateZombie.Instance.SetZombie(int.Parse(array2[3]),(ZombieType)int.Parse(array2[4]),PF(array2[1]),array2[5]=="1",true);
                    if(value==null)continue;
                    RegisterZombie(value,num);
                    value.transform.position=new Vector3(PF(array2[1]),PF(array2[2]),value.transform.position.z);
                } finally {Allow=false;_hostSpawn=false;}
			}
			float num2 = PF(array2[1]);
			float num3 = PF(array2[2]);
            if(!_motion.TryGetValue(num,out var motion)){motion=new MotionBuffer();_motion[num]=motion;}
            motion.Add(stamp,num2,num3);
			int num4 = int.Parse(array2[3]);
			if (value.theZombieRow != num4)
			{
				value.theZombieRow = num4;
			}
			if (!value.WasCollected)
			{
                value.isAttacking=array2[9]=="1";
                ZombieVisuals.Accept(num,visuals);
                ZombieEffects.Accept(num,visuals.Revision,effects);
                if(value.anim!=null){
                    var anim=value.anim;int hash=int.Parse(array2[6]);float phase=PF(array2[7]);
                    var local=anim.GetCurrentAnimatorStateInfo(0);
                    if(hash!=0 && anim.HasState(0,hash) && (local.fullPathHash!=hash || Mathf.Abs(local.normalizedTime-phase)>1.5f))anim.Play(hash,0,phase);
                    anim.speed=PF(array2[8]);_animationSpeeds[num]=anim.speed;
                }
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
            if(_zMissing[item]>=3&&!_terminalIds.Contains(item)&&
                _zVerification.TryRequest(item,Time.unscaledTime,out long nonce))
                Party.Send("zverify",ZombieVerification.Request(item,nonce));
		}
	}

    public static void ApplyZombieAnimation(Zombie zombie){
        if(!IsClient || zombie==null)return;
        if(IsSynced(zombie)&&zombie.rb!=null){zombie.rb.velocity=Vector2.zero;zombie.rb.angularVelocity=0;zombie.rb.interpolation=RigidbodyInterpolation2D.None;}
        if(zombie.anim==null)return;
        if(_idByZ.TryGetValue(zombie.Pointer,out int id)&&_animationSpeeds.TryGetValue(id,out float speed))zombie.anim.speed=speed;
    }

    public static void RebaseZombieMotion(){if(IsClient)foreach(var motion in _motion.Values)motion.RebaseLatest();}
    public static void RenderZombieMotion(){
        if(!IsClient || ResultApplied)return;
        foreach(var entry in _motion){
            if(_terminalIds.Contains(entry.Key)||!_zById.TryGetValue(entry.Key,out var zombie)||zombie==null||zombie.WasCollected)continue;
            if(entry.Value.Read(Time.time,out float x,out float y)){
                var position=zombie.transform.position;
                zombie.transform.position=new Vector3(x,y,position.z);
            }
        }
    }

    static void ReceiveZombieDeath(int id,int reason){
        if(id<=0||!_terminalIds.Add(id))return;
        if(!ZombieVisuals.DeferDeath(id,reason))KillLocal(id,reason);
    }
	internal static void KillLocal(int id, int reason)
	{
		if (_zById.TryGetValue(id, out var value) && value != null && !value.WasCollected)
		{
			_terminalPointers.Add(value.Pointer);
			Allow = true;
			try
			{
				ZombieDeathEffects.ApplyTerminalPose(id,value);
                using(ZombieDeathEffects.ReplayScope(value))value.Die(reason);
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

    static void ReceiveZombieVerification(string type,string payload)
    {
        if(!Running||ResultSent||ResultApplied)return;
        if(type=="zverify")
        {
            int sender=Party.SenderId;
            if(!Party.IsHost||!Party.RemotePlayers.Contains(sender)||
                !ZombieVerification.TryRequest(payload,out int id,out long nonce)||
                id>=_nextZombie||!_zVerification.AllowHostRequest(sender,id,Time.unscaledTime))return;
            if(CapturingCharred(id)||ZombieDeathEffects.Capturing(id))return;
            bool exists=_zById.TryGetValue(id,out var z)&&z!=null&&!z.WasCollected;
            bool? alive=ZombieVerification.HostVerdict(id,_nextZombie,_terminalIds.Contains(id),exists,
                exists&&z.theStatus==ZombieStatus.Dying,exists&&z.theHealth>0);
            if(!alive.HasValue)return; // A nonterminal native phase can have zero HP.
            if(!alive.Value)CompletePresentationDeath(id,exists?z:null);
            Party.SendTo(sender,"zverified",ZombieVerification.Reply(id,nonce,alive.Value));
            if(alive.Value)_nextZs=0; // Fresh canonical position/HP/effects follow.
            return;
        }
        if(!IsClient||Party.SenderId!=1||
            !ZombieVerification.TryReply(payload,out int verifiedId,out long verifiedNonce,out bool verifiedAlive)||
            !_zVerification.Complete(verifiedId,verifiedNonce)||_terminalIds.Contains(verifiedId))return;
        if(verifiedAlive)_zMissing.Remove(verifiedId);
        else ReceiveZombieDeath(verifiedId,2); // Host-confirmed cleanup, never guessed burning.
    }

	private static void OnMessage(string type, string payload)
	{
        if(type=="zverify"||type=="zverified"){ReceiveZombieVerification(type,payload);return;}
        if(type=="zc"){ReceiveCharred(payload);return;}
        if(type=="pause_request"||type=="pause_state"){CoopPause.Receive(type,payload);return;}
        if(type=="pause"||type=="resume")return;
        if(type=="bal"||type=="sg")return;
        if((ResultSent||ResultApplied)&&type!="win"&&type!="lose")return;
        if(type=="win"||type=="lose"){
            if(!Running || Party.IsHost || Party.SenderId!=1 || ResultApplied)return;
            if(type=="win"&&payload.Length>0)ApplyHostWave(payload);
            ResultApplied=true;HostLossAccepted=type=="lose";PartnerBank.Clear();PlacementPreview.Reset();
        }
        if(type=="wave"&&GuestRound&&Party.SenderId==1&&Board.Instance!=null){ApplyHostWave(payload);return;}

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
                    if(!Party.IsHost&&Party.SenderId==1)
					{
						ReceiveZombieDeath(int.Parse(array[0]), int.Parse(array[1]));
					}
					return;
				case "cd":
					TeamState.For(Party.SenderId).Cooldowns=payload;
                    if(Party.SenderId==Party.PartnerPlayer)PartnerBank.SetCooldowns(payload);
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
                if(type!="zn"||Party.IsHost||Party.SenderId!=1)return;
                if(array.Length<5||!int.TryParse(array[0],out int spawnId)||spawnId<=0)return;
                if(_terminalIds.Contains(spawnId))return;
                if(_zById.TryGetValue(spawnId,out var existing)&&existing!=null&&!existing.WasCollected)return;
				if (array.Length > 5 && _zById.TryGetValue(int.Parse(array[5]), out var value3) && value3 != null)
				{
					ForgetZombie(int.Parse(array[5]));
					RegisterZombie(value3, spawnId);
					return;
				}
				Allow = true;
				_hostSpawn = true;
				try
				{
					Zombie zombie2 = CreateZombie.Instance.SetZombie(int.Parse(array[1]), (ZombieType)int.Parse(array[2]), PF(array[3]), array[4] == "1");
					if (zombie2 != null)
					{
						RegisterZombie(zombie2, spawnId);
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
				if (type == "zs" && !Party.IsHost && Party.SenderId==1)
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
                    ApplyingHostResult=true;
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
					ApplyingHostResult=false;
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
                    ApplyingHostResult=true;
					VictorySync.ApplyHostWin();
				}
				catch (Exception ex7)
				{
					Plugin.Logger.LogError("win: " + ex7);
				}
				finally
				{
					ApplyingHostResult=false;
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
        if(ResultSent)return;ResultSent=true;VictorySync.HostResult(win);PartnerBank.Clear();PlacementPreview.Reset();
		Party.SendFast(win ? "win" : "lose", win?CaptureWaveState(true):"", reliable: true);
	}
}


