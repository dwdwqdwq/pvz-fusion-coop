using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Native glove placement passes the EXISTING plant as targetPlant to SetPlant.
// Its non-null-target branch reuses that GameObject, native grid movement and
// subtype state. Sending sp in addition to mv would create a second plant.
public static class PlantGloveSync
{
    sealed class LocalMove {public Plant Plant;public GloveMovePacket Packet;public Plant.DieReason? Death;}
    sealed class Pending {public int Sender;public GloveMovePacket Packet;public float Until;}
    static LocalMove local;
    static readonly HashSet<(int,string)> applied=new();
    static readonly Dictionary<(int,string),Pending> pending=new();
    static readonly Dictionary<string,Plant> results=new();
    static readonly Queue<(GloveMovePacket packet,Plant.DieReason? death)> outcomes=new();
    static readonly Dictionary<string,GloveMovePacket> authority=new();
    static Plant applyingPlant;
    static Plant.DieReason? applyingDeath;
    static float nextMalformedWarning;
    public static bool Applying {get;private set;}
    public static bool SuppressLegacyMove=>local!=null||Applying;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset(){local=null;pending.Clear();applied.Clear();results.Clear();authority.Clear();outcomes.Clear();Applying=false;applyingPlant=null;applyingDeath=null;}
    public static void RecordDeath(Plant plant,Plant.DieReason reason)
    {
        if(plant==null)return;
        if(local!=null&&local.Plant.Pointer==plant.Pointer)local.Death=reason;
        if(Applying&&applyingPlant!=null&&applyingPlant.Pointer==plant.Pointer)applyingDeath=reason;
    }
    static void PublishDeath(GloveMovePacket p,Plant.DieReason reason)=>Party.Send("pd",p.TargetColumn+"|"+p.TargetRow+"|"+p.TargetType+"|"+(int)reason+"|"+p.EntityId+"|"+p.Creator+"|"+p.SourceOperation);

    static List<Plant> At(int column,int row,int type)
    {
        var list=Board.Instance?.boardEntity?.plantArray;
        if(list==null)return new();
        return list.ToArray().Where(p=>p!=null&&!p.WasCollected&&p.thePlantColumn==column&&p.thePlantRow==row&&(int)p.thePlantType==type).ToList();
    }
    public static bool Begin(int column,int row,PlantType type,Plant target,bool localAction)
    {
        if(!Sync.Running||Session.Allow||Applying||!localAction||target==null||target.WasCollected||
           Mouse.Instance?.thePlantOnGlove==null||Mouse.Instance.thePlantOnGlove.Pointer!=target.Pointer)return false;
        int ordinal=At(target.thePlantColumn,target.thePlantRow,(int)target.thePlantType).FindIndex(p=>p.Pointer==target.Pointer);
        if(ordinal<0)return false;
        Sync.MarkAction(target.thePlantColumn,target.thePlantRow);Sync.MarkAction(column,row);
        var mine=target.TryCast<PotatoMine>();
        PlantIdentity.TryId(target,out int entityId);PlantIdentity.TrySourceOperation(target,out int creator,out string sourceOperation);
        local=new LocalMove{Plant=target,Packet=new(Guid.NewGuid().ToString("N"),target.thePlantColumn,target.thePlantRow,
            (int)target.thePlantType,ordinal,column,row,(int)type,Math.Max(0,target.thePlantHealth),mine!=null&&mine.isAready,entityId,creator,sourceOperation)};
        return true;
    }
    // Called once when the outer native SetPlant completes, including null.
    public static void Finish(Plant result)
    {
        var move=local;local=null;if(move==null)return;
        bool consumed=move.Death.HasValue;
        if(!consumed&&(result==null||result.WasCollected))return;
        if(!consumed&&(result.thePlantColumn!=move.Packet.TargetColumn||result.thePlantRow!=move.Packet.TargetRow))return;
        var packet=consumed?move.Packet:move.Packet with {TargetType=(int)result.thePlantType,Health=Math.Max(0,result.thePlantHealth)};
        if(!consumed)results[packet.Operation]=result;
        applied.Add((Party.PlayerId,packet.Operation));Party.Send("gm",packet.Encode());
        if(Party.IsHost&&consumed)PublishDeath(packet,move.Death.Value);
    }
    static void Receive(string type,string payload)
    {
        if(type!="gm"&&type!="gma")return;
        if(Party.SenderId<1||Party.SenderId==Party.PlayerId||(type=="gma"&&Party.SenderId!=1))return;
        if(!GloveMovePacket.TryRead(payload,out var packet))
        {
            if(Time.unscaledTime>=nextMalformedWarning){nextMalformedWarning=Time.unscaledTime+5;Plugin.Logger.LogWarning("Ignored invalid native glove packet");}
            return;
        }
        if(type=="gma")
        {
            authority[packet.Operation]=packet;
            ApplyAuthority(packet.Operation);
            return;
        }
        var key=(Party.SenderId,packet.Operation);if(applied.Contains(key)||pending.ContainsKey(key))return;
        pending[key]=new Pending{Sender=Party.SenderId,Packet=packet,Until=Time.unscaledTime+10};
        Apply(key);
    }
    static void ApplyAuthority(string operation)
    {
        if(authority.TryGetValue(operation,out var packet)&&results.TryGetValue(operation,out var result)&&result!=null&&!result.WasCollected)
        {
            authority.Remove(operation);
            if(packet.EntityId>0&&!PlantIdentity.BindAuthoritative(packet.EntityId,result))return;
            SetReady(result,packet.MineReady);result.thePlantHealth=packet.Health;
        }
    }
    static void SetReady(Plant plant,bool ready)
    {
        var mine=plant?.TryCast<PotatoMine>();
        if(ready&&mine!=null&&!mine.isAready)mine.AnimRiseOver();
    }
    static void Apply((int,string) key)
    {
        if(!Sync.Running||!pending.TryGetValue(key,out var request))return;
        var p=request.Packet;Sync.MarkAction(p.SourceColumn,p.SourceRow);Sync.MarkAction(p.TargetColumn,p.TargetRow);
        Plant source=null;
        if(p.EntityId>0)
        {
            if(PlantIdentity.IsRetired(p.EntityId)){pending.Remove(key);applied.Add(key);return;}
            if(!PlantIdentity.TryGet(p.EntityId,out source))return;
        }
        else if(p.Creator>0)
        {
            if(!PlantIdentity.TryOperationPlant(p.Creator,p.SourceOperation,out source))return;
        }
        else
        {
            if(PlantIdentity.BootstrapComplete)return;
            var candidates=At(p.SourceColumn,p.SourceRow,p.SourceType);if(p.Ordinal>=candidates.Count)return;
            source=candidates[p.Ordinal];
        }
        if((int)source.thePlantType!=p.SourceType){pending.Remove(key);applied.Add(key);return;}
        if(Party.IsHost)p=p with{EntityId=PlantIdentity.GetOrAssignHostId(source)};
        int sun=SharedSun.BeforeReplica();bool allow=Session.Allow;
        Applying=true;Session.Allow=true;applyingPlant=source;applyingDeath=null;
        try
        {
            if(request.Sender==1)SetReady(source,p.MineReady);
            var result=CreatePlant.Instance.SetPlant(p.TargetColumn,p.TargetRow,(PlantType)p.TargetType,source,Vector2.zero,false,true,null);
            if(result==null&&!applyingDeath.HasValue)return;
            if(result!=null&&!result.WasCollected)
            {
                if(request.Sender==1)SetReady(result,p.MineReady);
                results[p.Operation]=result;ApplyAuthority(p.Operation);
                if(Party.IsHost){var mine=result.TryCast<PotatoMine>();p=p with{Health=Math.Max(0,result.thePlantHealth),MineReady=mine!=null&&mine.isAready};}
            }
            // A host packet can repair HP; a guest never overwrites host health.
            if(request.Sender==1&&result!=null&&!result.WasCollected)result.thePlantHealth=p.Health;
            applied.Add(key);pending.Remove(key);
            if(Party.IsHost)outcomes.Enqueue((p,applyingDeath));
        }
        finally{Session.Allow=allow;Applying=false;applyingPlant=null;applyingDeath=null;SharedSun.AfterReplica(sun);}
    }
    public static void Tick()
    {
        // Incoming gm has now returned through NetworkRoom's relay. These
        // authoritative outcomes must follow it, never precede its placement.
        while(outcomes.Count>0)
        {
            var outcome=outcomes.Dequeue();
            if(outcome.death.HasValue)PublishDeath(outcome.packet,outcome.death.Value);
            else Party.Send("gma",outcome.packet.Encode());
        }
        foreach(var entry in pending.ToArray())
        {
            if(Time.unscaledTime>entry.Value.Until)
            {
                pending.Remove(entry.Key);
                Plugin.Logger.LogWarning("Native glove source unavailable; operation "+entry.Value.Packet.Operation);
                Session.ShowToast("Could not synchronize a glove move. Restart the shared level.",8);
                continue;
            }
            Apply(entry.Key);
        }
    }
}
