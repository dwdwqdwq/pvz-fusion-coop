using System;
using System.Collections.Generic;
namespace PvZCoop;
public sealed class SunLeaseQueue
{
    public readonly record struct Lease(int Player,int Token,Guid Operation,double Expires);
    readonly Queue<Lease> waiting=new();
    readonly HashSet<int> queuedPlayers=new();
    public int Owner {get;private set;}
    public int Token {get;private set;}
    public Guid Operation {get;private set;}
    public double Expires {get;private set;}
    public bool Busy=>Owner!=0;
    public int Waiting=>waiting.Count;
    public bool Request(int player,int token)=>Request(player,token,Guid.Empty,double.PositiveInfinity);
    public bool Request(int player,int token,Guid operation,double expires)
    {
        if(player<2||player>4||token<=0||double.IsNaN(expires)||Owner==player||!queuedPlayers.Add(player))return false;
        waiting.Enqueue(new(player,token,operation,expires));return true;
    }
    public bool GrantNext()=>GrantNext(0);
    public bool GrantNext(double now)
    {
        if(Busy)return false;
        while(waiting.Count>0)
        {
            var next=waiting.Dequeue();queuedPlayers.Remove(next.Player);
            if(next.Expires<=now)continue;
            Owner=next.Player;Token=next.Token;Operation=next.Operation;Expires=next.Expires;return true;
        }
        return false;
    }
    public bool Matches(int player,int token,Guid operation,double now)=>
        Busy&&Owner==player&&Token==token&&Operation==operation&&now<Expires;
    public Lease? Expire(double now)
    {
        if(!Busy||now<Expires)return null;
        var expired=new Lease(Owner,Token,Operation,Expires);Release();return expired;
    }
    public bool Complete(int player,int token)
    {
        if(player!=Owner||token!=Token||!Busy)return false;Release();return true;
    }
    public bool Cancel(int player,int token,Guid operation)
    {
        bool removed=false;
        if(Busy&&Owner==player&&Token==token&&Operation==operation){Release();removed=true;}
        var keep=waiting.ToArray();waiting.Clear();queuedPlayers.Clear();
        foreach(var item in keep)
            if(item.Player==player&&item.Token==token&&item.Operation==operation)removed=true;
            else{waiting.Enqueue(item);queuedPlayers.Add(item.Player);}
        return removed;
    }
    void Release(){Owner=Token=0;Operation=Guid.Empty;Expires=0;}
    public void Clear(){waiting.Clear();queuedPlayers.Clear();Release();}
}
