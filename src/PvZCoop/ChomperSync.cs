using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Common;
using UnityEngine;

namespace PvZCoop;

// A swallowed zombie uses ordinary Die(2). The bite, chewing and swallowing
// belong to the plant, so replaying zombie death alone cannot reproduce them.
// Replicas display the host animator; they never execute Chomp/Swallow attacks.
public static class ChomperSync
{
    readonly record struct Key(int Id,int Type);
    sealed record State(long Revision,Key Key,int Hash,float Phase,float Speed,int Status,
        float Remaining,float Maximum,bool CanChew,bool HasChew,bool Chew);
    sealed class Replica
    {
        public readonly Queue<State> Phases=new();
        public State Latest;
        public long Received,ReceivedPhase,Applied;
        public int NextFrame;
        public float Until;
        public IntPtr Pointer;
    }
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    static readonly Dictionary<Key,(IntPtr Pointer,int Hash,int Status,bool CanChew)> published=new();
    static readonly Dictionary<Key,Replica> replicas=new();
    static long revision;
    static float nextCapture,nextSnapshot;
    static readonly HashSet<IntPtr> hooks=new();
    static bool Active=>Sync.Running&&Session.BattleLive;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset(){published.Clear();replicas.Clear();revision=0;nextCapture=nextSnapshot=0;}
    public static void Install(Harmony harmony)
    {
        hooks.Clear();
        var names=new HashSet<string>{"ChomperSearchZombie","BiteEvent","Chomp","Bite","AttributeEvent","Swallow","ToChew","ChompBack","CreateMine","Produce"};
        foreach(var type in typeof(Chomper).Assembly.GetTypes().Where(t=>!t.ContainsGenericParameters&&(t==typeof(Chomper)||t.IsSubclassOf(typeof(Chomper)))))
        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Where(m=>!m.IsAbstract&&!m.ContainsGenericParameters&&m.ReturnType==typeof(void)&&names.Contains(m.Name)))
        {
            var field=Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
            if(field==null)throw new MissingFieldException("Chomper native method unavailable: "+method);
            var native=Marshal.ReadIntPtr((IntPtr)field.GetValue(null));
            byte first=Marshal.ReadByte(native);
            // Never detour IL2CPP's shared empty return stub. The two tiny Pea
            // wrappers forward to the guarded base method; leave them intact.
            if(first==0xC2||first==0xC3||(type==typeof(PeaChomper)&&(method.Name=="AttributeEvent"||method.Name=="ChompBack"))||!hooks.Add(native))continue;
            Plugin.Logger.LogInfo("Chomper authority hook: "+type.Name+"."+method.Name);
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(ChomperSync),nameof(HostGameplay)));
        }
        Plugin.Logger.LogInfo("Host Chomper presentation and authority hooks: "+hooks.Count);
    }
    static bool HostGameplay()=>!Active||!Sync.IsClient;
    static IEnumerable<(Key key,Chomper plant)> Plants()
    {
        var list=Board.Instance?.boardEntity?.plantArray;
        if(list==null)yield break;
        foreach(var p in list.ToArray())
        {
            if(p==null||p.WasCollected)continue;
            var ch=p.TryCast<Chomper>();if(ch==null)continue;
            int id;
            if(Party.IsHost)id=PlantIdentity.GetOrAssignHostId(p);
            else if(!PlantIdentity.TryId(p,out id))continue;
            if(id>0)yield return(new(id,(int)p.thePlantType),ch);
        }
    }
    static State Capture(Key key,Chomper p)
    {
        var a=p.anim;var s=a.IsInTransition(0)?a.GetNextAnimatorStateInfo(0):a.GetCurrentAnimatorStateInfo(0);
        bool has=a.parameters.ToArray().Any(v=>v.name=="chew"&&v.type==AnimatorControllerParameterType.Bool);
        return new(++revision,key,s.fullPathHash,s.normalizedTime,a.speed,(int)p.theStatus,
            Math.Max(0,p.AttributeCountdown),Math.Max(0,p.AttributeCountdownMaximum),p.canToChew,has,has&&a.GetBool("chew"));
    }
    static string Encode(State s)=>string.Join(",",s.Revision,s.Key.Id,s.Key.Type,s.Hash,
        s.Phase.ToString("R",Inv),s.Speed.ToString("R",Inv),s.Status,s.Remaining.ToString("R",Inv),s.Maximum.ToString("R",Inv),s.CanChew?1:0,s.HasChew?1:0,s.Chew?1:0);
    static bool Read(string body,out State state)
    {
        state=null;var p=body.Split(',');if(p.Length!=12)return false;
        if(!long.TryParse(p[0],out long rev)||rev<1)return false;
        int[] n=new int[4];int[] positions={1,2,3,6};
        for(int i=0;i<n.Length;i++)if(!int.TryParse(p[positions[i]],out n[i]))return false;
        if(n[0]<1||n[1]<0)return false;
        float[] f=new float[4];int[] floats={4,5,7,8};
        for(int i=0;i<f.Length;i++)if(!float.TryParse(p[floats[i]],NumberStyles.Float,Inv,out f[i])||!float.IsFinite(f[i]))return false;
        if(f[1]<0||f[1]>100||f[2]<0||f[2]>1000000||f[3]<0||f[3]>1000000||
            (p[9]!="0"&&p[9]!="1")||(p[10]!="0"&&p[10]!="1")||(p[11]!="0"&&p[11]!="1"))return false;
        state=new(rev,new(n[0],n[1]),n[2],f[0],f[1],n[3],f[2],f[3],p[9]=="1",p[10]=="1",p[11]=="1");return true;
    }
    static void Receive(string type,string payload)
    {
        if((type!="chomper_phase"&&type!="chomper_state")||!Sync.IsClient||Party.SenderId!=1||payload.Length>262144)return;
        foreach(var body in payload.Split(';',StringSplitOptions.RemoveEmptyEntries))
        {
            if(!Read(body,out var s)||PlantIdentity.IsRetired(s.Key.Id))continue;
            if(replicas.Count>=1024&&!replicas.ContainsKey(s.Key))continue;
            if(!replicas.TryGetValue(s.Key,out var r))replicas[s.Key]=r=new();
            bool phase=type=="chomper_phase";
            if(phase){if(s.Revision<=r.ReceivedPhase)continue;r.ReceivedPhase=s.Revision;}
            else if(s.Revision<=r.Received)continue;
            // Reliable transitions and replaceable snapshots have separate
            // queues. An older transition still deserves presentation, but it
            // must never replace the newest authoritative timer/status.
            if(s.Revision>r.Received){r.Received=s.Revision;r.Latest=s;}
            r.Until=Time.unscaledTime+5;
            if(phase)
            {
                // Preserve short bite/swallow states even if several transitions
                // arrive together after a stall, rather than ending on idle.
                if(r.Phases.Count>=64){r.Phases.Clear();Plugin.Logger.LogWarning("Chomper presentation caught up after a long stall");}
                r.Phases.Enqueue(s);
            }
        }
    }
    static void Apply(Chomper p,State s,bool transition,bool authoritative=true)
    {
        if(authoritative)
        {
            p.theStatus=(PlantStatus)s.Status;p.canToChew=s.CanChew;
            if(Math.Abs(p.AttributeCountdown-s.Remaining)>.2f||Math.Abs(p.AttributeCountdownMaximum-s.Maximum)>.2f)
                p.SetAttributeCountdown(s.Remaining,s.Maximum);
        }
        var a=p.anim;if(a==null)return;
        if(s.HasChew)a.SetBool("chew",s.Chew);
        var local=a.GetCurrentAnimatorStateInfo(0);
        if(s.Hash!=0&&a.HasState(0,s.Hash)&&(transition||local.fullPathHash!=s.Hash||Math.Abs(local.normalizedTime-s.Phase)>.35f))a.Play(s.Hash,0,s.Phase);
        a.speed=s.Speed;
    }
    public static void Tick()
    {
        if(!Active)return;
        if(Party.IsHost)
        {
            float now=Time.unscaledTime;if(now<nextCapture)return;nextCapture=now+.05f;
            bool snapshot=now>=nextSnapshot;if(snapshot)nextSnapshot=now+.1f;
            var all=new List<string>();var live=new HashSet<Key>();
            foreach(var (key,p) in Plants())
            {
                if(p.anim==null)continue;live.Add(key);var s=Capture(key,p);
                var stamp=(p.Pointer,s.Hash,s.Status,s.CanChew);
                if(!published.TryGetValue(key,out var before)||before!=stamp){Party.Send("chomper_phase",Encode(s));published[key]=stamp;}
                if(snapshot)all.Add(Encode(s));
            }
            foreach(var key in published.Keys.Where(k=>!live.Contains(k)).ToArray())published.Remove(key);
            if(snapshot)Party.Send("chomper_state",string.Join(";",all));
            return;
        }
        var plants=Plants().ToDictionary(v=>v.key,v=>v.plant);
        foreach(var pair in replicas.ToArray())
        {
            var r=pair.Value;
            if(PlantIdentity.IsRetired(pair.Key.Id)||Time.unscaledTime>r.Until){replicas.Remove(pair.Key);continue;}
            if(!plants.TryGetValue(pair.Key,out var plant)||plant.anim==null)continue;
            if(r.Pointer!=IntPtr.Zero&&r.Pointer!=plant.Pointer){r.Phases.Clear();r.Applied=0;}
            r.Pointer=plant.Pointer;
            if(r.Phases.Count>0)
            {
                if(Time.frameCount<r.NextFrame)continue;
                var s=r.Phases.Dequeue();
                if(r.Latest!=null&&r.Latest.Revision>r.Applied)
                {Apply(plant,r.Latest,false);r.Applied=r.Latest.Revision;}
                Apply(plant,s,true,s.Revision>=r.Applied);r.Applied=Math.Max(r.Applied,s.Revision);r.NextFrame=Time.frameCount+2;
            }
            else if(r.Latest!=null&&r.Latest.Revision>=r.Applied){Apply(plant,r.Latest,false);r.Applied=r.Latest.Revision;}
        }
    }
}
