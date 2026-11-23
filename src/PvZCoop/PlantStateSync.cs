using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

public sealed record PlantStateObservation(int Id,int Type,long Revision,bool SamePhase,
    int LocalStatus,int HostStatus,int LocalTarget,int HostTarget,int LocalAnimation,int HostAnimation,
    float AttackDrift,float ProduceDrift,float AttributeDrift,float BuffDrift);

// Host common-state repair, deliberately separate from lifecycle and from
// family-specific animation/ability replay. It never calls an attack or moves
// a plant to match a cell snapshot. All mutations address the bound host ID.
public static class PlantStateSync
{
    sealed record Pending(PlantStatePacket State,float Received,float Until);
    static readonly Dictionary<int,Pending> pending=new();
    static readonly Dictionary<int,long> received=new(),applied=new();
    static readonly Dictionary<int,(IntPtr Pointer,int Frame)> hostBorn=new(),guestBorn=new();
    static readonly Dictionary<int,PlantStateObservation> observations=new();
    static long revision;
    static float nextSend;
    public static long HealthRepairs {get;private set;}
    public static long TimerRepairs {get;private set;}
    public static long BuffRepairs {get;private set;}
    public static long PhaseMismatches {get;private set;}
    public static long AnimationMismatches {get;private set;}
    public static long UnmappedExpired {get;private set;}
    public static IReadOnlyDictionary<int,PlantStateObservation> Observations=>observations;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset()
    {
        pending.Clear();received.Clear();applied.Clear();hostBorn.Clear();guestBorn.Clear();observations.Clear();
        revision=0;nextSend=0;HealthRepairs=TimerRepairs=BuffRepairs=PhaseMismatches=AnimationMismatches=UnmappedExpired=0;
    }
    static Plant[] Plants()=>Board.Instance?.boardEntity?.plantArray?.ToArray()??Array.Empty<Plant>();
    static bool Ready(Plant p,int id,Dictionary<int,(IntPtr Pointer,int Frame)> births)
    {
        if(p==null||p.WasCollected)return false;
        if(!births.TryGetValue(id,out var born)||born.Pointer!=p.Pointer)
        {births[id]=(p.Pointer,Time.frameCount);return false;}
        return Time.frameCount>born.Frame+2;
    }
    static int Animation(Plant p,out float phase,out float speed)
    {
        phase=0;speed=1;if(p.anim==null)return 0;
        var state=p.anim.IsInTransition(0)?p.anim.GetNextAnimatorStateInfo(0):p.anim.GetCurrentAnimatorStateInfo(0);
        phase=state.normalizedTime;speed=p.anim.speed;return state.fullPathHash;
    }
    static int Target(Plant p)
    {
        var z=p.targetZombie;return z!=null&&!z.WasCollected&&Sync.TryZombieId(z,out int id)?id:0;
    }
    static PlantStatePacket Capture(Plant p,int id)
    {
        int hash=Animation(p,out float phase,out float speed);
        var timers=p.Timers?.ToArray()??Array.Empty<float>();
        // A private/native sentinel must not suppress HP for the entire actor.
        // Unsupported timer data is omitted rather than guessed or zeroed.
        if(timers.Length>32||timers.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1000000))timers=Array.Empty<float>();
        for(int i=0;i<timers.Length;i++)timers[i]=Math.Max(0,timers[i]);
        var state=new PlantStatePacket(++revision,id,(int)p.thePlantType,p.thePlantColumn,p.thePlantRow,
            Math.Max(0,p.thePlantHealth),Math.Max(0,p.thePlantMaxHealth),Math.Max(0,p.theShieldHealth),(int)p.theStatus,Target(p),
            Timer(p.thePlantAttackCountDown),Timer(p.thePlantProduceCountDown),Timer(p.AttributeCountdown),Timer(p.eventAttributeCountdownMaximum),
            Timer(p.lastEventAttributeCountdown),hash,float.IsFinite(phase)?phase:0,float.IsFinite(speed)&&speed>=0&&speed<=100?speed:1,timers);
        return PlantStatePacket.TryRead(state.Encode(),out _)?state:null;
    }
    static float Timer(float value)=>float.IsFinite(value)&&Math.Abs(value)<=1000000?value:-1;
    static void Receive(string type,string payload)
    {
        if(type!="plant_state"||!Sync.IsClient||Party.SenderId!=1||payload.Length>262144)return;
        foreach(var text in payload.Split(';',StringSplitOptions.RemoveEmptyEntries))
        {
            if(!PlantStatePacket.TryRead(text,out var state))continue;
            if(received.TryGetValue(state.Id,out long old)&&old>=state.Revision)continue;
            if(pending.Count>=1024&&!pending.ContainsKey(state.Id))continue;
            received[state.Id]=state.Revision;
            pending[state.Id]=new(state,Time.unscaledTime,Time.unscaledTime+3);
        }
    }
    static void RepairHealth(Plant p,PlantStatePacket state)
    {
        bool changed=p.thePlantHealth!=state.Health||p.thePlantMaxHealth!=state.MaxHealth||p.theShieldHealth!=state.Shield;
        if(!changed)return;
        int difference=p.thePlantHealth-state.Health;
        if(difference>0&&p.thePlantHealth>1)
        {
            // Retain nonterminal native damage/crack presentation, as the old
            // lawn reconciliation did. Never execute terminal Die/DieEvent from
            // a replaceable snapshot: reliable host pd owns that lifecycle.
            bool allow=Session.Allow;Session.Allow=true;
            try{p.TakeDamage(Math.Min(difference,p.thePlantHealth-1),null,DamageType.RealDamage,PlantType.Nothing,true);}
            finally{Session.Allow=allow;}
            if(p==null||p.WasCollected)return;
        }
        p.thePlantMaxHealth=state.MaxHealth;p.thePlantHealth=state.Health;p.theShieldHealth=state.Shield;
        if(p.healthSlider!=null)p.healthSlider.UpdateText();
        HealthRepairs++;
    }
    static void Apply(Plant p,PlantStatePacket state)
    {
        int animation=Animation(p,out _,out _),target=Target(p);
        bool samePhase=(int)p.theStatus==state.Status&&target==state.TargetZombie&&
            (animation==0||state.Animation==0||animation==state.Animation);
        var timers=p.Timers;float buffDrift=0;
        if(timers!=null&&timers.Length==state.BuffTimers.Length)
            for(int i=0;i<timers.Length;i++)buffDrift=Math.Max(buffDrift,Math.Abs(timers[i]-state.BuffTimers[i]));
        observations[state.Id]=new(state.Id,state.Type,state.Revision,samePhase,(int)p.theStatus,state.Status,target,state.TargetZombie,
            animation,state.Animation,Math.Abs(p.thePlantAttackCountDown-state.Attack),Math.Abs(p.thePlantProduceCountDown-state.Produce),
            Math.Abs(p.AttributeCountdown-state.Attribute),buffDrift);
        if(observations.Count>1024)
            foreach(var id in observations.OrderBy(v=>v.Value.Revision).Take(observations.Count-1024).Select(v=>v.Key).ToArray())observations.Remove(id);
        if(!samePhase)PhaseMismatches++;
        if(animation!=0&&state.Animation!=0&&animation!=state.Animation)AnimationMismatches++;
        // These families already have authoritative timers/phase handling.
        bool specialized=p.TryCast<Chomper>()!=null||p.TryCast<PotatoMine>()!=null;
        if(!specialized)
        {
            if(PlantStateRepairPolicy.RepairPositiveCycle(p.thePlantAttackCountDown,state.Attack,samePhase,.35f,out float attack))
            {p.thePlantAttackCountDown=attack;TimerRepairs++;}
            if(PlantStateRepairPolicy.RepairPositiveCycle(p.thePlantProduceCountDown,state.Produce,samePhase,.35f,out float produce))
            {p.thePlantProduceCountDown=produce;TimerRepairs++;}
            if(p.lastEventAttributeCountdown>0&&state.AttributeLast>0&&
                PlantStateRepairPolicy.RepairPositiveCycle(p.AttributeCountdown,state.Attribute,samePhase,.35f,out float attribute))
            {
                // Raw value only: do not reset completionPending/revision or
                // invoke virtual AttributeEvent/Swallow/production callbacks.
                p.attributeCountdown=attribute;
                if(state.AttributeMaximum>=0)p.eventAttributeCountdownMaximum=state.AttributeMaximum;
                TimerRepairs++;
            }
        }
        if(timers!=null&&timers.Length==state.BuffTimers.Length)
            for(int i=0;i<timers.Length;i++)
                if(Math.Abs(timers[i]-state.BuffTimers[i])>.35f)
                {timers[i]=PlantStateRepairPolicy.BuffValue(i,state.BuffTimers[i],timers[i]);BuffRepairs++;}
        RepairHealth(p,state);
        // No Animator.Play, action-state setter or target replacement here.
        // Divergence remains visible to QA instead of replaying a local attack.
    }
    public static void Tick()
    {
        if(!Sync.Running||!Session.BattleLive)return;
        if(Sync.IsHostRunning)
        {
            if(Time.unscaledTime<nextSend)return;nextSend=Time.unscaledTime+.2f;
            var states=new List<string>();
            foreach(var p in Plants())
            {
                if(p==null||p.WasCollected)continue;
                int id=PlantIdentity.GetOrAssignHostId(p);if(id<1)continue;
                if(!Ready(p,id,hostBorn))continue;
                var state=Capture(p,id);if(state!=null)states.Add(state.Encode());
            }
            Party.Send("plant_state",string.Join(";",states));return;
        }
        foreach(var pair in pending.ToArray())
        {
            if(Time.unscaledTime>pair.Value.Until){pending.Remove(pair.Key);UnmappedExpired++;continue;}
            if(!PlantIdentity.TryGet(pair.Key,out var p)||!Ready(p,pair.Key,guestBorn))continue;
            var state=pair.Value.State;
            // A queued pre-move frame cannot move/modify an actor at a new tile;
            // wait for the next host frame describing its canonical location.
            if((int)p.thePlantType!=state.Type||p.thePlantColumn!=state.Column||p.thePlantRow!=state.Row)continue;
            if(applied.TryGetValue(pair.Key,out long last)&&last>=state.Revision){pending.Remove(pair.Key);continue;}
            Apply(p,state);applied[pair.Key]=state.Revision;pending.Remove(pair.Key);
        }
    }
}
