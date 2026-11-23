using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace PvZCoop;
public sealed class LanTransport:IDisposable {
 const int MaxFrame=4*1024*1024;
 readonly TcpClient client;readonly NetworkStream stream;
 readonly ConcurrentQueue<string> incoming=new();readonly BlockingCollection<string> outgoing=new(512);
 readonly ConcurrentDictionary<string,string> inStates=new(),outStates=new();readonly AutoResetEvent wake=new(false);
 static readonly HashSet<string> stateTypes=new(){"zs","lawn","wave","cd","preview","tut","mws","mine_state","chomper_state","plant_roster","plant_state"};
 volatile bool closed;public bool Connected=>!closed;
 public LanTransport(TcpClient c){client=c;c.NoDelay=true;c.SendTimeout=60000;stream=c.GetStream();_=Task.Run(ReadLoop);_=Task.Run(WriteLoop);}
 static string StateKey(string frame){var p=frame.Split('|',6);return p.Length==6&&p[0]=="data"&&p[2]=="round"&&stateTypes.Contains(p[4])?p[1]+"|"+p[3]+"|"+p[4]:null;}
 static bool ResumeBarrier(string frame){var p=frame.Split('|',6);if(p.Length!=6||p[0]!="data"||p[2]!="round"||p[4]!="pause_state")return false;var state=p[5].Split('|');return state.Length>=2&&state[1]=="0";}
 void FlushOutgoing(){foreach(var item in outStates)if(outStates.TryRemove(item.Key,out var frame))Write(frame);}
 void FlushIncoming(){foreach(var item in inStates)if(inStates.TryRemove(item.Key,out var frame))incoming.Enqueue(frame);}
 public bool TryReceive(out string s){if(incoming.TryDequeue(out s))return true;foreach(var item in inStates)if(inStates.TryRemove(item.Key,out s))return true;s=null;return false;}
 public void Send(string s){if(closed)return;try{var key=StateKey(s);if(key!=null)outStates[key]=s;else if(!outgoing.TryAdd(s)){Dispose();return;}wake.Set();}catch(InvalidOperationException){Dispose();}}
 void ReadLoop(){try{var h=new byte[4];while(!closed){ReadExactly(h);int n=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(h);if(n<1||n>MaxFrame||incoming.Count>512||inStates.Count>128)throw new IOException("Invalid frame");var b=new byte[n];ReadExactly(b);string frame=Encoding.UTF8.GetString(b),key=StateKey(frame);if(key!=null)inStates[key]=frame;else{if(ResumeBarrier(frame))FlushIncoming();incoming.Enqueue(frame);}}}catch{Dispose();}}
 void ReadExactly(byte[] b){int p=0;while(p<b.Length){int n=stream.Read(b,p,b.Length-p);if(n==0)throw new EndOfStreamException();p+=n;}}
 void Write(string s){var b=Encoding.UTF8.GetBytes(s);if(b.Length<1||b.Length>MaxFrame)throw new IOException("Frame too large");var h=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(h,b.Length);stream.Write(h,0,4);stream.Write(b,0,b.Length);}
 void WriteLoop(){try{while(!closed){bool wrote=false;while(outgoing.TryTake(out var frame)){if(ResumeBarrier(frame))FlushOutgoing();Write(frame);wrote=true;}foreach(var item in outStates)if(outStates.TryRemove(item.Key,out var frame)){Write(frame);wrote=true;}if(!wrote)wake.WaitOne(1000);}}catch{Dispose();}}
 public void Dispose(){if(closed)return;closed=true;outgoing.CompleteAdding();wake.Set();client.Close();}
}
