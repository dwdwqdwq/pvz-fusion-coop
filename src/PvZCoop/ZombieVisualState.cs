using System;
using System.Globalization;

namespace PvZCoop;

// A revision orders reliable appearance events against coalesced movement snapshots.
public readonly record struct ZombieVisualState(long Revision, long Health, long MaxHealth,
    int Armor1Health, int Armor1Max, int Armor2Health, int Armor2Max,
    bool Armor1Exists, bool Armor1Active, bool Armor2Exists, bool Armor2Active,
    int Armor1Type, int Armor2Type, int Armor1Stage, int Armor2Stage,
    bool HandLost, bool HeadLost)
{
    public const int FieldCount = 17;
    public bool SameHealth(ZombieVisualState other) =>
        Health == other.Health && MaxHealth == other.MaxHealth &&
        Armor1Health == other.Armor1Health && Armor1Max == other.Armor1Max &&
        Armor2Health == other.Armor2Health && Armor2Max == other.Armor2Max &&
        Armor1Exists == other.Armor1Exists && Armor2Exists == other.Armor2Exists;
    public bool SameAppearance(ZombieVisualState other) =>
        Armor1Exists == other.Armor1Exists && Armor1Active == other.Armor1Active &&
        Armor2Exists == other.Armor2Exists && Armor2Active == other.Armor2Active &&
        Armor1Type == other.Armor1Type && Armor2Type == other.Armor2Type &&
        Armor1Stage == other.Armor1Stage && Armor2Stage == other.Armor2Stage &&
        HandLost == other.HandLost && HeadLost == other.HeadLost;

    public string Encode() => FormattableString.Invariant(
        $"{Revision},{Health},{MaxHealth},{Armor1Health},{Armor1Max},{Armor2Health},{Armor2Max},{B(Armor1Exists)},{B(Armor1Active)},{B(Armor2Exists)},{B(Armor2Active)},{Armor1Type},{Armor2Type},{Armor1Stage},{Armor2Stage},{B(HandLost)},{B(HeadLost)}");
    static int B(bool value) => value ? 1 : 0;

    public static bool TryRead(string[] fields, int offset, out ZombieVisualState state)
    {
        state = default;
        if (offset < 0 || fields.Length - offset != FieldCount) return false;
        var n = new long[FieldCount];
        for (int i = 0; i < n.Length; i++)
            if (!long.TryParse(fields[offset + i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i])) return false;
        if (n[0] < 1 || n[2] < 0) return false;
        for (int i = 3; i <= 14; i++) if (n[i] < int.MinValue || n[i] > int.MaxValue) return false;
        if (n[4] < 0 || n[6] < 0 || n[11] < 0 || n[11] > 1024 || n[12] < 0 || n[12] > 1024) return false;
        foreach (int i in new[] { 7, 8, 9, 10, 15, 16 }) if (n[i] != 0 && n[i] != 1) return false;
        if ((n[7] == 0 && n[8] != 0) || (n[9] == 0 && n[10] != 0)) return false;
        state = new(n[0], n[1], n[2], (int)n[3], (int)n[4], (int)n[5], (int)n[6],
            n[7] != 0, n[8] != 0, n[9] != 0, n[10] != 0,
            (int)n[11], (int)n[12], (int)n[13], (int)n[14], n[15] != 0, n[16] != 0);
        return true;
    }
}
