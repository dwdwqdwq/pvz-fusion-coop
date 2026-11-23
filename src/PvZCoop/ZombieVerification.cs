using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PvZCoop;

// Managed-only request bookkeeping. The caller supplies monotonic, unscaled
// time; no Unity objects, native callbacks or worker-thread mutations here.
public sealed class ZombieVerificationTracker
{
    sealed record Pending(long Nonce,double RetryAt);
    readonly Dictionary<int,Pending> pending=new();
    readonly Dictionary<(int Player,int Id),double> hostRequests=new();
    long nextNonce;
    public void Reset(){pending.Clear();hostRequests.Clear();nextNonce=0;}
    public void Forget(int id)=>pending.Remove(id);
    public bool TryRequest(int id,double now,out long nonce)
    {
        nonce=0;if(id<=0||!double.IsFinite(now))return false;
        if(pending.TryGetValue(id,out var old))
        {
            if(now<old.RetryAt)return false;
            nonce=old.Nonce;
        }
        else
        {
            if(pending.Count>=1024||nextNonce==long.MaxValue)return false;
            nonce=++nextNonce;
        }
        pending[id]=new(nonce,now+2);return true;
    }
    public bool Complete(int id,long nonce)
    {
        if(!pending.TryGetValue(id,out var old)||nonce!=old.Nonce)return false;
        pending.Remove(id);return true;
    }
    public bool AllowHostRequest(int player,int id,double now)
    {
        if(player<2||player>4||id<=0||!double.IsFinite(now))return false;
        var key=(player,id);
        if(hostRequests.TryGetValue(key,out double before)&&now-before<1)return false;
        if(hostRequests.Count>=2048)
        {
            foreach(var stale in hostRequests.Where(p=>now-p.Value>10).Select(p=>p.Key).ToArray())hostRequests.Remove(stale);
            if(hostRequests.Count>=2048&&!hostRequests.ContainsKey(key))return false;
        }
        hostRequests[key]=now;return true;
    }
}

public static class ZombieVerification
{
    // Native Zombie.Die sets Dying before removal. Health can be zero during
    // a rejected Die / boss phase; its final status is the authority.
    public static bool NativeTerminal(bool exists,bool dying)=>!exists||dying;
    public static bool FreshSnapshot(float stamp,float previous)=>float.IsFinite(stamp)&&stamp>previous;
    public static string Request(int id,long nonce)=>FormattableString.Invariant($"{id}|{nonce}");
    public static string Reply(int id,long nonce,bool alive)=>Request(id,nonce)+"|"+(alive?"1":"0");
    public static bool TryRequest(string text,out int id,out long nonce)
    {
        id=0;nonce=0;if(text==null||text.Length>64)return false;
        var p=text.Split('|');return p.Length==2&&
            int.TryParse(p[0],NumberStyles.None,CultureInfo.InvariantCulture,out id)&&id>0&&
            long.TryParse(p[1],NumberStyles.None,CultureInfo.InvariantCulture,out nonce)&&nonce>0;
    }
    public static bool TryReply(string text,out int id,out long nonce,out bool alive)
    {
        id=0;nonce=0;alive=false;if(text==null||text.Length>66)return false;
        int cut=text.LastIndexOf('|');if(cut<1||cut!=text.Length-2)return false;
        if(text[^1]!='0'&&text[^1]!='1')return false;
        alive=text[^1]=='1';return TryRequest(text[..cut],out id,out nonce);
    }
    // Native object existence/status matters: zero HP alone can be a boss
    // phase transition. null means do not issue a terminal confirmation yet.
    public static bool? HostVerdict(int id,int nextIssuedId,bool terminal,bool nativeExists,bool dying,bool positiveHealth)
    {
        if(id<=0||id>=nextIssuedId)return null;
        if(terminal||!nativeExists||dying)return false;
        return positiveHealth?true:null;
    }
}
