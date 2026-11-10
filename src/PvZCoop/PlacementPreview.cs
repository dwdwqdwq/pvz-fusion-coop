using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace PvZCoop;

// Visual placement intent only. No plants, colliders or game actions are created.
public static class PlacementPreview
{
    static Image tile, icon;
    static RectTransform friendArt;
    static int friendArtType=int.MinValue;
    static Image[] friendArrows, ownArrows;
    static TextMeshProUGUI label;
    static float nextSend,lastSend,expires,nextArtLookup;
    static string last="0",spriteName="";
    static int column,row,plant;
    static bool remote,exact;
    static Vector2 worldCenter,worldSize;
    static Sprite cachedArt;
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public static void Init(){Party.OnMessage+=Receive;}
    public static void Reset(){remote=false;last="0";expires=nextSend=lastSend=0;HideRemote();HideArrows(ownArrows);if(friendArt!=null){UnityEngine.Object.Destroy(friendArt.gameObject);friendArt=null;}friendArtType=int.MinValue;}
    static void HideArrows(Image[] list){if(list!=null)foreach(var a in list)Ui.Show(a,false);}
    static void HideRemote(){HideArrows(friendArrows);Ui.Show(icon,false);Ui.Show(label,false);Ui.Show(friendArt,false);}
    static void Receive(string type,string payload)
    {
        if(type=="sp"){remote=false;return;}
        if(type!="preview"||!Sync.Running)return;
        if(payload=="0"){remote=false;return;}
        var p=payload.Split('|');
        if((p.Length!=4&&p.Length!=8)||!int.TryParse(p[0],out int c)||!int.TryParse(p[1],out int r)||!int.TryParse(p[2],out int t)||c<0||c>63||r<0||r>31||p[3].Length>1024)return;
        string name;try{name=Encoding.UTF8.GetString(Convert.FromBase64String(p[3]));}catch(FormatException){return;}
        if(plant!=t||spriteName!=name){cachedArt=null;nextArtLookup=0;}
        exact=false;
        if(p.Length==8&&float.TryParse(p[4],NumberStyles.Float,Inv,out float x)&&float.TryParse(p[5],NumberStyles.Float,Inv,out float y)&&float.TryParse(p[6],NumberStyles.Float,Inv,out float w)&&float.TryParse(p[7],NumberStyles.Float,Inv,out float h)&&float.IsFinite(x)&&float.IsFinite(y)&&w>0&&h>0&&w<20&&h<20){exact=true;worldCenter=new Vector2(x,y);worldSize=new Vector2(w,h);}
        column=c;row=r;plant=t;spriteName=name;remote=true;expires=Time.unscaledTime+1.5f;
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
    public static void Tick()
    {
        if(!Sync.Running||Ui.Root==null){Reset();return;}
        if(ownArrows==null)ownArrows=MakeArrows();
        if(ReadVisual(out _,out Bounds own)&&Project(own.center,own.size,out Rect ownRect))DrawArrows(ownArrows,ownRect,Color.white);else HideArrows(ownArrows);
        if(Time.unscaledTime>=nextSend){nextSend=Time.unscaledTime+.08f;string state=ReadLocal();if(state!=last||(state!="0"&&Time.unscaledTime-lastSend>=.4f)){Party.SendFast("preview",state,false);last=state;lastSend=Time.unscaledTime;}}
        if(!remote||Time.unscaledTime>expires||Mouse.Instance==null){HideRemote();return;}
        Vector2 center=worldCenter,size=worldSize;
        if(!exact){float x=Mouse.Instance.GetBoxXFromColumn(column);center=new Vector2(x,Mouse.Instance.GetLandY(x,row));size=Vector2.one;}
        if(!Project(center,size,out Rect rect)){HideRemote();return;}
        if(friendArrows==null){friendArrows=MakeArrows();tile=friendArrows[0];icon=Ui.Img(Ui.Root,new Rect(0,0,1,1),Ui.White,new Color(1,1,1,.55f));icon.preserveAspect=true;icon.useSpriteMesh=true;label=Ui.Text(Ui.Root,new Rect(0,0,160,22),"TEAMMATE",15,new Color(.75f,1,.55f));}
        if(friendArtType!=plant){if(friendArt!=null){friendArt.gameObject.SetActive(false);UnityEngine.Object.Destroy(friendArt.gameObject);}friendArt=PartnerBank.MakePlantPreview(plant,Ui.Root);friendArtType=plant;}
        if(friendArt!=null){
            float factor=Mathf.Min(rect.width/Mathf.Max(1,friendArt.rect.width),rect.height/Mathf.Max(1,friendArt.rect.height));
            friendArt.localScale=new Vector3(factor,factor,1);
            friendArt.anchoredPosition=new Vector2(rect.center.x-friendArt.rect.width*factor/2,-rect.center.y+friendArt.rect.height*factor/2);
            Ui.Show(friendArt,true);Ui.Show(icon,false);
        }else{
            if((cachedArt==null||cachedArt.WasCollected)&&Time.unscaledTime>=nextArtLookup){nextArtLookup=Time.unscaledTime+2;cachedArt=spriteName.Length>0?ResolveArt(spriteName):null;if(cachedArt==null)cachedArt=Ui.Sprite(((PlantType)plant).ToString());}
            icon.sprite=cachedArt;Ui.Place(icon.rectTransform,rect);Ui.Show(icon,cachedArt!=null);
        }
        DrawArrows(friendArrows,rect,new Color(.6f,1f,.55f,1));
        Ui.Place(label.rectTransform,new Rect(Mathf.Clamp(rect.center.x-80,0,Ui.Width-160),Mathf.Max(0,rect.y-36),160,22));Ui.Show(label,true);
    }
}
