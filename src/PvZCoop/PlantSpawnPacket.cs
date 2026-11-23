using System;
using System.Globalization;

namespace PvZCoop;
public sealed record PlantSpawnPacket(int Column,int Row,int Type,bool Free,bool Effect,float X,float Y,string Operation,int EntityId)
{
    static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
    public string Encode()=>string.Join("|",Column,Row,Type,Free?1:0,Effect?1:0,X.ToString("R",Inv),Y.ToString("R",Inv),Operation,EntityId);
    public static bool TryRead(string body,out PlantSpawnPacket packet)
    {
        packet=null;if(string.IsNullOrEmpty(body)||body.Length>512)return false;
        var p=body.Split('|');if(p.Length!=9)return false;
        if(!int.TryParse(p[0],out int c)||!int.TryParse(p[1],out int r)||!int.TryParse(p[2],out int t)||
           c<0||c>63||r<0||r>31||t<0||t>1000000||(p[3]!="0"&&p[3]!="1")||(p[4]!="0"&&p[4]!="1")||
           !float.TryParse(p[5],NumberStyles.Float,Inv,out float x)||!float.IsFinite(x)||
           !float.TryParse(p[6],NumberStyles.Float,Inv,out float y)||!float.IsFinite(y)||
           !Guid.TryParseExact(p[7],"N",out _)||!int.TryParse(p[8],out int id)||id<0)return false;
        packet=new(c,r,t,p[3]=="1",p[4]=="1",x,y,p[7],id);return true;
    }
}
