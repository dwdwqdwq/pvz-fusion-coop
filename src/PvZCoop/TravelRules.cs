using System;
using System.Collections.Generic;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;

namespace PvZCoop;

public static class TravelRules
{
	private static bool _lifted;

	private static readonly System.Collections.Generic.List<(PlantType key, Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool> value)> _info = new System.Collections.Generic.List<(PlantType, Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool>)>();

	private static readonly System.Collections.Generic.List<PlantType> _sp = new System.Collections.Generic.List<PlantType>();

	private static readonly System.Collections.Generic.List<(PlantType key, TravelUnlocks value)> _unlock = new System.Collections.Generic.List<(PlantType, TravelUnlocks)>();

	public static void Tick()
	{
		if (PvP.Enabled && !_lifted)
		{
			Lift();
		}
		else if (!PvP.Enabled && _lifted)
		{
			Restore();
		}
	}

	private static void Lift()
	{
		_lifted = true;
		try
		{
			_info.Clear();
			_sp.Clear();
			_unlock.Clear();
			Il2CppSystem.Collections.Generic.Dictionary<PlantType, Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool>>.Enumerator enumerator = TravelDictionary.PlantInfo.GetEnumerator();
			while (enumerator.MoveNext())
			{
				Il2CppSystem.Collections.Generic.KeyValuePair<PlantType, Il2CppSystem.ValueTuple<Il2CppSystem.Nullable<PlantType>, Il2CppSystem.Object, Il2CppSystem.Object, bool>> current = enumerator.Current;
				_info.Add((current.Key, current.Value));
			}
			Il2CppSystem.Collections.Generic.HashSet<PlantType>.Enumerator enumerator2 = TravelDictionary.SpPlants.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				PlantType current2 = enumerator2.Current;
				_sp.Add(current2);
			}
			Il2CppSystem.Collections.Generic.Dictionary<PlantType, TravelUnlocks>.Enumerator enumerator3 = TravelDictionary.PlantToUnlock.GetEnumerator();
			while (enumerator3.MoveNext())
			{
				Il2CppSystem.Collections.Generic.KeyValuePair<PlantType, TravelUnlocks> current3 = enumerator3.Current;
				_unlock.Add((current3.Key, current3.Value));
			}
			TravelDictionary.PlantInfo.Clear();
			TravelDictionary.SpPlants.Clear();
			TravelDictionary.PlantToUnlock.Clear();
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(76, 3, out isEnabled);
			if (isEnabled)
			{
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PvP: travel-only fusions allowed (");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_info.Count);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" travel plants, ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_sp.Count);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" special, ");
				bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_unlock.Count);
				bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" travel unlocks)");
			}
			logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		}
		catch (System.Exception ex)
		{
			Plugin.Logger.LogError("lift travel rules: " + ex);
		}
	}

	private static void Restore()
	{
		_lifted = false;
		try
		{
			foreach (var (key, value) in _info)
			{
				if (!TravelDictionary.PlantInfo.ContainsKey(key))
				{
					TravelDictionary.PlantInfo.Add(key, value);
				}
			}
			foreach (PlantType item in _sp)
			{
				TravelDictionary.SpPlants.Add(item);
			}
			foreach (var (key2, value2) in _unlock)
			{
				if (!TravelDictionary.PlantToUnlock.ContainsKey(key2))
				{
					TravelDictionary.PlantToUnlock.Add(key2, value2);
				}
			}
			Plugin.Logger.LogInfo("travel-only fusion rules restored");
		}
		catch (System.Exception ex)
		{
			Plugin.Logger.LogError("restore travel rules: " + ex);
		}
	}
}
