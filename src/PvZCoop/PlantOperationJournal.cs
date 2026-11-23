using System;
using System.Collections.Generic;
using System.Linq;

namespace PvZCoop;

// Round-scoped, managed-only journal. Never evict terminal entries during the
// round: a very late retry must not become a new native placement.
public sealed class PlantOperationJournal
{
    public enum State { Waiting, Granted, Executing, Complete }
    public sealed class Entry
    {
        public int Player {get;init;}
        public int Token {get;init;}
        public Guid Operation {get;init;}
        public State Phase {get;internal set;}
        public double Expires {get;internal set;}
        public PlantTransaction Request {get;internal set;}
        public PlantTransactionResult Result {get;internal set;}
    }
    readonly Dictionary<(int,Guid),Entry> entries=new();
    readonly int capacity;
    public PlantOperationJournal(int capacity=8192){if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));this.capacity=capacity;}
    public int Count=>entries.Count;
    public void Clear()=>entries.Clear();
    public bool TryGet(int player,Guid operation,out Entry entry)=>entries.TryGetValue((player,operation),out entry);
    public bool Request(int player,int token,Guid operation,double expires,out Entry entry)
    {
        entry=null;
        if(player<2||player>4||token<=0||operation==Guid.Empty||!double.IsFinite(expires))return false;
        if(entries.TryGetValue((player,operation),out entry))return entry.Token==token;
        if(entries.Count>=capacity)return false;
        entry=new(){Player=player,Token=token,Operation=operation,Expires=expires};
        entries.Add((player,operation),entry);return true;
    }
    public bool Request(int player,PlantTransaction request,double expires,out Entry entry)
    {
        entry=null;
        if(request==null||!Request(player,request.LeaseToken,request.OperationId,expires,out entry))return false;
        if(entry.Request!=null)return entry.Request==request;
        // Bind the immutable intent when admitted, before granting the wallet.
        // An early cancel is already terminal and must remain untouched.
        if(entry.Phase!=State.Complete)entry.Request=request;
        return true;
    }
    public bool Grant(Entry entry,double now,double expires)
    {
        if(!Owns(entry)||entry.Phase!=State.Waiting||!double.IsFinite(now)||!double.IsFinite(expires)||now>=entry.Expires||expires<=now)return false;
        entry.Phase=State.Granted;entry.Expires=expires;return true;
    }
    public bool Begin(int player,PlantTransaction request,double now,out Entry entry)
    {
        entry=null;
        if(request==null||!double.IsFinite(now)||!entries.TryGetValue((player,request.OperationId),out entry)||
           entry.Token!=request.LeaseToken||entry.Phase!=State.Granted||now>=entry.Expires||
           (entry.Request!=null&&entry.Request!=request))return false;
        // Reserve before calling native code. Re-entrant callbacks and retries
        // cannot invoke the factory while the first call is still executing.
        entry.Request=request;entry.Phase=State.Executing;return true;
    }
    public bool Finish(Entry entry,bool accepted,int plantId,int balance,long revision,bool picked=false)
    {
        if(!Owns(entry)||entry.Phase!=State.Executing||balance<0||revision<0||plantId<0||!accepted&&(plantId!=0||picked))return false;
        entry.Result=new(entry.Operation,accepted,plantId,balance,revision,picked);entry.Phase=State.Complete;return true;
    }
    public bool Cancel(int player,int token,Guid operation,int balance,long revision,out Entry entry)
    {
        entry=null;if(balance<0||revision<0)return false;
        // A cancel can beat its request. Preserve its terminal identity too.
        if(!Request(player,token,operation,0,out entry))return false;
        if(entry.Phase==State.Executing)return false;
        if(entry.Phase==State.Complete)return true;
        entry.Result=new(operation,false,0,balance,revision);entry.Phase=State.Complete;return true;
    }
    public Entry[] Expire(double now,int balance,long revision)
    {
        if(!double.IsFinite(now)||balance<0||revision<0)return Array.Empty<Entry>();
        var expired=entries.Values.Where(e=>(e.Phase==State.Waiting||e.Phase==State.Granted)&&now>=e.Expires).ToArray();
        foreach(var entry in expired)Cancel(entry.Player,entry.Token,entry.Operation,balance,revision,out _);
        return expired;
    }
    bool Owns(Entry entry)=>entry!=null&&entries.TryGetValue((entry.Player,entry.Operation),out var stored)&&ReferenceEquals(entry,stored);
}
