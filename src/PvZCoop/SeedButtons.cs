using System;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public static class SeedButtons
{
	private enum Space
	{
		None,
		World,
		Local,
		Anchored
	}

	private static IntPtr _done;

	private static float _next;

	public static void Tick()
	{
		if (Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 0.3f;
		SeedLibrary instance = SeedLibrary.Instance;
		if (!(instance == null) && !(instance.Pointer == _done))
		{
			Transform transform = Find("illustrated book");
			Transform transform2 = Find("previous page");
			Transform transform3 = Find("re-select");
			Transform transform4 = Find("next page");
			if (!(transform == null) && !(transform2 == null) && !(transform3 == null) && !(transform4 == null))
			{
				Swap(transform, transform2);
				Swap(transform3, transform4);
				_done = instance.Pointer;
				Plugin.Logger.LogInfo("seed screen: swapped book/previous and re-select/next buttons");
			}
		}
	}

	private static (Space space, Action<Vector3> set) Home(Transform t)
	{
		UIButton ui = t.GetComponent<UIButton>();
		InGameBtn ig = t.GetComponent<InGameBtn>();
		if (ui == null && ig == null)
		{
			return (space: Space.None, set: null);
		}
		Vector3 vector = ((ui != null) ? ui.originPosition : ig.originPosition);
		Action<Vector3> item = ((ui != null) ? ((Action<Vector3>)delegate(Vector3 v)
		{
			ui.originPosition = v;
		}) : ((Action<Vector3>)delegate(Vector3 v)
		{
			ig.originPosition = v;
		}));
		RectTransform component = t.GetComponent<RectTransform>();
		if ((vector - t.position).sqrMagnitude < 0.0025000002f)
		{
			return (space: Space.World, set: item);
		}
		if ((vector - t.localPosition).sqrMagnitude < 0.0025000002f)
		{
			return (space: Space.Local, set: item);
		}
		if (component != null && (vector - component.anchoredPosition3D).sqrMagnitude < 0.0025000002f)
		{
			return (space: Space.Anchored, set: item);
		}
		return (space: Space.None, set: null);
	}

	private static Vector3 Read(Transform t, Space s)
	{
		return s switch
		{
			Space.World => t.position, 
			Space.Local => t.localPosition, 
			Space.Anchored => t.GetComponent<RectTransform>().anchoredPosition3D, 
			_ => Vector3.zero, 
		};
	}

	private static void Swap(Transform a, Transform b)
	{
		(Space, Action<Vector3>) tuple = Home(a);
		(Space, Action<Vector3>) tuple2 = Home(b);
		Vector3 position = a.position;
		a.position = b.position;
		b.position = position;
		tuple.Item2?.Invoke(Read(a, tuple.Item1));
		tuple2.Item2?.Invoke(Read(b, tuple2.Item1));
		ManualLogSource logger = Plugin.Logger;
		bool isEnabled;
		BepInExInfoLogInterpolatedStringHandler bepInExInfoLogInterpolatedStringHandler = new BepInExInfoLogInterpolatedStringHandler(27, 4, out isEnabled);
		if (isEnabled)
		{
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral("seed screen: ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(a.name);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" home ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(tuple.Item1);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(", ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(b.name);
			bepInExInfoLogInterpolatedStringHandler.AppendLiteral(" home ");
			bepInExInfoLogInterpolatedStringHandler.AppendFormatted(tuple2.Item1);
		}
		logger.LogInfo(bepInExInfoLogInterpolatedStringHandler);
	}

	private static Transform Find(string label)
	{
		Transform transform = null;
		foreach (TMP_Text item in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
		{
			if (item.gameObject.activeInHierarchy && item.text != null && item.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				transform = item.transform;
				break;
			}
		}
		if (transform == null)
		{
			foreach (Text item2 in UnityEngine.Object.FindObjectsOfType<Text>())
			{
				if (item2.gameObject.activeInHierarchy && item2.text != null && item2.text.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					transform = item2.transform;
					break;
				}
			}
		}
		if (transform == null)
		{
			return null;
		}
		Transform transform2 = transform;
		while (transform2 != null)
		{
			if (transform2.GetComponent<UIButton>() != null || transform2.GetComponent<TheButton>() != null || transform2.GetComponent<InGameBtn>() != null || transform2.GetComponent<Collider2D>() != null)
			{
				return transform2;
			}
			transform2 = transform2.parent;
		}
		return transform.parent ?? transform;
	}
}
