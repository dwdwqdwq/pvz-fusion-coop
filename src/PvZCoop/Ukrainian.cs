using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PvZCoop;

// Display-only localization: never changes level data, card identities, saves or network messages.
public static class Ukrainian
{
    static Dictionary<string,string> words=new(StringComparer.Ordinal);
    static readonly Dictionary<string,string> cache=new(StringComparer.Ordinal);
    static readonly List<(Regex regex,string target)> patterns=new();
    static readonly Dictionary<string,List<KeyValuePair<string,string>>> fragments=new(StringComparer.Ordinal);
    static Dictionary<int,string> plantNames=new();
    
    static readonly HashSet<IntPtr> inputLabels=new();
    static readonly HashSet<IntPtr> fallbackFonts=new();
    static readonly Regex placeholders=new(@"\{(\d+|[A-Za-z_][A-Za-z0-9_.]*)(?:[^{}]*)\}");
    static readonly Regex letters=new("[A-Za-zА-Яа-яІіЇїЄєҐґ]");
    static readonly Regex cyrillic=new("[А-Яа-яІіЇїЄєҐґ]");
    static TMP_FontAsset ukrainianFont;
    static Font systemFont;
    static float nextScan;
    static bool initializing,warning,almanacOpen,rendering;
    public static int Count=>words.Count;

    public static void Init(Harmony harmony)
    {
        if(!LanguageSelection.UkrainianEnabled)return;
        using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopUI.uk.json");
        words=JsonSerializer.Deserialize<Dictionary<string,string>>(resource)??new();
        using(var names=Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopUI.plant-names.json"))plantNames=JsonSerializer.Deserialize<Dictionary<int,string>>(names)??new();
        foreach(var p in words.Where(p=>p.Key.Length>=8&&p.Key!=p.Value&&!placeholders.IsMatch(p.Key))){
            string prefixKey=p.Key[..8];if(!fragments.TryGetValue(prefixKey,out var list)){list=new();fragments[prefixKey]=list;}list.Add(p);
        }
        foreach(var list in fragments.Values)list.Sort((a,b)=>b.Key.Length.CompareTo(a.Key.Length));
        foreach(var p in words.Where(p=>placeholders.IsMatch(p.Key)&&Regex.IsMatch(placeholders.Replace(p.Key,""),"[A-Za-zА-Яа-яІіЇїЄєҐґ]{2,}")).OrderByDescending(p=>p.Key.Length)){
            string expression="";int offset=0;
            foreach(Match m in placeholders.Matches(p.Key)){
                string capture=m.Groups[1].Value is "number" or "LevelNumber"?@"\d+":@"[^\r\n<>]{1,160}?";
                expression+=Regex.Escape(p.Key.Substring(offset,m.Index-offset))+"(?<"+CaptureName(m.Groups[1].Value)+">"+capture+")";
                offset=m.Index+m.Length;
            }
            expression+=Regex.Escape(p.Key[offset..]);
            try{patterns.Add((new Regex("^"+expression+"$",RegexOptions.CultureInvariant),p.Value));}catch{}
        }
        var prefix=new HarmonyMethod(typeof(Ukrainian),nameof(TextPrefix));
        harmony.Patch(AccessTools.PropertySetter(typeof(TMP_Text),"text"),prefix:prefix);
        foreach(var m in typeof(TMP_Text).GetMethods().Where(m=>m.Name=="SetText"&&m.GetParameters().Length>0&&m.GetParameters()[0].ParameterType==typeof(string)))
            harmony.Patch(m,prefix:prefix);
        harmony.Patch(AccessTools.PropertySetter(typeof(Text),"text"),prefix:new HarmonyMethod(typeof(Ukrainian),nameof(LegacyPrefix)));
        harmony.Patch(AccessTools.Method(typeof(AlmanacPlantMenu),"PerformSearch"),prefix:new HarmonyMethod(typeof(Ukrainian),nameof(SearchPrefix)));
        foreach(var type in new[]{typeof(TextMeshProUGUI),typeof(TextMeshPro)}) {
            var method=AccessTools.Method(type,type==typeof(TextMeshProUGUI)?"OnPreRenderCanvas":"OnPreRenderObject");
            if(method!=null)harmony.Patch(method,prefix:new HarmonyMethod(typeof(Ukrainian),nameof(BeforeRender)));
        }
        harmony.Patch(AccessTools.Method(typeof(Text),"OnPopulateMesh",new[]{typeof(VertexHelper)}),prefix:new HarmonyMethod(typeof(Ukrainian),nameof(BeforeLegacyRender)));
        Plugin.Logger.LogInfo("Ukrainian display localization: "+words.Count+" entries");
    }

    public static string Translate(string value)
        =>LanguageSelection.UkrainianEnabled?TranslateCore(value,true):value;
    static string TranslateCore(string value,bool replaceFragments)
    {
        if(string.IsNullOrEmpty(value)||!letters.IsMatch(value))return value;
        if(words.TryGetValue(value,out var exact))return exact;
        if(cache.TryGetValue(value,out var saved))return saved;
        string trimmed=value.Trim();
        if(words.TryGetValue(trimmed,out exact))return value[..(value.Length-value.TrimStart().Length)]+exact+value[(value.TrimEnd().Length)..];
        foreach(var p in patterns){
            var match=p.regex.Match(value);if(!match.Success)continue;
            string target=placeholders.Replace(p.target,m=>CaptureValue(match,m.Groups[1].Value));
            return Remember(value,target);
        }
        if(replaceFragments){string replaced=ReplaceFragments(value);if(replaced!=value)return Remember(value,TranslateCore(replaced,false));}
        if(value.Contains('\n'))return Remember(value,string.Join("\n",value.Split('\n').Select(Translate)));
        if(value.Contains('<')&&value.Contains('>')){
            var chunks=Regex.Split(value,"(<[^>]+>)");
            return Remember(value,string.Concat(chunks.Select(s=>s.StartsWith("<")&&s.EndsWith(">")?s:Translate(s))));
        }
        // Native HUDs often concatenate labels and numbers instead of using a format string.
        string result=Regex.Replace(value,@"[A-Za-z][A-Za-z '\-]*",m=>{
            string s=m.Value,core=s.Trim();
            if(words.TryGetValue(core,out string t))return s[..(s.Length-s.TrimStart().Length)]+t+s[s.TrimEnd().Length..];
            return s;
        });
        return Remember(value,result);
    }
    static string ReplaceFragments(string value)
    {
        var result=new StringBuilder(value.Length);bool changed=false;
        for(int i=0;i<value.Length;){
            bool found=false;
            if(i+8<=value.Length&&value[i]<128&&fragments.TryGetValue(value.Substring(i,8),out var candidates))foreach(var p in candidates){
                if(i+p.Key.Length>value.Length||string.CompareOrdinal(value,i,p.Key,0,p.Key.Length)!=0)continue;
                if(char.IsLetter(p.Key[0])&&i>0&&char.IsLetter(value[i-1]))continue;
                if(char.IsLetter(p.Key[^1])&&i+p.Key.Length<value.Length&&char.IsLetter(value[i+p.Key.Length]))continue;
                result.Append(p.Value);i+=p.Key.Length;changed=found=true;break;
            }
            if(!found){
                if(value[i]=='<'){int end=value.IndexOf('>',i);if(end>=0){result.Append(value,i,end-i+1);i=end+1;continue;}}
                result.Append(value[i++]);
            }
        }
        return changed?result.ToString():value;
    }
    static string Remember(string key,string value){if(cache.Count>8192)cache.Clear();cache[key]=value;return value;}
    static string CaptureName(string name)=>"v"+name.Replace('.','_');
    static string CaptureValue(Match match,string name){var value=match.Groups[CaptureName(name)].Value;if(name is "type" or "Level" or "mode" or "sceneType"){if(value=="Advanture")return "Пригоди";if(words.TryGetValue(value,out var label))return label;}return value;}
    public static string CardsHint(int count){if(!LanguageSelection.UkrainianEnabled)return count+" cards — hover to view";int n=count%100;string suffix=n is >=11 and <=14?"карток":count%10==1?"картка":count%10 is >=2 and <=4?"картки":"карток";return count+" "+suffix+" — наведіть курсор";}
    public static bool SearchPrefix(AlmanacPlantMenu __instance,string __0)
    {
        if(string.IsNullOrWhiteSpace(__0)||!cyrillic.IsMatch(__0))return true;
        var matches=new HashSet<int>(plantNames.Where(p=>Regex.Replace(Translate(p.Value),"<[^>]+>","").IndexOf(__0.Trim(),StringComparison.OrdinalIgnoreCase)>=0).Select(p=>p.Key));
        __instance.ShowPlants((Il2CppSystem.Func<PlantType,bool>)(new Func<PlantType,bool>(type=>matches.Contains((int)type))));
        return false;
    }
    public static void MarkInput(TMP_Text text){inputLabels.Add(text.Pointer);FontFor(text);}
    static bool IsInput(TMP_Text text){if(inputLabels.Contains(text.Pointer))return true;var input=text.GetComponentInParent<TMP_InputField>();return input!=null&&input.textComponent!=null&&input.textComponent.Pointer==text.Pointer;}
    public static void TextPrefix(TMP_Text __instance,ref string __0){if(NativeHealthCaption.Matches(__0))return;if(IsInput(__instance)){if(cyrillic.IsMatch(__0??""))FontFor(__instance);return;}string before=__0;__0=CompactCaption(__instance,Translate(__0));if(cyrillic.IsMatch(__0??"")){FontFor(__instance);if(before!=__0)FitLabel(__instance,__0);}}
    public static void LegacyPrefix(Text __instance,ref string __0){var input=__instance.GetComponentInParent<InputField>();if(input!=null&&input.textComponent!=null&&input.textComponent.Pointer==__instance.Pointer)return;__0=Translate(__0);if(cyrillic.IsMatch(__0??"")&&EnsureFont())__instance.font=systemFont;}
    public static void BeforeRender(TMP_Text __instance) {
        if(rendering || !LanguageSelection.UkrainianEnabled || __instance==null || IsInput(__instance))return;
        rendering=true;
        try {
            string original=__instance.text;
            if(NativeHealthCaption.Matches(original))return;
            string translated=CompactCaption(__instance,Translate(original));
            if(cyrillic.IsMatch(translated??""))FontFor(__instance);
            if(translated!=original){FitLabel(__instance,translated);__instance.text=translated;}
        } finally {rendering=false;}
    }
    public static void BeforeLegacyRender(Text __instance) {
        if(rendering || !LanguageSelection.UkrainianEnabled || __instance==null)return;
        rendering=true;
        try {string text=__instance.text;LegacyPrefix(__instance,ref text);if(text!=__instance.text)__instance.text=text;} finally {rendering=false;}
    }
    static bool EnsureFont()
    {
        if(ukrainianFont!=null)return true;if(initializing)return false;initializing=true;
        try{
            string fontPath=Path.Combine(BepInEx.Paths.GameRootPath,"BepInEx","cache","PvZUkrainian-0.10.1.ttf");
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopUI.UkrainianFont.ttf")){
                using var bytes=new MemoryStream();resource.CopyTo(bytes);byte[] data=bytes.ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(fontPath));
                if(!File.Exists(fontPath)||!File.ReadAllBytes(fontPath).SequenceEqual(data))File.WriteAllBytes(fontPath,data);
            }
            systemFont=new Font(Il2CppInterop.Runtime.IL2CPP.il2cpp_object_new(Il2CppInterop.Runtime.Il2CppClassPointerStore<Font>.NativeClassPtr));
            Font.Internal_CreateFontFromPath(systemFont,fontPath);
            ukrainianFont=TMP_FontAsset.CreateFontAsset(systemFont,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            if(systemFont==null||ukrainianFont==null)throw new Exception("Could not create the original-style Ukrainian font atlas.");
            systemFont.hideFlags=HideFlags.HideAndDontSave;ukrainianFont.hideFlags=HideFlags.HideAndDontSave;
            if(!ukrainianFont.TryAddCharacters("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz !?:.,+-/()%[]АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯабвгґдеєжзиіїйклмнопрстуфхцчшщьюя’—…",out string missing,false))
                Plugin.Logger.LogWarning("Missing Ukrainian font characters: "+missing);
            Plugin.Logger.LogInfo("Original-style Ukrainian font atlas loaded");
            return true;
        }catch(Exception e){if(!warning){warning=true;Plugin.Logger.LogError("Ukrainian font: "+e);}return false;}
        finally{initializing=false;}
    }
    public static void FontFor(TMP_Text text)
    {
        if(text==null||!EnsureFont())return;var font=text.font;
        if(font!=null&&font.Pointer!=ukrainianFont.Pointer&&fallbackFonts.Add(font.Pointer)){
            if(ukrainianFont.fallbackFontAssetTable==null)ukrainianFont.fallbackFontAssetTable=new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            ukrainianFont.fallbackFontAssetTable.Add(font);
        }
        if(font==null || font.Pointer!=ukrainianFont.Pointer){
            float outline=text.outlineWidth;var outlineColor=text.outlineColor;
            text.font=ukrainianFont;
            // An existing outline material can still reference the old atlas.
            text.fontSharedMaterial=ukrainianFont.material;
            text.outlineWidth=outline;text.outlineColor=outlineColor;
            text.havePropertiesChanged=true;
        }
        text.characterSpacing=0;
    }
    static void FitLabel(TMP_Text text,string value){
        if(text.rectTransform==null||(value.Length>90&&(value.Contains('\n')||text.rectTransform.rect.height>80)))return;
        float size=text.enableAutoSizing?text.fontSizeMax:text.fontSize;if(size<1)return;
        text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=Mathf.Min(size,Mathf.Min(text.fontSizeMin>0?text.fontSizeMin:size,Mathf.Max(.05f,size*.4f)));
        if(value.Length<=90&&value.Contains('\n'))text.enableWordWrapping=false;
    }
    static string CompactCaption(TMP_Text text,string value){
        if(string.IsNullOrEmpty(value)||value.Length>500||!almanacOpen||text.name!="TextShadow")return value;
        string plain=Regex.Replace(Regex.Replace(value,"<[^>]+>",""),@"\s+"," ").Trim();
        if(plain.Length>80)return value;
        string compact=null;
        if(plain=="Рослини Одіссеї")compact="Одіссея";
        else if(plain=="Рослини оборони")compact="Оборона";
        else if(plain.IndexOf("оді",StringComparison.OrdinalIgnoreCase)>=0&&plain.Contains('.'))compact=plain.StartsWith("Епічна")?"Епічна":"Одіссея та SP";
        else if(plain.EndsWith(" рослини",StringComparison.OrdinalIgnoreCase))compact=plain[..^8];
        if(compact==null)return value;
        bool emitted=false;
        return Regex.Replace(value,@"(?<=>)[^<]+|^[^<]+",m=>{if(string.IsNullOrWhiteSpace(m.Value))return m.Value;if(emitted)return "";emitted=true;return compact;});
    }
    public static void Tick()
    {
        if(!LanguageSelection.UkrainianEnabled)return;
        if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;
        almanacOpen=UnityEngine.Object.FindObjectOfType<AlmanacPlantMenu>()!=null;
        MenuArtwork.Tick();
    }

}
