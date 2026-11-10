using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
namespace PvZCoop;
public sealed class LanTransport : IDisposable
{
 const int MaxFrame=4*1024*1024;
 readonly TcpClient client;
 readonly NetworkStream stream;
 readonly ConcurrentQueue<string> incoming=new();
 readonly BlockingCollection<string> outgoing=new(512);
 volatile bool closed;
 public bool Connected=>!closed;
 public LanTransport(TcpClient c) { client=c; c.NoDelay=true; c.SendTimeout=10000; stream=c.GetStream(); _=Task.Run(ReadLoop); _=Task.Run(WriteLoop); }
 public bool TryReceive(out string s)=>incoming.TryDequeue(out s);
 public void Send(string s) { if(closed)return; try { if(!outgoing.TryAdd(s))Dispose(); } catch(InvalidOperationException){Dispose();} }
 void ReadLoop() { try { var h=new byte[4]; while(!closed) { ReadExactly(h); int n=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(h); if(n<1||n>MaxFrame||incoming.Count>512)throw new IOException("Invalid frame"); var b=new byte[n]; ReadExactly(b); incoming.Enqueue(Encoding.UTF8.GetString(b)); } } catch { Dispose(); } }
 void ReadExactly(byte[] b) { int p=0; while(p<b.Length) { int n=stream.Read(b,p,b.Length-p); if(n==0)throw new EndOfStreamException(); p+=n; } }
 void WriteLoop() { try { foreach(var s in outgoing.GetConsumingEnumerable()) { var b=Encoding.UTF8.GetBytes(s); if(b.Length<1||b.Length>MaxFrame)throw new IOException("Frame too large"); var h=new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(h,b.Length); stream.Write(h,0,4); stream.Write(b,0,b.Length); } } catch { Dispose(); } }
 public void Dispose() { if(closed)return; closed=true; outgoing.CompleteAdding(); client.Close(); }
}
