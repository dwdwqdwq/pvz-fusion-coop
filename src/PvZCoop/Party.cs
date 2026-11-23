using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Steamworks;
namespace PvZCoop;
public static class Party
{
 public static string Status="Create a server or enter host IP:port.",Code="";
 public static bool Fake,Local=true,LocalHost;
 public static int Port=27650;
 public static string BindAddress="0.0.0.0";
 static NetworkRoom room;
 public static bool Busy=>room?.Busy==true;
 public static bool SteamReady=>false;
 public static bool InParty=>room?.Connected==true;
 public static bool IsHost=>room?.IsHost==true;
 public static int PlayerId=>room?.PlayerId??0;
 public static int SenderId {get;private set;}
 public static int[] Members=>room?.Members??Array.Empty<int>();
 public static int[] RemotePlayers=>Members.Where(id=>id!=PlayerId).ToArray();
 public static int PlayerCount=>Members.Length;
 public static bool PartnerPresent=>RemotePlayers.Length>0;
 public static int ViewedPlayer {get;private set;}
 public static int PartnerPlayer=>RemotePlayers.Contains(ViewedPlayer)?ViewedPlayer:RemotePlayers.FirstOrDefault();
 static readonly Dictionary<int,string> names=new();static string sentName="";static bool namesDirty;
 public static string RoundId {get;private set;}="";
 public static bool ConnectionSlow=>room?.IsStalled==true;
 public static double EstimatedRttSeconds(int player)=>room?.RttSeconds(player)??.25;
 public static string PlayerName(int id)=>id==PlayerId?PlayerNames.Clean(GameAPP.playerName,id==1?"Host":"Player "+id):(names.TryGetValue(id,out var name)?name:(id==1?"Host":"Player "+id));
 public static string DisplayName(int id)=>PlayerNames.Short(PlayerName(id));
 public static string MyName=>PlayerName(PlayerId);
 public static string PartnerName=>DisplayName(SenderId>0&&SenderId!=PlayerId?SenderId:PartnerPlayer);
 public static CSteamID MyId=>CSteamID.Nil;
 public static CSteamID PartnerId=>CSteamID.Nil;
 public static CSteamID InviteLobby=CSteamID.Nil,InviteFrom=CSteamID.Nil;
 public static event Action<string,string> OnMessage;
 public static event Action RosterChanged;
 public static void ViewPlayer(int id){if(!RemotePlayers.Contains(id)||ViewedPlayer==id)return;ViewedPlayer=id;PartnerBank.Clear();if(Sync.Running){PartnerBank.Build();PartnerBank.SetCooldowns(TeamState.For(id).Cooldowns);}}
 public static void Init(){}
 public static List<(CSteamID id,string name,bool inGame)> OnlineFriends()=>new();
 public static bool Invite(CSteamID id)=>false;
 public static void AcceptInvite(){}
 public static void DeclineInvite(){}
 static bool Accept(int sender,string type,string payload)=>type=="profile"||(type=="round"&&RoundId.Length>0&&payload.StartsWith(RoundId+"|",StringComparison.Ordinal));
 static void Dispatch(int sender,string type,string payload){
  if(type=="profile"&&IsHost){if(payload.Length>1024)throw new FormatException("Invalid profile");names[sender]=PlayerNames.Clean(Encoding.UTF8.GetString(Convert.FromBase64String(payload)),"Player "+sender);namesDirty=true;return;}
  if(type=="profiles"&&!IsHost&&sender==1){foreach(var item in payload.Split(';',StringSplitOptions.RemoveEmptyEntries)){int split=item.IndexOf(':');int id=int.Parse(item[..split]);if(!Members.Contains(id))continue;names[id]=PlayerNames.Clean(Encoding.UTF8.GetString(Convert.FromBase64String(item[(split+1)..])),"Player "+id);}return;}
  if(type=="level"){
   if(IsHost||sender!=1)return;int cut=payload.IndexOf('|');if(cut!=32||!Guid.TryParseExact(payload[..cut],"N",out _))throw new FormatException("Invalid round");RoundId=payload[..cut];payload=payload[(cut+1)..];
  }else if(type=="round"){
   if(!payload.StartsWith(RoundId+"|",StringComparison.Ordinal)||RoundId.Length==0)return;
   payload=payload[(RoundId.Length+1)..];int cut=payload.IndexOf('|');if(cut<1)return;type=payload[..cut];payload=payload[(cut+1)..];
  }else if(type!="unl")return;
  SenderId=sender;try{OnMessage?.Invoke(type,payload);}finally{SenderId=0;}
 }
 static NetworkRoom NewRoom(){var r=new NetworkRoom();r.AdmissionLocked=()=>Session.Active;r.AcceptMessage=Accept;r.Message+=Dispatch;r.RttMeasured+=(id,seconds)=>Plugin.Logger.LogInfo("peer "+id+" RTT="+(seconds*1000).ToString("F0",System.Globalization.CultureInfo.InvariantCulture)+"ms");r.RosterChanged+=()=>{TeamState.Prune();foreach(int id in names.Keys.Where(id=>!Members.Contains(id)).ToArray())names.Remove(id);namesDirty=true;RosterChanged?.Invoke();};r.Disconnected+=(id,why)=>{Status=why;if(Session.Active){Leave();Session.ShowToast(why+" Return to menu and reconnect.",30);UnityEngine.Time.timeScale=0;}};return r;}
 public static void Create(){if(InParty||Busy)return;try{if(!IPAddress.TryParse(BindAddress,out var bind)||bind.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork){Status="Invalid server IPv4 address.";return;}room?.Dispose();room=NewRoom();room.Host(bind,Port);LocalHost=true;Code=(bind.Equals(IPAddress.Any)?LocalAddress():bind.ToString())+":"+Port;Status=room.Status;}catch(Exception e){Leave();Status="Cannot host: "+e.Message;}}
 public static void Join(string address){if(InParty||Busy)return;address=(address??"").Trim();int split=address.LastIndexOf(':');string host=split<0?address:address[..split];int port=Port;if(host.Length==0||(split>=0&&(!int.TryParse(address[(split+1)..],out port)||port<1||port>65535))){Status="Enter host IP:port, e.g. 192.168.1.10:27650";return;}room?.Dispose();room=NewRoom();Code=address;room.Join(host,port);Status=room.Status;}
 public static void Tick(){
  var current=room;if(current==null)return;current.Tick();if(!ReferenceEquals(room,current))return;Status=current.Status;
  if(PlayerId>0&&Members.Length>0){string name=MyName;if(name!=sentName){sentName=name;if(IsHost){names[1]=name;namesDirty=true;}else room.Send("profile",Convert.ToBase64String(Encoding.UTF8.GetBytes(name)));}}
  if(IsHost&&namesDirty){namesDirty=false;room.Send("profiles",string.Join(";",Members.Select(id=>id+":"+Convert.ToBase64String(Encoding.UTF8.GetBytes(PlayerName(id))))));}
  CoopPause.SetNetworkHold(Session.BattleLive&&ConnectionSlow);
 }
 static (string type,string body) Wrap(string type,string payload){
  if(type=="level"){RoundId=Guid.NewGuid().ToString("N");return(type,RoundId+"|"+payload);}
  if(type=="unl")return(type,payload);
  return("round",RoundId+"|"+type+"|"+payload);
 }
 public static void Send(string type,string payload){if(SharedSun.BufferOutgoing(0,type,payload)||room==null)return;var frame=Wrap(type,payload);room.Send(frame.type,frame.body);}
 public static void SendTo(int player,string type,string payload){if(SharedSun.BufferOutgoing(player,type,payload)||room==null)return;var frame=Wrap(type,payload);room.SendTo(player,frame.type,frame.body);}
 public static void SendFast(string type,string payload,bool reliable)=>Send(type,payload);
 public static void Leave(){var old=room;room=null;old?.Dispose();LocalHost=false;SenderId=ViewedPlayer=0;Code="";RoundId=sentName="";names.Clear();namesDirty=false;Session.Reset();TeamState.Clear();Unlocks.Clear();Status="Disconnected.";}
 public static void Shutdown()=>Leave();
 static string LocalAddress(){string fallback="127.0.0.1";foreach(var a in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()){if(a.OperationalStatus!=System.Net.NetworkInformation.OperationalStatus.Up||a.NetworkInterfaceType==System.Net.NetworkInformation.NetworkInterfaceType.Loopback)continue;var properties=a.GetIPProperties();foreach(var ip in properties.UnicastAddresses){if(ip.Address.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork||ip.Address.ToString().StartsWith("169.254."))continue;fallback=ip.Address.ToString();if(properties.GatewayAddresses.Count>0)return fallback;}}return fallback;}
}
