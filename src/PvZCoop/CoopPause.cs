using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace PvZCoop;

public static class CoopPause
{
 public static bool Applying {get;private set;}
 public static bool IsPaused {get;private set;}
 static bool applied,manual,networkHold,remotePaused,resyncAfterHold;
 static int revision,received;
 static float resumeScale=1;
 static GameStatus resumeStatus=GameStatus.InGame;
 public static bool Active=>Session.BattleLive&&Sync.Running&&!Sync.ResultSent&&!Sync.ResultApplied;
 public static void Reset(){Applying=IsPaused=applied=manual=networkHold=remotePaused=resyncAfterHold=false;revision=received=0;resumeScale=1;resumeStatus=GameStatus.InGame;}
 public static bool Intercept(bool pause){
  if(Applying)return true;if(Sync.Running&&!Session.BattleLive)return false;if(!Active)return true;
  Request(pause);return false;
 }
 public static void Request(bool pause){
  if(!Active)return;
  if(Party.IsHost)SetHost(pause);else Party.Send("pause_request",pause?"1":"0");
 }
 public static bool NetworkHeld=>networkHold;
 public static void SetNetworkHold(bool hold){
  if(networkHold==hold)return;networkHold=hold;
  if(hold)resyncAfterHold=true;
  if(hold)Session.ShowToast("Waiting for connection...",6);
  if(Party.IsHost)Publish();else UpdateEffective(remotePaused||networkHold);
 }
 static void UpdateEffective(bool pause){
  if(pause&&!IsPaused){resumeScale=Time.timeScale>0?Time.timeScale:1;resumeStatus=GameAPP.theGameStatus==GameStatus.Pause?GameStatus.InGame:GameAPP.theGameStatus;}
  IsPaused=pause;
 }
 static void SetHost(bool pause){manual=pause;Publish();}
 static void Publish(){
  bool pause=manual||networkHold;bool changed=pause!=IsPaused;
  UpdateEffective(pause);if(changed)revision++;
  Party.Send("pause_state",revision+"|"+(pause?1:0)+"|"+resumeScale.ToString(CultureInfo.InvariantCulture)+"|"+(int)resumeStatus+"|"+(networkHold?1:0));
 }
 public static void Receive(string type,string payload){
  if(!Active)return;
  if(type=="pause_request"&&Party.IsHost){if(payload=="0"||payload=="1")SetHost(payload=="1");}
  else if(type=="pause_state"&&!Party.IsHost){var p=payload.Split('|');if((p.Length!=4&&p.Length!=5)||!int.TryParse(p[0],out int rev)||rev<received)return;received=rev;if(p.Length==5&&p[4]=="1")resyncAfterHold=true;remotePaused=p[1]=="1";UpdateEffective(remotePaused||networkHold);resumeScale=Mathf.Clamp(float.Parse(p[2],CultureInfo.InvariantCulture),.01f,10);resumeStatus=(GameStatus)int.Parse(p[3]);}
 }
 public static void Tick(){
  if(!Active||applied==IsPaused)return;
  Applying=true;
  try{
   if(IsPaused){
    if(PauseMenu.Instance==null||!PauseMenu.Instance.gameObject.activeInHierarchy){
     var ui=InGameUI.Instance;
     if(ui!=null)ui.PauseGame();else UIMgr.EnterPauseMenu();
    }
   }else{
    if(resyncAfterHold){Sync.RebaseZombieMotion();resyncAfterHold=false;}
    var ui=InGameUI.Instance;
    if(PauseMenu.Instance!=null&&PauseMenu.Instance.gameObject.activeInHierarchy)UIMgr.BackToGame();
    GameAPP.theGameStatus=resumeStatus;Time.timeScale=resumeScale;
    if(ui!=null){
     if(!ui.gameObject.activeSelf)ui.gameObject.SetActive(true);
     ui.OnBackEnter();
     if(ui.canvasGroup!=null){ui.canvasGroup.alpha=1;ui.canvasGroup.interactable=true;ui.canvasGroup.blocksRaycasts=true;}
    }
   }
   applied=IsPaused;
  }finally{Applying=false;}
 }
}
