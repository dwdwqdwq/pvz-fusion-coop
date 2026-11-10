using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace PvZCoop;

public static class PriceDump
{
	private const int Size = 128;

	private const int Layer = 31;

	private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "pvzpricemanager");

	public static void Run()
	{
		string text = Path.Combine(Dir, "raw");
		Directory.CreateDirectory(text);
		(Dictionary<int, string>, Dictionary<int, string>) tuple = ReadNames();
		List<object> list = new List<object>();
		List<object> list2 = new List<object>();
		GameObject gameObject = new GameObject("PriceDumpCamera");
		gameObject.transform.position = new Vector3(5000f, 5000f, -50f);
		Camera camera = gameObject.AddComponent<Camera>();
		camera.orthographic = true;
		camera.cullingMask = int.MinValue;
		camera.clearFlags = CameraClearFlags.Color;
		camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
		camera.enabled = false;
		RenderTexture renderTexture = (camera.targetTexture = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32));
		Texture2D read = new Texture2D(128, 128, TextureFormat.RGBA32, mipChain: false);
		int num = 0;
		try
		{
			foreach (PlantType t in Enum.GetValues(typeof(PlantType)))
			{
				if (t < PlantType.Peashooter)
				{
					continue;
				}
				PlantDataManager.PlantData plantData = null;
				try
				{
					plantData = PlantDataManager.GetPlantData(t);
				}
				catch
				{
				}
				if (plantData != null)
				{
					tuple.Item1.TryGetValue((int)t, out var value);
					list.Add(new
					{
						id = (int)t,
						key = t.ToString(),
						name = (value ?? t.ToString()),
						cost = plantData.cost,
						cd = plantData.cd
					});
					if (Snap(() => CreatePlant.CreatePlantPreview(t, Color.white, null, new Vector2(5000f, 5000f)), camera, renderTexture, read, Path.Combine(text, $"p{t}.rgba")))
					{
						num++;
					}
				}
			}
			foreach (ZombieType t2 in Enum.GetValues(typeof(ZombieType)))
			{
				if (t2 < ZombieType.NormalZombie)
				{
					continue;
				}
				ZombieDataManager.ZombieData zombieData = null;
				try
				{
					zombieData = ZombieDataManager.GetZombieData(t2);
				}
				catch
				{
				}
				if (zombieData != null)
				{
					tuple.Item2.TryGetValue((int)t2, out var value2);
					list2.Add(new
					{
						id = (int)t2,
						key = t2.ToString(),
						name = (value2 ?? t2.ToString()),
						cost = zombieData.cost,
						cd = zombieData.cd
					});
					if (Snap(() => CreateZombie.CreateZombiePreview(t2, Color.white, null, new Vector2(5000f, 5000f)), camera, renderTexture, read, Path.Combine(text, $"z{t2}.rgba")))
					{
						num++;
					}
				}
			}
		}
		finally
		{
			camera.targetTexture = null;
			UnityEngine.Object.Destroy(gameObject);
			renderTexture.Release();
		}
		string contents = JsonSerializer.Serialize(new
		{
			size = 128,
			plants = list,
			zombies = list2
		}, new JsonSerializerOptions
		{
			WriteIndented = true
		});
		File.WriteAllText(Path.Combine(Dir, "data.json"), contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(48, 4, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("PRICEDUMP done: ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(list.Count);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" plants, ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(list2.Count);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" zombies, ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(num);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" pictures -> ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(Dir);
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
		Session.ShowToast($"Saved {list.Count} plants and {list2.Count} zombies for the price manager.", 5f);
	}

	private static bool Snap(Func<GameObject> make, Camera cam, RenderTexture rt, Texture2D read, string file)
	{
		GameObject gameObject = null;
		try
		{
			gameObject = make();
			if (gameObject == null)
			{
				return false;
			}
			Bounds bounds = default(Bounds);
			bool flag = false;
			foreach (Renderer componentsInChild in gameObject.GetComponentsInChildren<Renderer>(includeInactive: true))
			{
				componentsInChild.gameObject.layer = 31;
				if (componentsInChild.enabled && !(componentsInChild.bounds.size.sqrMagnitude < 1E-06f))
				{
					if (!flag)
					{
						bounds = componentsInChild.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(componentsInChild.bounds);
					}
				}
			}
			foreach (Transform componentsInChild2 in gameObject.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				componentsInChild2.gameObject.layer = 31;
			}
			if (!flag)
			{
				return false;
			}
			cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 50f);
			cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f;
			cam.Render();
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			read.ReadPixels(new Rect(0f, 0f, 128f, 128f), 0, 0);
			read.Apply();
			RenderTexture.active = active;
			Il2CppStructArray<Color32> pixels = read.GetPixels32();
			byte[] array = new byte[pixels.Length * 4];
			for (int i = 0; i < pixels.Length; i++)
			{
				array[i * 4] = pixels[i].r;
				array[i * 4 + 1] = pixels[i].g;
				array[i * 4 + 2] = pixels[i].b;
				array[i * 4 + 3] = pixels[i].a;
			}
			File.WriteAllBytes(file, array);
			return true;
		}
		catch (Exception ex)
		{
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(20, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral("PRICEDUMP picture ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(Path.GetFileName(file));
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral(": ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
			}
			logger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			return false;
		}
		finally
		{
			if (gameObject != null)
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}
	}

	private static (Dictionary<int, string> plants, Dictionary<int, string> zombies) ReadNames()
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		Dictionary<int, string> dictionary2 = new Dictionary<int, string>();
		foreach (TextAsset item in Resources.FindObjectsOfTypeAll<TextAsset>())
		{
			try
			{
				if (item.name == "LawnStrings")
				{
					Collect(item.text, "plants", "seedType", dictionary);
				}
				else if (item.name == "ZombieStrings")
				{
					Collect(item.text, "zombies", "theZombieType", dictionary2);
				}
			}
			catch (Exception ex)
			{
				ManualLogSource logger = Plugin.Logger;
				bool isEnabled;
				BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(18, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral("PRICEDUMP names ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(item.name);
					bepInExWarningLogInterpolatedStringHandler.AppendLiteral(": ");
					bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
				}
				logger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			}
		}
		return (plants: dictionary, zombies: dictionary2);
	}

	private static void Collect(string text, string list, string idKey, Dictionary<int, string> into)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(text.TrimStart('\ufeff'));
		foreach (JsonElement item in jsonDocument.RootElement.GetProperty(list).EnumerateArray())
		{
			if (item.TryGetProperty(idKey, out var value) && item.TryGetProperty("name", out var value2))
			{
				into[value.GetInt32()] = value2.GetString();
			}
		}
	}
}
