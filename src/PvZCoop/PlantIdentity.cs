using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Host IDs describe a native lifetime, not a tile. Runtime replicas are bound
// by the originating placement operation; moving a plant never changes its ID.
public static class PlantIdentity
{
    sealed class Entry {public int Id,Instance;public Plant Plant;}
    sealed class Operation {public Plant Plant;public int Id;public bool Retired;}
    readonly record struct Initial(int Id,int Column,int Row,int Type,int Ordinal);
    static readonly Dictionary<int,Entry> byId=new();
    static readonly Dictionary<(IntPtr,int),int> byNative=new();
    static readonly HashSet<int> retiredIds=new();
    static readonly HashSet<(IntPtr,int)> retiredNative=new();
    static readonly Dictionary<(int,string),Operation> operations=new();
    static readonly Dictionary<(IntPtr,int),(int Creator,string Operation)> sourceOperations=new();
    static readonly Dictionary<(int,string),int> pendingBindings=new();
    static readonly Queue<(string type,string body)> outgoing=new();
    static Initial[] pendingInitial;
    static int nextId;
    static bool initialSent,initialApplied;
    static float initialUntil,nextWarning;
    public static int BoundCount=>byId.Values.Count(e=>Live(e.Plant)&&e.Plant.GetInstanceID()==e.Instance);
    public static bool BootstrapComplete=>Party.IsHost?initialSent:initialApplied;
    public static bool Enabled=>Sync.Running;
    static bool Live(Plant p)=>p!=null&&!p.WasCollected;
    static (IntPtr,int) Native(Plant p)=>(p.Pointer,p.GetInstanceID());
    static Plant[] Plants()=>Board.Instance?.boardEntity?.plantArray?.ToArray().Where(Live).ToArray()??Array.Empty<Plant>();
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset()
    {
        byId.Clear();byNative.Clear();retiredIds.Clear();retiredNative.Clear();operations.Clear();sourceOperations.Clear();
        pendingBindings.Clear();outgoing.Clear();pendingInitial=null;nextId=0;initialSent=initialApplied=false;
        initialUntil=nextWarning=0;
    }
    public static bool TryId(Plant plant,out int id)
    {
        id=0;return Live(plant)&&byNative.TryGetValue(Native(plant),out id)&&!retiredIds.Contains(id);
    }
    public static bool TryGet(int id,out Plant plant)
    {
        plant=null;
        if(id<=0||retiredIds.Contains(id)||!byId.TryGetValue(id,out var e)||!Live(e.Plant)||e.Plant.GetInstanceID()!=e.Instance)return false;
        plant=e.Plant;return true;
    }
    public static bool IsRetired(int id)=>id>0&&retiredIds.Contains(id);
    public static bool IsTracked(Plant plant)=>Live(plant)&&(TryId(plant,out _)||sourceOperations.ContainsKey(Native(plant)));
    public static bool TrySourceOperation(Plant plant,out int creator,out string operation)
    {
        creator=0;operation="";
        if(!Live(plant)||!sourceOperations.TryGetValue(Native(plant),out var source))return false;
        creator=source.Creator;operation=source.Operation;return true;
    }
    public static bool TryOperationPlant(int creator,string operation,out Plant plant)
    {
        plant=null;
        if(!operations.TryGetValue((creator,operation),out var source)||source.Retired||!Live(source.Plant))return false;
        plant=source.Plant;return true;
    }
    public static string Reference(Plant plant)
    {
        TryId(plant,out int id);TrySourceOperation(plant,out int creator,out string operation);
        return id+"|"+creator+"|"+operation;
    }
    public static bool BindAuthoritative(int id,Plant plant)=>Bind(id,plant);
    public static int GetOrAssignHostId(Plant plant)
    {
        if(!Party.IsHost||!Sync.Running||!Live(plant))return 0;
        if(TryId(plant,out int id))return id;
        if(retiredNative.Contains(Native(plant)))return 0;
        id=checked(++nextId);Bind(id,plant);return id;
    }
    static bool Bind(int id,Plant plant)
    {
        if(id<=0||retiredIds.Contains(id)||!Live(plant)||retiredNative.Contains(Native(plant)))return false;
        if(TryGet(id,out var existing)&&Native(existing)!=Native(plant))
        {Warn("Refused an ambiguous plant identity binding "+id);return false;}
        var native=Native(plant);
        if(byNative.TryGetValue(native,out int old)&&old!=id)
        {
            // A canonical host fusion may replace a lifetime while a guest's
            // native factory reused its object. Exact operation binding wins.
            byId.Remove(old);retiredIds.Add(old);
        }
        byNative[native]=id;byId[id]=new Entry{Id=id,Instance=native.Item2,Plant=plant};
        return true;
    }
    public static int MarkRetired(Plant plant)
    {
        if(!Live(plant))return 0;
        var native=Native(plant);byNative.TryGetValue(native,out int id);
        bool canonical=Party.IsHost||Session.Allow;
        if(id>0){byNative.Remove(native);byId.Remove(id);if(canonical)retiredIds.Add(id);}
        // An optimistic removal also ends this exact native lifetime.
        // A delayed glove acknowledgement must never bind its pooled object.
        retiredNative.Add(native);
        foreach(var operation in operations.Values.Where(o=>Live(o.Plant)&&Native(o.Plant)==native))operation.Retired=true;
        return id;
    }
    public static (string Operation,int Id) LocalCreated(Plant plant)
    {
        string operation=Guid.NewGuid().ToString("N");
        int id=Party.IsHost?GetOrAssignHostId(plant):0;
        operations[(Party.PlayerId,operation)]=new Operation{Plant=plant,Id=id};
        if(Live(plant))sourceOperations[Native(plant)]=(Party.PlayerId,operation);
        return(operation,id);
    }
    public static bool ValidOperation(string operation)=>Guid.TryParseExact(operation,"N",out _);
    public static Plant ReceiveSpawn(int creator,string operation,int suppliedId,Func<Plant> factory)
    {
        if(creator<1||creator>4||!ValidOperation(operation)||suppliedId<0)return null;
        var key=(creator,operation);
        if(operations.TryGetValue(key,out var seen))
        {
            if(Party.IsHost&&seen.Id>0)QueueBinding(creator,operation,seen.Id);
            return !seen.Retired&&Live(seen.Plant)?seen.Plant:null;
        }
        // Record before native invocation: nested/repeated delivery cannot
        // enter the same factory twice even if it does not return a plant.
        var record=new Operation();operations[key]=record;
        var plant=factory();record.Plant=plant;
        if(Live(plant))sourceOperations[Native(plant)]=key;
        if(Party.IsHost)
        {
            record.Id=GetOrAssignHostId(plant);
            if(record.Id>0)QueueBinding(creator,operation,record.Id);
            else outgoing.Enqueue(("plant_reject",creator+"|"+operation));
        }
        else if(creator==1&&suppliedId>0){record.Id=suppliedId;Bind(suppliedId,plant);}
        ApplyBinding(key);return plant;
    }
    static void QueueBinding(int creator,string operation,int id)=>outgoing.Enqueue(("plant_bind",creator+"|"+operation+"|"+id));
    static void ApplyBinding((int,string) key)
    {
        if(!pendingBindings.TryGetValue(key,out int id)||!operations.TryGetValue(key,out var op))return;
        if(op.Retired||!Live(op.Plant)){pendingBindings.Remove(key);return;}
        if(Bind(id,op.Plant)){op.Id=id;pendingBindings.Remove(key);}
    }
    static void Receive(string type,string body)
    {
        if(Party.IsHost||Party.SenderId!=1)return;
        if(type=="plant_bind")
        {
            var p=body.Split('|');
            if(p.Length!=3||!int.TryParse(p[0],out int creator)||creator<1||creator>4||!ValidOperation(p[1])||!int.TryParse(p[2],out int id)||id<1)return;
            pendingBindings[(creator,p[1])]=id;ApplyBinding((creator,p[1]));
        }
        else if(type=="plant_reject")
        {
            var p=body.Split('|');if(p.Length!=2||!int.TryParse(p[0],out int creator)||!ValidOperation(p[1]))return;
            if(!operations.TryGetValue((creator,p[1]),out var op)||op.Retired)return;
            if(Live(op.Plant))
            {
                bool allow=Session.Allow;int sun=SharedSun.BeforeReplica();Session.Allow=true;
                try{ZombieDeathEffects.RemoveReplicaPlant(op.Plant,Plant.DieReason.Hid);}
                finally{Session.Allow=allow;SharedSun.AfterReplica(sun);}
            }
            op.Retired=true;
            if(creator==Party.PlayerId)Session.ShowToast("The host could not place that plant. Try again.",5);
        }
        else if(type=="plant_bootstrap"&&!initialApplied)
        {
            if(body.Length>131072)return;
            var rows=new List<Initial>();var ids=new HashSet<int>();
            foreach(var raw in body.Split(';',StringSplitOptions.RemoveEmptyEntries))
            {
                var p=raw.Split(',');if(p.Length!=5)return;var n=new int[5];
                for(int i=0;i<n.Length;i++)if(!int.TryParse(p[i],out n[i]))return;
                if(n[0]<1||!ids.Add(n[0])||n[1]<0||n[1]>63||n[2]<0||n[2]>31||n[3]<0||n[4]<0||n[4]>63)return;
                rows.Add(new(n[0],n[1],n[2],n[3],n[4]));
            }
            pendingInitial=rows.ToArray();initialUntil=Time.unscaledTime+10;TryBootstrap();
        }
    }
    static void TryBootstrap()
    {
        if(pendingInitial==null||!Sync.Running)return;
        var plants=Plants();var assignments=new List<(int,Plant)>();
        foreach(var group in pendingInitial.GroupBy(s=>(s.Column,s.Row,s.Type)))
        {
            var candidates=plants.Where(p=>p.thePlantColumn==group.Key.Column&&p.thePlantRow==group.Key.Row&&(int)p.thePlantType==group.Key.Type).ToArray();
            // This structural fallback is one initial inventory transaction,
            // never a mechanism to guess the identity of runtime replacements.
            if(candidates.Length!=group.Count())return;
            foreach(var row in group)
            {
                if(row.Ordinal>=candidates.Length)return;
                var plant=candidates[row.Ordinal];
                if(operations.Values.Any(o=>Live(o.Plant)&&Native(o.Plant)==Native(plant)))return;
                if(TryId(plant,out int old)&&old!=row.Id)return;
                assignments.Add((row.Id,plant));
            }
        }
        foreach(var (id,plant) in assignments)if(!Bind(id,plant))return;
        initialApplied=true;pendingInitial=null;
        Plugin.Logger.LogInfo("Initial plant identities bound: "+assignments.Count);
    }
    public static void Tick()
    {
        if(!Sync.Running)return;
        // NetworkRoom relays an incoming guest sp after the dispatch returns.
        // Bindings must follow that relay, not race in front of the creation.
        while(outgoing.Count>0){var e=outgoing.Dequeue();Party.Send(e.type,e.body);}
        if(Party.IsHost&&!initialSent&&Session.BattleLive)
        {
            var rows=new List<string>();var ordinals=new Dictionary<(int,int,int),int>();
            foreach(var plant in Plants())
            {
                // Runtime operations already have exact GUID bindings.
                if(operations.Values.Any(o=>Live(o.Plant)&&Native(o.Plant)==Native(plant)))continue;
                int id=GetOrAssignHostId(plant);if(id<=0)continue;
                var key=(plant.thePlantColumn,plant.thePlantRow,(int)plant.thePlantType);
                ordinals.TryGetValue(key,out int ordinal);ordinals[key]=ordinal+1;
                rows.Add(id+","+key.Item1+","+key.Item2+","+key.Item3+","+ordinal);
            }
            Party.Send("plant_bootstrap",string.Join(";",rows));initialSent=true;
        }
        if(!Party.IsHost&&pendingInitial!=null)
        {
            TryBootstrap();
            if(pendingInitial!=null&&Time.unscaledTime>initialUntil){Warn("Initial plant identity inventory did not match; refusing a guessed binding");pendingInitial=null;}
        }
    }
    static void Warn(string text)
    {
        if(Time.unscaledTime<nextWarning)return;nextWarning=Time.unscaledTime+5;
        Plugin.Logger.LogWarning(text);
    }
}
