using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using PvZCoop;
class Program
{
 static List<NetworkRoom> rooms=new();static int assertions;
 static void Check(bool result,string text){if(!result)throw new Exception(text);assertions++;Console.WriteLine("PASS "+text);}
 static async Task Pump(Func<bool> until,int ms=8000){var end=DateTime.UtcNow.AddMilliseconds(ms);while(!until()){if(DateTime.UtcNow>end)throw new Exception("Network test timed out");foreach(var r in rooms.ToArray())r.Tick();await Task.Delay(2);}for(int i=0;i<8;i++){foreach(var r in rooms.ToArray())r.Tick();await Task.Delay(2);}}
 static async Task Main(){try{
  using var host=new NetworkRoom();rooms.Add(host);host.Host(IPAddress.Loopback,0);
  var seen=new Dictionary<NetworkRoom,List<(int sender,string type,string payload)>>();
  void Watch(NetworkRoom r){seen[r]=new();r.Message+=(id,t,p)=>seen[r].Add((id,t,p));}
  Watch(host);var clients=new List<NetworkRoom>();
  for(int i=0;i<3;i++){var c=new NetworkRoom();clients.Add(c);rooms.Add(c);Watch(c);c.Join("127.0.0.1",host.BoundPort);await Pump(()=>c.Members.Length==i+2);}
  await Pump(()=>rooms.All(r=>r.Members.Length==4));
  Check(clients.Select(c=>c.PlayerId).OrderBy(x=>x).SequenceEqual(new[]{2,3,4}),"three guests get separate host-assigned IDs");
  Check(rooms.All(r=>r.Members.SequenceEqual(new[]{1,2,3,4})),"identical four-player roster on every PC");
  host.Send("level","41");await Pump(()=>clients.All(c=>seen[c].Any(m=>m.type=="level")));
  Check(clients.All(c=>seen[c].Single(m=>m.type=="level").sender==1),"host level broadcast reaches all three guests exactly once");
  clients[0].Send("ready","1|Україна");await Pump(()=>seen[host].Any(m=>m.type=="ready")&&clients.Skip(1).All(c=>seen[c].Any(m=>m.type=="ready")));
  Check(seen[clients[0]].All(m=>m.type!="ready")&&clients.Skip(1).All(c=>seen[c].Single(m=>m.type=="ready").sender==clients[0].PlayerId),"guest relay preserves sender without echo to origin");
  host.SendTo(clients[1].PlayerId,"sun_grant","1|300|1");await Pump(()=>seen[clients[1]].Any(m=>m.type=="sun_grant"));
  Check(clients.Where(c=>c!=clients[1]).All(c=>seen[c].All(m=>m.type!="sun_grant")),"sun grant targets one player, never all guests");
  clients[1].Send("sun_request","1");await Pump(()=>seen[host].Any(m=>m.type=="sun_request"));
  Check(clients.All(c=>seen[c].All(m=>m.type!="sun_request")),"wallet requests remain host-only");
  using var fifth=new NetworkRoom();rooms.Add(fifth);string rejection="";fifth.Disconnected+=(id,s)=>rejection=s;fifth.Join("127.0.0.1",host.BoundPort);await Pump(()=>rejection.Length>0);
  Check(!fifth.Connected&&rejection.Contains("full")&&host.Members.Length==4,"fifth player rejected without disturbing the four-player room");rooms.Remove(fifth);
  int removed=clients[2].PlayerId;clients[2].Dispose();rooms.Remove(clients[2]);await Pump(()=>host.Members.Length==3&&clients.Take(2).All(c=>c.Members.Length==3));Check(!host.Members.Contains(removed),"lobby disconnect updates every remaining roster");
  using var replacement=new NetworkRoom();rooms.Add(replacement);Watch(replacement);replacement.Join("127.0.0.1",host.BoundPort);await Pump(()=>rooms.All(r=>r.Members.Length==4));Check(replacement.PlayerId==removed,"released lobby slot can be reused");
  replacement.Send("go","");await Pump(()=>host.Members.Length==3);Check(!replacement.Connected,"guest cannot spoof the host start command");rooms.Remove(replacement);
  host.AdmissionLocked=()=>true;using var late=new NetworkRoom();rooms.Add(late);string locked="";late.Disconnected+=(id,s)=>locked=s;late.Join("127.0.0.1",host.BoundPort);await Pump(()=>locked.Length>0);Check(locked.Contains("level")&&host.Members.Length==3,"late join rejected while a level is open");
  var decks=new DeckReservations();decks.Offer(1,1,new[]{0,2});decks.Offer(2,1,new[]{0,1});decks.Offer(3,1,new[]{2,3});decks.Offer(4,1,new[]{1,3,4});
  Check(decks.Slots[2].SetEquals(new[]{1})&&decks.Slots[3].SetEquals(new[]{3})&&decks.Slots[4].SetEquals(new[]{4}),"four decks reserve normal and extra slots without duplication");
  decks.Offer(2,2,new[]{5});decks.Offer(2,1,new[]{6});Check(decks.Slots[2].SetEquals(new[]{5}),"stale deck revision cannot overwrite accepted selection");
  decks.Prune(new[]{1,3,4});decks.Offer(4,2,new[]{4,5});Check(decks.Slots[4].Contains(5),"disconnect releases card reservations");
  var leases=new SunLeaseQueue();Check(leases.Request(2,1)&&leases.Request(3,1)&&leases.Request(4,1),"identical request tokens are independent for three guests");Check(!leases.Request(3,2),"one outstanding wallet request per player");int balance=300;
  foreach(int id in new[]{2,3,4}){Check(leases.GrantNext()&&leases.Owner==id,"wallet queue grants FIFO player "+id);Check(!leases.Complete(id==2?3:2,1),"another player cannot commit token 1 for player "+id);balance-=100;Check(leases.Complete(id,1),"wallet commit attributed to correct player "+id);}
  Check(balance==0&&!leases.Busy&&leases.Waiting==0,"three serialized purchases cannot double-spend shared sun");

  host.AcceptMessage=(id,t,p)=>t!="round"||p.StartsWith("current|");
  int oldCount=seen[host].Count;
  clients[0].Send("round","obsolete|ready|1");
  for(int i=0;i<50;i++){foreach(var r in rooms)r.Tick();await Task.Delay(2);}
  Check(seen[host].Count==oldCount,"obsolete round ignored before host dispatch and relay");
  clients[0].Send("round","current|ready|1");
  await Pump(()=>seen[host].Any(m=>m.payload=="current|ready|1"));
  Check(clients[1].Connected,"current round envelope accepted without disconnect");
  var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();
  var socket=new System.Net.Sockets.TcpClient();var accepted=listener.AcceptTcpClientAsync();await socket.ConnectAsync(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);
  using(var sender=new LanTransport(socket))using(var receiver=new LanTransport(await accepted)){
   for(int n=0;n<3000;n++){sender.Send("data|1|round|epoch|zs|"+n);if(n%1000==0)sender.Send("event|"+n);}
   await Task.Delay(250);var frames=new List<string>();while(receiver.TryReceive(out string frame))frames.Add(frame);
   Check(sender.Connected&&receiver.Connected,"snapshot congestion does not overflow reliable action queue");
   Check(frames.Where(f=>f.StartsWith("event|")).SequenceEqual(new[]{"event|0","event|1000","event|2000"}),"reliable events survive congestion in exact order");
   Check(frames.Contains("data|1|round|epoch|zs|2999")&&frames.Count<20,"latest snapshot replaces thousands of obsolete positions");

   sender.Send("data|1|round|epoch|zs|recovery-position");sender.Send("data|1|round|epoch|pause_state|9|0|1|0|0");
   await Task.Delay(150);var recovered=new List<string>();while(receiver.TryReceive(out string f))recovered.Add(f);
   Check(recovered.FindIndex(f=>f.EndsWith("recovery-position"))>=0&&recovered.FindIndex(f=>f.Contains("pause_state"))>recovered.FindIndex(f=>f.EndsWith("recovery-position")),"latest position is applied before network pause resumes");
  }listener.Stop();

  var motion=new MotionBuffer();float previousX=10,maxStep=0,atGap=0,afterGap=0;bool forward=false;
  for(int frame=0;frame<300;frame++){float t=frame/60f;if(frame%6==0&&!(t>1&&t<1.7f))motion.Add(t,10-t*.2f,0);motion.Read(t,out float x,out _);if(frame>0){maxStep=Math.Max(maxStep,Math.Abs(x-previousX));if(x>previousX+.0001f)forward=true;}previousX=x;if(frame==75)atGap=x;if(frame==100)afterGap=x;}
  Check(!forward&&maxStep<.01f,"predicted zombie walking stays smooth during 700 ms snapshot gap");
  Check(afterGap<atGap-.04f,"walking continues locally instead of freezing between delayed packets");
  motion.Add(6,8.8f,0);motion.RebaseLatest();motion.Read(6,out float recoveredX,out _);Check(Math.Abs(recoveredX-8.8f)<.0001f,"recovery anchors the latest host position before leaving pause");
  Console.WriteLine("COMPLETE "+assertions+" assertions");
 }finally{foreach(var r in rooms)r.Dispose();}}
}
