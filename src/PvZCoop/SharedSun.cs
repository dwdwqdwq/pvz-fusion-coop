using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace PvZCoop;

// The host executes every paid guest action. A guest never proposes a wallet
// delta. The round-scoped journal owns idempotent decisions and cancellation.
public static class SharedSun
{
    sealed class Pending {public PlantTransaction Request;public double Deadline,Retry;public bool Granted,Cancel;}
    static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    static double Now=>clock.Elapsed.TotalSeconds;
    static readonly SunLeaseQueue leases=new();
    static readonly PlantOperationJournal journal=new();
    static readonly List<(int Player,string Type,string Body)> publications=new();
    static readonly Dictionary<(int,Guid),string> receipts=new();
    static Pending pending;
    static int nextToken,clientBalance,published=int.MinValue;
    static long revision,lastRevision;
    static bool initialized,recording,faulted;
    public static bool Busy=>pending!=null||leases.Busy||faulted;
    public static bool CanReceiveIncome=>!Sync.Running||Party.IsHost||Session.Allow;
    public static void Init()=>Party.OnMessage+=Receive;
    public static void Reset()
    {
        leases.Clear();journal.Clear();publications.Clear();receipts.Clear();pending=null;
        nextToken=0;revision=lastRevision=0;published=int.MinValue;initialized=recording=faulted=false;HostPlanting.Reset();
    }
    public static void Start(){Reset();initialized=true;clientBalance=Board.Instance==null?0:Board.Instance.theSun;if(Party.IsHost)Publish();}
    static bool Live(CardUI c)=>c!=null&&!c.WasCollected;
    static double Lease(int player)=>PlantTransactionPolicy.LeaseSeconds(Party.EstimatedRttSeconds(player));
    public static bool InterceptMouseClick()
    {
        var m=Mouse.Instance;
        return !Sync.Running||HostPlanting.ExecutionActive||!Input.GetMouseButtonDown(0)||m==null||
            m.theCardOnMouse==null||m.thePlantTypeOnMouse==PlantType.Nothing||m.theMouseColumn<0||m.theMouseRow<0||RequestPlanting();
    }
    public static bool RequestPlanting()
    {
        if(!Sync.Running||HostPlanting.ExecutionActive)return true;
        var m=Mouse.Instance;return m==null||!Live(m.theCardOnMouse)||Request(m.theCardOnMouse,0);
    }
    public static bool SelectCard(CardUI card,bool inner)=>!Sync.Running||HostPlanting.ExecutionActive||!Live(card)||Request(card,inner?2:1);
    static bool Request(CardUI card,int kind)
    {
        if(!Session.BattleLive||CoopPause.IsPaused||faulted)return false;
        if(Party.IsHost)return !leases.Busy&&Board.Instance.theSun>=card.theSeedCost;
        if(!initialized||pending!=null||nextToken==int.MaxValue)return false;
        var mouse=Mouse.Instance;if(mouse==null)return false;
        var op=Guid.NewGuid();int c=mouse.theMouseColumn,r=mouse.theMouseRow;
        if(kind!=0){c=Math.Clamp(c,0,Board.Instance.columnNum-1);r=Math.Clamp(r,0,Board.Instance.rowNum-1);}
        var spawn=new PlantSpawnPacket(c,r,(int)card.thePlantType,false,true,0,0,op.ToString("N"),0);
        pending=new(){Request=new(op,++nextToken,DeckSharing.Key(card),spawn,kind,(int)mouse.plantTowards),Deadline=Now+Lease(1)};
        Retry();return false;
    }
    static void Apply(int value)
    {
        if(Board.Instance==null)return;Board.Instance.theSun=Math.Max(0,value);
        if(InGameUI.Instance!=null)InGameUI.Instance.UpdateSunDisplay();Sync.PartnerSun=Board.Instance.theSun;
    }
    static void SetClient(int value,long rev){if(rev<lastRevision)return;lastRevision=rev;clientBalance=Math.Max(0,value);Apply(clientBalance);}
    static void Publish(){published=Math.Max(0,Board.Instance.theSun);Apply(published);Party.Send("sun_balance",(++revision)+"|"+published);}
    public static bool BufferOutgoing(int player,string type,string body)
    {
        if(!recording)return false;publications.Add((player,type,body));return true;
    }
    static void Flush()
    {
        var batch=publications.ToArray();publications.Clear();
        foreach(var x in batch)if(x.Player==0)Party.Send(x.Type,x.Body);else Party.SendTo(x.Player,x.Type,x.Body);
    }
    static string CardReceipt(int player,int key)
    {
        var card=HostPlanting.GetCard(player,key);if(!Live(card))return "";
        return FormattableString.Invariant($"{key}|{card.CD:R}|{card.fullCD:R}|{card.theSeedCost}|{card.usedTimes}|{card.Durability}");
    }
    static void Reply(PlantOperationJournal.Entry entry)
    {
        if(entry?.Result==null)return;
        string receipt=receipts.TryGetValue((entry.Player,entry.Operation),out var saved)?saved:"";
        if(entry.Request!=null){string current=CardReceipt(entry.Player,entry.Request.CardKey);if(current.Length>0)receipt=current;}
        Party.SendTo(entry.Player,"sun_result",entry.Result.Encode()+"~"+receipt);
    }
    static void Grant(PlantOperationJournal.Entry e)=>Party.SendTo(e.Player,"sun_grant",e.Token+"|"+e.Operation.ToString("N")+"|"+Board.Instance.theSun+"|"+revision);
    static void Admit(int player,PlantTransaction request)
    {
        if(!journal.Request(player,request,Now+Lease(player),out var e))
        {
            // Capacity never evicts tombstones. Unknown intents therefore can
            // never be granted this round; acknowledge rejection so UI unlocks.
            if(!journal.TryGet(player,request.OperationId,out _))
                Party.SendTo(player,"sun_result",new PlantTransactionResult(request.OperationId,false,0,Math.Max(0,Board.Instance.theSun),revision).Encode()+"~");
            return;
        }
        if(e.Phase==PlantOperationJournal.State.Complete){Reply(e);return;}
        if(e.Phase==PlantOperationJournal.State.Granted){Grant(e);return;}
        if(e.Phase==PlantOperationJournal.State.Executing)return;
        if(faulted||!Session.BattleLive||CoopPause.IsPaused||!TeamState.For(player).Slots.Contains(request.CardKey))
        {Cancel(player,request.LeaseToken,request.OperationId);return;}
        if(!leases.Request(player,e.Token,e.Operation,e.Expires)&&leases.Owner==player&&leases.Operation!=e.Operation)Cancel(player,e.Token,e.Operation);
    }
    static void Cancel(int player,int token,Guid op)
    {
        if(!journal.Cancel(player,token,op,Math.Max(0,Board.Instance.theSun),revision,out var e))
        {
            if(token>0&&op!=Guid.Empty&&!journal.TryGet(player,op,out _))
                Party.SendTo(player,"sun_result",new PlantTransactionResult(op,false,0,Math.Max(0,Board.Instance.theSun),revision).Encode()+"~");
            return;
        }
        leases.Cancel(player,token,op);Reply(e);
    }
    static void Place(int player,PlantTransaction request)
    {
        if(journal.TryGet(player,request.OperationId,out var old)&&old.Phase==PlantOperationJournal.State.Complete){Reply(old);return;}
        if(faulted||!leases.Matches(player,request.LeaseToken,request.OperationId,Now)||!journal.Begin(player,request,Now,out var e))return;
        try
        {
            var card=HostPlanting.GetCard(player,request.CardKey);
            if(!Live(card)||(int)card.thePlantType!=request.Spawn.Type)
            {
                if(!journal.Finish(e,false,0,Math.Max(0,Board.Instance.theSun),revision))throw new InvalidOperationException("Cannot reject invalid card intent.");
                leases.Complete(player,request.LeaseToken);Reply(e);return;
            }
            publications.Clear();recording=true;HostPlanting.Outcome outcome;
            try{outcome=HostPlanting.Execute(player,request.CardKey,request.Spawn.Column,request.Spawn.Row,request.Kind,request.Direction);}
            finally{recording=false;}
            bool accepted=outcome.Consumed||outcome.Picked;
            if(!journal.Finish(e,accepted,accepted?outcome.PlantId:0,Math.Max(0,Board.Instance.theSun),++revision,outcome.Picked))throw new InvalidOperationException("Cannot commit native action.");
            receipts[(player,e.Operation)]=CardReceipt(player,request.CardKey);
            leases.Complete(player,request.LeaseToken);Flush();Publish();Reply(e);
        }
        catch(Exception error)
        {
            // A partial native mutation cannot be refunded safely. Keep its
            // executing identity and pause instead of claiming rollback.
            recording=false;faulted=true;
            Plugin.Logger.LogError("Indeterminate native operation "+e.Operation+" buffered="+publications.Count+": "+error);
            CoopPause.Request(true);Party.Send("sun_fault",e.Operation.ToString("N"));Session.ShowToast("Planting could not be confirmed. Restart this level.",30);
        }
    }
    static void Retry()
    {
        if(pending==null)return;pending.Retry=Now+.5;
        if(pending.Cancel)Party.Send("sun_cancel",pending.Request.LeaseToken+"|"+pending.Request.OperationId.ToString("N"));
        else Party.Send(pending.Granted?"sun_place":"sun_request",pending.Request.Encode());
    }
    static CardUI LocalCard(int key)=>InGameUI.Instance?.Cards?.ToArray().FirstOrDefault(c=>Live(c)&&DeckSharing.Key(c)==key);
    static void ApplyCard(CardUI card,string body)
    {
        if(!Live(card))return;var p=body.Split('|');if(p.Length!=6||!int.TryParse(p[0],out int key)||key!=DeckSharing.Key(card)||
            !float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float cd)||!float.IsFinite(cd)||cd<0||
            !float.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float full)||!float.IsFinite(full)||full<0||
            !int.TryParse(p[3],out int cost)||cost<0||!int.TryParse(p[4],out int uses)||uses<0||!int.TryParse(p[5],out int durability)||durability< -1)return;
        card.CD=cd;card.fullCD=full;card.theSeedCost=cost;card.usedTimes=uses;card.Durability=durability;
    }
    static void PickUpUI(CardUI card)
    {
        if(!Live(card)||Mouse.Instance==null)return;var m=Mouse.Instance;
        // Replay only the stock UI tail, not ClickOnCard's paid dispatch again.
        if(Live(m.theCardOnMouse))m.theCardOnMouse.PutDown();m.ClearItemOnMouse(true);m.mouseItemType=(MouseItemType)1;
        if(!card.PickUp())return;m.theCardOnMouse=card;m.thePlantTypeOnMouse=card.thePlantType;m.CreatePlantOnMouse(card.thePlantType);
    }
    static void Receive(string type,string payload)
    {
        if(!Sync.Running||Board.Instance==null)return;
        if(Party.IsHost)
        {
            int player=Party.SenderId;if(player<2||player>4)return;
            if(type=="sun_request"&&PlantTransaction.TryRead(payload,out var request))Admit(player,request);
            else if(type=="sun_place"&&PlantTransaction.TryRead(payload,out var placement))Place(player,placement);
            else if(type=="sun_cancel"){var p=payload.Split('|');if(p.Length==2&&int.TryParse(p[0],out int token)&&Guid.TryParseExact(p[1],"N",out var op))Cancel(player,token,op);}
            return;
        }
        if(Party.SenderId!=1)return;
        if(type=="sun_balance")
        {var p=payload.Split('|');if(p.Length==2&&long.TryParse(p[0],out long rev)&&rev>=0&&int.TryParse(p[1],out int balance)&&balance>=0){SetClient(balance,rev);initialized=true;}}
        else if(type=="sun_grant")
        {
            var p=payload.Split('|');if(p.Length!=4||!int.TryParse(p[0],out int token)||!Guid.TryParseExact(p[1],"N",out var op)||!int.TryParse(p[2],out int balance)||balance<0||!long.TryParse(p[3],out long rev)||rev<0)return;
            SetClient(balance,rev);if(pending==null||pending.Request.OperationId!=op||pending.Request.LeaseToken!=token)return;
            if(Now>=pending.Deadline)pending.Cancel=true;pending.Granted=true;Retry();
        }
        else if(type=="sun_result")
        {
            int cut=payload.IndexOf('~');if(cut<0||!PlantTransactionResult.TryRead(payload[..cut],out var result))return;SetClient(result.Balance,result.BalanceRevision);
            if(pending==null||pending.Request.OperationId!=result.OperationId)return;
            var done=pending;pending=null;var card=LocalCard(done.Request.CardKey);ApplyCard(card,payload[(cut+1)..]);
            if(result.Accepted)
            {
                if(result.Picked)PickUpUI(card);
                else if(done.Request.Kind==0&&Mouse.Instance!=null&&Live(Mouse.Instance.theCardOnMouse)&&DeckSharing.Key(Mouse.Instance.theCardOnMouse)==done.Request.CardKey)
                {Mouse.Instance.theCardOnMouse.PutDown();Mouse.Instance.ClearItemOnMouse(true);}
            }
            else Session.ShowToast("The host could not complete that card action. Try again.",3);
        }
        else if(type=="sun_fault"){faulted=true;Session.ShowToast("Planting could not be confirmed. Restart this level.",30);}
    }
    public static void Tick()
    {
        if(!Sync.Running||Board.Instance==null)return;if(!initialized)Start();
        if(Party.IsHost)
        {
            if(faulted){if(!CoopPause.IsPaused)CoopPause.Request(true);return;}
            HostPlanting.Tick();if(Board.Instance.theSun!=published)Publish();leases.Expire(Now);
            foreach(var e in journal.Expire(Now,Math.Max(0,Board.Instance.theSun),revision)){leases.Cancel(e.Player,e.Token,e.Operation);Reply(e);}
            if(leases.GrantNext(Now))
            {if(journal.TryGet(leases.Owner,leases.Operation,out var e)&&journal.Grant(e,Now,leases.Expires))Grant(e);else leases.Complete(leases.Owner,leases.Token);}
        }
        else
        {
            if(pending!=null&&!faulted){if(Now>=pending.Deadline)pending.Cancel=true;if(Now>=pending.Retry)Retry();}
            if(Board.Instance.theSun!=clientBalance)Apply(clientBalance);
        }
        Sync.PartnerSun=Board.Instance.theSun;
    }
    public static int BeforeReplica()=>Board.Instance==null?0:Board.Instance.theSun;
    public static void AfterReplica(int balance){if(Sync.Running&&Board.Instance!=null)Apply(balance);}
}
