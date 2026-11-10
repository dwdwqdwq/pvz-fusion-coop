using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;

namespace PvZCoop;

public static class Unlocks
{
	private static HashSet<int> _host;

	private static HashSet<int> _lastSent;

	private static float _nextCheck;

	private static bool _computing;

	public static void Init()
	{
		Party.OnMessage += OnMessage;
	}

	public static void Clear()
	{
		_host = null;
		_lastSent = null;
	}

	public static bool? Override(PlantType t)
	{
		if (_computing)
		{
			return null;
		}
		if (PvP.Enabled)
		{
			return true;
		}
		if (_host == null || !Party.InParty || Party.IsHost)
		{
			return null;
		}
		return _host.Contains((int)t);
	}

	private static HashSet<int> ReadMine()
	{
		HashSet<int> hashSet = new HashSet<int>();
		_computing = true;
		try
		{
			foreach (PlantType value in Enum.GetValues(typeof(PlantType)))
			{
				if (PlantDataManager.IsUnlocked(value))
				{
					hashSet.Add((int)value);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("read unlocks: " + ex.Message);
		}
		finally
		{
			_computing = false;
		}
		return hashSet;
	}

	public static void SendNow()
	{
		if (Party.InParty && Party.IsHost)
		{
			_lastSent = ReadMine();
			Party.Send("unl", Encode(_lastSent));
		}
	}

	public static void Tick()
	{
		if (!Party.InParty || !Party.IsHost || !Party.PartnerPresent)
		{
			if (!Party.InParty)
			{
				Clear();
			}
		}
		else if (!(Time.unscaledTime < _nextCheck))
		{
			_nextCheck = Time.unscaledTime + 5f;
			HashSet<int> hashSet = ReadMine();
			if (_lastSent == null || !hashSet.SetEquals(_lastSent))
			{
				_lastSent = hashSet;
				Party.Send("unl", Encode(hashSet));
			}
		}
	}

	private static void OnMessage(string type, string payload)
	{
		if (type != "unl" || Party.IsHost)
		{
			return;
		}
		HashSet<int> hashSet = Decode(payload);
		if (_host != null)
		{
			foreach (int item in hashSet.Where((int t) => !_host.Contains(t)))
			{
				_computing = true;
				try
				{
					if (!PlantDataManager.IsUnlocked((PlantType)item))
					{
						PlantDataManager.SetPlantUnlock((PlantType)item);
					}
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogError("unlock: " + ex.Message);
				}
				finally
				{
					_computing = false;
				}
				PlantType plantType = (PlantType)item;
				Session.ShowToast("New plant unlocked: " + plantType);
			}
		}
		_host = hashSet;
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(33, 1, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("using the host's ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(hashSet.Count);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" unlocked plants");
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
	}

	private static string Encode(HashSet<int> set)
	{
		StringBuilder stringBuilder = new StringBuilder();
		List<int> list = set.OrderBy((int x) => x).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			int num2 = list[num];
			int num3 = num2;
			while (num + 1 < list.Count && list[num + 1] == num3 + 1)
			{
				num3 = list[++num];
			}
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(num2);
			if (num3 != num2)
			{
				stringBuilder.Append(':').Append(num3);
			}
		}
		return stringBuilder.ToString();
	}

	private static HashSet<int> Decode(string s)
	{
		HashSet<int> hashSet = new HashSet<int>();
		string[] array = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(':');
			int num = int.Parse(array2[0]);
			int num2 = ((array2.Length > 1) ? int.Parse(array2[1]) : num);
			for (int j = num; j <= num2; j++)
			{
				hashSet.Add(j);
			}
		}
		return hashSet;
	}
}
