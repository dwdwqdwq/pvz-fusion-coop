using System;
namespace PvZCoop;
// Only the host advances waves. Guests replay the host's native warning event.
public static class WaveSync {
 static bool replay;
 static LevelProgressSnapshot progress;
 public static void ResetProgress()=>progress=null;
 public static string CaptureProgress(bool completed=false){
  var p=LevelProgress.Instance;
  return p?.slider==null?"-1|-1":new LevelProgressSnapshot(completed?1:p.slider.normalizedValue,p.configuredFlagCount).Encode();
 }
 public static void ReadProgress(string[] fields){if(fields.Length>=12&&LevelProgressSnapshot.TryRead(fields[10],fields[11],out var value))progress=value;}
 public static void ApplyProgress(LevelProgress ui){
  if(!Sync.GuestRound||progress==null||ui==null||ui.slider==null||ui.board==null||Board.Instance==null||ui.board.Pointer!=Board.Instance.Pointer)return;
  if(ui.configuredFlagCount!=progress.Flags&&ui.layout!=null&&ui.sampleFlag!=null)ui.RebuildFlags(progress.Flags);
  ui.slider.normalizedValue=progress.Value;
  if(ui.flags!=null)ui.FlagUpdate();
 }
 public static bool HugeWave(int wave){
  if(!Session.Active)return true;
  if(!Session.BattleLive)return false;
  if(Sync.GuestRound)return replay;
  Party.Send("huge_wave",wave.ToString());return true;
 }
 public static void Receive(string type,string payload){
  if(!Session.BattleLive||Board.Instance==null)return;
  if(type=="next_wave_request"&&Party.IsHost){if(payload=="toggle")Board.Instance.ToggleAutoNextWave();else Board.Instance.RequestNextWave();return;}
  if(type!="huge_wave"||!Sync.GuestRound||!int.TryParse(payload,out int wave))return;
  replay=true;try{Board.Instance.HugeWaveEvent(wave);}finally{replay=false;}
 }
}
