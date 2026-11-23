using System;
using System.Globalization;

namespace PvZCoop;

public sealed record MineStatePacket(long Revision,int Id,int Type,float Remaining,float Maximum,bool Ready)
{
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public string Encode()=>string.Join(",",Revision,Id,Type,Remaining.ToString("R",Inv),Maximum.ToString("R",Inv),Ready?1:0);
    public static bool TryRead(string text,out MineStatePacket packet)
    {
        packet=null;var p=text.Split(',');
        if(p.Length!=6||!long.TryParse(p[0],out long revision)||revision<1||!int.TryParse(p[1],out int id)||id<1||
           !int.TryParse(p[2],out int type)||type<0||
           !float.TryParse(p[3],NumberStyles.Float,Inv,out float remaining)||!float.IsFinite(remaining)||remaining<0||remaining>1000000||
           !float.TryParse(p[4],NumberStyles.Float,Inv,out float maximum)||!float.IsFinite(maximum)||maximum<0||maximum>1000000||
           (p[5]!="0"&&p[5]!="1"))return false;
        packet=new(revision,id,type,remaining,maximum,p[5]=="1");return true;
    }
}
