using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace PvZCoop;

// The real Fusion isExtra flag identifies the expensive copy; price is not identity.
public static class DeckSharing
{
    static readonly HashSet<int> peerSlots=new();
    static readonly HashSet<int> hostSlots=new();
    static readonly Dictionary<IntPtr,(CardUI card,bool disabled)> tinted=new();
    static string signature="";
    static int sentRevision,ackRevision,peerRevision;
    static float nextRefresh;
    static bool applying;
    public static int Key(CardUI card)=>checked(card.theSeedType*2+(card.isExtra?1:0));
    public static bool Active=>Session.Active&&!Session.Started&&Party.PartnerPresent;
    public static bool Ready=>!Active||Party.IsHost||sentRevision==ackRevision;
    public static void Init(){Party.OnMessage+=Receive;}
    public static void Reset(){Restore();peerSlots.Clear();hostSlots.Clear();signature="";sentRevision=ackRevision=peerRevision=0;nextRefresh=0;applying=false;}
    static List<CardUI> Cards(){var result=new List<CardUI>();var ui=InGameUI.Instance;if(ui?.Cards!=null)foreach(var c in ui.Cards)if(c!=null&&!c.WasCollected&&c.theSeedType>=0)result.Add(c);return result;}
    static HashSet<int> Mine()=>new(Cards().Select(Key));
    static string Encode(IEnumerable<int> keys)=>string.Join(",",keys.OrderBy(x=>x));
    static HashSet<int> Decode(string value){var r=new HashSet<int>();if(value.Length==0)return r;var parts=value.Split(',');if(parts.Length>128)throw new FormatException("Too many deck slots");foreach(var p in parts){if(!int.TryParse(p,out int n)||n<0)throw new FormatException("Invalid deck slot");r.Add(n);}return r;}
    public static bool BeforeAdd(CardUI card)
    {
        if(!Active||applying||card==null||card.theSeedType<0)return true;
        int key=Key(card);
        if(Cards().Any(c=>c.Pointer==card.Pointer))return true;
        if(peerSlots.Contains(key)||Mine().Contains(key)){Session.ShowToast("That card is already selected. Try its extra copy if available.",2);return false;}
        return true;
    }
    public static bool KeepNativeSibling(CardUI card)=>!Active||card==null||!peerSlots.Contains(card.theSeedType*2+(card.isExtra?0:1));
    public static void Changed(){if(Active&&!applying)RefreshNow();}
    public static void RefreshNow()
    {
        if(!Active)return;
        var mine=Mine();string current=Encode(mine);if(current==signature)return;
        signature=current;Session.SelectionChanged();
        if(Party.IsHost){hostSlots.Clear();hostSlots.UnionWith(mine);if(peerSlots.RemoveWhere(hostSlots.Contains)>0)Session.PartnerReady=false;SendSnapshot();}
        else {sentRevision++;Party.Send("deck_offer",sentRevision+"|"+current);}
    }
    static void SendSnapshot(){Party.Send("deck_slots",peerRevision+"|"+Encode(hostSlots)+"|"+Encode(peerSlots));}
    static void Receive(string type,string payload)
    {
        if(!Active)return;
        if(type=="deck_offer"&&Party.IsHost){var p=payload.Split('|');if(p.Length!=2||!int.TryParse(p[0],out int revision)||revision<peerRevision)return;RefreshNow();peerRevision=revision;peerSlots.Clear();peerSlots.UnionWith(Decode(p[1]));peerSlots.ExceptWith(hostSlots);Session.PartnerReady=false;SendSnapshot();}
        else if(type=="deck_slots"&&!Party.IsHost){var p=payload.Split('|');if(p.Length!=3||!int.TryParse(p[0],out int revision)||revision<ackRevision)return;peerSlots.Clear();peerSlots.UnionWith(Decode(p[1]));var allowed=Decode(p[2]);ackRevision=revision;
            if(revision>0&&revision==sentRevision){bool removed=false;applying=true;try{foreach(var c in Cards())if(!allowed.Contains(Key(c))){InGameUI.Instance.RemoveCardFromBank(c,true);removed=true;}}finally{applying=false;}var remaining=Mine();signature=Encode(remaining);if(!remaining.SetEquals(allowed)){signature="!";RefreshNow();}if(removed){Session.SelectionChanged();Session.ShowToast("Teammate selected that card first. Its other copy is a separate slot.",3);}}
        }
    }
    public static bool CanStart(){RefreshNow();return Ready&&!Mine().Overlaps(peerSlots);}
    public static void Tick()
    {
        if(!Active){Restore();return;}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;RefreshNow();
        var lib=SeedLibrary.Instance;if(lib==null)return;
        foreach(var c in lib.GetComponentsInChildren<CardUI>(true)){
            if(c==null||c.WasCollected||c.theSeedType<0)continue;
            bool blocked=peerSlots.Contains(Key(c));
            if(blocked){if(!tinted.ContainsKey(c.Pointer))tinted[c.Pointer]=(c,c.disabled);c.disabled=true;}
            else if(tinted.TryGetValue(c.Pointer,out var old)){c.disabled=old.disabled;tinted.Remove(c.Pointer);}
        }
    }
    static void Restore(){foreach(var item in tinted.Values)if(item.card!=null&&!item.card.WasCollected)item.card.disabled=item.disabled;tinted.Clear();}
}
