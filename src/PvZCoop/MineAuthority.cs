using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace PvZCoop;

// Contact callbacks still run on replica colliders. They must not consume a
// guest mine or execute a second explosion, including during native glove
// placement replay (where Session.Allow is intentionally true).
public static class MineAuthority
{
    public static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(PotatoMine),"PotatoUpdate"),prefix:new HarmonyMethod(typeof(MineAuthority),nameof(HostMaturity)));
        int hooks=0;
        foreach(var type in typeof(Plant).Assembly.GetTypes().Where(t=>!t.ContainsGenericParameters))
        {
            bool potato=type==typeof(PotatoMine)||type.IsSubclassOf(typeof(PotatoMine));
            bool contact=type==typeof(ContactMine)||type.IsSubclassOf(typeof(ContactMine));
            if(!potato&&!contact)continue;
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
            {
                if(method.IsAbstract||method.ContainsGenericParameters||method.ReturnType!=typeof(void))continue;
                bool guarded=potato&&method.Name=="Explode"&&method.GetParameters().Length==0;
                guarded|=contact&&method.Name=="TryDetonate"&&method.GetParameters().Length==1;
                guarded|=contact&&method.Name=="ExplodeOnContact"&&method.GetParameters().Length==0;
                if(!guarded)continue;
                harmony.Patch(method,prefix:new HarmonyMethod(typeof(MineAuthority),nameof(HostOnly)));
                hooks++;
            }
        }
        Plugin.Logger.LogInfo("Host-authoritative native mine hooks: "+hooks);
    }

    static bool HostMaturity(PotatoMine __instance)=>MineStateSync.CanRunNativeUpdate(__instance);
    static bool HostOnly()=>!Sync.IsClient;
}
