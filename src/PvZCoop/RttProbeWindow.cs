using System;
using System.Collections.Generic;
using System.Linq;

namespace PvZCoop;

// A pong is useful only if its nonce belongs to this exact peer/socket.
// Several probes coexist: RTT may exceed the one-second heartbeat period.
public sealed class RttProbeWindow
{
    readonly SortedDictionary<long,double> sent=new();
    long next;
    public bool HasSample {get;private set;}
    public double SmoothedSeconds {get;private set;}
    public double EstimatedSeconds=>HasSample?SmoothedSeconds:.25;
    public int Pending=>sent.Count;
    public long Probe(double now)
    {
        if(!double.IsFinite(now))throw new ArgumentOutOfRangeException(nameof(now));
        foreach(var stale in sent.Where(p=>now-p.Value>10).Select(p=>p.Key).ToArray())sent.Remove(stale);
        while(sent.Count>=8)sent.Remove(sent.First().Key);
        long nonce=checked(++next);sent.Add(nonce,now);return nonce;
    }
    public bool Pong(long nonce,double now)
    {
        if(!double.IsFinite(now)||!sent.TryGetValue(nonce,out double began)||now<began)return false;
        sent.Remove(nonce);double sample=now-began;if(sample>10)return false;
        SmoothedSeconds=HasSample?SmoothedSeconds*.875+sample*.125:sample;
        HasSample=true;return true;
    }
}
