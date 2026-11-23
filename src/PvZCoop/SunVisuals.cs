using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Only the host produces income. Replicas run Fusion's own smooth falling,
// flying and shrinking animation; they never run a plant's income callback.
public static class SunVisuals
{
    sealed class Drop
    {
        public int Id, Column, Row, Move;
        public CoinSun Coin;
        public bool Started;
        public string[] Initial;
        public GameObject Target;
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly Dictionary<IntPtr, Drop> drops = new();
    static readonly Dictionary<int, Drop> replicas = new();
    static readonly HashSet<int> seen = new();
    static int nextId;
    static bool creating;

    public static void Init() => Party.OnMessage += Receive;

    public static void Reset()
    {
        foreach (var drop in replicas.Values) Remove(drop);
        drops.Clear(); replicas.Clear(); seen.Clear(); nextId = 0; creating = false;
    }

    public static bool Created(GameObject go, int column, int row, int move)
    {
        if (!Sync.Running || PvP.Enabled || go == null) return false;
        var coin = go.GetComponent<CoinSun>();
        if (coin == null) return false;
        if (Party.IsHost)
        {
            drops[coin.Pointer] = new Drop { Id = ++nextId, Column = column, Row = row, Move = move, Coin = coin };
        }
        else if (!creating)
        {
            // Local sunflower timers are independent. Display only host drops.
            UnityEngine.Object.Destroy(go);
        }
        return true;
    }

    static string F(float value) => value.ToString("R", Inv);
    static float P(string value) => float.Parse(value, Inv);

    static string Serialize(Drop drop)
    {
        var coin = drop.Coin;
        var pos = coin.transform.position; var scale = coin.transform.localScale;
        var velocity = coin.velocity; var start = coin.startPosition;
        bool custom = coin.action != null && coin.target != null;
        var target = custom ? coin.target.position : Vector3.zero;
        return string.Join("|", drop.Id, drop.Column, drop.Row, coin.theCoinType, drop.Move,
            F(pos.x), F(pos.y), F(pos.z), F(velocity.x), F(velocity.y), F(start.x), F(start.y),
            coin.isLand ? "1" : "0", coin.sunPrice, coin.realCost, coin.save ? "1" : "0", coin.followValue,
            F(scale.x), F(scale.y), F(scale.z), custom ? "1" : "0", F(target.x), F(target.y), F(target.z));
    }

    public static void Started(CoinSun coin)
    {
        if (coin == null || !drops.TryGetValue(coin.Pointer, out var drop) || drop.Started) return;
        drop.Started = true;
        if (Party.IsHost)
        {
            Party.Send("sun_spawn", Serialize(drop));
            return;
        }
        // Start randomizes the arc and determines terrain landing. Restore the
        // host's actual starting state afterwards, using the local HUD target.
        var p = drop.Initial;
        coin.transform.position = new Vector3(P(p[5]), P(p[6]), P(p[7]));
        coin.velocity = new Vector2(P(p[8]), P(p[9]));
        coin.startPosition = new Vector2(P(p[10]), P(p[11]));
        coin.isLand = p[12] == "1"; coin.sunPrice = int.Parse(p[13]);
        coin.realCost = int.Parse(p[14]); coin.save = p[15] == "1"; coin.followValue = int.Parse(p[16]);
        coin.transform.localScale = new Vector3(P(p[17]), P(p[18]), P(p[19]));
        coin.action = null;
        if (p[20] == "1")
        {
            drop.Target = new GameObject("CoopSunTarget");
            drop.Target.transform.SetParent(Board.Instance.transform, false);
            drop.Target.transform.position = new Vector3(P(p[21]), P(p[22]), P(p[23]));
            coin.target = drop.Target.transform;
        }
        drop.Initial = null;
    }

    static void Remove(Drop drop)
    {
        if (drop.Coin != null && !drop.Coin.WasCollected) UnityEngine.Object.Destroy(drop.Coin.gameObject);
        if (drop.Target != null) UnityEngine.Object.Destroy(drop.Target);
    }

    static void Receive(string type, string payload)
    {
        if (!Sync.Running || Party.IsHost || Party.SenderId != 1 || PvP.Enabled) return;
        if (type != "sun_spawn") return;
        var p = payload.Split('|');
        if (p.Length != 24 || !int.TryParse(p[0], out int id) || id <= 0 || seen.Contains(id)) return;
        // Validate before creating a native item or recording its ID.
        foreach (int i in new[] { 0, 1, 2, 3, 4, 12, 13, 14, 15, 16, 20 }) if (!int.TryParse(p[i], out _)) return;
        foreach (int i in new[] { 5, 6, 7, 8, 9, 10, 11, 17, 18, 19, 21, 22, 23 })
            if (!float.TryParse(p[i], NumberStyles.Float, Inv, out float value) || !float.IsFinite(value)) return;
        creating = true;
        try
        {
            var go = CreateItem.Instance.SetCoin(int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]),
                new Vector3(P(p[5]), P(p[6]), P(p[7])), true);
            var coin = go == null ? null : go.GetComponent<CoinSun>();
            if (coin == null) { if (go != null) UnityEngine.Object.Destroy(go); return; }
            var drop = new Drop { Id = id, Coin = coin, Initial = p };
            seen.Add(id); replicas[id] = drop; drops[coin.Pointer] = drop;
        }
        finally { creating = false; }
    }

    public static void Tick()
    {
        if (!Sync.Running) return;
        foreach (var pair in drops.ToArray())
        {
            var drop = pair.Value;
            if (drop.Coin != null && !drop.Coin.WasCollected) continue;
            drops.Remove(pair.Key);
            // Each replica finishes its native animation. A host removal event
            // arriving alongside a delayed spawn would otherwise erase it before
            // the guest ever sees it. The wallet remains host-authoritative.
            if (!Party.IsHost) { replicas.Remove(drop.Id); if (drop.Target != null) UnityEngine.Object.Destroy(drop.Target); }
        }
    }
}
