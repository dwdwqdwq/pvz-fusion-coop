using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace PvZCoop;

// A language change applies on the next launch. This keeps native prefabs,
// text, material/font state and sprite animations consistent for the session.
public static class LanguageSelection
{
    static ConfigEntry<string> preference;
    public static string Active { get; private set; }="en";
    public static string Selected=>LanguageChoice.Normalize(preference?.Value??Active);
    public static bool UkrainianEnabled=>Active=="uk";
    public static bool RestartRequired=>Selected!=Active;
    public static bool Open;
    static Btn menu,english,ukrainian;
    static RectTransform panel;
    static TMP_Text status,selected;

    public static void Init(ConfigFile config)
    {
        preference=config.Bind("Language","Interface","en",new ConfigDescription("Interface language: en = original English, uk = Ukrainian. Changes apply after restarting the game.",new AcceptableValueList<string>("en","uk")));
        Active=LanguageChoice.Normalize(preference.Value);
        Plugin.Logger.LogInfo("Interface language: "+Active);
    }

    public static void Select(string code)
    {
        if(!LanguageChoice.IsSupported(code))throw new ArgumentException("Supported interface languages: en, uk",nameof(code));
        preference.Value=code;preference.ConfigFile.Save();
        Plugin.Logger.LogInfo("Selected language: "+code+"; active until restart: "+Active);
    }

    static void Build()
    {
        menu=Ui.Button(Ui.Root,new Rect(0,10,300,58),"LANGUAGE",()=>Open=!Open,fontSize:24);
        panel=Ui.Dialog(Ui.Root,new Rect(0,84,430,350));panel.name="LanguageSettings";
        Ui.Hotspot(Ui.Text(panel,new Rect(363,10,55,55),"X",40,Ui.Red).rectTransform,()=>Open=false);
        var title=Ui.Text(panel,new Rect(25,24,340,40),"Language / Мова",29,Ui.Gold);
        Ukrainian.FontFor(title);
        english=Ui.Button(panel,new Rect(38,88,354,60),"English",()=>Select("en"),fontSize:27);
        ukrainian=Ui.Button(panel,new Rect(38,160,354,60),"Українська",()=>Select("uk"),fontSize:27);
        Ukrainian.FontFor(ukrainian.Label);
        selected=Ui.Text(panel,new Rect(28,232,374,36),"",21,Ui.Pale);Ukrainian.FontFor(selected);
        status=Ui.Text(panel,new Rect(28,272,374,52),"",20,Ui.Gold);Ukrainian.FontFor(status);
    }

    public static void Tick(MainMenu native,bool onMain)
    {
        if(!Screens.Built)return;
        if(menu==null)Build();
        bool available=onMain&&!Screens.PanelOpen;
        if(available)foreach(var sub in UnityEngine.Object.FindObjectsOfType<BaseMenu>())
            if(sub.isActiveAndEnabled&&native!=null&&sub.Pointer!=native.Pointer){available=false;break;}
        if(!available)Open=false;
        Ui.Place(menu.Rt,new Rect(Mathf.Max(8,Ui.Width-318),10,300,58));
        Ui.Place(panel,new Rect(Mathf.Max(8,Ui.Width-448),84,430,350));
        Ui.Show(menu.Rt,available&&!Open);Ui.Show(panel,available&&Open);
        menu.Text=RestartRequired?"LANGUAGE *":"LANGUAGE";
        english.SetColor(Selected=="en"?Ui.Gold:Ui.Green);
        ukrainian.SetColor(Selected=="uk"?Ui.Gold:Ui.Green);
        selected.text=Selected=="uk"?"Selected: Українська":"Selected: English";
        status.text=RestartRequired?"Restart the game to apply the selected language.":"Choose your interface language.";
    }
}
