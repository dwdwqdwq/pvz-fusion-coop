using System.Collections.Generic;
using System.Linq;
namespace PvZCoop;
public static class TeamState
{
 public sealed class Player
 {
  public bool Ready,InLevel,Loaded;
  public List<(int type,int cost)> Deck=new();
  public List<int> Slots=new();
  public string Cooldowns="";
 }
 static readonly Dictionary<int,Player> players=new();
 public static Player For(int id){if(!players.TryGetValue(id,out var p)){p=new Player();players[id]=p;}return p;}
 public static bool AllReady=>Party.RemotePlayers.Length>0&&Party.RemotePlayers.All(id=>For(id).Ready&&For(id).InLevel);
 public static void Reset(){players.Clear();}
 public static void Clear()=>Reset();
 public static void Prune(){foreach(int id in players.Keys.Where(id=>!Party.Members.Contains(id)).ToArray())players.Remove(id);}
}
