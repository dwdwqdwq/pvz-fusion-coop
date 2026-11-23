namespace PvZCoop;
public sealed class NativeDeathGate
{
    public int Depth {get;private set;}
    public int Reason {get;private set;}
    public bool Failed {get;private set;}
    public bool HasPresentation {get;private set;}
    public void Enter(int reason){Depth++;Reason=reason;}
    public void Presented()=>HasPresentation=true;
    public void MarkFailed()=>Failed=true;
    public bool Exit(bool failed){Failed|=failed;if(Depth==0)return false;return --Depth==0;}
    public bool ShouldPublish(bool terminal)=>Depth==0&&!Failed&&!HasPresentation&&terminal;
    public bool ShouldPublishPresentation()=>Depth==0&&!Failed&&HasPresentation;
}
