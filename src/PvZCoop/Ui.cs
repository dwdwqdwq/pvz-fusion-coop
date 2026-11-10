using System;
using System.Collections.Generic;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public static class Ui
{
	public static RectTransform Root;

	private static Canvas _canvas;

	private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

	private static readonly List<Btn> _buttons = new List<Btn>();

	private static Sprite _white;

	private static Sprite _circle;

	public static readonly Color Gold = new Color(1f, 0.86f, 0.25f);

	public static readonly Color Green = new Color(0.5f, 1f, 0.3f);

	public static readonly Color Red = new Color(1f, 0.25f, 0.2f);

	public static readonly Color Pale = new Color(0.92f, 0.92f, 0.85f);

	public static readonly Color Dim = new Color(0.6f, 0.6f, 0.6f);

	public static float Scale => (float)Screen.height / 1080f;

	public static float Width => (float)Screen.width / Scale;

	public static TMP_FontAsset Font => GameAPP.font;

	public static Sprite White => _white ?? (_white = MakeSprite(4, (int x, int y) => 1f));

	public static Sprite Circle => _circle ?? (_circle = MakeSprite(64, delegate(int x, int y)
	{
		float num = Mathf.Sqrt(((float)x - 31.5f) * ((float)x - 31.5f) + ((float)y - 31.5f) * ((float)y - 31.5f));
		return Mathf.Clamp01(31.5f - num);
	}));

	private static bool _mouseOverUi;
    public static bool MouseOverUi => _mouseOverUi || PartnerBank.Hovering;

	public static void Init(GameObject host)
	{
		GameObject gameObject = new GameObject("PvZCoopUI");
		gameObject.transform.SetParent(host.transform, worldPositionStays: false);
		_canvas = gameObject.AddComponent<Canvas>();
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		_canvas.sortingOrder = 32767;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		canvasScaler.matchWidthOrHeight = 1f;
		Root = gameObject.GetComponent<RectTransform>();
	}

	public static Sprite Sprite(string name)
	{
		if (_sprites.TryGetValue(name, out var value) && value != null && !value.WasCollected)
		{
			return value;
		}
		foreach (Sprite item in Resources.FindObjectsOfTypeAll<Sprite>())
		{
			if (item != null && string.Equals(item.name,name,StringComparison.OrdinalIgnoreCase))
			{
				_sprites[name] = item;
				return item;
			}
		}
		return null;
	}

	public static Sprite Trimmed(string name, float px = 3f)
	{
		string key = name + "#trim";
		if (_sprites.TryGetValue(key, out var value) && value != null && !value.WasCollected)
		{
			return value;
		}
		Sprite sprite = Sprite(name);
		if (sprite == null)
		{
			return null;
		}
		try
		{
			Rect textureRect = sprite.textureRect;
			Sprite sprite2 = UnityEngine.Sprite.Create(sprite.texture, new Rect(textureRect.x + px, textureRect.y + px, textureRect.width - 2f * px, textureRect.height - 2f * px), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
			sprite2.hideFlags = HideFlags.DontUnloadUnusedAsset;
			_sprites[key] = sprite2;
			return sprite2;
		}
		catch (Exception ex)
		{
			ManualLogSource logger = Plugin.Logger;
			bool isEnabled;
			BepInExWarningLogInterpolatedStringHandler bepInExWarningLogInterpolatedStringHandler = new BepInExWarningLogInterpolatedStringHandler(7, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral("trim ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(name);
				bepInExWarningLogInterpolatedStringHandler.AppendLiteral(": ");
				bepInExWarningLogInterpolatedStringHandler.AppendFormatted(ex.Message);
			}
			logger.LogWarning(bepInExWarningLogInterpolatedStringHandler);
			_sprites[key] = sprite;
			return sprite;
		}
	}

	private static Sprite MakeSprite(int size, Func<int, int, float> alpha)
	{
		Texture2D texture2D = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		Il2CppStructArray<Color32> il2CppStructArray = new Il2CppStructArray<Color32>(size * size);
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				il2CppStructArray[i * size + j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)(alpha(j, i) * 255f));
			}
		}
		texture2D.SetPixels32(il2CppStructArray);
		texture2D.Apply();
		texture2D.hideFlags = HideFlags.DontUnloadUnusedAsset;
		Sprite sprite = UnityEngine.Sprite.Create(texture2D, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
		sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
		return sprite;
	}

	public static RectTransform Node(Transform parent, string name, Rect r)
	{
		GameObject gameObject = new GameObject(name);
		gameObject.transform.SetParent(parent ?? Root, worldPositionStays: false);
		RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(0f, 1f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(0f, 1f);
		Place(rectTransform, r);
		return rectTransform;
	}

	public static void Place(RectTransform rt, Rect r)
	{
		rt.anchoredPosition = new Vector2(r.x, 0f - r.y);
		rt.sizeDelta = new Vector2(r.width, r.height);
	}

	public static Image Img(Transform parent, Rect r, Sprite sprite, Color? color = null, bool sliced = false)
	{
		Image image = Node(parent, "img", r).gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.color = color ?? Color.white;
		image.raycastTarget = false;
		if (sliced)
		{
			image.type = Image.Type.Sliced;
		}
		if (sprite == null)
		{
			image.color = new Color(0f, 0f, 0f, 0f);
		}
		return image;
	}

	public static RawImage Raw(Transform parent, Rect r)
	{
		RawImage rawImage = Node(parent, "raw", r).gameObject.AddComponent<RawImage>();
		rawImage.raycastTarget = false;
		return rawImage;
	}

	public static TextMeshProUGUI Text(Transform parent, Rect r, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = true)
	{
		TextMeshProUGUI textMeshProUGUI = Node(parent, "text", r).gameObject.AddComponent<TextMeshProUGUI>();
		if (Font != null)
		{
			textMeshProUGUI.font = Font;
		}
		textMeshProUGUI.fontSize = size;
		textMeshProUGUI.text = text;
		textMeshProUGUI.color = color;
		textMeshProUGUI.alignment = align;
		textMeshProUGUI.enableWordWrapping = true;
		textMeshProUGUI.raycastTarget = false;
		if (outline)
		{
			textMeshProUGUI.outlineWidth = 0.22f;
			textMeshProUGUI.outlineColor = new Color32(20, 15, 10, byte.MaxValue);
		}
		return textMeshProUGUI;
	}

	public static RectTransform Dialog(Transform parent, Rect r, bool header = false, bool compact = false)
	{
		RectTransform rectTransform = Node(parent, "dialog", r);
		Sprite sprite = (compact ? (Art.CompactFrame ?? Art.Frame) : Art.Frame);
		if (sprite != null)
		{
			Img(rectTransform, new Rect(0f, 0f, r.width, r.height), sprite, null, sliced: true);
			if (header)
			{
				Img(rectTransform, new Rect(r.width / 2f - 150f, -62f, 300f, 103f), Sprite("dialog_header"));
			}
			return rectTransform;
		}
		float num = 98.12f;
		float num2 = 110f;
		float num3 = 89.1f;
		float num4 = 104.5f;
		float width = r.width;
		float height = r.height;
		float num5 = width - num - num2;
		float num6 = height - num3 - num4;
		Img(rectTransform, new Rect(0f, 0f, num, num3), Trimmed("dialog_topleft"));
		Img(rectTransform, new Rect(num, 0f, num5, num3), Trimmed("dialog_topmiddle"));
		Img(rectTransform, new Rect(num + num5, 0f, num2, num3), Trimmed("dialog_topright"));
		if (num6 > 0f)
		{
			Img(rectTransform, new Rect(0f, num3, num, num6), Trimmed("dialog_centerleft"));
			Img(rectTransform, new Rect(num, num3, num5, num6), Trimmed("dialog_centermiddle"));
			Img(rectTransform, new Rect(num + num5, num3, num2, num6), Trimmed("dialog_centerright"));
		}
		Img(rectTransform, new Rect(0f, height - num4, num, num4), Trimmed("dialog_bottomleft"));
		Img(rectTransform, new Rect(num, height - num4, num5, num4), Trimmed("dialog_bottommiddle"));
		Img(rectTransform, new Rect(num + num5, height - num4, num2, num4), Trimmed("dialog_bottomright"));
		if (header)
		{
			Img(rectTransform, new Rect(width / 2f - 150f, -62f, 300f, 103f), Sprite("dialog_header"));
		}
		return rectTransform;
	}

	public static Image Plate(Transform parent, Rect r, float alpha = 0.78f)
	{
		return Img(parent, r, Sprite("InputFieldBackground"), new Color(0.08f, 0.07f, 0.06f, alpha), sliced: true);
	}

	public static Btn Button(Transform parent, Rect r, string label, Action onClick, string sprite = "buttonbig", float fontSize = 30f, Color? textColor = null)
	{
		Btn btn = new Btn();
		btn.Bg = Img(parent, r, Sprite(sprite));
		btn.Rt = btn.Bg.rectTransform;
		btn.Label = Text(btn.Rt, new Rect(6f, 2f, r.width - 12f, r.height - 6f), label, fontSize, textColor ?? Green);
		RectTransform rectTransform = btn.Label.rectTransform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(6f, 4f);
		rectTransform.offsetMax = new Vector2(-6f, -2f);
		btn.TextColor = textColor ?? Green;
		btn.OnClick = onClick;
		_buttons.Add(btn);
		return btn;
	}

	public static Btn Hotspot(RectTransform rt, Action onClick)
	{
		Btn btn = new Btn
		{
			Rt = rt,
			OnClick = onClick
		};
		_buttons.Add(btn);
		return btn;
	}

	public static void Show(Component c, bool on)
	{
		if (c != null && c.gameObject.activeSelf != on)
		{
			c.gameObject.SetActive(on);
		}
	}

	public static Rect FromScreen(Rect px)
	{
		return new Rect(px.x / Scale, px.y / Scale, px.width / Scale, px.height / Scale);
	}

	public static void Update()
	{
		Btn btn = null;
		Vector2 screenPoint = Input.mousePosition;
		for (int num = _buttons.Count - 1; num >= 0; num--)
		{
			Btn btn2 = _buttons[num];
			if (btn2.Rt == null || btn2.Rt.WasCollected)
			{
				_buttons.RemoveAt(num);
			}
			else if (btn2.Rt.gameObject.activeInHierarchy && btn2.Enabled && RectTransformUtility.RectangleContainsScreenPoint(btn2.Rt, screenPoint, null))
			{
				btn = btn2;
				break;
			}
		}
		foreach (Btn button in _buttons)
		{
			button.SetHover(button == btn);
		}
		_mouseOverUi = btn != null;
		if (btn != null && Input.GetMouseButtonUp(0))
		{
			try
			{
				GameAPP.PlaySound(SoundType.ButtonClick, 1f);
			}
			catch
			{
			}
			try
			{
				btn.OnClick?.Invoke();
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("ui click: " + ex);
			}
		}
	}
}
