using System.Globalization;

namespace PvZCoop;

public sealed class LevelProgressSnapshot
{
    public float Value { get; }
    public int Flags { get; }
    public LevelProgressSnapshot(float value,int flags){Value=value;Flags=flags;}
    public string Encode()=>Value.ToString("R",CultureInfo.InvariantCulture)+"|"+Flags;
    public static bool TryRead(string value,string flags,out LevelProgressSnapshot snapshot)
    {
        snapshot=null;
        if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float v)||!float.IsFinite(v)||v<0||v>1||
            !int.TryParse(flags,out int n)||n<0||n>512)return false;
        snapshot=new LevelProgressSnapshot(v,n);return true;
    }
}
