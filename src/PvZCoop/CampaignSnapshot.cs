using System;
using System.Globalization;
using System.Linq;

namespace PvZCoop;

// Only earned adventure flags are merged. A teammate's better progress is kept.
public sealed class CampaignSnapshot
{
    public int Cycle { get; }
    public int NextLevel { get; }
    public int[] Completed { get; }
    public CampaignSnapshot(int cycle,int nextLevel,int[] completed)
    { Cycle=cycle;NextLevel=nextLevel;Completed=completed; }
    public string Encode()=>Cycle+";"+NextLevel+";"+string.Join(",",Completed);
    public static bool TryRead(string text,out CampaignSnapshot snapshot)
    {
        snapshot=null;if(string.IsNullOrEmpty(text)||text.Length>1024)return false;
        var p=text.Split(';');
        if(p.Length!=3||!int.TryParse(p[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out int cycle)||cycle<1||cycle>100000||
            !int.TryParse(p[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int next)||next<1||next>45)return false;
        var entries=p[2].Split(',',StringSplitOptions.RemoveEmptyEntries);
        if(entries.Length>45)return false;
        var levels=new int[entries.Length];
        for(int i=0;i<entries.Length;i++)if(!int.TryParse(entries[i],out levels[i])||levels[i]<1||levels[i]>45)return false;
        snapshot=new CampaignSnapshot(cycle,next,levels.Distinct().OrderBy(n=>n).ToArray());return true;
    }
    public bool Merge(bool[] completed,int cycle,ref int nextLevel)
    {
        bool changed=false;
        foreach(int level in Completed)if(level<completed.Length&&!completed[level]){completed[level]=true;changed=true;}
        if(cycle==Cycle&&NextLevel>nextLevel&&Completed.Contains(NextLevel-1)){nextLevel=NextLevel;changed=true;}
        return changed;
    }
}
