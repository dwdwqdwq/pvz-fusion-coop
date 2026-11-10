using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Steamworks;
namespace PvZCoop;
public static class Party
{
 public static string Status="Create a server or enter host IP:port.", Code="";
 public static bool Fake, Local=true, LocalHost, Busy;
 public static int Port=27650;
 public static string BindAddress="0.0.0.0";
 public static bool SteamReady=>false;
 public static bool InParty=>hosting||transport?.Connected==true;
 public static bool IsHost=>hosting;
 public static bool PartnerPresent=>transport?.Connected==true&&verified;
 public static string MyName=>IsHost?"Host":"Player 2";
 public static string PartnerName=>IsHost?"Player 2":"Host";
 public static CSteamID MyId=>CSteamID.Nil;
 public static CSteamID PartnerId=>CSteamID.Nil;
 public static CSteamID InviteLobby=CSteamID.Nil, InviteFrom=CSteamID.Nil;
 public static event Action<string,string> OnMessage;
 static TcpListener listener;
 static LanTransport transport;
 static Task<TcpClient> connecting;
 static bool hosting,verified;
 static DateTime deadline,lastReceive,lastPing;
 public static void Init(){}
 public static List<(CSteamID id,string name,bool inGame)> OnlineFriends()=>new();
 public static bool Invite(CSteamID id)=>false;
 public static void AcceptInvite(){}
 public static void DeclineInvite(){}
 public static void Create() {
  if(InParty||Busy)return;
  try { if(!IPAddress.TryParse(BindAddress,out var bind) || bind.AddressFamily!=AddressFamily.InterNetwork) {Status="Invalid server IPv4 address.";return;} listener=new TcpListener(bind,Port); listener.Start(1); hosting=LocalHost=true; Code=(bind.Equals(IPAddress.Any)?LocalAddress():bind.ToString())+":"+Port; Status="Waiting for player 2. Share "+Code; }
  catch(Exception e){Leave(); Status="Cannot host: "+e.Message;}
 }
 public static void Join(string address) {
  if(InParty||Busy)return;
  address=(address??"").Trim(); int split=address.LastIndexOf(':'); string host=split<0?address:address.Substring(0,split); int port=Port;
  if(host.Length==0||(split>=0&&(!int.TryParse(address.Substring(split+1),out port)||port<1||port>65535))) { Status="Enter host IP:port, e.g. 192.168.1.10:27650"; return; }
  Busy=true; Code=address; Status="Connecting to "+address+"..."; connecting=Connect(host,port);
 }
 static async Task<TcpClient> Connect(string host,int port) { var c=new TcpClient(); try { await c.ConnectAsync(host,port).WaitAsync(TimeSpan.FromSeconds(9)); return c; } catch {c.Dispose();throw;} }
 static void Attach(TcpClient c) { transport=new LanTransport(c); verified=false; lastReceive=lastPing=DateTime.UtcNow; deadline=DateTime.UtcNow.AddSeconds(10); transport.Send("handshake|fusion41-lan-2"); Status="Checking game version..."; }
 public static void Tick() {
  try {
   if(listener!=null&&listener.Pending()) { var c=listener.AcceptTcpClient(); if(transport!=null)c.Close();else Attach(c); }
   if(connecting?.IsCompleted==true) {var task=connecting;connecting=null;Busy=false;Attach(task.GetAwaiter().GetResult());}
   if(transport==null)return;
   int budget=256;
   while(budget-->0&&transport.TryReceive(out string message)) {
    lastReceive=DateTime.UtcNow; int split=message.IndexOf('|'); if(split<=0)throw new FormatException("Invalid message"); string type=message.Substring(0,split),payload=message.Substring(split+1);
    if(type=="handshake") { if(payload!="fusion41-lan-2")throw new InvalidOperationException("Both PCs need the same co-op version."); verified=true; Status="Connected. Host: close this panel and choose a normal level."; }
    else if(type=="ping"){}
    else if(type=="bye"){Disconnect("Partner disconnected.");return;}
    else if(verified) { try {OnMessage?.Invoke(type,payload);} catch(Exception e){Plugin.Logger.LogError("Message "+type+": "+e);Disconnect("Sync error. Return to menu and reconnect.");return;} }
   }
   if(!transport.Connected){Disconnect("Connection lost. Return to menu and reconnect.");return;}
   if((!verified&&DateTime.UtcNow>deadline)||DateTime.UtcNow-lastReceive>TimeSpan.FromSeconds(20)){Disconnect("Connection timed out.");return;}
   if(DateTime.UtcNow-lastPing>TimeSpan.FromSeconds(3)){lastPing=DateTime.UtcNow;transport.Send("ping|");}
  }catch(Exception e){Disconnect("Connection error: "+e.Message);}
 }
 static void Disconnect(string reason) {bool playing=Session.Active;Leave();Status=reason;if(playing){Session.ShowToast(reason,30);UnityEngine.Time.timeScale=0;}}
 public static void Send(string type,string payload){if(PartnerPresent)transport.Send(type+"|"+payload);}
 public static void SendFast(string type,string payload,bool reliable)=>Send(type,payload);
 public static void Leave() {
  transport?.Dispose();transport=null;listener?.Stop();listener=null;
  if(connecting!=null){_=connecting.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();else _=t.Exception;});connecting=null;}
  hosting=verified=Busy=false;Code="";Session.Reset();Unlocks.Clear();Status="Disconnected.";
 }
 public static void Shutdown()=>Leave();
 static string LocalAddress() {
  string fallback="127.0.0.1";
  foreach(var a in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
   if(a.OperationalStatus!=System.Net.NetworkInformation.OperationalStatus.Up || a.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Loopback)continue;
   var properties=a.GetIPProperties();
   foreach(var ip in properties.UnicastAddresses) {
    if(ip.Address.AddressFamily!=AddressFamily.InterNetwork || ip.Address.ToString().StartsWith("169.254."))continue;
    fallback=ip.Address.ToString();
    if(properties.GatewayAddresses.Count>0)return fallback;
   }
  }
  return fallback;
 }
}
