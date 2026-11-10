using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PvZCoop;

// Replace the native illustration itself. Keep its original RectTransform,
// click target, navigation, animation and layout at every canvas resolution.
public static class MenuArtwork
{
    static Texture2D atlas;
    static Texture2D highlightedAtlas;
    static readonly Dictionary<string,Sprite> replacements=new(StringComparer.Ordinal);
    static readonly Dictionary<int,Rect> cells=new();
    static bool failed;

    public static void Init(Harmony harmony)
    {
        foreach(string property in new[]{"sprite","overrideSprite"})
            harmony.Patch(AccessTools.PropertySetter(typeof(Image),property),prefix:new HarmonyMethod(typeof(MenuArtwork),nameof(SpritePrefix)));
    }
    public static void SpritePrefix(ref Sprite __0){if(__0!=null)__0=Replace(__0);}

    static int Cell(string n)
    {
        if(n.StartsWith("SelectorScreen_StartAdventure_"))return 13;
        if(n.StartsWith("SelectorScreen_Adventure_"))return 0;
        if(n.StartsWith("SelectorScreen_Challenges_"))return 2;
        if(n.StartsWith("SelectorScreen_Challenge_")&&!n.Contains("Shadow"))return 1;
        if(n.StartsWith("SelectorScreen_Survival_"))return 3;
        if(n.StartsWith("SelectorScreen_Vasebreaker_")||n=="SelectorScreen_vasebreaker_highlight")return 14;
        if(n.StartsWith("SelectorScreen_Almanac"))return 4;
        if(n.StartsWith("SelectorScreen_Options"))return 5;
        if(n.StartsWith("SelectorScreen_Help"))return 6;
        if(n.StartsWith("SelectorScreen_Quit"))return 7;
        if(n=="SelectorScreen_WoodSign1")return 8;
        if(n=="SelectorScreen_WoodSign2"||n=="SelectorScreen_WoodSign2_press")return 9;
        if(n=="StartReady")return 10;
        if(n=="StartSet")return 11;
        if(n=="StartPlant")return 12;
        return -1;
    }

    static bool Load()
    {
        if(atlas!=null)return true;if(failed)return false;
        try{
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopUI.MenuUkrainian.png");
            using var data=new MemoryStream();stream.CopyTo(data);
            atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(atlas,data.ToArray(),false))throw new Exception("Menu PNG decoding failed");
            atlas.filterMode=FilterMode.Bilinear;atlas.wrapMode=TextureWrapMode.Clamp;atlas.hideFlags=HideFlags.HideAndDontSave;
            var pixels=atlas.GetPixels32().ToArray();
            for(int cell=0;cell<15;cell++){
                int ox=cell%4*atlas.width/4,oy=(3-cell/4)*atlas.height/4;
                int cw=(cell%4+1)*atlas.width/4-ox,ch=(4-cell/4)*atlas.height/4-oy;
                int minX=cw,minY=ch,maxX=-1,maxY=-1;
                for(int y=0;y<ch;y++)for(int x=0;x<cw;x++){
                    if(pixels[(oy+y)*atlas.width+ox+x].a<16)continue;
                    minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                }
                if(maxX<minX)throw new Exception("Empty menu atlas cell "+cell);
                cells[cell]=new Rect(ox+minX,oy+minY,maxX-minX+1,maxY-minY+1);
            }
            // Native hover changes the lettering to green. Use the very same
            // texture coordinates and alpha, so switching states cannot jump.
            var hover=new Color32[pixels.Length];Array.Copy(pixels,hover,pixels.Length);
            foreach(var entry in cells){
                int cell=entry.Key;var rect=entry.Value;
                bool stone=cell<4||cell==13||cell==14,caption=cell>=5&&cell<=7;
                if(!stone&&!caption&&cell!=4&&cell!=9)continue;
                var lettering=stone?LetterMask(pixels,rect):null;
                for(int y=0;y<(int)rect.height;y++)for(int x=0;x<(int)rect.width;x++){
                    int i=((int)rect.y+y)*atlas.width+(int)rect.x+x;var p=pixels[i];
                    if(p.a<8)continue;
                    float v=Math.Max(p.r,Math.Max(p.g,p.b))/255f;
                    bool glyph=false;
                    if(stone)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                        int nx=x+dx,ny=y+dy;if(nx>=0&&ny>=0&&nx<(int)rect.width&&ny<(int)rect.height&&lettering[ny*(int)rect.width+nx])glyph=true;
                    }
                    if((stone&&glyph&&v<.35f)||(caption&&v<.48f)){
                        float shade=.78f+v;
                        float blend=stone?Mathf.Clamp01((.35f-v)/.12f):1;
                        hover[i]=new Color32((byte)Mathf.Lerp(p.r,65*shade,blend),(byte)Mathf.Lerp(p.g,Math.Min(255,242*shade),blend),(byte)Mathf.Lerp(p.b,24*shade,blend),p.a);
                    }else if(cell==4||cell==9)hover[i]=new Color32((byte)Math.Min(255,p.r*1.12f),(byte)Math.Min(255,p.g*1.12f),(byte)Math.Min(255,p.b*1.12f),p.a);
                }
            }
            highlightedAtlas=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false);
            highlightedAtlas.SetPixels32(new Il2CppStructArray<Color32>(hover));highlightedAtlas.Apply();
            highlightedAtlas.filterMode=FilterMode.Bilinear;highlightedAtlas.wrapMode=TextureWrapMode.Clamp;highlightedAtlas.hideFlags=HideFlags.HideAndDontSave;
            Plugin.Logger.LogInfo("Ukrainian native menu illustrations loaded");return true;
        }catch(Exception e){failed=true;atlas=null;highlightedAtlas=null;Plugin.Logger.LogError("Menu artwork: "+e);return false;}
    }

    static bool[] LetterMask(Color32[] pixels,Rect rect)
    {
        int w=(int)rect.width,h=(int)rect.height;var dark=new bool[w*h];var mask=new bool[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            var p=pixels[((int)rect.y+y)*atlas.width+(int)rect.x+x];dark[y*w+x]=p.a>128&&Math.Max(p.r,Math.Max(p.g,p.b))<48;
        }
        var component=new List<int>();var queue=new Queue<int>();
        for(int seed=0;seed<dark.Length;seed++){
            if(!dark[seed])continue;component.Clear();queue.Enqueue(seed);dark[seed]=false;
            int minX=w,minY=h,maxX=0,maxY=0;
            while(queue.Count>0){
                int i=queue.Dequeue(),x=i%w,y=i/w;component.Add(i);
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                if(x>0&&dark[i-1]){dark[i-1]=false;queue.Enqueue(i-1);}
                if(x+1<w&&dark[i+1]){dark[i+1]=false;queue.Enqueue(i+1);}
                if(y>0&&dark[i-w]){dark[i-w]=false;queue.Enqueue(i-w);}
                if(y+1<h&&dark[i+w]){dark[i+w]=false;queue.Enqueue(i+w);}
            }
            int bw=maxX-minX+1,bh=maxY-minY+1;
            if(bh>h*.30f&&bw>w*.03f&&bw<w*.35f&&component.Count>bw*bh*.25f)foreach(int i in component)mask[i]=true;
        }
        return mask;
    }

    public static Sprite Replace(Sprite original)
    {
        if(original==null)return original;string name=original.name;
        if(name.StartsWith("UKArt/"))return original;
        int cell=Cell(name);if(cell<0)return original;
        if(replacements.TryGetValue(name,out var saved))return saved;
        if(!Load())return original;
        var pivot=original.pivot;var size=original.rect.size;
        bool highlight=name.Contains("highlight",StringComparison.OrdinalIgnoreCase)||name.EndsWith("2")||name.EndsWith("_press");
        var sprite=Sprite.Create(highlight?highlightedAtlas:atlas,cells[cell],new Vector2(pivot.x/size.x,pivot.y/size.y),original.pixelsPerUnit);
        sprite.name="UKArt/"+name;sprite.hideFlags=HideFlags.HideAndDontSave;
        replacements[name]=sprite;return sprite;
    }

    public static void Tick()
    {
        foreach(var image in UnityEngine.Object.FindObjectsOfType<Image>(true)){
            if(image==null||image.sprite==null)continue;
            var translated=Replace(image.sprite);if(translated.Pointer!=image.sprite.Pointer)image.sprite=translated;
            if(!translated.name.StartsWith("UKArt/"))continue;
            var state=image.GetComponentInParent<Selectable>();if(state==null)continue;
            var sprites=state.spriteState;bool changed=false;
            if(sprites.highlightedSprite!=null){var r=Replace(sprites.highlightedSprite);changed|=r.Pointer!=sprites.highlightedSprite.Pointer;sprites.highlightedSprite=r;}
            if(sprites.pressedSprite!=null){var r=Replace(sprites.pressedSprite);changed|=r.Pointer!=sprites.pressedSprite.Pointer;sprites.pressedSprite=r;}
            if(sprites.selectedSprite!=null){var r=Replace(sprites.selectedSprite);changed|=r.Pointer!=sprites.selectedSprite.Pointer;sprites.selectedSprite=r;}
            if(sprites.disabledSprite!=null){var r=Replace(sprites.disabledSprite);changed|=r.Pointer!=sprites.disabledSprite.Pointer;sprites.disabledSprite=r;}
            if(changed)state.spriteState=sprites;
        }
        LayoutLevelNumber();
    }

    static void LayoutLevelNumber()
    {
        foreach(var big in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true)){
            if(big.name!="BigLevel"||big.transform.parent==null||big.transform.parent.name!="AdvantureLevels")continue;
            var parent=big.transform.parent;if(parent.Find("NativeLevelSeparator")!=null)continue;
            var small=parent.Find("SmallLevel")?.GetComponent<TMP_Text>();if(small==null)continue;
            // The translated carved caption is wider than LEVEL. Move only
            // the two native dynamic digits into the tab's free right side.
            big.rectTransform.anchoredPosition+=new Vector2(24,0);
            small.rectTransform.anchoredPosition+=new Vector2(24,0);
            var go=new GameObject("NativeLevelSeparator");go.layer=big.gameObject.layer;go.AddComponent<RectTransform>();go.transform.SetParent(parent,false);
            TMP_Text dash=big.TryCast<TextMeshPro>()!=null?go.AddComponent<TextMeshPro>():go.AddComponent<TextMeshProUGUI>();
            dash.font=big.font;dash.fontSharedMaterial=big.fontSharedMaterial;dash.fontStyle=big.fontStyle;
            dash.text="-";dash.color=big.color;dash.alignment=big.alignment;dash.enableWordWrapping=false;dash.raycastTarget=false;
            dash.rectTransform.anchorMin=big.rectTransform.anchorMin;dash.rectTransform.anchorMax=big.rectTransform.anchorMax;
            dash.rectTransform.pivot=big.rectTransform.pivot;dash.rectTransform.sizeDelta=big.rectTransform.sizeDelta;
            dash.rectTransform.localRotation=big.rectTransform.localRotation;dash.rectTransform.localScale=big.rectTransform.localScale;
            var sourceRenderer=big.GetComponent<Renderer>();var targetRenderer=go.GetComponent<Renderer>();
            if(sourceRenderer!=null&&targetRenderer!=null){targetRenderer.sortingLayerID=sourceRenderer.sortingLayerID;targetRenderer.sortingOrder=sourceRenderer.sortingOrder;}
            dash.rectTransform.anchoredPosition=(big.rectTransform.anchoredPosition+small.rectTransform.anchoredPosition)*.5f;
            var position=dash.rectTransform.localPosition;position.z=big.rectTransform.localPosition.z;dash.rectTransform.localPosition=position;
            dash.fontSize=big.fontSize*.8f;
        }
    }
}
