using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public class AvatarView
{
	private readonly RawImage _raw;

	private readonly Image _disc;

	private readonly TextMeshProUGUI _initial;

	private CSteamID _id;

	private string _name = "";

	public AvatarView(Transform parent, Rect r)
	{
		_disc = Ui.Img(parent, r, Ui.Circle, Ui.Gold);
		_initial = Ui.Text(_disc.rectTransform, new Rect(0f, 0f, r.width, r.height), "", r.height * 0.55f, Color.white);
		_raw = Ui.Raw(parent, r);
	}

	public void Set(CSteamID id, string name, Color placeholder)
	{
		_id = id;
		_name = name ?? "";
		_disc.color = placeholder;
		Tick();
	}

	public void Show(bool on)
	{
		Ui.Show(_disc, on);
		Ui.Show(_raw, on);
	}

	public void Tick()
	{
		Texture2D texture2D = Avatars.Get(_id);
		if (texture2D != null && _raw.texture != texture2D)
		{
			_raw.texture = texture2D;
		}
		bool flag = texture2D != null;
		_raw.color = (flag ? Color.white : new Color(0f, 0f, 0f, 0f));
		string text = ((_name.Length > 0) ? _name.Substring(0, 1).ToUpperInvariant() : "?");
		if (_initial.text != text)
		{
			_initial.text = text;
		}
		_initial.color = (flag ? new Color(0f, 0f, 0f, 0f) : Color.white);
	}
}
