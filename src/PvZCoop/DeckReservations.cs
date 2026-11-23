using System;
using System.Collections.Generic;
using System.Linq;
namespace PvZCoop;
// Host-only ledger. First accepted real slot wins; a normal and extra card
// are separate keys. No price heuristics or invented copies.
public sealed class DeckReservations
{
 public readonly Dictionary<int,HashSet<int>> Slots=new();
 public readonly Dictionary<int,int> Revisions=new();
 public bool Offer(int player,int revision,IEnumerable<int> requested)
 {
  if(player<1||player>4||revision<0)throw new ArgumentOutOfRangeException();
  if(Revisions.TryGetValue(player,out int previous)&&revision<previous)return false;
  var mine=new HashSet<int>(requested);if(mine.Count>128||mine.Any(k=>k<0))throw new FormatException("Invalid slots");
  foreach(var p in Slots)if(p.Key!=player)mine.ExceptWith(p.Value);
  bool changed=!Slots.TryGetValue(player,out var old)||!old.SetEquals(mine);
  Slots[player]=mine;Revisions[player]=revision;return changed;
 }
 public HashSet<int> Others(int player)=>new(Slots.Where(p=>p.Key!=player).SelectMany(p=>p.Value));
 public void Prune(IEnumerable<int> members){var set=new HashSet<int>(members);foreach(int id in Slots.Keys.Where(id=>!set.Contains(id)).ToArray()){Slots.Remove(id);Revisions.Remove(id);}}
 public void Clear(){Slots.Clear();Revisions.Clear();}
}
