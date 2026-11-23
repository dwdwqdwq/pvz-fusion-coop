using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace PvZCoop;
public static class DeckSharing
{
 static readonly HashSet<int> peerSlots=new();
 static readonly DeckReservations ledger=new();
 static readonly Dictionary<IntPtr,(CardUI card,bool disabled)> tinted=new();
 static readonly Dictionary<IntPtr,(CardUI card,Transform parent,int order,bool active)> visual=new();
 static string signature="!";static int sentRevision,ackRevision;static float nextRefresh;static bool applying;
 public static int Key(CardUI card)=>checked(card.theSeedType*2+(card.isExtra?1:0));
 public static bool Active=>Session.Active&&!Session.Started&&Party.PartnerPresent;
 public static bool Ready=>!Active||Party.IsHost||sentRevision==ackRevision;
 public static void Init(){Party.OnMessage+=Receive;Party.RosterChanged+=()=>{ledger.Prune(Party.Members);};}
 public static void Reset(){Restore();peerSlots.Clear();ledger.Clear();signature="!";sentRevision=ackRevision=0;nextRefresh=0;applying=false;}
    static List<CardUI> Cards(){var result=new List<CardUI>();var ui=InGameUI.Instance;if(ui!=null&&!ui.WasCollected&&ui.Cards!=null)foreach(var c in ui.Cards)if(c!=null&&!c.WasCollected&&c.theSeedType>=0)result.Add(c);return result;}
 static HashSet<int> Mine()=>new(Cards().Select(Key));
 static string Encode(IEnumerable<int> keys)=>string.Join(",",keys.OrderBy(x=>x));
 static HashSet<int> Decode(string value){var r=new HashSet<int>();if(value.Length==0)return r;var parts=value.Split(',');if(parts.Length>128)throw new FormatException("Too many deck slots");foreach(var p in parts){if(!int.TryParse(p,out int n)||n<0)throw new FormatException("Invalid deck slot");r.Add(n);}return r;}
    public static bool BeforeClick(CardUI card){
        if(!Active||card==null||card.theSeedType<0)return true;
        bool selected=Mine().Contains(Key(card));
        card.isSelected=selected;
        if(!selected&&peerSlots.Contains(Key(card))){Session.ShowToast("That card is already selected. Try its extra copy if available.",2);return false;}
        return true;
    }
    public static void AfterClick(CardUI card){
        if(Active&&card!=null&&card.theSeedType>=0)card.isSelected=Mine().Contains(Key(card));
    }
 public static bool BeforeAdd(CardUI card){if(!Active||applying||card==null||card.theSeedType<0)return true;if(Cards().Any(c=>c.Pointer==card.Pointer))return true;if(peerSlots.Contains(Key(card))||Mine().Contains(Key(card))){Session.ShowToast("That card is already selected. Try its extra copy if available.",2);return false;}return true;}
 public static bool KeepNativeSibling(CardUI card)=>!Active||card==null||(!peerSlots.Contains(Key(card)^1)&&!Mine().Contains(Key(card)^1));
 public static void Changed(){if(Active&&!applying)RefreshNow();}
 static void ApplyAllowed(HashSet<int> allowed){bool removed=false;applying=true;try{foreach(var c in Cards())if(!allowed.Contains(Key(c))){InGameUI.Instance.RemoveCardFromBank(c,true);removed=true;}}finally{applying=false;}signature=Encode(Mine());if(removed){Session.SelectionChanged();Session.ShowToast("Teammate selected that card first. Its other copy is a separate slot.",3);}}
 public static void RefreshNow(){if(!Active)return;string current=Encode(Mine());if(current==signature)return;signature=current;Session.SelectionChanged();sentRevision++;if(Party.IsHost){ledger.Offer(1,sentRevision,Mine());ApplyAllowed(ledger.Slots[1]);peerSlots.Clear();peerSlots.UnionWith(ledger.Others(1));SendSnapshots();}else Party.Send("deck_offer",sentRevision+"|"+current);}
 static void SendSnapshots(){foreach(int id in Party.RemotePlayers){ledger.Revisions.TryGetValue(id,out int rev);Party.SendTo(id,"deck_slots",rev+"|"+Encode(ledger.Others(id))+"|"+(ledger.Slots.TryGetValue(id,out var mine)?Encode(mine):""));}}
 static void Receive(string type,string payload){if(!Active)return;
  if(type=="deck_offer"&&Party.IsHost){var p=payload.Split('|');if(p.Length!=2||!int.TryParse(p[0],out int revision))return;int id=Party.SenderId;if(ledger.Revisions.TryGetValue(id,out int old)&&revision<old)return;RefreshNow();ledger.Offer(id,revision,Decode(p[1]));TeamState.For(id).Ready=false;peerSlots.Clear();peerSlots.UnionWith(ledger.Others(1));SendSnapshots();}
  else if(type=="deck_slots"&&!Party.IsHost&&Party.SenderId==1){var p=payload.Split('|');if(p.Length!=3||!int.TryParse(p[0],out int revision)||revision<ackRevision)return;peerSlots.Clear();peerSlots.UnionWith(Decode(p[1]));ackRevision=revision;if(revision>0&&revision==sentRevision){ApplyAllowed(Decode(p[2]));if(signature!=Encode(Decode(p[2]))){signature="!";RefreshNow();}}}
 }
 public static bool CanStart(){RefreshNow();if(!Ready||Mine().Overlaps(peerSlots))return false;if(Party.IsHost)foreach(int id in Party.RemotePlayers)if(!ledger.Slots.TryGetValue(id,out var slots)||!slots.SetEquals(TeamState.For(id).Slots))return false;return true;}
    public static void Tick()
    {
        if(!Active){Restore();return;}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.15f;RefreshNow();
        var lib=SeedLibrary.Instance;if(lib==null)return;
        var library=lib.GetComponentsInChildren<CardUI>(true).Where(c=>c!=null&&!c.WasCollected&&c.theSeedType>=0).ToArray();
        var selectedCards=Cards();var mine=selectedCards.Select(c=>c.Pointer).ToHashSet();var mineKeys=selectedCards.Select(Key).ToHashSet();
        var libraryKeys=library.Select(Key).ToHashSet();
        foreach(var c in library){
            if(c==null||c.WasCollected||c.theSeedType<0)continue;
            int key=Key(c);bool blocked=peerSlots.Contains(key);
            bool opposite=peerSlots.Contains(key^1)||mineKeys.Contains(key^1);
            // A teammate's base card satisfies the native sibling requirement.
            // Override only the exact slot, never disable both copies by plant type.
            if(blocked||opposite){
                if(!tinted.ContainsKey(c.Pointer))tinted[c.Pointer]=(c,c.disabled);c.disabled=blocked;
                if(!visual.ContainsKey(c.Pointer))visual[c.Pointer]=(c,c.transform.parent,c.transform.GetSiblingIndex(),c.gameObject.activeSelf);
                c.isSelected=mineKeys.Contains(key);
                // Fusion stacks the native base/extra packets inside one holder.
                // Expose the available native packet when its sibling is remote.
                bool sibling=libraryKeys.Contains(key^1);
                if(sibling && blocked!=opposite && !mineKeys.Contains(key)){
                    c.gameObject.SetActive(!blocked);
                    if(!blocked)c.transform.SetAsLastSibling();
                }
            }
            else {
                if(tinted.TryGetValue(c.Pointer,out var old)){c.disabled=old.disabled;tinted.Remove(c.Pointer);}
                RestoreVisual(c.Pointer,mine,mineKeys);
            }
        }
    }
    static void RestoreVisual(IntPtr pointer,HashSet<IntPtr> mine,HashSet<int> mineKeys){
        if(!visual.TryGetValue(pointer,out var item))return;
        var card=item.card;
        if(card!=null&&!card.WasCollected){
            var parent=card.transform.parent;
            if(!mine.Contains(pointer)&&item.parent!=null&&parent!=null&&parent.Pointer==item.parent.Pointer){card.gameObject.SetActive(item.active);card.transform.SetSiblingIndex(item.order);}
            card.isSelected=mineKeys.Contains(Key(card));
        }
        visual.Remove(pointer);
    }
    static void Restore(){
        if(tinted.Count==0&&visual.Count==0)return;
        var selectedCards=Cards();var mine=selectedCards.Select(c=>c.Pointer).ToHashSet();var mineKeys=selectedCards.Select(Key).ToHashSet();
        foreach(var item in tinted.Values)if(item.card!=null&&!item.card.WasCollected){
            int key=Key(item.card);
            item.card.disabled=Session.Started&&mine.Contains(item.card.Pointer)&&peerSlots.Contains(key^1)&&!peerSlots.Contains(key)?false:item.disabled;
        }
        tinted.Clear();
        foreach(var pointer in visual.Keys.ToArray())RestoreVisual(pointer,mine,mineKeys);
    }
}
