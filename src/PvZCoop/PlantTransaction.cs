using System;
using System.Globalization;

namespace PvZCoop;

public sealed record PlantTransaction(Guid OperationId,int LeaseToken,int CardKey,PlantSpawnPacket Spawn,int Kind=0,int Direction=1)
{
    public string Encode()=>OperationId.ToString("N")+"|"+LeaseToken+"|"+CardKey+"|"+Kind+"|"+Direction+"|"+Spawn.Encode();
    public static bool TryRead(string text,out PlantTransaction request)
    {
        request=null;if(text==null||text.Length>1024)return false;
        var p=text.Split('|',6);
        if(p.Length!=6||!Guid.TryParseExact(p[0],"N",out var op)||op==Guid.Empty||
           !int.TryParse(p[1],out int token)||token<1||!int.TryParse(p[2],out int key)||key<0||
           !int.TryParse(p[3],out int kind)||kind<0||kind>2||!int.TryParse(p[4],out int direction)||
           !PlantSpawnPacket.TryRead(p[5],out var spawn)||spawn.EntityId!=0||spawn.Operation!=p[0]||spawn.Free)return false;
        request=new(op,token,key,spawn,kind,direction);return true;
    }
}

// PlantId=0 on success means a native card action consumed the card without
// creating a new lifetime (repair/eat/other native utility actions).
public sealed record PlantTransactionResult(Guid OperationId,bool Accepted,int PlantId,int Balance,long BalanceRevision,bool Picked=false)
{
    public string Encode()=>string.Join("|",OperationId.ToString("N"),Accepted?1:0,PlantId,Balance,BalanceRevision,Picked?1:0);
    public static bool TryRead(string text,out PlantTransactionResult result)
    {
        result=null;if(text==null||text.Length>256)return false;var p=text.Split('|');
        if(p.Length!=6||!Guid.TryParseExact(p[0],"N",out var op)||op==Guid.Empty||(p[1]!="0"&&p[1]!="1")||
           !int.TryParse(p[2],out int id)||id<0||!int.TryParse(p[3],out int balance)||balance<0||
           !long.TryParse(p[4],NumberStyles.Integer,CultureInfo.InvariantCulture,out long revision)||revision<0||
           (p[5]!="0"&&p[5]!="1")||(p[1]=="0"&&(id!=0||p[5]=="1")))return false;
        result=new(op,p[1]=="1",id,balance,revision,p[5]=="1");return true;
    }
}

// Pure protocol policy; no Unity or IL2CPP calls.
public static class PlantTransactionPolicy
{
    public static double LeaseSeconds(double rttSeconds)=>Math.Max(3,4*(double.IsFinite(rttSeconds)&&rttSeconds>=0?rttSeconds:.25));
}
