using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

public static class PartnerBank
{
	private static GameObject _bank;

	private static TextMeshProUGUI _sunText;
    private static Image _body;
    private static RectTransform _badge;
    private static TextMeshProUGUI _badgeText;
    private static float _keepOpenUntil;
    public static bool Expanded { get; private set; }
    public static bool Hovering { get; private set; }
    private static readonly System.Collections.Generic.List<RectTransform> _views = new();
    private static readonly System.Collections.Generic.Dictionary<int,RectTransform> _viewTypes = new();

	private static readonly System.Collections.Generic.HashSet<IntPtr> _cards = new System.Collections.Generic.HashSet<IntPtr>();

	private static Canvas _canvas;

	public static Rect ScreenRect;

	public static Rect MyScreenRect;
    public static float ReservedBottomPixels => 0f;
    public static void Clear() { Destroy(); ScreenRect = MyScreenRect = default; }

	private static readonly System.Collections.Generic.List<CardUI> _partnerCards = new System.Collections.Generic.List<CardUI>();

	private static float[] _partnerCd = Array.Empty<float>();
    private static readonly System.Collections.Generic.List<int> _viewSlots=new();
    private static readonly System.Collections.Generic.List<Image> _cooldownMasks=new();
    private static readonly System.Collections.Generic.Dictionary<int,float> _cooldownTarget=new();
    private static readonly System.Collections.Generic.Dictionary<int,float> _cooldownDisplay=new();

	private static readonly Il2CppStructArray<Vector3> _corners = new Il2CppStructArray<Vector3>(4L);

	public static bool Visible
	{
		get
		{
			if (_bank != null)
			{
				return !_bank.WasCollected;
			}
			return false;
		}
	}

	public static bool IsPartnerCard(CardUI c)
	{
		if (c != null)
		{
			return _cards.Contains(c.Pointer);
		}
		return false;
	}

	public static void Build()
	{
		Destroy();
		try
		{
			BuildInner();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("partner bank: " + ex);
		}
	}

	private static void Destroy()
	{
		if (_bank != null && !_bank.WasCollected)
		{
			UnityEngine.Object.Destroy(_bank);
		}
		if(_badge!=null && !_badge.WasCollected)UnityEngine.Object.Destroy(_badge.gameObject);
        _badge=null;_badgeText=null;Expanded=Hovering=false;_keepOpenUntil=0;
        _bank = null;
		_sunText = null;
		_cards.Clear();
		_partnerCards.Clear();
        _views.Clear();_viewTypes.Clear();_viewSlots.Clear();_cooldownMasks.Clear();_cooldownTarget.Clear();_cooldownDisplay.Clear();
		_partnerCd = Array.Empty<float>();
	}

    public static string ReadMyCooldowns()
    {
        var ui=InGameUI.Instance;if(ui?.Cards==null)return "";
        var values=new System.Collections.Generic.List<string>();
        foreach(var card in ui.Cards)if(card!=null)values.Add(DeckSharing.Key(card)+":"+(card.slider==null?0:card.slider.value).ToString("0.###",CultureInfo.InvariantCulture));
        return string.Join(",",values);
    }
    public static void SetCooldowns(string payload)
    {
        foreach(var item in payload.Split(',',StringSplitOptions.RemoveEmptyEntries)){
            var p=item.Split(':');if(p.Length!=2||!int.TryParse(p[0],out int key)||!float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||!float.IsFinite(value))continue;
            value=Mathf.Clamp01(value);_cooldownTarget[key]=value;
            if(!Expanded||!_cooldownDisplay.TryGetValue(key,out float old)||value>old)_cooldownDisplay[key]=value;
        }
    }
    private static void UpdateCooldowns()
    {
        for(int i=0;i<_views.Count&&i<_viewSlots.Count;i++){
            int key=_viewSlots[i];float target=_cooldownTarget.TryGetValue(key,out float value)?value:0;
            float shown=_cooldownDisplay.TryGetValue(key,out float old)?old:target;
            shown=target>shown?target:Mathf.MoveTowards(shown,target,Mathf.Max(.03f,Mathf.Abs(target-shown)/.12f)*Time.unscaledDeltaTime);
            _cooldownDisplay[key]=shown;
            var mask=_cooldownMasks[i];
            Ui.Place(mask.rectTransform,new Rect(0,0,100,140*shown));
            mask.gameObject.SetActive(shown>.001f);
        }
    }

    private static void BuildInner()
    {
        var ui = InGameUI.Instance;
        if(ui?.Cards == null || ui.Cards.Count == 0 || Ui.Root == null) return;
        var frame=Ui.Node(Ui.Root,"TeammateCards",new Rect(18,150,1100,84));
        _body=Ui.Img(frame,new Rect(0,0,1100,84),CoopArt.Badge);
        _badge=Ui.Img(Ui.Root,new Rect(18,150,248,46),CoopArt.Badge).rectTransform;
        _badgeText=Ui.Text(_badge,new Rect(13,3,225,24),"TEAMMATE",19,Ui.Gold,TextAlignmentOptions.Left);
        Ui.Text(_badge,new Rect(13,25,225,18),Ukrainian.CardsHint(Session.PartnerDeck.Count),14,Ui.Pale,TextAlignmentOptions.Left);
        _bank=frame.gameObject;_bank.name="TeammateCards";
        _canvas=Ui.Root.GetComponentInParent<Canvas>();
        // Both players use the stock sun counter; the hover badge only identifies the deck.
        CardUI template=ui.Cards[0];
        for(int i=0;i<Session.PartnerDeck.Count;i++) {
            var go=UnityEngine.Object.Instantiate(template.gameObject,template.transform.parent,false);
            go.SetActive(false);
            var card=go.GetComponent<CardUI>();
            if(card==null){UnityEngine.Object.Destroy(go);continue;}
            card.onDestroy=null;card.enabled=false;
            card.thePlantType=(PlantType)Session.PartnerDeck[i].type;card.theSeedType=Session.PartnerDeck[i].type;card.theSeedCost=Session.PartnerDeck[i].cost;
            try{Mouse.Instance.ChangeCardSprite(card.thePlantType,card);}catch(Exception ex){Plugin.Logger.LogWarning("Card artwork: "+ex.Message);}
            var source=go.GetComponent<RectTransform>();
            var view=Ui.Node(frame,"PlantArtTemplate"+i,new Rect(0,0,Mathf.Max(1,source.rect.width),Mathf.Max(1,source.rect.height)));
            if(!_viewTypes.ContainsKey((int)card.thePlantType))_viewTypes.Add((int)card.thePlantType,view);
            foreach(var img in go.GetComponentsInChildren<Image>(true)) {
                if(img.sprite==null || !img.enabled || !img.gameObject.activeSelf || img.GetComponentInParent<Slider>()!=null)continue;
                img.rectTransform.GetWorldCorners(_corners);
                Vector3 lo=source.InverseTransformPoint(_corners[0]),hi=source.InverseTransformPoint(_corners[2]);
                var image=Ui.Img(view,new Rect(lo.x-source.rect.xMin,source.rect.yMax-hi.y,hi.x-lo.x,hi.y-lo.y),img.sprite,Color.white);
                image.type=img.type;image.preserveAspect=img.preserveAspect;
            }
            foreach(var renderer in go.GetComponentsInChildren<SpriteRenderer>(true)) {
                if(renderer.sprite==null || !renderer.enabled || !renderer.gameObject.activeSelf)continue;
                Vector3 lo=source.InverseTransformPoint(renderer.bounds.min),hi=source.InverseTransformPoint(renderer.bounds.max);
                Ui.Img(view,new Rect(lo.x-source.rect.xMin,source.rect.yMax-hi.y,hi.x-lo.x,hi.y-lo.y),renderer.sprite,Color.white);
            }
            view.gameObject.SetActive(false);
            var packet=Ui.Img(frame,new Rect(0,0,100,140),CoopArt.CardFrame).rectTransform;
            packet.gameObject.name="TeammateSeedPacket"+i;
            _views.Add(packet);
            _viewSlots.Add(i<Session.PartnerSlotKeys.Count?Session.PartnerSlotKeys[i]:Session.PartnerDeck[i].type*2);
            Ui.Img(packet,new Rect(7,18,86,91),Ui.White,new Color(.83f,.92f,.69f));
            var artwork=MakePlantPreview((int)card.thePlantType,packet,1f);
            if(artwork!=null){
                float scale=Mathf.Min(78f/Mathf.Max(1,artwork.rect.width),80f/Mathf.Max(1,artwork.rect.height));
                artwork.localScale=new Vector3(scale,scale,1);
                artwork.anchoredPosition=new Vector2(50f-artwork.rect.width*scale/2,-64f+artwork.rect.height*scale/2);
            }
            Ui.Text(packet,new Rect(5,113,70,23),card.theSeedCost.ToString(),23,new Color(.12f,.14f,.07f),TextAlignmentOptions.Center,false);
            Ui.Img(packet,new Rect(78,117,16,16),CoopArt.SunIcon);
            _cooldownMasks.Add(Ui.Img(packet,new Rect(0,0,100,0),Ui.White,new Color(0,0,0,.65f)));
            UnityEngine.Object.Destroy(go);
        }
        Layout();
    }
    public static RectTransform MakePlantPreview(int type,Transform parent,float alpha=.55f)
    {
        if(!_viewTypes.TryGetValue(type,out var template)||template==null)return null;
        var parts=new System.Collections.Generic.List<(Image image,Rect rect)>();
        float left=float.MaxValue,top=float.MaxValue,right=float.MinValue,bottom=float.MinValue;
        foreach(var image in template.GetComponentsInChildren<Image>(true)) {
            if(image.sprite==null||!image.enabled)continue;
            image.rectTransform.GetWorldCorners(_corners);
            Vector3 lo=template.InverseTransformPoint(_corners[0]),hi=template.InverseTransformPoint(_corners[2]);
            var r=new Rect(lo.x,-hi.y,hi.x-lo.x,hi.y-lo.y);
            if(r.width<=0||r.height<=0)continue;
            parts.Add((image,r));left=Mathf.Min(left,r.x);top=Mathf.Min(top,r.y);right=Mathf.Max(right,r.xMax);bottom=Mathf.Max(bottom,r.yMax);
        }
        if(parts.Count==0)return null;
        var result=Ui.Node(parent,"TeammatePlantGhost",new Rect(0,0,right-left,bottom-top));
        foreach(var part in parts){var r=part.rect;r.x-=left;r.y-=top;var image=Ui.Img(result,r,part.image.sprite,new Color(1,1,1,alpha));image.preserveAspect=part.image.preserveAspect;image.type=part.image.type;}
        return result;
    }
    private static void Layout(Vector2? pointerOverride=null)
    {
        UpdateCooldowns();
        MyScreenRect=ToScreen(InGameUI.Instance?.SeedBank?.GetComponent<RectTransform>(),InGameUI.Instance?.SeedBank?.GetComponentInParent<Canvas>());
        int columns=Mathf.Max(1,Mathf.Min(_views.Count,(int)((Mathf.Min(1120f,Ui.Width-36f)-24f)/76f)));
        int rows=Mathf.Max(1,(_views.Count+columns-1)/columns);
        float width=24f+columns*76f,height=16f+rows*104f;
        Rect badgeRect=new Rect(18f,150f,248f,46f);
        Rect panelRect=new Rect(18f,200f,width,height);
        Vector2 pointer=pointerOverride ?? new Vector2(Input.mousePosition.x/Ui.Scale,(Screen.height-Input.mousePosition.y)/Ui.Scale);
        bool over=badgeRect.Contains(pointer) || (Expanded && panelRect.Contains(pointer));
        if(over)_keepOpenUntil=Time.unscaledTime+.18f;
        Hovering=over;Expanded=over || Time.unscaledTime<_keepOpenUntil;
        Ui.Place(_badge,badgeRect);
        var rt=_bank.GetComponent<RectTransform>();
        Ui.Place(rt,panelRect);Ui.Place(_body.rectTransform,new Rect(0,0,width,height));
        for(int i=0;i<_views.Count;i++) {
            var c=_views[i];
            c.anchorMin=c.anchorMax=new Vector2(0,1);c.pivot=new Vector2(0,1);
            float factor=Mathf.Min(70f/Mathf.Max(1,c.rect.width),98f/Mathf.Max(1,c.rect.height));
            c.localScale=new Vector3(factor,factor,1);
            c.anchoredPosition=new Vector2(12f+(i%columns)*76f,-8f-(i/columns)*104f);
        }
        _bank.SetActive(Expanded);
        ScreenRect=ToScreen(Expanded?rt:_badge);
    }

	private static string Path(Transform t)
	{
		string text = t.name;
		Transform parent = t.parent;
		while (parent != null)
		{
			text = parent.name + "/" + text;
			parent = parent.parent;
		}
		return text;
	}

	public static void Tick()
	{
		if (Visible)
		{
			if (_sunText != null && Sync.PartnerSun >= 0)
			{
				_sunText.text = (Board.Instance==null?Sync.PartnerSun:Board.Instance.theSun).ToString();
			}
            Layout();
		}
	}

	private static Rect ToScreen(RectTransform rt)
	{
		return ToScreen(rt, _canvas);
	}

	public static Rect ToScreen(RectTransform rt, Canvas canvas)
	{
		if (rt == null)
		{
			return default(Rect);
		}
		rt.GetWorldCorners(_corners);
		Camera cam = ((canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null);
		Vector2 vector = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
		Vector2 vector2 = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
		return Rect.MinMaxRect(vector.x, (float)Screen.height - vector2.y, vector2.x, (float)Screen.height - vector.y);
	}
}




