namespace PvZCoop;

// The round envelope rejects old-round messages; this gate rejects duplicates
// within the current round and prevents reward collection before victory.
public sealed class RoundCompletion
{
    public bool Won { get; private set; }
    public bool Collected { get; private set; }
    public void Reset(){Won=Collected=false;}
    public bool Win(){if(Won)return false;Won=true;return true;}
    public bool Collect(){if(!Won||Collected)return false;Collected=true;return true;}
}
