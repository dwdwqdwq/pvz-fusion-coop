using System;
using System.Globalization;

namespace PvZCoop;

public sealed record GloveMovePacket(string Operation,int SourceColumn,int SourceRow,int SourceType,int Ordinal,
    int TargetColumn,int TargetRow,int TargetType,int Health,bool MineReady,int EntityId=0,int Creator=0,string SourceOperation="")
{
    public string Encode()=>string.Join("|",Operation,SourceColumn,SourceRow,SourceType,Ordinal,
        TargetColumn,TargetRow,TargetType,Health,MineReady?1:0,EntityId,Creator,SourceOperation);
    public static bool TryRead(string payload,out GloveMovePacket packet)
    {
        packet=null;if(string.IsNullOrEmpty(payload)||payload.Length>512)return false;
        var p=payload.Split('|');if((p.Length!=10&&p.Length!=13)||!Guid.TryParseExact(p[0],"N",out _))return false;
        var n=new int[9];for(int i=0;i<n.Length;i++)if(!int.TryParse(p[i+1],NumberStyles.Integer,CultureInfo.InvariantCulture,out n[i]))return false;
        if(n[0]<0||n[0]>63||n[1]<0||n[1]>31||n[2]<0||n[2]>1000000||n[3]<0||n[3]>31||
           n[4]<0||n[4]>63||n[5]<0||n[5]>31||n[6]<0||n[6]>1000000||n[7]<0||(n[8]!=0&&n[8]!=1))return false;
        int id=0,creator=0;string source="";
        if(p.Length==13){
            if(!int.TryParse(p[10],NumberStyles.Integer,CultureInfo.InvariantCulture,out id)||id<0||
               !int.TryParse(p[11],NumberStyles.Integer,CultureInfo.InvariantCulture,out creator)||creator<0||creator>4)return false;
            source=p[12];if((creator==0&&source.Length!=0)||(creator!=0&&!Guid.TryParseExact(source,"N",out _)))return false;
        }
        packet=new(p[0],n[0],n[1],n[2],n[3],n[4],n[5],n[6],n[7],n[8]==1,id,creator,source);return true;
    }
}
