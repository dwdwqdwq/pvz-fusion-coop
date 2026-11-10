using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace PvZCoop;
public static class CoopArt {
 static Sprite bank,sun,body,badge,arrow,cardFrame,sunIcon;
 public static Sprite CardFrame => cardFrame ?? (cardFrame=Load("CoopUI.CardFrame.png"));
 public static Sprite SunIcon => sunIcon ?? (sunIcon=Sprite.Create(Bank.texture,new Rect(16,34,44,44),new Vector2(.5f,.5f),100f));
 public static Sprite Badge => badge ?? (badge=Load("CoopUI.SubPanel.png"));
 public static Sprite Arrow => arrow ?? (arrow=Load("CoopUI.Arrow.png"));
 static Sprite Load(string name) {
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
  if(stream==null)return Ui.White;
  using var bytes=new MemoryStream();stream.CopyTo(bytes);
  var t=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(t,bytes.ToArray());t.filterMode=FilterMode.Bilinear;
  return Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),100f);
 }
 public static Sprite Sun => sun ?? (sun=Sprite.Create(Bank.texture,new Rect(0,0,74,88),new Vector2(.5f,.5f),100f));
 public static Sprite Body => body ?? (body=Sprite.Create(Bank.texture,new Rect(74,0,374,88),new Vector2(.5f,.5f),100f));
 public static Sprite Bank { get {
  if(bank!=null)return bank;
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("CoopUI.TwoSeedBank.png");
  if(stream==null)return Ui.White;
  using var bytes=new MemoryStream();stream.CopyTo(bytes);
  var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);ImageConversion.LoadImage(texture,bytes.ToArray());
  texture.filterMode=FilterMode.Bilinear;
  bank=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100f);return bank;
 } }
}
