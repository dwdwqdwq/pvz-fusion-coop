using PvZCoop;
using System.Net;
int count=0;void Check(bool x,string m){if(!x)throw new Exception(m);Console.WriteLine("PASS "+m);count++;}
var packet=new GloveMovePacket(Guid.NewGuid().ToString("N"),2,2,4,0,5,2,4,300,true);
Check(GloveMovePacket.TryRead(packet.Encode(),out var decoded)&&decoded==packet,"native move identity, coordinates, health and maturity round trip");
foreach(string bad in new[]{"",packet.Encode()+"|extra",packet.Encode().Replace("|300|","|-1|"),packet.Encode()[..^1]+"2",new string('x',513)})Check(!GloveMovePacket.TryRead(bad,out _),"malformed move rejected");
using var host=new NetworkRoom();using var guest=new NetworkRoom();
var received=new List<(int id,string type,string body)>();var incoming=new List<(int id,string type,string body)>();
host.Message+=(id,t,b)=>received.Add((id,t,b));guest.Message+=(id,t,b)=>incoming.Add((id,t,b));
host.Host(IPAddress.Loopback,0);guest.Join("127.0.0.1",host.BoundPort);
async Task Pump(Func<bool> until){var deadline=DateTime.UtcNow.AddSeconds(8);while(!until()){host.Tick();guest.Tick();if(DateTime.UtcNow>deadline)throw new Exception("network timeout");await Task.Delay(2);}}
await Pump(()=>host.Members.Length==2&&guest.PlayerId==2);
guest.Send("round","epoch|gm|"+packet.Encode());await Pump(()=>received.Count==1);
Check(received[0]==(2,"round","epoch|gm|"+packet.Encode()),"guest move reaches host with authenticated sender");
host.Send("round","epoch|gma|"+packet.Encode());await Pump(()=>incoming.Count==1);
Check(incoming[0].id==1&&incoming[0].body.Contains("|gma|"),"host authoritative move outcome reaches guest");
guest.Send("round","epoch|gma|"+packet.Encode());await Pump(()=>!guest.Connected);
Check(received.Count==1,"guest cannot spoof authoritative move outcome");
var gate=new NativeDeathGate();gate.Enter(0);gate.Enter(1);Check(!gate.Exit(false),"nested death waits for outer callback");Check(gate.Exit(false)&&gate.ShouldPublish(true)&&gate.Reason==1,"native final death reason preserved");
var burn=new NativeDeathGate();burn.Enter(2);burn.Presented();burn.Exit(false);Check(!burn.ShouldPublish(true),"native burn does not duplicate generic death");
Console.WriteLine("COMPLETE "+count+" assertions");
