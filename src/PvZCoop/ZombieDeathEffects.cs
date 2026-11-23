using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;
using Il2CppInterop.Common;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace PvZCoop;

// Replay native presentation, not the plant's attack/explosion or guest damage.
public static class ZombieDeathEffects
{
    public sealed class Call { public Zombie Zombie;public int Id,Kind,Row;public Vector3 Position; }
    public sealed class DeathCall {public Zombie Zombie;public int Id,Row;public Vector3 Position;public ZombieVisualState Before;public Exception Error;public Call Presentation;public readonly NativeDeathGate Gate=new();}
    static readonly Dictionary<int,(Vector3 position,int row)> terminalPoses=new();
    static readonly Dictionary<int,DeathCall> dying=new();
    static int replayDepth;static IntPtr replayTarget,removalTarget;static bool failed;
    public static bool Replaying=>replayDepth>0;
    public static bool IsReplayTarget(Zombie zombie)=>Replaying&&zombie!=null&&zombie.Pointer==replayTarget;
    public static bool IsRemovalTarget(Plant plant)=>Replaying&&plant!=null&&plant.Pointer==removalTarget;
    static bool GuestReplay=>Replaying&&Sync.IsClient;
    sealed class ReplayLease:IDisposable {
        readonly IntPtr previous,previousPlant;bool disposed;
        public ReplayLease(Zombie target,Plant plant=null){previous=replayTarget;previousPlant=removalTarget;replayTarget=target==null?IntPtr.Zero:target.Pointer;removalTarget=plant==null?IntPtr.Zero:plant.Pointer;replayDepth++;}
        public void Dispose(){if(disposed)return;disposed=true;replayDepth--;replayTarget=previous;removalTarget=previousPlant;}
    }
    public static IDisposable ReplayScope(Zombie target)=>new ReplayLease(target);
    public static IDisposable PlantRemovalScope(Plant target)=>new ReplayLease(null,target);
    public static unsafe void RemoveReplicaPlant(Plant plant,Plant.DieReason reason)
    {
        if(plant==null||plant.WasCollected)return;
        if(!Sync.IsClient){plant.Die(reason);return;}
        using(PlantRemovalScope(plant))
        {
            // Generated virtual wrappers resolve an override before invocation.
            // Resolve the base native method explicitly for replica cleanup so
            // derived Die overrides cannot execute an independent explosion.
            var field=typeof(Plant).GetField("NativeMethodInfoPtr_Die_Public_Virtual_New_Void_DieReason_0",BindingFlags.Static|BindingFlags.NonPublic);
            if(field==null)throw new MissingFieldException("Native Plant.Die cleanup method is unavailable");
            var method=(IntPtr)field.GetValue(null);int value=(int)reason;
            void** args=stackalloc void*[1];args[0]=&value;IntPtr error=IntPtr.Zero;
            IL2CPP.il2cpp_runtime_invoke(method,plant.Pointer,args,ref error);
            Il2CppException.RaiseExceptionIfNecessary(error);
        }
    }
    static readonly Dictionary<int,Call> active=new();
    static readonly HashSet<IntPtr> installedNative=new();
    static long gameAssemblyBase;
    // Fusion 4.1 native inspection: tiny forwarding Die/Charred/damage bodies
    // are covered by their substantial shared/base destinations. The bare
    // BodyTakeDamage setter is eleven bytes; detouring it can overwrite its
    // neighbour. These RVAs are deliberately not installation targets.
    static readonly HashSet<long> unsafeSmallBodies=new(){
        0x3ED8E0,0x677ED0,0x678780,0x67C9D0,0x6A0A50,0x6A9CB0,
        0x6B8D70,0x6C9790,0x6CECC0,0x6F8970,0x674CC0,0x675430,
        0x678A10,0x6891B0,0x688ED0,0x690920,0x690AF0,0x6A50D0,
        0x6A9D70,0x6AAD90,0x6AD970,0x6B4000,0x6B3660,0x6B72B0,
        0x6C86E0,0x6F6C90,0x6FB5D0,0x6FCCC0,0x6FF770,0x72DD70
    };
    static readonly Dictionary<int,(Zombie zombie,int kind,int frame)> pending=new();
    public static bool Capturing(int id)=>active.ContainsKey(id)||dying.ContainsKey(id);
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset(){active.Clear();pending.Clear();dying.Clear();terminalPoses.Clear();failed=false;}
    public static void PublishTerminalPose(int id,Vector3 position,int row)
    {
        if(!Sync.IsHostRunning)return;
        Party.Send("terminalpose",id+"|"+position.x.ToString("R",CultureInfo.InvariantCulture)+"|"+position.y.ToString("R",CultureInfo.InvariantCulture)+"|"+position.z.ToString("R",CultureInfo.InvariantCulture)+"|"+row);
    }
    public static void ApplyTerminalPose(int id,Zombie zombie)
    {
        if(zombie==null||zombie.WasCollected||!terminalPoses.Remove(id,out var pose))return;
        zombie.theZombieRow=pose.row;
        zombie.transform.position=pose.position;
        if(zombie.rb!=null){zombie.rb.position=new Vector2(pose.position.x,pose.position.y);zombie.rb.velocity=Vector2.zero;zombie.rb.angularVelocity=0;}
    }
    public static void Install(Harmony harmony)
    {
        gameAssemblyBase=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Single(m=>m.ModuleName.Equals("GameAssembly.dll",StringComparison.OrdinalIgnoreCase)).BaseAddress.ToInt64();
        installedNative.Clear();
        Plugin.Logger.LogInfo("Native death install: scanning zombie types");
        int hooks=0;
        foreach(var type in typeof(Zombie).Assembly.GetTypes().Where(t=>!t.ContainsGenericParameters&&(t==typeof(Zombie)||t.IsSubclassOf(typeof(Zombie)))))
        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .Where(m=>!m.IsAbstract&&!m.ContainsGenericParameters&&m.Name=="Die"&&m.ReturnType==typeof(void)&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(int)))
        {Patch(harmony,method,nameof(DiePrefix),nameof(DieFinalizer));hooks++;}
        Patch(harmony,AccessTools.Method(typeof(Zombie),"SetCarred"),nameof(BurnPrefix),nameof(BurnFinalizer));
        var damageNames=new HashSet<string>{"TakeDamage","BodyTakeDamage","FirstArmorTakeDamage","SecondArmorTakeDamage","ApplyDamage"};
        foreach(string name in damageNames)Guard(harmony,typeof(Zombie),name,nameof(DamageGuard));
        foreach(var type in typeof(Zombie).Assembly.GetTypes().Where(t=>!t.ContainsGenericParameters&&(t==typeof(Zombie)||t.IsSubclassOf(typeof(Zombie)))))
        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(m=>!m.IsAbstract&&!m.ContainsGenericParameters&&((m.Name=="Charred"&&m.GetParameters().Length==4)||(type!=typeof(Zombie)&&damageNames.Contains(m.Name)))))
            Patch(harmony,method,nameof(DamageGuard));
        foreach(string name in new[]{"DropItem","DropKilledAward","DropGardenPlant"})
            Guard(harmony,typeof(Zombie),name,nameof(WorldGuard));
        // Do not detour every plant override. IL2CPP folds empty callbacks into
        // a shared return stub; patching one changes unrelated native methods.
        // Replica removal invokes the substantial base Die directly. The known
        // ContactMine callback is the shared live explosion path for hybrids.
        Guard(harmony,typeof(Plant),"Die",nameof(PlantDieGuard));
        Guard(harmony,typeof(Plant),"TakeDamage",nameof(WorldGuard));
        Guard(harmony,typeof(ContactMine),"DieEvent",nameof(WorldGuard));
        Patch(harmony,AccessTools.Method(typeof(CreatePlant),"SetPlant"),nameof(PlantSpawnGuard));
        Patch(harmony,AccessTools.Method(typeof(CreateZombie),"SetZombie"),nameof(ZombieSpawnGuard));
        Patch(harmony,AccessTools.Method(typeof(Board),"GetSun"),nameof(WorldGuard));
        Patch(harmony,AccessTools.Method(typeof(CreateBullet),"SetBullet"),nameof(BulletSpawnGuard));
        Plugin.Logger.LogInfo("Native zombie death hooks: "+hooks+" base/derived Die plus shared SetCarred");
    }
    static void Guard(Harmony harmony,Type type,string name,string prefix)
    {
        var method=AccessTools.Method(type,name);
        if(method==null){Plugin.Logger.LogWarning("Native death optional guard unavailable: "+type.Name+"."+name);return;}
        Patch(harmony,method,prefix);
    }
    static void Patch(Harmony harmony,MethodInfo method,string prefix,string finalizer=null)
    {
        if(method==null)throw new MissingMethodException("Required native death hook is unavailable: "+prefix);
        string label=method.DeclaringType.FullName+"."+method.Name+"("+string.Join(",",method.GetParameters().Select(p=>p.ParameterType.Name))+")";
        var field=Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method);
        if(field==null)throw new MissingFieldException("Native death hook pointer unavailable: "+label);
        var info=(IntPtr)field.GetValue(null);
        var native=Marshal.ReadIntPtr(info);
        long rva=native.ToInt64()-gameAssemblyBase;
        byte first=Marshal.ReadByte(native);
        if(unsafeSmallBodies.Contains(rva)||first==0xC3||first==0xC2){Plugin.Logger.LogInfo("Native death hook skipped small/shared body: "+label+" RVA="+rva.ToString("X"));return;}
        if(!installedNative.Add(native)){Plugin.Logger.LogInfo("Native death hook skipped alias: "+label+" RVA="+rva.ToString("X"));return;}
        Plugin.Logger.LogInfo("Native death hook begin: "+label+" -> "+prefix);
        harmony.Patch(method,prefix:new HarmonyMethod(typeof(ZombieDeathEffects),prefix),finalizer:finalizer==null?null:new HarmonyMethod(typeof(ZombieDeathEffects),finalizer));
        Plugin.Logger.LogInfo("Native death hook ready: "+label);
    }
    static bool WorldGuard()=>!GuestReplay;
    static bool PlantDieGuard(Plant __instance)=>!GuestReplay||IsRemovalTarget(__instance);
    static bool DamageGuard(Zombie __instance)=>!Sync.IsClient||IsReplayTarget(__instance)||(!Replaying&&!Sync.IsSynced(__instance));
    static bool PlantSpawnGuard(ref Plant __result){if(!GuestReplay)return true;__result=null;return false;}
    static bool ZombieSpawnGuard(ref Zombie __result){if(!GuestReplay)return true;__result=null;return false;}
    static bool BulletSpawnGuard(ref Bullet __result){if(!GuestReplay)return true;__result=null;return false;}
    static bool DiePrefix(Zombie __instance,int __0,out DeathCall __state)
    {
        __state=null;
        if(GuestReplay&&__instance.Pointer!=replayTarget)return false;
        if(Sync.IsClient&&Sync.IsSynced(__instance)&&!IsReplayTarget(__instance))return false;
        if(!Sync.IsHostRunning||!Sync.TryZombieId(__instance,out int id)||active.ContainsKey(id))return true;
        if(Sync.CapturingCharred(id)){Sync.OnZombieDie(__instance,__0);return true;}
        if(!dying.TryGetValue(id,out var call)){call=new DeathCall{Zombie=__instance,Id=id,Before=ZombieVisuals.Capture(__instance),Position=__instance.transform.position,Row=__instance.theZombieRow};dying[id]=call;}
        call.Gate.Enter(__0);__state=call;return true;
    }
    static void DieFinalizer(DeathCall __state,Exception __exception)
    {
        if(__state==null)return;
        __state.Error ??= __exception;
        if(!__state.Gate.Exit(__exception!=null))return;
        dying.Remove(__state.Id);var gate=__state.Gate;
        if(gate.Failed){Fail("native.death",__state.Error);return;}
        // A nested SetCarred has returned, but the surrounding derived/base
        // Die chain must also succeed before publishing its terminal effect.
        if(gate.ShouldPublishPresentation()&&__state.Presentation!=null)
        {PublishPresentation(__state.Presentation);return;}
        var z=__state.Zombie;bool gone=z==null||z.WasCollected;
        // Boss overrides may reject death or enter another phase. Invocation
        // itself is not terminal; zero HP alone is not a death certificate.
        if(!gate.ShouldPublish(gone||z.theStatus==ZombieStatus.Dying))return;
        if(!gone&&(!Sync.TryZombieId(z,out int current)||current!=__state.Id))return;
        ZombieVisuals.Publish(__state.Id,gone?__state.Before:ZombieVisuals.Capture(z),final:true);
        PublishTerminalPose(__state.Id,__state.Position,__state.Row);
        Party.Send("zd",__state.Id+"|"+gate.Reason);Sync.CompletePresentationDeath(__state.Id,z);
    }
    static bool BurnPrefix(Zombie __instance,out Call __state)
    {
        __state=null;if(GuestReplay&&__instance.Pointer!=replayTarget)return false;
        if(Sync.IsClient&&Sync.IsSynced(__instance)&&!IsReplayTarget(__instance))return false;
        __state=Begin(__instance,1);return true;
    }
    static void BurnFinalizer(Call __state,Exception __exception)=>Finish(__state,__exception);
    public static Call Begin(Zombie zombie,int kind)
    {
        if(!Sync.IsHostRunning||zombie==null||!Sync.TryZombieId(zombie,out int id)||active.ContainsKey(id)||Sync.CapturingCharred(id))return null;
        var call=new Call{Zombie=zombie,Id=id,Kind=kind,Position=zombie.transform.position,Row=zombie.theZombieRow};active[id]=call;return call;
    }
    public static void Finish(Call call,Exception error)
    {
        if(call==null)return;active.Remove(call.Id);
        dying.TryGetValue(call.Id,out var outer);
        if(error!=null)
        {
            if(outer!=null){outer.Error??=error;outer.Gate.MarkFailed();}
            else Fail("native.death.effect",error);
            return;
        }
        if(outer!=null)
        {
            outer.Gate.Presented();outer.Presentation??=call;
            return;
        }
        PublishPresentation(call);
    }
    static void PublishPresentation(Call call)
    {
        PublishTerminalPose(call.Id,call.Position,call.Row);
        Party.Send("zfx",call.Id+"|"+call.Kind);
        Sync.CompletePresentationDeath(call.Id,call.Zombie);
    }
    static void Receive(string type,string payload)
    {
        if(type=="terminalpose")
        {
            if(!Sync.IsClient||Party.SenderId!=1)return;
            var pose=payload.Split('|');
            if(pose.Length!=5||!int.TryParse(pose[0],out int poseId)||poseId<1||!int.TryParse(pose[4],out int row)||row<0||row>16)return;
            if(!float.TryParse(pose[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float x)||!float.IsFinite(x)||
               !float.TryParse(pose[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float y)||!float.IsFinite(y)||
               !float.TryParse(pose[3],NumberStyles.Float,CultureInfo.InvariantCulture,out float z)||!float.IsFinite(z))return;
            terminalPoses[poseId]=(new Vector3(x,y,z),row);return;
        }
        if(type!="zfx"||!Sync.IsClient||Party.SenderId!=1)return;
        var p=payload.Split('|');
        if(p.Length!=2||!int.TryParse(p[0],out int id)||!int.TryParse(p[1],out int kind)||kind!=1||
            !Sync.ReservePresentationDeath(id,out var zombie))return;
        pending[id]=(zombie,kind,Time.frameCount+2);
    }
    public static void Tick()
    {
        if(failed){failed=false;bool allow=Session.Allow;Session.Allow=true;try{Session.LeaveLevel();Party.Leave();Time.timeScale=1;Session.ShowToast("Could not replay the host's native zombie death. Reconnect before continuing.",20);}finally{Session.Allow=allow;}return;}
        foreach(var pair in pending.ToArray())
        {
            if(Time.frameCount<pair.Value.frame)continue;
            pending.Remove(pair.Key);var zombie=pair.Value.zombie;
            if(zombie==null||zombie.WasCollected)continue;
            bool session=Session.Allow;Session.Allow=true;
            using(ReplayScope(zombie))
            try{ApplyTerminalPose(pair.Key,zombie);zombie.SetCarred();Sync.CompletePresentationDeath(pair.Key,zombie);}
            catch(Exception error){Fail("replay.death.effect",error);}
            finally{Session.Allow=session;}
        }
    }
    static void Fail(string scope,Exception error){Plugin.Logger.LogError(scope+": "+error);failed=true;}
}
