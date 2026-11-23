using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Native callers configure child countdowns AFTER SetPlant. Publish completed
// host values by lifetime ID: moving a mine never changes its timer identity.
public static class MineStateSync
{
    sealed record Pending(MineStatePacket State,float Until);
    static readonly Dictionary<int,Pending> pending=new();
    static readonly Dictionary<int,long> received=new();
    static readonly Dictionary<int,int> hostBorn=new(),guestBorn=new();
    static readonly Dictionary<int,bool> maturity=new();
    static readonly Dictionary<int,int> immatureAnimation=new();
    static float nextSend;
    static long revision;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset(){pending.Clear();received.Clear();hostBorn.Clear();guestBorn.Clear();maturity.Clear();immatureAnimation.Clear();nextSend=0;revision=0;}
    static bool Initialized(int id,Dictionary<int,int> births)
    {
        if(!births.TryGetValue(id,out int frame)){births[id]=Time.frameCount;return false;}
        return Time.frameCount>frame+2;
    }
    // The substantial native PotatoUpdate triggers local rising at timer zero.
    // Hold that decision for the host; do not detour tiny AnimRiseOver setters.
    public static bool CanRunNativeUpdate(PotatoMine mine)
    {
        if(!Sync.IsClient||!Session.BattleLive||!PlantIdentity.IsTracked(mine))return true;
        return PlantIdentity.TryId(mine,out int id)&&maturity.TryGetValue(id,out bool ready)&&ready;
    }
    public static void Tick()
    {
        if(!Sync.Running||!Session.BattleLive)return;
        if(Sync.IsHostRunning)
        {
            if(Time.unscaledTime<nextSend)return;nextSend=Time.unscaledTime+.1f;
            var states=new List<string>();
            var plants=Board.Instance?.boardEntity?.plantArray?.ToArray()??Array.Empty<Plant>();
            foreach(var plant in plants)
            {
                if(plant==null||plant.WasCollected)continue;
                var mine=plant.TryCast<PotatoMine>();if(mine==null)continue;
                int id=PlantIdentity.GetOrAssignHostId(plant);if(id<1||!Initialized(id,hostBorn))continue;
                float remaining=mine.AttributeCountdown,maximum=mine.AttributeCountdownMaximum;
                if(!float.IsFinite(remaining)||!float.IsFinite(maximum))continue;
                var state=new MineStatePacket(++revision,id,(int)plant.thePlantType,Math.Max(0,remaining),Math.Max(0,maximum),mine.isAready);
                if(MineStatePacket.TryRead(state.Encode(),out _))states.Add(state.Encode());
            }
            Party.Send("mine_state",string.Join(";",states));return;
        }
        foreach(var pair in pending.ToArray())
        {
            if(PlantIdentity.IsRetired(pair.Key)||Time.unscaledTime>pair.Value.Until){pending.Remove(pair.Key);continue;}
            if(!PlantIdentity.TryGet(pair.Key,out var plant)||!Initialized(pair.Key,guestBorn))continue;
            var state=pair.Value.State;
            if((int)plant.thePlantType!=state.Type){pending.Remove(pair.Key);continue;}
            var mine=plant.TryCast<PotatoMine>();if(mine==null){pending.Remove(pair.Key);continue;}
            // Only this exact lifetime is changed. No coordinate/ordinal fallback.
            if(!mine.isAready&&mine.anim!=null&&!immatureAnimation.ContainsKey(state.Id))
                immatureAnimation[state.Id]=mine.anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            maturity[state.Id]=state.Ready;
            if(!state.Ready&&mine.isAready)
            {
                mine.CancelInvoke("AnimRiseOver");mine.isAready=false;
                if(mine.anim!=null)
                {
                    mine.anim.ResetTrigger("rise");
                    if(immatureAnimation.TryGetValue(state.Id,out int hash)&&hash!=0&&mine.anim.HasState(0,hash))mine.anim.Play(hash,0,0);
                }
            }
            if(Math.Abs(mine.AttributeCountdown-state.Remaining)>.1f||Math.Abs(mine.AttributeCountdownMaximum-state.Maximum)>.1f)
                mine.SetAttributeCountdown(state.Remaining,state.Maximum);
            if(state.Ready&&!mine.isAready)
            {
                if(mine.anim!=null)mine.anim.SetTrigger("rise");
                mine.AnimRiseOver();
            }
            pending.Remove(pair.Key);
        }
    }
    static void Receive(string type,string payload)
    {
        if(type!="mine_state"||!Sync.IsClient||Party.SenderId!=1||payload.Length>262144)return;
        foreach(var text in payload.Split(';',StringSplitOptions.RemoveEmptyEntries))
        {
            if(!MineStatePacket.TryRead(text,out var state)||PlantIdentity.IsRetired(state.Id))continue;
            if(received.TryGetValue(state.Id,out long old)&&old>=state.Revision)continue;
            if(pending.Count>=1024&&!pending.ContainsKey(state.Id))continue;
            received[state.Id]=state.Revision;pending[state.Id]=new(state,Time.unscaledTime+3);
        }
    }
}
