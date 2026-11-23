using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Replay only Fusion's presentation helpers. Damage, head-loss gameplay callbacks,
// native health decay and the decision to die remain authoritative on the host.
public static class ZombieVisuals
{
    sealed class Replica
    {
        public Zombie Zombie;
        public int Born;
        public ZombieVisualState? Desired;
        public long Revision;
        public int? Death;
        public bool ErrorLogged;
        public bool CaptionErrorLogged;
        public IntPtr CaptionPointer;
        public ZombieVisualState? CaptionState;
        public string Caption;
        public float CaptionRetryAt;
    }
    static readonly Dictionary<int, Replica> replicas = new();
    static readonly Dictionary<int, ZombieVisualState> published = new();
    static long revision;

    public static void Init() => Party.OnMessage += Receive;
    public static void Reset() { replicas.Clear(); published.Clear(); revision = 0; ZombieEffects.Reset(); }
    public static void Register(int id, Zombie z)
    {
        ZombieEffects.Register(id,z);
        if (!Party.IsHost) replicas[id] = new Replica { Zombie = z, Born = Time.frameCount };
    }
    public static void Forget(int id) { replicas.Remove(id); published.Remove(id); ZombieEffects.Forget(id); }

    public static ZombieVisualState Capture(Zombie z) => new(++revision, z.theHealth, z.theMaxHealth,
        z.theFirstArmorHealth, z.theFirstArmorMaxHealth, z.theSecondArmorHealth, z.theSecondArmorMaxHealth,
        z.theFirstArmor != null, z.theFirstArmor != null && z.theFirstArmor.activeSelf,
        z.theSecondArmor != null, z.theSecondArmor != null && z.theSecondArmor.activeSelf,
        (int)z.theFirstArmorType, (int)z.theSecondArmorType, z.theFirstArmorBroken, z.theSecondArmorBroken,
        z.loseHand, z.beforeDying);

    public static void Publish(int id, ZombieVisualState state, bool final = false)
    {
        if (!Sync.IsHostRunning) return;
        if (final || !published.TryGetValue(id, out var previous) || !state.SameAppearance(previous))
            Party.Send("zv", id + "|" + state.Encode());
        published[id] = state;
    }
    public static void HostChanged(Zombie z)
    {
        if (Sync.IsHostRunning && z != null && !z.WasCollected && Sync.TryZombieId(z, out int id)) Publish(id, Capture(z));
    }
    static void Receive(string type, string payload)
    {
        if (type != "zv" || !Sync.IsClient || Party.SenderId != 1) return;
        int cut = payload.IndexOf('|');
        if (cut < 1 || !int.TryParse(payload[..cut], out int id) || id < 1 ||
            !ZombieVisualState.TryRead(payload[(cut + 1)..].Split(','), 0, out var state)) return;
        Accept(id, state);
    }
    public static void Accept(int id, ZombieVisualState state)
    {
        if (!replicas.TryGetValue(id, out var replica) || replica.Death.HasValue || state.Revision <= replica.Revision) return;
        replica.Revision = state.Revision;
        replica.Desired = state;
        if (Ready(replica)) ApplySafely(replica);
    }
    static bool Ready(Replica replica) => replica.Zombie != null && !replica.Zombie.WasCollected &&
        replica.Zombie.isActiveAndEnabled && Time.frameCount > replica.Born + 1;

    // Give a newly spawned native object time to finish Start before detaching parts.
    // A terminal event must not overtake its final appearance or resurrect from an old snapshot.
    public static bool DeferDeath(int id, int reason)
    {
        if (!replicas.TryGetValue(id, out var replica)) return false;
        replica.Death = reason;
        return true;
    }
    public static void Tick()
    {
        if (!Sync.IsClient) return;
        foreach (var entry in replicas.ToArray())
        {
            var replica = entry.Value;
            if (replica.Zombie == null || replica.Zombie.WasCollected) { Forget(entry.Key); continue; }
            if (!Ready(replica)) continue;
            if (replica.Desired.HasValue) ApplySafely(replica);
            if (replica.Death.HasValue) Sync.KillLocal(entry.Key, replica.Death.Value);
        }
    }

    static void ApplySafely(Replica replica)
    {
        try { Apply(replica); }
        catch (Exception ex)
        {
            if (!replica.ErrorLogged) { replica.ErrorLogged = true; Plugin.Logger.LogWarning("Zombie presentation: " + ex); }
        }
        finally { RefreshCaptionSafely(replica); }
    }

    static void RefreshCaptionSafely(Replica replica)
    {
        try
        {
            var z = replica.Zombie;
            var text = z.healthText;
            if (text == null || !text.isActiveAndEnabled || !text.gameObject.activeInHierarchy)
            {
                // Native UpdateHealthText skips inactive labels. Do not record a
                // display update until there is an actual visible label to draw.
                replica.CaptionState = null; replica.CaptionPointer = IntPtr.Zero;
                return;
            }
            var state = replica.Desired.Value;
            bool dirty = !replica.CaptionState.HasValue || replica.CaptionPointer != text.Pointer ||
                !state.SameHealth(replica.CaptionState.Value) || text.text != replica.Caption;
            if (!dirty && Time.unscaledTime < replica.CaptionRetryAt) return;
            // Data equality does not imply that TMP rendered those values.
            // Keep this independent of armor/limb particles, and periodically
            // repair native render-cache invalidation without rebuilding each frame.
            z.UpdateHealthText();
            text.havePropertiesChanged = true;
            text.SetVerticesDirty();
            text.ForceMeshUpdate(false, true);
            replica.CaptionPointer = text.Pointer; replica.CaptionState = state;
            replica.Caption = text.text; replica.CaptionRetryAt = Time.unscaledTime + .5f;
        }
        catch (Exception ex)
        {
            if (!replica.CaptionErrorLogged) { replica.CaptionErrorLogged = true; Plugin.Logger.LogWarning("Zombie HP caption: " + ex); }
        }
    }

    static void Apply(Replica replica)
    {
        var z = replica.Zombie; var state = replica.Desired.Value;
        bool firstChanged = z.theFirstArmorHealth != state.Armor1Health || z.theFirstArmorMaxHealth != state.Armor1Max || z.theFirstArmorBroken != state.Armor1Stage;
        bool secondChanged = z.theSecondArmorHealth != state.Armor2Health || z.theSecondArmorMaxHealth != state.Armor2Max || z.theSecondArmorBroken != state.Armor2Stage;
        z.theHealth = state.Health; z.theMaxHealth = state.MaxHealth;
        z.theFirstArmorHealth = state.Armor1Health; z.theFirstArmorMaxHealth = state.Armor1Max;
        z.theSecondArmorHealth = state.Armor2Health; z.theSecondArmorMaxHealth = state.Armor2Max;

        if (z.theFirstArmor != null)
        {
            if (!state.Armor1Exists)
            {
                try { z.FirstArmorFall(); }
                finally
                {
                    if (z.theFirstArmor != null) UnityEngine.Object.Destroy(z.theFirstArmor);
                    z.theFirstArmor = null;
                }
            }
            else
            {
                if (firstChanged) z.FirstArmorBroken();
                if (z.theFirstArmor != null) z.theFirstArmor.SetActive(state.Armor1Active);
            }
        }
        if (z.theSecondArmor != null)
        {
            if (!state.Armor2Exists)
            {
                try { z.SecondArmorFall(); }
                finally
                {
                    if (z.theSecondArmor != null) UnityEngine.Object.Destroy(z.theSecondArmor);
                    z.theSecondArmor = null;
                }
            }
            else
            {
                if (secondChanged) z.SecondArmorBroken();
                if (z.theSecondArmor != null) z.theSecondArmor.SetActive(state.Armor2Active);
            }
        }
        z.theFirstArmorType = (Zombie.FirstArmorType)state.Armor1Type;
        z.theSecondArmorType = (Zombie.SecondArmorType)state.Armor2Type;
        z.theFirstArmorBroken = state.Armor1Stage; z.theSecondArmorBroken = state.Armor2Stage;
        if (state.HandLost && !z.loseHand)
        {
            z.loseHand = true;
            z.FindAndDestoryZombieHand(z.gameObject);
            z.FindAndReplaceArmUpperSprite(z.gameObject);
            Detach(z, "LoseArm");
            GameAPP.PlaySound(7);
        }
        if (state.HeadLost && !z.beforeDying)
        {
            z.beforeDying = true;
            // LoseHeadEvent may explode, summon or attack. Only the host runs it.
            z.FindAndDestoryZombieHead(z.gameObject);
            Detach(z, "LoseHead");
            GameAPP.PlaySound(7);
        }
    }
    static void Detach(Zombie z, string name)
    {
        // AnimLoseActive reparents the particle; gather first so siblings cannot be skipped.
        var parts = new List<Transform>();
        for (int i = 0; i < z.transform.childCount; i++)
        {
            var child = z.transform.GetChild(i);
            if (child.name == name) parts.Add(child);
        }
        foreach (var part in parts) z.AnimLoseActive(part);
    }
    public static void RestoreHealth(Zombie z)
    {
        if (!Sync.IsClient || z == null || z.WasCollected || !Sync.TryZombieId(z, out int id) ||
            !replicas.TryGetValue(id, out var replica) || !replica.Desired.HasValue || replica.Death.HasValue) return;
        // Native FixedUpdate drains a headless zombie's HP directly, bypassing TakeDamage.
        // Restore the latest host value, including its live caption, after that local tick.
        long health = replica.Desired.Value.Health;
        if (z.theHealth != health) { z.theHealth = health; replica.CaptionState = null; RefreshCaptionSafely(replica); }
    }
}
