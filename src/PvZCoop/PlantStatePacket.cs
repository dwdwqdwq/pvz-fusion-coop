using System;
using System.Globalization;
using System.Linq;

namespace PvZCoop;

// Common native fields only. A state snapshot is never a spawn, move, attack,
// fusion or death command. Identity/lifecycle stay on the reliable channel.
public sealed record PlantStatePacket(long Revision,int Id,int Type,int Column,int Row,
    int Health,int MaxHealth,int Shield,int Status,int TargetZombie,
    float Attack,float Produce,float Attribute,float AttributeMaximum,float AttributeLast,
    int Animation,float Phase,float AnimationSpeed,float[] BuffTimers)
{
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public string Encode()=>string.Join("|",Revision,Id,Type,Column,Row,Health,MaxHealth,Shield,Status,TargetZombie,
        F(Attack),F(Produce),F(Attribute),F(AttributeMaximum),F(AttributeLast),Animation,F(Phase),F(AnimationSpeed),
        string.Join(",",BuffTimers.Select(F)));
    static string F(float value)=>value.ToString("R",Inv);
    public static bool TryRead(string text,out PlantStatePacket packet)
    {
        packet=null;if(string.IsNullOrEmpty(text)||text.Length>2048)return false;
        var p=text.Split('|');if(p.Length!=19||!long.TryParse(p[0],NumberStyles.None,Inv,out long revision)||revision<1)return false;
        int[] n=new int[10];int[] fields={1,2,3,4,5,6,7,8,9,15};
        for(int i=0;i<n.Length;i++)if(!int.TryParse(p[fields[i]],NumberStyles.Integer,Inv,out n[i]))return false;
        if(n[0]<1||n[1]<0||n[1]>1000000||n[2]<0||n[2]>63||n[3]<0||n[3]>31||
            n[4]<0||n[5]<0||n[6]<0||n[7]<0||n[7]>65535||n[8]<0)return false;
        float[] f=new float[7];int[] floats={10,11,12,13,14,16,17};
        for(int i=0;i<f.Length;i++)if(!float.TryParse(p[floats[i]],NumberStyles.Float,Inv,out f[i])||!float.IsFinite(f[i]))return false;
        if(f.Take(5).Any(v=>Math.Abs(v)>1000000)||f[6]<0||f[6]>100)return false;
        var values=p[18].Length==0?Array.Empty<string>():p[18].Split(',');if(values.Length>32)return false;
        var timers=new float[values.Length];
        for(int i=0;i<timers.Length;i++)if(!float.TryParse(values[i],NumberStyles.Float,Inv,out timers[i])||!float.IsFinite(timers[i])||timers[i]<0||timers[i]>1000000)return false;
        packet=new(revision,n[0],n[1],n[2],n[3],n[4],n[5],n[6],n[7],n[8],f[0],f[1],f[2],f[3],f[4],n[9],f[5],f[6],timers);
        return true;
    }
}

public static class PlantStateRepairPolicy
{
    // Do not cross action boundaries. Resetting an expired cooldown or forcing
    // completion can replay or cancel a native attack/AttributeEvent.
    public static bool RepairPositiveCycle(float local,float host,bool samePhase,float tolerance,out float value)
    {
        value=local;
        if(!samePhase||!float.IsFinite(local)||!float.IsFinite(host)||local<=0||host<=0||Math.Abs(local-host)<=tolerance)return false;
        value=host;return true;
    }
    public static float BuffValue(int index,float host,float local)
    {
        // Native timer3 expiry invokes the color-reset callback. Preserve its
        // natural crossing instead of replacing the timer by zero beforehand.
        return index==3&&host<=0&&local>0?.001f:Math.Max(0,host);
    }
}
