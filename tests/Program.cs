using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using PvZCoop;
class Program {
 static async Task Main(){
  if(LanguageChoice.Normalize(null)!="en"||LanguageChoice.Normalize(" UK-ua ")!="uk"||LanguageChoice.Normalize("unsupported")!="en")throw new Exception("Language default/normalization failed");
  if(!LanguageChoice.IsSupported("en")||!LanguageChoice.IsSupported("uk")||LanguageChoice.IsSupported("unsupported"))throw new Exception("Language whitelist failed");
  Console.WriteLine("PASS: language defaults, aliases and unsupported-value handling");
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
  var a=new TcpClient();await a.ConnectAsync(IPAddress.Loopback,port);var b=await listener.AcceptTcpClientAsync();
  using var first=new LanTransport(a);using var second=new LanTransport(b);
  string large="lawn|"+new string('x',250000)+" Україна 植物";
  first.Send("level|0|2|-1|");first.Send(large);first.Send("go|");
  string[] expected={"level|0|2|-1|",large,"go|"};foreach(var e in expected){var m=await Receive(second);if(m!=e)throw new Exception("Lost or reordered frame");}
  for(int i=0;i<100;i++)second.Send("sp|"+i);
  for(int i=0;i<100;i++)if(await Receive(first)!="sp|"+i)throw new Exception("Ordering failed");
  Console.WriteLine("PASS: bidirectional TCP, ordered 100 messages, 250 KB payload, Unicode");
  first.Dispose();await Until(()=>!second.Connected);listener.Stop();Console.WriteLine("PASS: disconnect detection");
  listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;
  using var raw=new TcpClient();await raw.ConnectAsync(IPAddress.Loopback,port);using var receiver=new LanTransport(await listener.AcceptTcpClientAsync());
  byte[] frame=Encoding.UTF8.GetBytes("sp|1|2|0|0|1|0|0"),header=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header,frame.Length);
  foreach(byte v in header){raw.GetStream().WriteByte(v);await Task.Delay(2);}foreach(byte v in frame){raw.GetStream().WriteByte(v);await Task.Delay(2);}if(await Receive(receiver)!=Encoding.UTF8.GetString(frame))throw new Exception("Fragmented frame failed");
  raw.GetStream().Write(new byte[]{0x7f,0xff,0xff,0xff});await Until(()=>!receiver.Connected);listener.Stop();Console.WriteLine("PASS: fragmented frames and oversized-frame rejection");
 }
 static async Task<string> Receive(LanTransport t){var deadline=DateTime.UtcNow.AddSeconds(10);while(DateTime.UtcNow<deadline){if(t.TryReceive(out var s))return s;await Task.Delay(5);}throw new Exception("Receive timeout");}
 static async Task Until(Func<bool> done){var deadline=DateTime.UtcNow.AddSeconds(10);while(!done()){if(DateTime.UtcNow>deadline)throw new Exception("Timeout");await Task.Delay(5);}}
}
