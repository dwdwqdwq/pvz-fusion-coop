using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace PvZCoop;

public static class Art
{
	private sealed class Px
	{
		public readonly int W;

		public readonly int H;

		public readonly Color32[] D;

		public Color32 this[int x, int y]
		{
			get
			{
				return D[y * W + x];
			}
			set
			{
				D[y * W + x] = value;
			}
		}

		public Px(int w, int h)
		{
			W = w;
			H = h;
			D = new Color32[w * h];
		}

		public Px Copy()
		{
			Px px = new Px(W, H);
			Array.Copy(D, px.D, D.Length);
			return px;
		}

		public Px Crop(int x0, int y0, int w, int h)
		{
			Px px = new Px(w, h);
			for (int i = 0; i < h; i++)
			{
				Array.Copy(D, (y0 + i) * W + x0, px.D, i * w, w);
			}
			return px;
		}

		public Px Resize(int w, int h)
		{
			if (w == W && h == H)
			{
				return Copy();
			}
			Px px = new Px(w, h);
			float num = (float)W / (float)w;
			float num2 = (float)H / (float)h;
			bool flag = num > 1.5f || num2 > 1.5f;
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					if (flag)
					{
						px.D[i * w + j] = Box((float)j * num, (float)i * num2, num, num2);
						continue;
					}
					float num3 = ((float)j + 0.5f) * num - 0.5f;
					float num4 = ((float)i + 0.5f) * num2 - 0.5f;
					int num5 = Mathf.Clamp((int)Math.Floor(num3), 0, W - 1);
					int num6 = Mathf.Clamp((int)Math.Floor(num4), 0, H - 1);
					int x = Math.Min(num5 + 1, W - 1);
					int y = Math.Min(num6 + 1, H - 1);
					float tx = Mathf.Clamp01(num3 - (float)num5);
					float ty = Mathf.Clamp01(num4 - (float)num6);
					px.D[i * w + j] = Mix(this[num5, num6], this[x, num6], this[num5, y], this[x, y], tx, ty);
				}
			}
			return px;
		}

		private Color32 Box(float fx, float fy, float sw, float sh)
		{
			int num = (int)fx;
			int num2 = (int)fy;
			int num3 = Math.Min(W, (int)Math.Ceiling(fx + sw));
			int num4 = Math.Min(H, (int)Math.Ceiling(fy + sh));
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			float num8 = 0f;
			int num9 = 0;
			for (int i = num2; i < num4; i++)
			{
				for (int j = num; j < num3; j++)
				{
					Color32 color = D[i * W + j];
					float num10 = (float)(int)color.a / 255f;
					num5 += (float)(int)color.r * num10;
					num6 += (float)(int)color.g * num10;
					num7 += (float)(int)color.b * num10;
					num8 += (float)(int)color.a;
					num9++;
				}
			}
			if (num9 == 0 || num8 <= 0f)
			{
				return new Color32(0, 0, 0, 0);
			}
			float num11 = num8 / 255f;
			return new Color32((byte)(num5 / num11), (byte)(num6 / num11), (byte)(num7 / num11), (byte)(num8 / (float)num9));
		}

		private static Color32 Mix(Color32 a, Color32 b, Color32 c, Color32 d, float tx, float ty)
		{
			float num = (1f - tx) * (1f - ty) * (float)(int)a.a;
			float num2 = tx * (1f - ty) * (float)(int)b.a;
			float num3 = (1f - tx) * ty * (float)(int)c.a;
			float num4 = tx * ty * (float)(int)d.a;
			float num5 = num + num2 + num3 + num4;
			float num6 = (1f - tx) * (1f - ty) * (float)(int)a.a + tx * (1f - ty) * (float)(int)b.a + (1f - tx) * ty * (float)(int)c.a + tx * ty * (float)(int)d.a;
			if (num5 <= 0f)
			{
				return new Color32(0, 0, 0, 0);
			}
			return new Color32((byte)(((float)(int)a.r * num + (float)(int)b.r * num2 + (float)(int)c.r * num3 + (float)(int)d.r * num4) / num5), (byte)(((float)(int)a.g * num + (float)(int)b.g * num2 + (float)(int)c.g * num3 + (float)(int)d.g * num4) / num5), (byte)(((float)(int)a.b * num + (float)(int)b.b * num2 + (float)(int)c.b * num3 + (float)(int)d.b * num4) / num5), (byte)num6);
		}

		public void Over(Px s, int ox, int oy)
		{
			for (int i = 0; i < s.H; i++)
			{
				int num = oy + i;
				if (num < 0 || num >= H)
				{
					continue;
				}
				for (int j = 0; j < s.W; j++)
				{
					int num2 = ox + j;
					if (num2 < 0 || num2 >= W)
					{
						continue;
					}
					Color32 color = s.D[i * s.W + j];
					if (color.a != 0)
					{
						if (color.a == byte.MaxValue)
						{
							D[num * W + num2] = color;
							continue;
						}
						Color32 color2 = D[num * W + num2];
						float num3 = (float)(int)color.a / 255f;
						float num4 = (float)(int)color2.a / 255f;
						float num5 = num3 + num4 * (1f - num3);
						D[num * W + num2] = new Color32((byte)(((float)(int)color.r * num3 + (float)(int)color2.r * num4 * (1f - num3)) / num5), (byte)(((float)(int)color.g * num3 + (float)(int)color2.g * num4 * (1f - num3)) / num5), (byte)(((float)(int)color.b * num3 + (float)(int)color2.b * num4 * (1f - num3)) / num5), (byte)(num5 * 255f));
					}
				}
			}
		}

		public Px Trim()
		{
			int num = W;
			int num2 = H;
			int num3 = -1;
			int num4 = -1;
			for (int i = 0; i < H; i++)
			{
				for (int j = 0; j < W; j++)
				{
					if (D[i * W + j].a > 8)
					{
						if (j < num)
						{
							num = j;
						}
						if (j > num3)
						{
							num3 = j;
						}
						if (i < num2)
						{
							num2 = i;
						}
						if (i > num4)
						{
							num4 = i;
						}
					}
				}
			}
			if (num3 >= 0)
			{
				return Crop(num, num2, num3 - num + 1, num4 - num2 + 1);
			}
			return this;
		}
	}

	public static Sprite Frame;

	public static Sprite CompactFrame;

	public static Sprite PlantCard;

	public static Sprite ZombieCard;

	private static bool _tried;

	private const float FramePpu = 227.27274f;

	public static void Build()
	{
		if (_tried)
		{
			return;
		}
		_tried = true;
		try
		{
			BuildFrames();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning("frame art: " + ex.Message);
			Frame = (CompactFrame = null);
		}
		try
		{
			BuildCards();
		}
		catch (Exception ex2)
		{
			Plugin.Logger.LogWarning("card art: " + ex2.Message);
			PlantCard = (ZombieCard = null);
		}
	}

	private static void BuildFrames()
	{
		Px px = P("dialog_topleft", 0, 0, 3, 3);
		Px px2 = P("dialog_topmiddle", 3, 0, 3, 3);
		Px px3 = P("dialog_topright", 3, 0, 0, 3);
		Px l = P("dialog_centerleft", 0, 3, 3, 3);
		Px px4 = P("dialog_centermiddle", 3, 3, 3, 3);
		Px r = P("dialog_centerright", 3, 3, 0, 3);
		Px px5 = P("dialog_bottomleft", 0, 3, 3, 0);
		Px px6 = P("dialog_bottommiddle", 3, 3, 3, 0);
		Px px7 = P("dialog_bottomright", 3, 3, 0, 0);
		int L = px.W;
		int M = px2.W;
		int w = px3.W;
		int h = px.H;
		int h2 = px4.H;
		int num = Math.Max(px5.H, Math.Max(px6.H, px7.H));
		Px full = new Px(L + M + w, h + h2 + num);
		Row(0, px, px2, px3);
		Row(h, l, px4, r);
		Row(h + h2, px5, px6, px7);
		Frame = ToSprite(full.Resize(full.W / 2, full.H / 2), new Vector4(L, num, w, h) / 2f, 227.27274f);
		int num2 = h + h2 + 42;
		int h3 = num2 + 58;
		int num3 = full.H - 130;
		Px px8 = full.Crop(0, 0, full.W, h3);
		Px px9 = full.Crop(0, num3, full.W, full.H - num3);
		Px px10 = new Px(full.W, px8.H + px9.H);
		px10.Over(px8, 0, 0);
		px10.Over(px9, 0, px8.H);
		int num4 = px10.H - num2 - 1;
		CompactFrame = ToSprite(px10.Resize(px10.W / 2, px10.H / 2), new Vector4(L, num4, w, h) / 2f, 227.27274f);
		static Px P(string n, int num5, int t, int num6, int b)
		{
			Px px11 = Read(n);
			return px11.Crop(num5, t, px11.W - num5 - num6, px11.H - t - b).Resize(px11.W, px11.H);
		}
		void Row(int y, Px px11, Px m, Px s)
		{
			full.Over(px11, L - px11.W, y);
			full.Over(m.Resize(M, m.H), L, y);
			full.Over(s, L + M, y);
		}
	}

	private static void BuildCards()
	{
		Px px = Read("SeedPacket_Default");
		int num = px.H * 106 / 140;
		int num2 = Math.Min(px.H, px.H * 138 / 140);
		int num3 = px.W * 58 / 100;
		for (int i = num; i < num2; i++)
		{
			for (int j = num3; j < px.W; j++)
			{
				int x = ((j <= px.W * 92 / 100) ? (px.W * 30 / 100) : (px.W - 1 - j));
				px[j, i] = px[x, i];
			}
		}
		Px card = px.Resize(px.W * 4, px.H * 4);
		RectInt win = new RectInt(px.W * 6 / 100 * 4, px.H * 16 / 140 * 4, px.W * 87 / 100 * 4, px.H * 88 / 140 * 4);
		PlantCard = Make("PeaShooter");
		ZombieCard = Make("NormalZombie");
		Sprite Make(string art)
		{
			Px px2 = Read(art, largest: true).Trim();
			float num4 = Math.Min((float)win.width * 0.86f / (float)px2.W, (float)win.height * 0.9f / (float)px2.H);
			px2 = px2.Resize(Math.Max(1, (int)((float)px2.W * num4)), Math.Max(1, (int)((float)px2.H * num4)));
			Px px3 = card.Copy();
			px3.Over(px2, win.x + (win.width - px2.W) / 2, win.y + (win.height - px2.H) / 2);
			return ToSprite(px3, Vector4.zero, 100f);
		}
	}

	private static Px Read(string name, bool largest = false)
	{
		Sprite sprite = null;
		foreach (Sprite item in Resources.FindObjectsOfTypeAll<Sprite>())
		{
			if (!(item == null) && !(item.name != name))
			{
				if (sprite == null || (largest && item.rect.width * item.rect.height > sprite.rect.width * sprite.rect.height))
				{
					sprite = item;
				}
				if (!largest)
				{
					break;
				}
			}
		}
		if (sprite == null)
		{
			throw new Exception("sprite not loaded: " + name);
		}
		Texture2D texture = sprite.texture;
		Rect textureRect = sprite.textureRect;
		int num = Mathf.RoundToInt(textureRect.width);
		int num2 = Mathf.RoundToInt(textureRect.height);
		RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
		RenderTexture active = RenderTexture.active;
		Texture2D texture2D = null;
		try
		{
			Graphics.Blit(texture, temporary);
			RenderTexture.active = temporary;
			texture2D = new Texture2D(num, num2, TextureFormat.RGBA32, mipChain: false);
			texture2D.ReadPixels(new Rect(textureRect.x, textureRect.y, num, num2), 0, 0);
			texture2D.Apply();
			Il2CppStructArray<Color32> pixels = texture2D.GetPixels32();
			Px px = new Px(num, num2);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					px.D[i * num + j] = pixels[(num2 - 1 - i) * num + j];
				}
			}
			return px;
		}
		finally
		{
			RenderTexture.active = active;
			RenderTexture.ReleaseTemporary(temporary);
			if (texture2D != null)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}
	}

	private static Sprite ToSprite(Px p, Vector4 border, float ppu)
	{
		Texture2D texture2D = new Texture2D(p.W, p.H, TextureFormat.RGBA32, mipChain: false);
		Il2CppStructArray<Color32> il2CppStructArray = new Il2CppStructArray<Color32>(p.W * p.H);
		for (int i = 0; i < p.H; i++)
		{
			for (int j = 0; j < p.W; j++)
			{
				il2CppStructArray[(p.H - 1 - i) * p.W + j] = p.D[i * p.W + j];
			}
		}
		texture2D.SetPixels32(il2CppStructArray);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
		Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, p.W, p.H), new Vector2(0.5f, 0.5f), ppu, 0u, SpriteMeshType.FullRect, border, generateFallbackPhysicsShape: false);
		sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return sprite;
	}
}
