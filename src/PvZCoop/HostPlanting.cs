using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PvZCoop;

// Opt-in host adapter. The orchestrator owns operation IDs, outgoing-message
// buffering, leases and uncertain native exceptions. No native-layout reflection.
public static class HostPlanting
{
    public sealed record Outcome(bool Consumed,int PlantId,bool Picked=false);
    sealed record Shadow(CardUI Card);
    readonly record struct NativeKey(IntPtr Pointer,int Instance);
    static readonly Dictionary<NativeKey,NativeKey> started=new();
    static readonly Dictionary<(int Player,int Key),Shadow> cards=new();
    static readonly List<Plant> created=new();
    static GameObject root;
    static CardUI executingCard;
    static bool observedUse;
    static Vector3 screenPosition;
    static int lastTickFrame=-1;
    public static bool ExecutionActive {get;private set;}
    public static string LastFailure {get;private set;}="";
    public static IReadOnlyList<Plant> CreatedPlants=>created;

    static bool Live(UnityEngine.Object value)=>value!=null&&!value.WasCollected;
    static NativeKey Key(UnityEngine.Object value)=>new(value.Pointer,value.GetInstanceID());
    public static void MarkStarted(CardUI card)
    {
        if(!Live(card))return;
        var board=card.board;
        started[Key(card)]=Live(board)?Key(board):default;
    }
    public static bool SupportedMode()
    {
        if(HoverGardenManager.IsLevel){LastFailure="HoverGarden held-placement state is not synchronized.";return false;}
        if(TravelRogueManager.IsLevel){LastFailure="TravelRogue equipment state is not synchronized.";return false;}
        return true;
    }
    static bool OwnedSlot(int player,int key)=>player>=1&&player<=4&&Party.Members.Contains(player)&&
        (player==Party.PlayerId?Session.MySlotKeys:TeamState.For(player).Slots).Contains(key);
    public static CardUI GetCard(int player,int key)
    {
        LastFailure="";
        if(!Party.IsHost||!Sync.Running||!Live(Board.Instance)||!OwnedSlot(player,key))
        {LastFailure="Card slot is not in the player's authoritative deck.";return null;}
        if(!SupportedMode())return null;
        if(cards.TryGetValue((player,key),out var cached))
        {
            // A limited card destroyed by its native action is spent, not a
            // reason to manufacture another full card on a repeated request.
            if(Live(cached.Card))return cached.Card;
            LastFailure="The native card was consumed or removed.";return null;
        }
        var library=SeedLibrary.Instance;
        if(!Live(library)){LastFailure="Native card library is not available.";return null;}
        var prototype=library.GetComponentsInChildren<CardUI>(true)
            .FirstOrDefault(c=>Live(c)&&c.theSeedType>=0&&DeckSharing.Key(c)==key&&!PartnerBank.IsPartnerCard(c));
        if(!Live(prototype)){LastFailure="Exact native card variant is not available.";return null;}
        // Ordinary native prefabs also serialize travel/data objects. Mode
        // ownership, finite treasure durability and an actual linked target
        // distinguish special state; non-null alone rejects normal packets.
        // Unlimited cards do not mutate data in native UseOnce (4.1 RVA43F878).
        if(prototype.hoverGardenCard!=null||(prototype.data!=null&&prototype.Durability>=0)||prototype.p_plant!=null)
        {LastFailure="Special card equipment/action state is not synchronized: travel="+(prototype.travelRogueCard!=null)+
            ", garden="+(prototype.hoverGardenCard!=null)+", data="+(prototype.data!=null)+
            ", linkedCard="+(prototype.p_card!=null)+", linkedPlant="+(prototype.p_plant!=null);return null;}
        bool initialized=started.TryGetValue(Key(prototype),out var initializedBoard);
        if(initialized&&initializedBoard!=Key(Board.Instance))
        {LastFailure="Native card prototype was initialized for another board.";return null;}
        if(!Live(root)){root=new GameObject("Coop host native card state");root.SetActive(false);}
        GameObject copy=null;
        try
        {
            // Inactive parent prevents Unity from scheduling Awake/Start/Update
            // while the adapter prepares and then manually maintains this card.
            copy=UnityEngine.Object.Instantiate(prototype.gameObject,root.transform,false);
            copy.SetActive(false);
            var card=copy.GetComponent<CardUI>();
            if(!Live(card)||card.transform.childCount<4)
                throw new InvalidOperationException("Native card hierarchy is incomplete.");
            card.board=Board.Instance;card.onDestroy=null;card.plantAction=null;
            card.p_card=null;card.p_plant=null;
            // Awake/Start normally bind these runtime-only UI references.
            // Instantiate under an inactive root does not copy that binding;
            // use the same child indices as native Awake (1) and Start (2).
            card.text=card.transform.GetChild(1).GetComponent<TMPro.TextMeshProUGUI>();
            card.slider=card.transform.GetChild(2).GetComponent<UnityEngine.UI.Slider>();
            // Start initializes the cloned slider and native per-level costs.
            // A copy of an already initialized prototype must not apply those
            // price/cooldown modifiers a second time.
            if(!initialized){card.Awake();card.Start();}
            else
            {
                // Preserve the host's already derived per-level values. Running
                // Start again would apply modifiers twice just to bind UI.
                card.theSeedType=prototype.theSeedType;card.thePlantType=prototype.thePlantType;
                card.theZombieType=prototype.theZombieType;card.isExtra=prototype.isExtra;
                card.theSeedCost=prototype.theSeedCost;card.fullCD=prototype.fullCD;
                card.thePlantLevel=prototype.thePlantLevel;card.maxUsedTimes=prototype.maxUsedTimes;
                card.useLimited=prototype.useLimited;card.Durability=prototype.Durability;
                card.MaxDurability=prototype.MaxDurability;card.nightAddCost=prototype.nightAddCost;
                card.snowAddCost=prototype.snowAddCost;
            }
            MarkStarted(card);
            card.onDestroy=null;card.plantAction=null;card.board=Board.Instance;
            card.disabled=false;card.isPickUp=false;card.isSelected=false;card.onCardBank=true;
            card.CD=card.fullCD;
            if(card.slider==null)throw new InvalidOperationException("Native card slider is missing.");
            cards[(player,key)]=new(card);
            return card;
        }
        catch
        {
            if(Live(copy))UnityEngine.Object.Destroy(copy);
            throw;
        }
    }
    public static void Tick()
    {
        if(!Party.IsHost||!Session.BattleLive||!Sync.Running||CoopPause.IsPaused||ExecutionActive||lastTickFrame==Time.frameCount)return;
        lastTickFrame=Time.frameCount;
        foreach(var state in cards.Values.ToArray())
        {
            var card=state.Card;
            if(!Live(card)||card.disabled||card.Durability==0)continue;
            card.CDUpdate();
        }
    }
    public static void Reset()
    {
        if(ExecutionActive)throw new InvalidOperationException("Cannot reset during a native planting action.");
        foreach(var state in cards.Values)
            if(Live(state.Card))started.Remove(Key(state.Card));
        cards.Clear();created.Clear();executingCard=null;observedUse=false;LastFailure="";lastTickFrame=-1;
        if(Live(root))UnityEngine.Object.Destroy(root);
        root=null;
        // Keep native-library Start markers: Reset may run while the same
        // library still exists. Pointer+Unity instance identity prevents reuse.
    }
    public static bool TryMousePosition(ref Vector3 result)
    {
        if(!ExecutionActive)return false;
        result=screenPosition;return true;
    }
    public static void ObserveUse(CardUI card)
    {
        if(ExecutionActive&&Live(card)&&Live(executingCard)&&Key(card)==Key(executingCard))observedUse=true;
    }
    public static void ObservePlant(Plant plant)
    {
        if(!ExecutionActive||!Live(plant)||created.Any(p=>Live(p)&&Key(p)==Key(plant)))return;
        created.Add(plant);
    }
    static Outcome Reject(string reason){LastFailure=reason;return new(false,0);}
    public static Outcome Execute(int player,int key,int column,int row,int kind=0,int direction=1)
    {
        if(ExecutionActive)throw new InvalidOperationException("Nested host planting execution is not supported.");
        LastFailure="";created.Clear();
        if(!Party.IsHost||!Sync.Running||!Session.BattleLive||CoopPause.IsPaused)
            return Reject("The host battle is not accepting planting actions.");
        var board=Board.Instance;var mouse=Mouse.Instance;
        if(kind<0||kind>2||!Enum.IsDefined(typeof(Towards),direction))return Reject("Invalid native card action or direction.");
        if(!Live(board)||!Live(mouse)||column<0||row<0||column>=board.columnNum||row>=board.rowNum)
            return Reject("Target cell is outside the current board.");
        var card=GetCard(player,key);
        if(!Live(card))return new(false,0);
        if(card.disabled||card.Durability==0||
           (card.useLimited&&card.usedTimes>=card.maxUsedTimes+(TypeMgr.IsPuff(card.thePlantType)?2:0)))
            return Reject("Native card has no remaining uses.");
        card.Update();mouse.ChangeCost(card.thePlantType,card);
        if(card.theSeedCost<0||!float.IsFinite(card.CD)||!float.IsFinite(card.fullCD)||card.fullCD<0)
            return Reject("Native card price or cooldown is invalid.");
        if(card.CD<card.fullCD)return Reject("Native card is recharging.");
        if(board.theSun<card.theSeedCost)return Reject("Not enough shared sun.");
        var camera=Camera.main;
        if(!Live(camera))return Reject("Board camera is not available.");
        float x=mouse.GetBoxXFromColumn(column),y=mouse.GetLandY(x,row);
        if(!float.IsFinite(x)||!float.IsFinite(y))return Reject("Native cell position is invalid.");
        var saved=new MouseState(mouse);
        int sunBefore=board.theSun,usesBefore=card.usedTimes;float cdBefore=card.CD;
        observedUse=false;executingCard=card;
        screenPosition=camera.WorldToScreenPoint(new Vector3(x,y,0));
        ExecutionActive=true;
        try
        {
            mouse.theMouseColumn=column;mouse.theMouseRow=row;
            mouse.theBoxXofMouse=x;mouse.theBoxYofMouse=y;mouse.mouseX=x;mouse.mouseY=y;
            mouse.theCardOnMouse=card;mouse.thePlantTypeOnMouse=card.thePlantType;
            mouse.plantTowards=(Towards)direction;mouse.theZombieTypeOnMouse=(ZombieType)(-1);
            mouse.mouseItemType=(MouseItemType)1;
            mouse.theItemOnMouse=null;mouse.preview=null;mouse.theIZECardOnMouse=null;
            mouse.thePlantOnGlove=null;mouse.theZombieOnGlove=null;mouse.theGardenPlantOnGlove=null;mouse.cannonPlant=null;
            mouse.board=board;
            // Native code handles spending, CD, upgrades and use limits. Never
            // apply Board.UseSun or CardUI.UseOnce again in the orchestrator.
            if(kind==1)mouse.ClickOnCard(card);
            else if(kind==2)mouse.ClickCard(card);
            else mouse.TryToSetPlantByCard();
            bool picked=kind==1&&Live(mouse.theCardOnMouse)&&Key(mouse.theCardOnMouse)==Key(card)&&card.isPickUp;
            bool consumed=observedUse||board.theSun!=sunBefore||
                (Live(card)&&(card.usedTimes!=usesBefore||card.CD<cdBefore));
            int primary=0;
            foreach(var plant in created)
                if(Live(plant)){primary=PlantIdentity.GetOrAssignHostId(plant);if(primary>0)break;}
            if(primary>0&&!consumed)throw new InvalidOperationException("Native planting created a plant without consumption confirmation.");
            if(!consumed)LastFailure="Native placement did not consume the card.";
            return new(consumed,primary,picked);
        }
        finally
        {
            // Clear the scope even when native placement throws after a partial
            // mutation. The caller must not turn such an exception into refund.
            try
            {
                // A native selection may allocate a private cursor preview.
                // Detach/destroy it before restoring the host's actual objects.
                if(kind!=0)
                {
                    if(Live(mouse.theItemOnMouse))UnityEngine.Object.Destroy(mouse.theItemOnMouse);
                    if(Live(mouse.preview)&&mouse.preview!=mouse.theItemOnMouse)UnityEngine.Object.Destroy(mouse.preview);
                    if(Live(card))card.PutDown();
                }
            }
            finally{try{saved.Restore(mouse);}finally{ExecutionActive=false;executingCard=null;observedUse=false;}}
        }
    }
    sealed class MouseState
    {
        readonly int row,column;readonly float boxX,boxY,x,y;
        readonly PlantType plantType;readonly ZombieType zombieType;readonly Towards towards;
        readonly MouseItemType itemType;readonly GameObject item,preview;readonly CardUI card;readonly IZECard ize;
        readonly Plant plantGlove,cannon;readonly Zombie zombieGlove;readonly ZenGarden.GardenPlant gardenGlove;readonly Board board;
        public MouseState(Mouse m)
        {
            row=m.theMouseRow;column=m.theMouseColumn;boxX=m.theBoxXofMouse;boxY=m.theBoxYofMouse;x=m.mouseX;y=m.mouseY;
            plantType=m.thePlantTypeOnMouse;zombieType=m.theZombieTypeOnMouse;towards=m.plantTowards;itemType=m.mouseItemType;
            item=m.theItemOnMouse;preview=m.preview;card=m.theCardOnMouse;ize=m.theIZECardOnMouse;
            plantGlove=m.thePlantOnGlove;zombieGlove=m.theZombieOnGlove;gardenGlove=m.theGardenPlantOnGlove;cannon=m.cannonPlant;board=m.board;
        }
        public void Restore(Mouse m)
        {
            if(!Live(m))return;
            m.theMouseRow=row;m.theMouseColumn=column;m.theBoxXofMouse=boxX;m.theBoxYofMouse=boxY;m.mouseX=x;m.mouseY=y;
            m.thePlantTypeOnMouse=plantType;m.theZombieTypeOnMouse=zombieType;m.plantTowards=towards;m.mouseItemType=itemType;
            m.theItemOnMouse=item;m.preview=preview;m.theCardOnMouse=card;m.theIZECardOnMouse=ize;
            m.thePlantOnGlove=plantGlove;m.theZombieOnGlove=zombieGlove;m.theGardenPlantOnGlove=gardenGlove;m.cannonPlant=cannon;m.board=board;
        }
    }
}
