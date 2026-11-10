using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public class Btn
{
	public RectTransform Rt;

	public Image Bg;

	public TextMeshProUGUI Label;

	public Action OnClick;

	public Color TextColor = Ui.Green;

	private bool _enabled = true;

	private bool _hover;

	public bool Enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			if (_enabled != value)
			{
				_enabled = value;
				Refresh();
			}
		}
	}

	public string Text
	{
		set
		{
			if (Label != null && Label.text != value)
			{
				Label.text = value;
			}
		}
	}

	public void SetColor(Color c)
	{
		if (!(TextColor == c))
		{
			TextColor = c;
			Refresh();
		}
	}

	public void SetHover(bool h)
	{
		if (_hover != h)
		{
			_hover = h;
			Refresh();
		}
	}

	private void Refresh()
	{
		if (Bg != null)
		{
			Bg.color = ((!_enabled) ? new Color(0.55f, 0.55f, 0.55f) : (_hover ? new Color(1.15f, 1.15f, 1.15f) : Color.white));
		}
		if (Label != null)
		{
			Label.color = ((!_enabled) ? Ui.Dim : (_hover ? Color.Lerp(TextColor, Color.white, 0.45f) : TextColor));
		}
	}
}
