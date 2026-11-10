using System;
using UnityEngine;
namespace PvZCoop;

// The host owns the wallet. A short planting lease serializes native spending,
// while the original Fusion placement code still decides cost, fusion and refunds.
public static class SharedSun
{
    sealed class Click {
        public CardUI Card; public int Kind; public int Column,Row;public float X,Y;public Vector3 Preview;
        public Click(Mouse m,CardUI card=null,int kind=0){Kind=kind;Card=card??m.theCardOnMouse;Column=m.theMouseColumn;Row=m.theMouseRow;X=m.theBoxXofMouse;Y=m.theBoxYofMouse;Preview=m.preview==null?Vector3.zero:m.preview.transform.position;}
    }
    static Click pending,queuedHost;
    static int nextToken,pendingToken,hostLease,clientBalance,published=int.MinValue,revision,lastRevision;
    static bool initialized,replaying,awaitingCommit;
    static DateTime deadline;
    public static bool CanReceiveIncome=>!Sync.Running||Party.IsHost||replaying||Session.Allow;
    public static bool Busy=>pending!=null||awaitingCommit||hostLease!=0;
    public static void Init(){Party.OnMessage+=Receive;}
    public static void Reset(){pending=queuedHost=null;pendingToken=hostLease=nextToken=revision=lastRevision=0;published=int.MinValue;initialized=replaying=awaitingCommit=false;}
    public static void Start(){Reset();initialized=true;clientBalance=Board.Instance==null?0:Board.Instance.theSun;if(Party.IsHost)Publish(0);}
    public static bool InterceptMouseClick(){var m=Mouse.Instance;if(!Sync.Running||replaying||!Input.GetMouseButtonDown(0)||m==null||m.theCardOnMouse==null||m.thePlantTypeOnMouse==PlantType.Nothing||m.theMouseColumn<0||m.theMouseRow<0)return true;return RequestPlanting();}
    public static bool RequestPlanting()
    {
        if(!Sync.Running||replaying)return true;var m=Mouse.Instance;if(m?.theCardOnMouse==null)return true;
        if(Party.IsHost){if(hostLease==0){if(Board.Instance.theSun<m.theCardOnMouse.theSeedCost){Session.ShowToast("Not enough shared sun.",2);return false;}return true;}if(queuedHost==null)queuedHost=new Click(m);return false;}
        if(!initialized||pending!=null||awaitingCommit)return false;
        pending=new Click(m);pendingToken=++nextToken;deadline=DateTime.UtcNow.AddSeconds(8);Party.Send("sun_request",pendingToken.ToString());return false;
    }
    public static bool SelectCard(CardUI card,bool inner)
    {
        if(!Sync.Running||replaying||card==null||Mouse.Instance==null)return true;
        var click=new Click(Mouse.Instance,card,inner?2:1);
        if(Party.IsHost){if(hostLease==0)return Board.Instance.theSun>=card.theSeedCost;if(queuedHost==null)queuedHost=click;return false;}
        if(!initialized||pending!=null||awaitingCommit)return false;
        pending=click;pendingToken=++nextToken;deadline=DateTime.UtcNow.AddSeconds(8);Party.Send("sun_request",pendingToken.ToString());return false;
    }
    static void Apply(int balance)
    {
        if(Board.Instance==null)return;
        Board.Instance.theSun=Math.Max(0,balance);
        var ui=InGameUI.Instance;if(ui!=null)ui.UpdateSunDisplay();
        Sync.PartnerSun=Board.Instance.theSun;
    }
    static int Add(int balance,int delta)=>(int)Math.Clamp((long)balance+delta,0,int.MaxValue);
    static void SetClient(int value,int rev){if(rev<lastRevision)return;lastRevision=rev;clientBalance=Math.Max(0,value);if(Board.Instance!=null)Apply(clientBalance);}
    static void Publish(int completed){if(!Sync.Running||Board.Instance==null)return;published=Math.Max(0,Board.Instance.theSun);Apply(published);Party.Send("sun_balance",(++revision)+"|"+published+"|"+completed);Sync.PartnerSun=published;}
    static void Execute(Click click)
    {
        var m=Mouse.Instance;if(m==null||click?.Card==null||click.Card.WasCollected||(click.Kind==0&&(m.theCardOnMouse==null||m.theCardOnMouse.Pointer!=click.Card.Pointer)))return;
        if(Board.Instance==null||Board.Instance.theSun<click.Card.theSeedCost){Session.ShowToast("Not enough shared sun.",2);return;}
        int row=m.theMouseRow,column=m.theMouseColumn;float x=m.theBoxXofMouse,y=m.theBoxYofMouse;var preview=m.preview;Vector3 position=preview==null?Vector3.zero:preview.transform.position;
        m.theMouseRow=click.Row;m.theMouseColumn=click.Column;m.theBoxXofMouse=click.X;m.theBoxYofMouse=click.Y;if(preview!=null)preview.transform.position=click.Preview;
        replaying=true;Sync.BeginAction();
        try{if(click.Kind==1)m.ClickOnCard(click.Card);else if(click.Kind==2)m.ClickCard(click.Card);else m.TryToSetPlantByCard();}
        finally{Sync.EndAction();replaying=false;m.theMouseRow=row;m.theMouseColumn=column;m.theBoxXofMouse=x;m.theBoxYofMouse=y;if(preview!=null&&!preview.WasCollected)preview.transform.position=position;}
    }
    static void Receive(string type,string payload)
    {
        if(type=="sun_request"&&Party.IsHost&&Sync.Running){if(!int.TryParse(payload,out int token)||token<=0)return;if(hostLease!=0){Party.Send("sun_denied",token.ToString());return;}hostLease=token;deadline=DateTime.UtcNow.AddSeconds(8);Party.Send("sun_grant",token+"|"+Board.Instance.theSun+"|"+revision);}
        else if(type=="sun_grant"&&!Party.IsHost){var p=payload.Split('|');if(p.Length!=3||!int.TryParse(p[0],out int token)||!int.TryParse(p[1],out int value)||!int.TryParse(p[2],out int rev))return;if(!Sync.Running||pending==null||token!=pendingToken){Party.Send("sun_commit",token+"|0");return;}SetClient(value,rev);var click=pending;pending=null;int before=Board.Instance.theSun;try{Execute(click);}finally{int delta=Board.Instance==null?0:Board.Instance.theSun-before;clientBalance=Board.Instance==null?before:Board.Instance.theSun;awaitingCommit=true;deadline=DateTime.UtcNow.AddSeconds(8);Party.Send("sun_commit",token+"|"+delta);}}
        else if(type=="sun_commit"&&Party.IsHost&&Sync.Running){var p=payload.Split('|');if(p.Length!=2||!int.TryParse(p[0],out int token)||!int.TryParse(p[1],out int delta)||token!=hostLease)return;Apply(Add(Board.Instance.theSun,delta));hostLease=0;Publish(token);}
        else if(type=="sun_balance"&&!Party.IsHost&&Sync.Running){var p=payload.Split('|');if(p.Length!=3||!int.TryParse(p[0],out int rev)||!int.TryParse(p[1],out int value)||!int.TryParse(p[2],out int completed))return;SetClient(value,rev);initialized=true;if(completed==pendingToken){awaitingCommit=false;pendingToken=0;}}
        else if(type=="sun_denied"&&!Party.IsHost&&int.TryParse(payload,out int denied)&&denied==pendingToken){pending=null;awaitingCommit=false;pendingToken=0;Session.ShowToast("Another planting is finishing. Try again.",2);}
    }
    public static void Tick()
    {
        if(!Sync.Running||Board.Instance==null)return;
        if(!initialized)Start();
        if((pending!=null||awaitingCommit||hostLease!=0)&&DateTime.UtcNow>deadline){Party.Leave();Session.ShowToast("Shared sun connection timed out. Return to menu and reconnect.",30);Time.timeScale=0;return;}
        if(Party.IsHost){if(hostLease==0&&queuedHost!=null){var click=queuedHost;queuedHost=null;Execute(click);}if(hostLease==0&&Board.Instance.theSun!=published)Publish(0);}
        else if(!replaying && Board.Instance.theSun!=clientBalance)Apply(clientBalance);
        Sync.PartnerSun=Board.Instance.theSun;
    }
    public static int BeforeReplica()=>Board.Instance==null?0:Board.Instance.theSun;
    public static void AfterReplica(int balance){if(Sync.Running&&Board.Instance!=null)Apply(balance);}
}
