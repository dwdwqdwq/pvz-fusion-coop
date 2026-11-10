using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;

namespace PvZCoop;

public static class Prices
{
	private static Dictionary<int, int> _plantPrice;

	private static Dictionary<int, int> _zombiePrice;

	private static Dictionary<int, float> _plantCd;

	private static Dictionary<int, float> _zombieCd;

	private static bool _logged;

	private static void Load()
	{
		if (_plantPrice != null)
		{
			return;
		}
		_plantPrice = new Dictionary<int, int>();
		_zombiePrice = new Dictionary<int, int>();
		_plantCd = new Dictionary<int, float>();
		_zombieCd = new Dictionary<int, float>();
		try
		{
			using Stream stream = typeof(Prices).Assembly.GetManifestResourceStream("prices.json");
			if (stream == null)
			{
				return;
			}
			JsonDocument doc = JsonDocument.Parse(stream);
			try
			{
				Read("plants", _plantPrice, _plantCd);
				Read("zombies", _zombiePrice, _zombieCd);
				ManualLogSource logger = Plugin.Logger;
				bool isEnabled;
				BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(65, 4, out isEnabled);
				if (isEnabled)
				{
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral("prices: plants ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_plantPrice.Count);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" prices / ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_plantCd.Count);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" cooldowns, zombies ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_zombiePrice.Count);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" prices / ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(_zombieCd.Count);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" cooldowns");
				}
				logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			}
			finally
			{
				if (doc != null)
				{
					((IDisposable)doc).Dispose();
				}
			}
			void Read(string list, Dictionary<int, int> price, Dictionary<int, float> cd)
			{
				if (!doc.RootElement.TryGetProperty(list, out var value))
				{
					return;
				}
				foreach (JsonElement item in value.EnumerateArray())
				{
					int @int = item.GetProperty("id").GetInt32();
					if (item.TryGetProperty("price", out var value2) && value2.ValueKind == JsonValueKind.Number)
					{
						price[@int] = value2.GetInt32();
					}
					if (item.TryGetProperty("cd", out var value3) && value3.ValueKind == JsonValueKind.Number)
					{
						cd[@int] = (float)value3.GetDouble();
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("prices.json: " + ex.Message);
		}
	}

	public static void Apply(ZombieDataManager.ZombieData d)
	{
		if (d == null)
		{
			return;
		}
		Load();
		int theZombieType = (int)d.theZombieType;
		if (_zombiePrice.TryGetValue(theZombieType, out var value) && d.cost != value)
		{
			d.cost = value;
			if (!_logged)
			{
				_logged = true;
				ManualLogSource logger = Plugin.Logger;
				bool isEnabled;
				BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(19, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral("prices: ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(d.theZombieType);
					bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" now costs ");
					bepInExInfoLogInterpolatedStringHandler.AppendFormatted(value);
				}
				logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
			}
		}
		if (_zombieCd.TryGetValue(theZombieType, out var value2))
		{
			d.cd = value2;
		}
	}

	public static void Apply(PlantDataManager.PlantData d)
	{
		if (d != null)
		{
			Load();
			int thePlantType = (int)d.thePlantType;
			if (_plantPrice.TryGetValue(thePlantType, out var value) && d.cost != value)
			{
				d.cost = value;
			}
			if (_plantCd.TryGetValue(thePlantType, out var value2))
			{
				d.cd = value2;
			}
		}
	}

	public static float? ZombieCooldown(ZombieType t)
	{
		Load();
		if (!_zombieCd.TryGetValue((int)t, out var value))
		{
			return null;
		}
		return value;
	}
}
