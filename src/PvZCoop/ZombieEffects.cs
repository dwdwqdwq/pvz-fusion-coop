using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Native effects own their original ice/butter sprites, colors and cleanup.
// Only the host decides which effects exist; guest damage remains blocked.
public static class ZombieEffects
{
    sealed class Replica
    {
        public Zombie Zombie; public int Born; public long Received, Applied;
        public ZombieEffectState State; public bool ErrorLogged;
    }
    static readonly Dictionary<int, Replica> replicas = new();
    static readonly Dictionary<int, ZombieEffectState> published = new();
    public static bool Applying { get; private set; }
    public static void Init() => Party.OnMessage += Receive;
    public static void Reset() { replicas.Clear(); published.Clear(); Applying = false; }
    public static void Register(int id, Zombie z) { if (!Party.IsHost) replicas[id] = new() { Zombie = z, Born = Time.frameCount }; }
    public static void Forget(int id) { replicas.Remove(id); published.Remove(id); }
    public static bool AllowLocal(Zombie z, EffectType type) => Applying || !Sync.IsClient || z == null || !Sync.IsSynced(z) || !ZombieEffectState.Types.Contains((int)type);

    public static ZombieEffectState Capture(Zombie z)
    {
        var list = new List<TimedZombieEffect>();
        foreach (int type in ZombieEffectState.Types)
        {
            if (z.effects == null || !z.effects.TryGetValue((EffectType)type, out var effect) || effect == null) continue;
            float remaining = 0, value = 1;
            switch ((EffectType)type)
            {
                case EffectType.Cold: var cold = effect.TryCast<ColdEffect>(); if (cold != null) { remaining = cold.duration; value = cold.coldSpeed; } break;
                case EffectType.Freeze: var ice = effect.TryCast<FreezeEffect>(); if (ice != null) remaining = ice.duration; break;
                case EffectType.Poison: var poison = effect.TryCast<PoisonEffect>(); if (poison != null) remaining = poison.duration; break;
                case EffectType.Butter: var butter = effect.TryCast<ButterEffect>(); if (butter != null) { remaining = butter.duration; value = butter.showButter ? 0 : 1; } break;
            }
            if (float.IsFinite(remaining) && remaining > 0 && float.IsFinite(value)) list.Add(new(type, remaining, value));
        }
        return new(Math.Max(0, z.freezeLevel), Math.Max(0, z.poisonLevel), list.ToArray());
    }
    public static void Publish(int id, long revision, ZombieEffectState state)
    {
        if (!Sync.IsHostRunning) return;
        if ((!published.TryGetValue(id, out var old) && state.Effects.Length > 0) || (old != null && !state.SameAppearance(old)))
            Party.Send("zf", id + "|" + revision + "|" + state.Encode());
        published[id] = state;
    }
    public static void HostChanged(Zombie z, EffectType type)
    {
        if (!Sync.IsHostRunning || z == null || z.WasCollected || !ZombieEffectState.Types.Contains((int)type) || !Sync.TryZombieId(z, out int id)) return;
        var revision = ZombieVisuals.Capture(z).Revision;
        Publish(id, revision, Capture(z));
    }
    static void Receive(string type, string payload)
    {
        if (type != "zf" || !Sync.IsClient || Party.SenderId != 1) return;
        var p = payload.Split('|');
        if (p.Length == 3 && int.TryParse(p[0], out int id) && id > 0 && long.TryParse(p[1], out long revision) &&
            ZombieEffectState.TryRead(p[2], out var state)) Accept(id, revision, state);
    }
    public static void Accept(int id, long revision, ZombieEffectState state)
    {
        if (Sync.IsZombieTerminal(id) || !Sync.TryGetLivingZombie(id,out _) ||
            !replicas.TryGetValue(id, out var replica) || revision <= replica.Received || state == null) return;
        replica.Received = revision; replica.State = state;
    }
    public static void Tick()
    {
        if (!Sync.IsClient) return;
        foreach (var entry in replicas.ToArray())
        {
            var replica = entry.Value; var z = replica.Zombie;
            if (z == null || z.WasCollected || Sync.IsZombieTerminal(entry.Key) ||
                !Sync.TryGetZombie(entry.Key,out var registered) || registered.Pointer != z.Pointer)
            { Forget(entry.Key); continue; }
            // A native nonterminal zero-HP phase can recover; keep its replica
            // registration, but do not apply a status while it is not living.
            if (!Sync.TryGetLivingZombie(entry.Key,out _)) continue;
            if (!z.isActiveAndEnabled || Time.frameCount <= replica.Born + 1 || replica.State == null || replica.Applied == replica.Received) continue;
            Applying = true;
            try { Apply(z, replica.State); replica.Applied = replica.Received; }
            catch (Exception ex) { if (!replica.ErrorLogged) { replica.ErrorLogged = true; Plugin.Logger.LogWarning("Zombie effects: " + ex); } }
            finally { Applying = false; }
        }
    }
    static void Apply(Zombie z, ZombieEffectState state)
    {
        foreach (int type in ZombieEffectState.Types)
            if (!state.Effects.Any(e => e.Type == type) && z.HasBuff((EffectType)type)) z.RemoveBuff((EffectType)type);
        foreach (var item in state.Effects)
        {
            var type = (EffectType)item.Type;
            // A butter packet can intentionally hide its sprite; rebuild only if that option changes.
            if (type == EffectType.Butter && z.effects.TryGetValue(type, out var current) && current.TryCast<ButterEffect>() is { } oldButter && oldButter.showButter != (item.Value == 0)) z.RemoveBuff(type);
            if (!z.HasBuff(type)) EffectManager.SetEffect(z, type, item.Remaining, item.Value);
            if (!z.effects.TryGetValue(type, out var effect) || effect == null) continue;
            switch (type)
            {
                case EffectType.Cold: var cold = effect.TryCast<ColdEffect>(); if (cold != null) { cold.duration = item.Remaining; cold.coldSpeed = item.Value; z.coldSpeed = item.Value; } break;
                case EffectType.Freeze: var ice = effect.TryCast<FreezeEffect>(); if (ice != null) ice.duration = item.Remaining; break;
                case EffectType.Poison: var poison = effect.TryCast<PoisonEffect>(); if (poison != null) poison.duration = item.Remaining; break;
                case EffectType.Butter: var butter = effect.TryCast<ButterEffect>(); if (butter != null) butter.duration = item.Remaining; break;
            }
        }
        z.freezeLevel = state.FreezeLevel; z.poisonLevel = state.PoisonLevel;
    }
}
