using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Steamworks;
using UnityEngine;

namespace PvZCoop;

public static class Avatars
{
	private static readonly Dictionary<ulong, Texture2D> _cache = new Dictionary<ulong, Texture2D>();

	private static readonly Dictionary<ulong, float> _retryAt = new Dictionary<ulong, float>();

	public static Texture2D Get(CSteamID id)
	{
		if (!Party.SteamReady || id == CSteamID.Nil)
		{
			return null;
		}
		if (_cache.TryGetValue(id.m_SteamID, out var value) && value != null && !value.WasCollected)
		{
			return value;
		}
		if (_retryAt.TryGetValue(id.m_SteamID, out var value2) && Time.unscaledTime < value2)
		{
			return null;
		}
		_retryAt[id.m_SteamID] = Time.unscaledTime + 1f;
		try
		{
			int mediumFriendAvatar = SteamFriends.GetMediumFriendAvatar(id);
			if (mediumFriendAvatar <= 0)
			{
				SteamFriends.RequestUserInformation(id, bRequireNameOnly: false);
				return null;
			}
			if (!SteamUtils.GetImageSize(mediumFriendAvatar, out var pnWidth, out var pnHeight) || pnWidth == 0)
			{
				return null;
			}
			byte[] array = new byte[pnWidth * pnHeight * 4];
			if (!SteamUtils.GetImageRGBA(mediumFriendAvatar, array, array.Length))
			{
				return null;
			}
			Texture2D texture2D = new Texture2D((int)pnWidth, (int)pnHeight, TextureFormat.RGBA32, mipChain: false);
			Il2CppStructArray<Color32> il2CppStructArray = new Il2CppStructArray<Color32>((int)(pnWidth * pnHeight));
			float num = (float)(pnWidth - 1) / 2f;
			float num2 = (float)pnWidth / 2f;
			for (int i = 0; i < pnHeight; i++)
			{
				for (int j = 0; j < pnWidth; j++)
				{
					int num3 = (int)(((pnHeight - 1 - i) * pnWidth + j) * 4);
					float num4 = Mathf.Sqrt(((float)j - num) * ((float)j - num) + ((float)i - num) * ((float)i - num));
					byte a = (byte)((float)(int)array[num3 + 3] * Mathf.Clamp01(num2 - num4));
					il2CppStructArray[(int)(i * pnWidth + j)] = new Color32(array[num3], array[num3 + 1], array[num3 + 2], a);
				}
			}
			texture2D.SetPixels32(il2CppStructArray);
			texture2D.Apply();
			texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
			_cache[id.m_SteamID] = texture2D;
			return texture2D;
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning("avatar: " + ex.Message);
			return null;
		}
	}
}
