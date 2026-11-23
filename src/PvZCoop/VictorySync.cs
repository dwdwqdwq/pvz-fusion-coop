using System;
using System.Collections.Generic;
using System.Linq;
using GameLevel;
using UnityEngine;

namespace PvZCoop;

public static class VictorySync
{
    static readonly RoundCompletion completion=new();
    static readonly Dictionary<IntPtr,int> started=new();
    static bool pending,replayingCollection;
    public static bool Won=>completion.Won;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset(){completion.Reset();started.Clear();pending=replayingCollection=false;}
    public static void HostResult(bool win){if(win)completion.Win();}

    public static void ApplyHostWin()
    {
        if(!completion.Win()||Party.IsHost||Board.Instance==null)return;
        var board=Board.Instance;
        // BoardVictory.Win() is a manual award shortcut. Natural OnUpdate also
        // invokes the level-specific callback and OnBoardWin. Replay that same
        // approved side-effect path without running independent guest checks.
        board.over=true;
        var callback=board.boardVicotry?.Victiory;
        if(callback!=null)callback.Invoke();else Lawnf.SetAward(board,Vector2.zero,false,false);
        if(LevelManager.TryGetLevelData(out var data)&&data!=null)data.OnBoardWin(board);
        if(board.boardStatistics!=null)board.boardStatistics.GameOver(GameResult.Victory);
    }

    public static void PrizeStarted(PrizeMgr prize)
    {
        if(Session.Active&&prize!=null&&!prize.fake)started[prize.Pointer]=Time.frameCount;
    }
    static bool IsCurrent(PrizeMgr prize)=>prize!=null&&!prize.fake&&Board.Instance!=null&&
        prize.GetComponentInParent<Board>()?.Pointer==Board.Instance.Pointer;
    public static bool BeforeCollect(PrizeMgr prize,out bool publishProgress)
    {
        publishProgress=false;
        if(!Session.Active||!Session.Started||!Party.PartnerPresent||!IsCurrent(prize))return true;
        if(replayingCollection)return true;
        if(!Won)return false;
        if(!Party.IsHost){if(!completion.Collected)Party.Send("prize_request","");return false;}
        if(!completion.Collect())return false;
        Party.Send("prize_collect","");publishProgress=true;return true;
    }
    public static bool AllowGoBack(PrizeMgr prize)=>!Sync.GuestRound||!Won||!IsCurrent(prize);
    static void Receive(string type,string payload)
    {
        if(!Session.Active||!Session.Started)return;
        if(type=="prize_request"&&Party.IsHost&&Won&&!completion.Collected)pending=true;
        else if(type=="prize_collect"&&!Party.IsHost&&Party.SenderId==1&&Won&&!completion.Collected)pending=true;
    }
    public static void Tick()
    {
        if(!pending||!Won||completion.Collected||!Session.Active||Board.Instance==null)return;
        var prize=UnityEngine.Object.FindObjectsOfType<PrizeMgr>().FirstOrDefault(p=>IsCurrent(p)&&!p.isClicked&&
            started.TryGetValue(p.Pointer,out int frame)&&Time.frameCount>frame);
        if(prize==null)return;
        pending=false;
        if(Party.IsHost){prize.Clicked();return;}
        if(!completion.Collect())return;
        replayingCollection=true;bool previous=Session.Allow;Session.Allow=true;
        try{prize.Clicked();}finally{Session.Allow=previous;replayingCollection=false;}
    }
}
