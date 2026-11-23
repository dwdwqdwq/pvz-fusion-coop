using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PvZCoop;

// One authoritative host, three guests. All sender IDs are assigned by the host;
// guests never choose an envelope sender or issue host-only game messages.
public sealed class NetworkRoom:IDisposable
{
 public const string Protocol="fusion41-coop-p0-atomic-v2";
 public const int Capacity=4;
 static readonly System.Diagnostics.Stopwatch Clock=System.Diagnostics.Stopwatch.StartNew();
 static double Now=>Clock.Elapsed.TotalSeconds;
 sealed class Peer {
  public int Id;public LanTransport Wire;public bool Verified;
  public DateTime Received=DateTime.UtcNow,Deadline=DateTime.UtcNow.AddSeconds(30);
  public double LastPing,LastRttLog,RejectAt;public string RejectReason;
  public readonly RttProbeWindow Rtt=new();
 }
 readonly List<Peer> peers=new();
 readonly SortedSet<int> members=new();
 TcpListener listener;Task<TcpClient> connecting;bool hosting;
 public int PlayerId {get;private set;}
 public bool IsHost=>hosting;
 public bool Busy=>connecting!=null;
 public bool Connected=>hosting||peers.Any(p=>p.Verified&&p.Wire.Connected);
 public int[] Members=>members.ToArray();
 public Func<bool> AdmissionLocked=()=>false;
 public Func<int,string,string,bool> AcceptMessage=(id,type,payload)=>true;
 public bool IsStalled=>peers.Any(p=>p.Verified&&DateTime.UtcNow-p.Received>TimeSpan.FromSeconds(4));
 public event Action<int,string,string> Message;
 public event Action RosterChanged;
 public event Action<int,string> Disconnected;
 public event Action<int,double> RttMeasured;
 public double RttSeconds(int player)=>player==PlayerId?0:peers.FirstOrDefault(p=>p.Verified&&p.Id==player)?.Rtt.EstimatedSeconds??.25;
 public string Status="Create a server or enter host IP:port.";
 public int BoundPort=>listener==null?0:((IPEndPoint)listener.LocalEndpoint).Port;
 static readonly HashSet<string> clientMessages=new(StringComparer.Ordinal){"inlevel","ready","deck_offer","sun_request","sun_commit","sun_place","sun_cancel","zverify","prize_request","sp","pd","mv","gm","cob","preview","cd","pause","resume","profile","battle_loaded","pause_request","restart_request","level_exit","next_wave_request"};
 static readonly HashSet<string> relayed=new(StringComparer.Ordinal){"inlevel","ready","pd","mv","gm","cob","preview","cd","pause","resume","battle_loaded"};
 public void Host(IPAddress address,int port){if(Connected||Busy)throw new InvalidOperationException("Already connected");listener=new TcpListener(address,port);listener.Start(Capacity);hosting=true;PlayerId=1;members.Add(1);Status="Waiting for teammates (1 / 4).";RosterChanged?.Invoke();}
 public void Join(string host,int port){if(Connected||Busy)throw new InvalidOperationException("Already connected");Status="Connecting...";connecting=Connect(host,port);}
 static async Task<TcpClient> Connect(string host,int port){var c=new TcpClient();try{await c.ConnectAsync(host,port).WaitAsync(TimeSpan.FromSeconds(9));return c;}catch{c.Dispose();throw;}}
 static void Reject(TcpClient c,string reason){using(c){c.SendTimeout=1000;byte[] body=System.Text.Encoding.UTF8.GetBytes("reject|"+reason),header=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header,body.Length);var stream=c.GetStream();stream.Write(header,0,4);stream.Write(body,0,body.Length);}}
 void Attach(TcpClient client,int id){var p=new Peer{Id=id,Wire=new LanTransport(client)};peers.Add(p);if(hosting)p.Wire.Send("hello|"+Protocol);}
 void Roster(){members.Clear();members.Add(1);foreach(var p in peers.Where(p=>p.Verified))members.Add(p.Id);string roster=string.Join(",",members);foreach(var p in peers.Where(p=>p.Verified))p.Wire.Send("roster|"+roster);Status=$"Connected: {members.Count} / 4. Host chooses a normal level.";RosterChanged?.Invoke();}
 public void Tick(){try{
  if(listener!=null)while(listener.Pending()){
   var c=listener.AcceptTcpClient();int id=Enumerable.Range(2,3).FirstOrDefault(n=>peers.All(p=>p.Id!=n));
   if(id==0||AdmissionLocked()){Reject(c,id==0?"Room is full (4 players).":"A level is already open. Join before the host chooses a level.");}else Attach(c,id);
  }
  if(connecting?.IsCompleted==true){var t=connecting;connecting=null;Attach(t.GetAwaiter().GetResult(),1);}
  foreach(var p in peers.ToArray()){
   if(!peers.Contains(p))continue;
   if(p.RejectReason!=null){if(Now>=p.RejectAt||!p.Wire.Connected)Drop(p,p.RejectReason);continue;}
   int budget=256;
   while(budget-->0&&p.Wire.TryReceive(out string frame)){
    p.Received=DateTime.UtcNow;int split=frame.IndexOf('|');if(split<1){Drop(p,"Invalid network message.");break;}string type=frame[..split],payload=frame[(split+1)..];
    if(type=="reject"){Status=payload;Drop(p,payload);break;}
    if(type=="hello"){
     if(payload!=Protocol){QueueRejection(p,"All players need the same P0 review build.");break;}
     if(p.Verified)continue;
     if(hosting&&AdmissionLocked()){QueueRejection(p,"The host already opened a level. Join before the level starts.");break;}
     p.Verified=true;if(!hosting)p.Wire.Send("hello|"+Protocol);if(hosting){p.Wire.Send("welcome|"+p.Id);Roster();}
     continue;
    }
    if(!p.Verified)continue;
    if(type=="ping"){
     if(payload.Length<=20&&long.TryParse(payload,out long nonce)&&nonce>0)p.Wire.Send("pong|"+nonce);
     continue;
    }
    if(type=="pong"){
     if(payload.Length<=20&&long.TryParse(payload,out long nonce)&&p.Rtt.Pong(nonce,Now)&&Now-p.LastRttLog>=5){p.LastRttLog=Now;RttMeasured?.Invoke(p.Id,p.Rtt.EstimatedSeconds);}
     continue;
    }
    if(type=="welcome"&&!hosting){if(!int.TryParse(payload,out int id)||id<2||id>4){Drop(p,"Invalid player slot.");break;}PlayerId=id;continue;}
    if(type=="roster"&&!hosting){var ids=payload.Split(',').Select(int.Parse).ToArray();if(ids.Length>4||ids.Distinct().Count()!=ids.Length||ids.Any(id=>id<1||id>4)||!ids.Contains(1)||!ids.Contains(PlayerId))throw new FormatException("Invalid roster");members.Clear();members.UnionWith(ids);Status=$"Connected: {members.Count} / 4. Host chooses a normal level.";RosterChanged?.Invoke();continue;}
    if(type=="bye"){Drop(p,"A player disconnected.");break;}
    if(type!="data")continue;int a=payload.IndexOf('|'),b=a<0?-1:payload.IndexOf('|',a+1);if(a<1||b<0)throw new FormatException("Invalid routed message");
    string verb=payload[(a+1)..b],body=payload[(b+1)..];
    if(hosting){
     string action=verb;
     if(verb=="round"){int c=body.IndexOf('|'),d=c<0?-1:body.IndexOf('|',c+1);action=d>c?body[(c+1)..d]:"";}
     if(payload[..a]!="0"||!clientMessages.Contains(action)){Drop(p,"Invalid client authority.");break;}
     int sender=p.Id;if(!AcceptMessage(sender,verb,body))continue;
     bool early=action=="ready"||action=="inlevel"||action=="battle_loaded";
     if(early)Broadcast(sender,verb,body,sender);
     Message?.Invoke(sender,verb,body);
     if(!early&&relayed.Contains(action))Broadcast(sender,verb,body,sender);
    }
    else{if(!int.TryParse(payload[..a],out int sender)||!members.Contains(sender)||sender==PlayerId)throw new FormatException("Invalid sender");Message?.Invoke(sender,verb,body);}
   }
   if(!peers.Contains(p)||p.RejectReason!=null)continue;
   if(!p.Wire.Connected){Drop(p,"Connection lost.");continue;}
   if((!p.Verified&&DateTime.UtcNow>p.Deadline)||DateTime.UtcNow-p.Received>TimeSpan.FromSeconds(60)){Drop(p,"Connection timed out.");continue;}
   if(p.Verified&&Now-p.LastPing>=1){p.LastPing=Now;p.Wire.Send("ping|"+p.Rtt.Probe(Now));}
  }
 }catch(Exception e){Status="Connection error: "+e.Message;Dispose();Disconnected?.Invoke(0,Status);}}
 void Broadcast(int sender,string type,string payload,int except=0){foreach(var p in peers.Where(p=>p.Verified&&p.Id!=except).ToArray())p.Wire.Send($"data|{sender}|{type}|{payload}");}
 public void Send(string type,string payload){if(hosting)Broadcast(1,type,payload);else foreach(var p in peers.Where(p=>p.Verified).ToArray())p.Wire.Send("data|0|"+type+"|"+payload);}
 public void SendTo(int player,string type,string payload){if(!hosting)throw new InvalidOperationException("Only host can target a player");var p=peers.FirstOrDefault(p=>p.Verified&&p.Id==player);p?.Wire.Send("data|1|"+type+"|"+payload);}
 void QueueRejection(Peer p,string reason){
  p.Wire.Send("reject|"+reason);p.RejectReason=reason;p.RejectAt=Now+.25;Status=reason;
 }
 void Drop(Peer p,string reason){if(!peers.Remove(p))return;p.Wire.Dispose();Status=reason;if(hosting)Roster();else{members.Clear();PlayerId=0;RosterChanged?.Invoke();}Disconnected?.Invoke(p.Id,reason);}
 public void Dispose(){foreach(var p in peers.ToArray())p.Wire.Dispose();peers.Clear();listener?.Stop();listener=null;if(connecting!=null){_=connecting.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();else _=t.Exception;});connecting=null;}hosting=false;PlayerId=0;members.Clear();}
}
