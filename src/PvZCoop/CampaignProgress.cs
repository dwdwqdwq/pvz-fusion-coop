using System;
using System.Linq;

namespace PvZCoop;

public static class CampaignProgress
{
    public static void Init()=>Party.OnMessage+=Receive;
    public static string Encode()
    {
        if(Session.Level!=LevelType.Advanture||GameAPP.advLevelCompleted==null||GameAPP.advLevelCompleted.Length<2||GameAPP.advantureLevel<1||GameAPP.advantureLevel>45)return "";
        var flags=GameAPP.advLevelCompleted;
        return new CampaignSnapshot(Math.Max(1,GameAPP.advantureZhouMu),GameAPP.advantureLevel,
            Enumerable.Range(1,Math.Min(45,flags.Length-1)).Where(n=>flags[n]).ToArray()).Encode();
    }
    public static void SendNow(){if(Party.IsHost&&Party.PartnerPresent){string payload=Encode();if(payload.Length>0)Party.Send("campaign",payload);}}
    static void Receive(string type,string payload)
    {if(type=="campaign"&&!Party.IsHost&&Party.SenderId==1)Apply(payload);}
    public static void Apply(string payload)
    {
        if(Party.IsHost||!Party.InParty||!CampaignSnapshot.TryRead(payload,out var snapshot)||GameAPP.advLevelCompleted==null)return;
        var flags=GameAPP.advLevelCompleted;var values=flags.ToArray();int next=GameAPP.advantureLevel;
        if(!snapshot.Merge(values,Math.Max(1,GameAPP.advantureZhouMu),ref next))return;
        for(int i=1;i<values.Length;i++)if(values[i])flags[i]=true;
        GameAPP.advantureLevel=next;
        if(SaveInfo.Instance!=null)SaveInfo.Instance.SavePlayerData();
        Plugin.Logger.LogInfo("Co-op adventure progress merged; next level "+next);
    }
}
