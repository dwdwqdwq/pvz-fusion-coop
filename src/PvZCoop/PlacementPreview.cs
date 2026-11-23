using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace PvZCoop;
public static class PlacementPreview
{
 sealed class Remote { public int Id,Column,Row,Plant,ArtType=int.MinValue,ArtRevision=-1;public string SpriteName="";public bool Active,Exact;public float Expires,NextLookup;public Vector2 Center,Size;public Sprite Cached;public Image Icon;public Image[] Arrows;public RectTransform Art;public TMP_Text Label; }
 static readonly Dictionary<int,Remote> remotes=new();
 static Image[] ownArrows;static float nextSend,lastSend;static string last="0";
 static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
 public static void Init(){Party.OnMessage+=Receive;}
 static void HideArrows(Image[] list){if(list!=null)foreach(var a in list)Ui.Show(a,false);}
 static void Hide(Remote v){HideArrows(v.Arrows);Ui.Show(v.Icon,false);Ui.Show(v.Label,false);Ui.Show(v.Art,false);}
 public static void Reset(){foreach(var v in remotes.Values){if(v.Arrows!=null)foreach(var a in v.Arrows)UnityEngine.Object.Destroy(a.gameObject);if(v.Icon!=null)UnityEngine.Object.Destroy(v.Icon.gameObject);if(v.Art!=null)UnityEngine.Object.Destroy(v.Art.gameObject);if(v.Label!=null)UnityEngine.Object.Destroy(v.Label.gameObject);}remotes.Clear();last="0";nextSend=lastSend=0;HideArrows(ownArrows);}
 static void Receive(string type,string payload){
  int id=Party.SenderId;if(id<=0||id==Party.PlayerId)return;
  if(!remotes.TryGetValue(id,out var v)){v=new Remote{Id=id};remotes[id]=v;}
  if(type=="sp"){v.Active=false;return;}if(type!="preview"||!Sync.Running)return;if(payload=="0"){v.Active=false;return;}
  var p=payload.Split('|');if((p.Length!=4&&p.Length!=8)||!int.TryParse(p[0],out int c)||!int.TryParse(p[1],out int r)||!int.TryParse(p[2],out int t)||c<0||c>63||r<0||r>31||p[3].Length>1024)return;
  string name;try{name=Encoding.UTF8.GetString(Convert.FromBase64String(p[3]));}catch(FormatException){return;}
  if(v.Plant!=t||v.SpriteName!=name){v.Cached=null;v.NextLookup=0;}v.Exact=false;
  if(p.Length==8&&float.TryParse(p[4],NumberStyles.Float,Inv,out float x)&&float.TryParse(p[5],NumberStyles.Float,Inv,out float y)&&float.TryParse(p[6],NumberStyles.Float,Inv,out float w)&&float.TryParse(p[7],NumberStyles.Float,Inv,out float h)&&float.IsFinite(x)&&float.IsFinite(y)&&w>0&&h>0&&w<20&&h<20){v.Exact=true;v.Center=new Vector2(x,y);v.Size=new Vector2(w,h);}
  v.Column=c;v.Row=r;v.Plant=t;v.SpriteName=name;v.Active=true;v.Expires=Time.unscaledTime+1.5f;
 }
    static bool ReadVisual(out Sprite sprite,out Bounds bounds)
    {
        sprite=null;bounds=default;
        var mouse=Mouse.Instance;
        if(mouse==null||mouse.thePlantTypeOnMouse==PlantType.Nothing||mouse.preview==null||!mouse.preview.activeInHierarchy||mouse.theMouseColumn<0||mouse.theMouseRow<0||Ui.MouseOverUi)return false;
        Renderer chosen=null;
        foreach(var renderer in mouse.preview.GetComponentsInChildren<SpriteRenderer>())
            if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.sprite!=null&&!renderer.sprite.name.ToLowerInvariant().Contains("shadow")&&(chosen==null||renderer.bounds.size.sqrMagnitude>chosen.bounds.size.sqrMagnitude)){sprite=renderer.sprite;chosen=renderer;}
        if(chosen!=null)bounds=chosen.bounds;
        else {
            foreach(var renderer in mouse.preview.GetComponentsInChildren<Renderer>())
                if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.bounds.size.sqrMagnitude>.01f){bounds=renderer.bounds;chosen=renderer;break;}
            if(sprite==null&&mouse.theCardOnMouse!=null)foreach(var image in mouse.theCardOnMouse.GetComponentsInChildren<Image>())if(image.enabled&&image.sprite!=null){sprite=image.sprite;break;}
            if(chosen==null)bounds=new Bounds(mouse.preview.transform.position,Vector3.one);
        }
        return true;
    }
    static string SpriteKey(Sprite sprite)
    {
        if(sprite==null)return "";
        Rect r=sprite.rect;
        return sprite.name+"\n"+(sprite.texture==null?"":sprite.texture.name)+"\n"+r.x.ToString(Inv)+","+r.y.ToString(Inv)+","+r.width.ToString(Inv)+","+r.height.ToString(Inv);
    }
    static Sprite ResolveArt(string key)
    {
        var parts=key.Split('\n');
        if(parts.Length==3){
            var n=parts[2].Split(',');
            if(n.Length==4&&float.TryParse(n[0],NumberStyles.Float,Inv,out float x)&&float.TryParse(n[1],NumberStyles.Float,Inv,out float y)&&float.TryParse(n[2],NumberStyles.Float,Inv,out float w)&&float.TryParse(n[3],NumberStyles.Float,Inv,out float h))
                foreach(var sprite in Resources.FindObjectsOfTypeAll<Sprite>()){
                    if(sprite==null||sprite.name!=parts[0]||sprite.texture==null||sprite.texture.name!=parts[1])continue;
                    Rect r=sprite.rect;if(Mathf.Abs(r.x-x)<.1f&&Mathf.Abs(r.y-y)<.1f&&Mathf.Abs(r.width-w)<.1f&&Mathf.Abs(r.height-h)<.1f)return sprite;
                }
        }
        return parts.Length>0?Ui.Sprite(parts[0]):null;
    }
    static string ReadLocal()
    {
        if(!ReadVisual(out Sprite sprite,out Bounds bounds))return "0";
        var m=Mouse.Instance;Vector3 center=bounds.center,size=bounds.size;
        return m.theMouseColumn.ToString(Inv)+"|"+m.theMouseRow.ToString(Inv)+"|"+((int)m.thePlantTypeOnMouse).ToString(Inv)+"|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(SpriteKey(sprite)))+"|"+center.x.ToString("0.###",Inv)+"|"+center.y.ToString("0.###",Inv)+"|"+Mathf.Max(.1f,size.x).ToString("0.###",Inv)+"|"+Mathf.Max(.1f,size.y).ToString("0.###",Inv);
    }
    static bool Project(Vector2 center,Vector2 size,out Rect rect)
    {
        rect=default;var camera=Camera.main;if(camera==null)return false;
        Vector3 low=camera.WorldToScreenPoint(new Vector3(center.x-size.x/2,center.y-size.y/2,0));
        Vector3 high=camera.WorldToScreenPoint(new Vector3(center.x+size.x/2,center.y+size.y/2,0));
        if(low.z<=0||high.x<0||low.x>Screen.width||high.y<0||low.y>Screen.height)return false;
        rect=new Rect(low.x/Ui.Scale,(Screen.height-high.y)/Ui.Scale,Mathf.Max(1,(high.x-low.x)/Ui.Scale),Mathf.Max(1,(high.y-low.y)/Ui.Scale));return true;
    }
    static Image[] MakeArrows()
    {
        var list=new Image[4];for(int i=0;i<4;i++){list[i]=Ui.Img(Ui.Root,new Rect(0,0,20,22),CoopArt.Arrow);list[i].preserveAspect=true;Ui.Show(list[i],false);}return list;
    }
    static void DrawArrows(Image[] list,Rect rect,Color color)
    {
        float s=20f,gap=7f;
        Vector2[] positions={new Vector2(rect.x-gap,rect.center.y),new Vector2(rect.xMax+gap,rect.center.y),new Vector2(rect.center.x,rect.y-gap),new Vector2(rect.center.x,rect.yMax+gap)};
        float[] rotations={180,0,90,270};
        color.a=.85f+.15f*Mathf.Sin(Time.unscaledTime*4f);
        for(int i=0;i<4;i++){
            var rt=list[i].rectTransform;rt.pivot=new Vector2(.5f,.5f);
            Ui.Place(rt,new Rect(positions[i].x,positions[i].y,s,s*21f/19f));rt.localEulerAngles=new Vector3(0,0,rotations[i]);list[i].color=color;Ui.Show(list[i],true);
        }
    }
    public static void Tick(){
  if(!Sync.Running||Sync.ResultSent||Sync.ResultApplied||Ui.Root==null){Reset();return;}
  if(ownArrows==null)ownArrows=MakeArrows();
  if(ReadVisual(out _,out Bounds own)&&Project(own.center,own.size,out Rect ownRect))DrawArrows(ownArrows,ownRect,TeamUi.ColorFor(Party.PlayerId));else HideArrows(ownArrows);
  if(Time.unscaledTime>=nextSend){nextSend=Time.unscaledTime+.08f;string state=ReadLocal();if(state!=last||(state!="0"&&Time.unscaledTime-lastSend>=.4f)){Party.SendFast("preview",state,false);last=state;lastSend=Time.unscaledTime;}}
  foreach(var v in remotes.Values)Draw(v);
 }
 static void Draw(Remote v){
  if(!v.Active||Time.unscaledTime>v.Expires||Mouse.Instance==null){Hide(v);return;}
  Vector2 center=v.Center,size=v.Size;if(!v.Exact){float x=Mouse.Instance.GetBoxXFromColumn(v.Column);center=new Vector2(x,Mouse.Instance.GetLandY(x,v.Row));size=Vector2.one;}
  if(!Project(center,size,out Rect rect)){Hide(v);return;}
  if(v.Arrows==null){v.Arrows=MakeArrows();v.Icon=Ui.Img(Ui.Root,new Rect(0,0,1,1),Ui.White,new Color(1,1,1,.55f));v.Icon.preserveAspect=true;v.Icon.useSpriteMesh=true;v.Label=Ui.Text(Ui.Root,new Rect(0,0,160,22),Party.DisplayName(v.Id),15,TeamUi.ColorFor(v.Id));}
  if(v.ArtType!=v.Plant||v.ArtRevision!=PartnerBank.ArtRevision){if(v.Art!=null){v.Art.gameObject.SetActive(false);UnityEngine.Object.Destroy(v.Art.gameObject);}v.Art=PartnerBank.MakePlantPreview(v.Plant,Ui.Root);v.ArtType=v.Plant;v.ArtRevision=PartnerBank.ArtRevision;}
  if(v.Art!=null){float factor=Mathf.Min(rect.width/Mathf.Max(1,v.Art.rect.width),rect.height/Mathf.Max(1,v.Art.rect.height));v.Art.localScale=new Vector3(factor,factor,1);v.Art.anchoredPosition=new Vector2(rect.center.x-v.Art.rect.width*factor/2,-rect.center.y+v.Art.rect.height*factor/2);Ui.Show(v.Art,true);Ui.Show(v.Icon,false);}
  else{if((v.Cached==null||v.Cached.WasCollected)&&Time.unscaledTime>=v.NextLookup){v.NextLookup=Time.unscaledTime+2;v.Cached=v.SpriteName.Length>0?ResolveArt(v.SpriteName):null;if(v.Cached==null)v.Cached=Ui.Sprite(((PlantType)v.Plant).ToString());}v.Icon.sprite=v.Cached;Ui.Place(v.Icon.rectTransform,rect);Ui.Show(v.Icon,v.Cached!=null);}
  Ukrainian.MarkInput(v.Label);v.Label.text=Party.DisplayName(v.Id);v.Label.enableAutoSizing=true;v.Label.fontSizeMin=10;v.Label.fontSizeMax=15;v.Label.enableWordWrapping=false;v.Label.overflowMode=TextOverflowModes.Ellipsis;DrawArrows(v.Arrows,rect,TeamUi.ColorFor(v.Id));Ui.Place(v.Label.rectTransform,new Rect(Mathf.Clamp(rect.center.x-80,0,Ui.Width-160),Mathf.Max(0,rect.y-36),160,22));Ui.Show(v.Label,true);
 }
}
