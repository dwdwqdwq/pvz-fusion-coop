using System;
using System.Globalization;
using System.Linq;

namespace PvZCoop;

public readonly record struct TimedZombieEffect(int Type, float Remaining, float Value);
public sealed record ZombieEffectState(int FreezeLevel, int PoisonLevel, TimedZombieEffect[] Effects)
{
    public static readonly int[] Types = { 0, 2, 4, 6 }; // cold, freeze, poison, butter
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public string Encode() => FreezeLevel + ":" + PoisonLevel + "/" + string.Join("~", Effects.OrderBy(e => e.Type)
        .Select(e => e.Type + ":" + e.Remaining.ToString("R", Inv) + ":" + e.Value.ToString("R", Inv)));
    public bool SameAppearance(ZombieEffectState other) => other != null && FreezeLevel == other.FreezeLevel && PoisonLevel == other.PoisonLevel &&
        Effects.OrderBy(e => e.Type).Select(e => (e.Type, e.Value)).SequenceEqual(other.Effects.OrderBy(e => e.Type).Select(e => (e.Type, e.Value)));
    public static bool TryRead(string payload, out ZombieEffectState state)
    {
        state = null;
        if (payload == null || payload.Length > 1024) return false;
        var p = payload.Split('/'); if (p.Length != 2) return false;
        var levels = p[0].Split(':');
        if (levels.Length != 2 || !int.TryParse(levels[0], out int freeze) || !int.TryParse(levels[1], out int poison) || freeze < 0 || poison < 0) return false;
        var entries = p[1].Length == 0 ? Array.Empty<string>() : p[1].Split('~'); if (entries.Length > Types.Length) return false;
        var effects = new TimedZombieEffect[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i].Split(':');
            if (e.Length != 3 || !int.TryParse(e[0], out int type) || !Types.Contains(type) ||
                !float.TryParse(e[1], NumberStyles.Float, Inv, out float remaining) || !float.IsFinite(remaining) || remaining <= 0 ||
                !float.TryParse(e[2], NumberStyles.Float, Inv, out float value) || !float.IsFinite(value) ||
                effects.Take(i).Any(old => old.Type == type)) return false;
            if (type == 6 && value != 0 && value != 1) return false;
            effects[i] = new(type, remaining, value);
        }
        state = new(freeze, poison, effects); return true;
    }
}
