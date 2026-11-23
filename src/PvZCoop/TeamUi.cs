using UnityEngine;
using TMPro;
namespace PvZCoop;
public static class TeamUi
{
 public static Rect TabRect(int index)=>new Rect(18+index*220,150,212,60);
 static readonly Btn[] tabs=new Btn[3];
 static readonly TMP_Text[] names=new TMP_Text[3],hints=new TMP_Text[3];
 public static Color ColorFor(int id)=>id switch{1=>new Color(1,.45f,.35f),2=>new Color(1,.88f,.3f),3=>new Color(.3f,.9f,1),_=>new Color(.85f,.55f,1)};
 public static void Tick()
 {
  if(Ui.Root==null||!Screens.Built)return;int[] remote=Party.RemotePlayers;bool show=Sync.Running&&!Sync.ResultSent&&!Sync.ResultApplied&&remote.Length>1;
  for(int i=0;i<3;i++){
   if(tabs[i]==null){int index=i;tabs[i]=Ui.Button(Ui.Root,TabRect(i),"",()=>{var ids=Party.RemotePlayers;if(index<ids.Length)Party.ViewPlayer(ids[index]);},"buttonsmall",18);tabs[i].Label.text="";
    names[i]=Ui.Text(tabs[i].Rt,new Rect(10,4,192,31),"",24,Ui.Pale);Ukrainian.MarkInput(names[i]);
    names[i].enableAutoSizing=true;names[i].fontSizeMin=20;names[i].fontSizeMax=24;names[i].enableWordWrapping=false;names[i].overflowMode=TextOverflowModes.Ellipsis;
    hints[i]=Ui.Text(tabs[i].Rt,new Rect(10,31,192,17),"",14,Ui.Pale);hints[i].enableWordWrapping=false;hints[i].overflowMode=TextOverflowModes.Ellipsis;}
   bool active=show&&i<remote.Length;Ui.Show(tabs[i].Rt,active);if(!active)continue;
   int id=remote[i];names[i].text=Party.DisplayName(id);names[i].color=ColorFor(id);hints[i].text=(LanguageSelection.UkrainianEnabled?Ukrainian.CardsHint(TeamState.For(id).Deck.Count).Split('—')[0].Trim():TeamState.For(id).Deck.Count+" cards");tabs[i].SetColor(ColorFor(id));
   var pointer=new Vector2(Input.mousePosition.x/Ui.Scale,(Screen.height-Input.mousePosition.y)/Ui.Scale);
   if(TabRect(i).Contains(pointer))Party.ViewPlayer(id);
  }
 }
}
