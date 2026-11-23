using GameLevel;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PvZCoop;

[HarmonyPatch]
public static class Patches
{
    [HarmonyPatch(typeof(CardUI), "Start")]
    [HarmonyPostfix]
    private static void NativeCardStarted(CardUI __instance)=>HostPlanting.MarkStarted(__instance);

    [HarmonyPatch(typeof(CardUI), "UseOnce")]
    [HarmonyPostfix]
    private static void NativeCardUsed(CardUI __instance)=>HostPlanting.ObserveUse(__instance);

    [HarmonyPatch(typeof(Input), "get_mousePosition")]
    [HarmonyPrefix]
    private static bool RemotePlantPointer(ref Vector3 __result)=>!HostPlanting.TryMousePosition(ref __result);

    [HarmonyPatch(typeof(TMPro.TextMeshProUGUI), "OnPreRenderCanvas")]
    [HarmonyPostfix]
    private static void TeammateNameRender(TMPro.TextMeshProUGUI __instance)=>PartnerBank.FinishNameRender(__instance);

	[HarmonyPatch(typeof(UIMgr), "EnterGame")]
	[HarmonyPrefix]
	private static bool EnterGame(LevelType levelType, int levelNumber, int id, string name)
	{
		return Session.OnEnterGame(levelType, levelNumber, id, name);
	}

	[HarmonyPatch(typeof(UIMgr), "EnterMainMenu")]
	[HarmonyPrefix]
	private static void EnterMainMenu()
	{
		Session.OnBackToMenu();
	}

    [HarmonyPatch(typeof(BaseMenu), "BackToMainMenu")]
    [HarmonyPrefix]
    private static void NativeBackToMenu()=>Session.OnBackToMenu();

	[HarmonyPatch(typeof(UIMgr), "BackToMenu")]
	[HarmonyPrefix]
	private static void BackToMenu()
	{
		Session.OnBackToMenu();
	}

	[HarmonyPatch(typeof(SeedLibrary), "OnStartGame")]
	[HarmonyPrefix]
	private static bool SeedLibraryStart(SeedLibrary __instance)
	{
		return Session.OnStartClicked(null, delegate
		{
			__instance.OnStartGame();
		}, "SeedLibrary.OnStartGame");
	}

	[HarmonyPatch(typeof(BoardSpawner), "SummonZombies")]
	[HarmonyPrefix]
	private static bool SummonPre(int __0)
	{
		if (PvP.Enabled && !Session.Allow)
		{
			return false;
		}
		if (Session.Active && (!Session.BattleLive || Sync.GuestRound))return false;
		Sync.OnSummonStart(__0);
		return true;
	}

	[HarmonyPatch(typeof(BoardSpawner), "SummonZombies")]
	[HarmonyPostfix]
	private static void SummonPost()
	{
		Sync.OnSummonEnd();
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPostfix]
	private static void ZombieCreated(int __0, ZombieType __1, float __2, bool __3, Zombie __result)
	{
		Sync.OnZombieCreated(__result, __0, __1, __2, __3);
	}

    [HarmonyPatch(typeof(Zombie), "Update")]
    [HarmonyPostfix]
    private static void GuestZombieAnimation(Zombie __instance)=>Sync.ApplyZombieAnimation(__instance);

    [HarmonyPatch(typeof(Zombie), "FixedUpdate")]
    [HarmonyPostfix]
    private static void GuestZombieHealth(Zombie __instance)=>ZombieVisuals.RestoreHealth(__instance);

    [HarmonyPatch(typeof(Zombie), "TakeDamage")]
    [HarmonyPostfix]
    private static void ZombieVisualChange(Zombie __instance)=>ZombieVisuals.HostChanged(__instance);

    [HarmonyPatch(typeof(EffectManager), "SetEffect", new[]{typeof(Zombie),typeof(EffectType),typeof(float),typeof(float)})]
    [HarmonyPrefix]
    private static bool GuestEffectAuthority(Zombie __0,EffectType __1,ref bool __result){
        if(ZombieEffects.AllowLocal(__0,__1))return true;
        __result=false;return false;
    }
    [HarmonyPatch(typeof(EffectManager), "SetEffect", new[]{typeof(Zombie),typeof(EffectType),typeof(float),typeof(float)})]
    [HarmonyPostfix]
    private static void HostEffect(Zombie __0,EffectType __1)=>ZombieEffects.HostChanged(__0,__1);
    [HarmonyPatch(typeof(Entity), "RemoveBuff")]
    [HarmonyPostfix]
    private static void HostEffectRemoved(Entity __instance,EffectType __0)=>ZombieEffects.HostChanged(__instance.TryCast<Zombie>(),__0);

	[HarmonyPatch(typeof(Zombie), "TakeDamage")]
	[HarmonyPrefix]
	private static bool ZombieDamage(Zombie __instance)
	{
		if (Sync.IsClient && !ZombieDeathEffects.IsReplayTarget(__instance))
		{
			return !Sync.IsSynced(__instance);
		}
		return true;
	}

	[HarmonyPatch(typeof(Zombie), "Charred")]
	[HarmonyPrefix]
	private static bool ZombieCharred(Zombie __instance,int __0,PlantType __1,bool __2,DamageType __3,out Sync.CharredCall __state)
	{
        __state=Sync.BeginCharred(__instance,__0,__1,__2,__3);
		if (Sync.IsClient && !ZombieDeathEffects.IsReplayTarget(__instance))
		{
			return !Sync.IsSynced(__instance);
		}
		return true;
	}

    [HarmonyPatch(typeof(Zombie), "Charred")]
    [HarmonyFinalizer]
    private static void ZombieCharredFinished(Sync.CharredCall __state,System.Exception __exception)=>Sync.EndCharred(__state,__exception);
    [HarmonyPatch(typeof(Zombie), "OnAnimatorMove")]
    [HarmonyPatch(typeof(Zombie), "MoveXUpdate")]
    [HarmonyPatch(typeof(Zombie), "MoveYUpdate")]
    [HarmonyPrefix]
    private static bool RemoteZombiePosition(Zombie __instance)=>Sync.LocalZombieMotion(__instance);

	[HarmonyPatch(typeof(Plant), "TakeDamage")]
	[HarmonyPrefix]
	private static bool PlantDamage(Il2CppObjectBase __1)
	{
		if (Sync.IsClient && !Session.Allow)
		{
			return false;
		}
		if (PvP.Enabled && __1 != null)
		{
			Zombie zombie = __1.TryCast<Zombie>();
			if (zombie != null && !zombie.isMindControlled)
			{
				PvP.OnBite();
			}
		}
		return true;
	}

	[HarmonyPatch(typeof(GameLose), "ProcessZombieEnter")]
	[HarmonyPrefix]
	private static bool ZombieEnter(Zombie __0)
	{
        if(Sync.GuestRound)return false;
		if (!PvP.Enabled || Session.Allow)
		{
			return true;
		}
		if (__0 != null && !__0.isMindControlled && Sync.Running && Party.IsHost)
		{
			PvP.Finish(PvP.Team.Attack);
		}
		return false;
	}

	[HarmonyPatch(typeof(UIMgr), "EnterLoseMenu")]
	[HarmonyPrefix]
	private static bool LoseMenu()
	{
        if(Party.InParty&&!Party.IsHost)return Sync.ApplyingHostResult || Sync.HostLossAccepted;
		if (!PvP.Enabled || Session.Allow)
		{
			return true;
		}
		if (Sync.Running && Party.IsHost)
		{
			PvP.Finish(PvP.Team.Attack);
		}
		return false;
	}

	[HarmonyPatch(typeof(DroppedItem), "Start")]
	[HarmonyPrefix]
	private static bool DroppedItemStart(DroppedItem __instance)
	{
		if (!PvP.IsAttacker)
		{
			return true;
		}
		Object.Destroy(__instance.gameObject);
		return false;
	}

	[HarmonyPatch(typeof(Zombie), "DropItem")]
	[HarmonyPrefix]
	private static bool DropItem()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Zombie), "DropKilledAward")]
	[HarmonyPrefix]
	private static bool DropAward()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(SaveMgr), "LoadBoard")]
	[HarmonyPrefix]
	private static bool LoadBoard()
	{
		return !PvP.Enabled && !Sync.GuestRound;
	}

	[HarmonyPatch(typeof(SaveMgr), "LoadPlant")]
	[HarmonyPrefix]
	private static bool LoadPlant()
	{
		return !PvP.Enabled && !Sync.GuestRound;
	}

	[HarmonyPatch(typeof(SaveMgr), "SaveBoard")]
	[HarmonyPostfix]
	private static void SaveBoard()
	{
		if (PvP.Enabled)
		{
			PvP.WipeSavedLawn();
		}
	}

	[HarmonyPatch(typeof(CreatePlant), "LimTravel")]
	[HarmonyPostfix]
	private static void LimTravel(PlantType theSeedType, ref bool __result)
	{
		if (PvP.Enabled)
		{
			__result = false;
		}
	}

	[HarmonyPatch(typeof(Board), "NewZombieUpdate")]
	[HarmonyPrefix]
	private static bool WaveClock()
	{
		return !PvP.Enabled && !Sync.GuestRound && (!Session.Active||Session.BattleLive);
	}

    [HarmonyPatch(typeof(Board), "StartNextWave")]
    [HarmonyPrefix]
    private static bool NextWave()=>!Session.Active||Session.BattleLive&&!Sync.GuestRound;
    [HarmonyPatch(typeof(Board), "RequestNextWave")]
    [HarmonyPrefix]
    private static bool RequestWave(){
        if(!Session.Active)return true;
        if(!Session.BattleLive)return false;
        if(!Sync.GuestRound)return true;
        Party.Send("next_wave_request","");return false;
    }
    [HarmonyPatch(typeof(Board), "ToggleAutoNextWave")]
    [HarmonyPrefix]
    private static bool ToggleWave(){
        if(!Session.Active)return true;
        if(!Session.BattleLive)return false;
        if(!Sync.GuestRound)return true;
        Party.Send("next_wave_request","toggle");return false;
    }
    [HarmonyPatch(typeof(Board), "HugeWaveEvent")]
    [HarmonyPrefix]
    private static bool HugeWave(int __0)=>WaveSync.HugeWave(__0);

	[HarmonyPatch(typeof(Mouse), "ClickZombieCard")]
	[HarmonyPrefix]
	private static bool ClickZombieCard(IZECard __0)
	{
		return PvP.CanPickZombieCard(__0);
	}

	[HarmonyPatch(typeof(ZombieDataManager), "GetZombieData")]
	[HarmonyPostfix]
	private static void ZombieData(ZombieDataManager.ZombieData __result)
	{
		Prices.Apply(__result);
		PvP.PriceZombie(__result);
	}

	[HarmonyPatch(typeof(PlantDataManager), "GetPlantData")]
	[HarmonyPostfix]
	private static void PlantData(PlantDataManager.PlantData __result)
	{
		Prices.Apply(__result);
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetZombieByCard")]
	[HarmonyPrefix]
	private static bool PlaceZombiePre(Mouse __instance, out (int sun, int cost, bool placing) __state)
	{
		IZECard theIZECardOnMouse = __instance.theIZECardOnMouse;
		__state = (sun: (Board.Instance != null) ? Board.Instance.theSun : 0, cost: (theIZECardOnMouse != null) ? theIZECardOnMouse.theSeedCost : 0, placing: false);
		bool flag = PvP.OnPlaceZombie(__instance);
		__state.placing = flag && PvP.Placing;
		return flag;
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetZombieByCard")]
	[HarmonyPostfix]
	private static void PlaceZombiePost((int sun, int cost, bool placing) __state)
	{
		PvP.AfterPlaceZombie(__state.sun, __state.cost, __state.placing);
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPrefix]
	private static bool SetZombieLocal(ref Zombie __result)
	{
		if (!Sync.BlockLocalZombie())
		{
			return true;
		}
		__result = null;
		return false;
	}

	[HarmonyPatch(typeof(BungiZombie), "SetTarget")]
	[HarmonyPostfix]
	private static void BungeeTarget(BungiZombie __instance)
	{
		Sync.OnBungeeTarget(__instance);
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombie")]
	[HarmonyPrefix]
	private static void SetZombiePre(ref bool __3)
	{
		if (PvP.Placing)
		{
			__3 = false;
		}
	}

	[HarmonyPatch(typeof(CreateZombie), "SetZombieWithMindControl")]
	[HarmonyPrefix]
	private static bool SetZombieMind(CreateZombie __instance, int __0, ZombieType __1, float __2, ref Zombie __result)
	{
		if (!PvP.Placing)
		{
			return true;
		}
		__result = __instance.SetZombie(__0, __1, __2, false);
		return false;
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPrefix]
	private static bool ShovelAllowed()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Glove), "Use")]
	[HarmonyPrefix]
	private static bool GloveAllowed()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Shovel), "OnUpdate")]
	[HarmonyPrefix]
	private static bool ShovelUpdate()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(Glove), "OnUpdate")]
	[HarmonyPrefix]
	private static bool GloveUpdate()
	{
		return !PvP.IsAttacker;
	}

	[HarmonyPatch(typeof(InGameTool), "Update")]
	[HarmonyPrefix]
	private static bool ToolUpdate(InGameTool __instance)
	{
		if (PvP.IsAttacker)
		{
			if (!(__instance.TryCast<Shovel>() != null))
			{
				return !(__instance.TryCast<Glove>() != null);
			}
			return false;
		}
		return true;
	}

	[HarmonyPatch(typeof(Shovel), "OnClick")]
	[HarmonyPrefix]
	private static bool ShovelClick(ref bool __result)
	{
		if (!PvP.IsAttacker)
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(InGameTool), "OnClick")]
	[HarmonyPrefix]
	private static bool ToolClick(InGameTool __instance, ref bool __result)
	{
		if (!PvP.IsAttacker || (__instance.TryCast<Shovel>() == null && __instance.TryCast<Glove>() == null))
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(Plant), "Crashed")]
	[HarmonyPrefix]
	private static bool PlantCrashed()
	{
		if (Sync.IsClient)
		{
			return Session.Allow;
		}
		return true;
	}

	[HarmonyPatch(typeof(PlantDataManager), "IsUnlocked")]
	[HarmonyPostfix]
	private static void IsUnlocked(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(PlantDataManager), "CheckIfPlantUnlock")]
	[HarmonyPostfix]
	private static void CheckIfPlantUnlock(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(AdvantureConfig), "CheckPlantUnlock")]
	[HarmonyPostfix]
	private static void AdvCheckPlantUnlock(PlantType __0, ref bool __result)
	{
		bool? flag = Unlocks.Override(__0);
		if (flag.HasValue)
		{
			__result = flag.Value;
		}
	}

	[HarmonyPatch(typeof(CreatePlant), "SetPlant")]
	[HarmonyPrefix]
	private static void SetPlantPre(int __0, int __1, PlantType __2, Plant __3, Vector2 __4, bool __5, bool __6)
	{
		PvP.Placing_Plant++;
		Sync.OnSetPlantPre(__0, __1, __2, __4, __5, __6, __3);
	}

	[HarmonyPatch(typeof(CreatePlant), "SetPlant")]
	[HarmonyPostfix]
	private static void SetPlantPost(Plant __result)
	{
		if (PvP.Placing_Plant > 0)
		{
			PvP.Placing_Plant--;
		}
		Sync.OnSetPlantPost(__result);
        HostPlanting.ObservePlant(__result);
	}

	[HarmonyPatch(typeof(Plant), "Die")]
	[HarmonyPrefix]
	private static bool PlantDie(Plant __instance, Plant.DieReason __0)
	{
        if(Sync.IsClient && Session.BattleLive && !Session.Allow && !Sync.IsLocalPlayerAction && PlantIdentity.IsTracked(__instance))return false;
		Sync.OnPlantDie(__instance, __0);
        return true;
	}

	[HarmonyPatch(typeof(Plant), "MoveBox")]
	[HarmonyPrefix]
	private static void PlantMove(Plant __instance, int __0, int __1)
	{
		Sync.OnPlantMove(__instance, __0, __1);
	}

	[HarmonyPatch(typeof(Mouse), "MouseClick")]
	[HarmonyPrefix]
	private static bool ClickPre()
	{
        if(PartnerBank.Hovering || !SharedSun.InterceptMouseClick())return false;
		Sync.BeginAction();
        return true;
	}

	[HarmonyPatch(typeof(Mouse), "MouseClick")]
	[HarmonyPostfix]
	private static void ClickPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByCard")]
	[HarmonyPrefix]
	private static bool PlantCardPre(out bool __state)
	{
		__state=SharedSun.RequestPlanting();
        if(__state)Sync.BeginAction();return __state;
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByCard")]
	[HarmonyFinalizer]
	private static void PlantCardPost(bool __state)
	{
		if(__state)Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByGlove")]
	[HarmonyPrefix]
	private static void GlovePre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Mouse), "TryToSetPlantByGlove")]
	[HarmonyPostfix]
	private static void GlovePost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "PutDownItem")]
	[HarmonyPrefix]
	private static void PutDownPre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Mouse), "PutDownItem")]
	[HarmonyPostfix]
	private static void PutDownPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPrefix]
	private static void ShovelPre()
	{
		Sync.BeginAction();
	}

	[HarmonyPatch(typeof(Shovel), "Use")]
	[HarmonyPostfix]
	private static void ShovelPost()
	{
		Sync.EndAction();
	}

	[HarmonyPatch(typeof(Mouse), "ClickOnCard")]
	[HarmonyPrefix]
	private static bool ClickOnCard(CardUI __0)
	{
		return !PartnerBank.IsPartnerCard(__0) && SharedSun.SelectCard(__0,false);
	}

	[HarmonyPatch(typeof(Mouse), "ClickCard")]
	[HarmonyPrefix]
	private static bool ClickCard(CardUI __0, ref bool __result)
	{
		if (!PartnerBank.IsPartnerCard(__0) && SharedSun.SelectCard(__0,true))
		{
			return true;
		}
		__result = false;
		return false;
	}

	[HarmonyPatch(typeof(CreateItem), "SetCoin")]
	[HarmonyPostfix]
	private static void CoinCreated(GameObject __result, int __0, int __1, int __3)
	{
		if (SunVisuals.Created(__result, __0, __1, __3)) return;
		Sync.OnCoinCreated(__result);
	}

    [HarmonyPatch(typeof(CoinSun), "Start")]
    [HarmonyPostfix]
    private static void SunStarted(CoinSun __instance) => SunVisuals.Started(__instance);

	[HarmonyPatch(typeof(Board), "GetSun")]
	[HarmonyPrefix]
	private static bool GetSun(ref float __0, bool __1)
	{
		return Sync.OnGetSun(ref __0, __1);
	}

	[HarmonyPatch(typeof(Mower), "StartMove")]
	[HarmonyPrefix]
	private static bool MowerStart(Mower __instance)
	{
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.OnMowerStart(__instance);
		return true;
	}

    [HarmonyPatch(typeof(InGameUI), "PauseGame")]
    [HarmonyPrefix]
    private static bool NativePauseClick()=>CoopPause.Intercept(true);

	[HarmonyPatch(typeof(UIMgr), "EnterPauseMenu")]
	[HarmonyPrefix]
	private static bool Paused()=>CoopPause.Intercept(true);

	[HarmonyPatch(typeof(UIMgr), "BackToGame")]
	[HarmonyPrefix]
	private static bool Resumed()=>CoopPause.Intercept(false);

    [HarmonyPatch(typeof(BoardVictory), "OnUpdate")]
    [HarmonyPrefix]
    private static bool GuestVictoryClock(Board __0,out bool __state){
        __state=Sync.IsHostRunning&&__0!=null&&Board.Instance!=null&&__0.Pointer==Board.Instance.Pointer&&!__0.over;
        return !(Party.InParty&&!Party.IsHost)&&!(Session.Active&&!Session.Started);
    }

    [HarmonyPatch(typeof(BoardVictory), "OnUpdate")]
    [HarmonyPostfix]
    private static void NaturalVictory(Board __0,bool __state){
        if(__state&&Party.IsHost&&__0!=null&&__0.over&&!Sync.ResultSent)Sync.SendResult(true);
    }

    [HarmonyPatch(typeof(Lawnf),"SetAward",new[]{typeof(Board),typeof(Vector2),typeof(bool),typeof(bool)})]
    [HarmonyPrefix]
    private static bool AwardAuthority(bool __3,ref PrizeMgr __result){
        if(!__3&&Sync.GuestRound&&!Sync.ApplyingHostResult&&!VictorySync.Won){__result=null;return false;}
        return true;
    }
    [HarmonyPatch(typeof(Lawnf),"SetAward",new[]{typeof(Board),typeof(Vector2),typeof(bool),typeof(bool)})]
    [HarmonyPostfix]
    private static void AwardCreated(Board __0,bool __3,PrizeMgr __result){
        if(!__3&&__result!=null&&Sync.IsHostRunning&&__0!=null&&__0.over&&!Sync.ResultSent)Sync.SendResult(true);
    }
    [HarmonyPatch(typeof(PrizeMgr),"Start")]
    [HarmonyPostfix]
    private static void PrizeReady(PrizeMgr __instance)=>VictorySync.PrizeStarted(__instance);
    [HarmonyPatch(typeof(PrizeMgr),"Clicked")]
    [HarmonyPrefix]
    private static bool CollectPrize(PrizeMgr __instance,out bool __state)=>VictorySync.BeforeCollect(__instance,out __state);
    [HarmonyPatch(typeof(PrizeMgr),"Clicked")]
    [HarmonyPostfix]
    private static void PrizeCollected(bool __state){if(__state)CampaignProgress.SendNow();}
    [HarmonyPatch(typeof(PrizeMgr),"GoBack")]
    [HarmonyPrefix]
    private static bool GuestPrizeTransition(PrizeMgr __instance)=>VictorySync.AllowGoBack(__instance);
    [HarmonyPatch(typeof(LevelProgress),"Update")]
    [HarmonyPostfix]
    private static void GuestLevelProgress(LevelProgress __instance)=>WaveSync.ApplyProgress(__instance);

	[HarmonyPatch(typeof(BoardVictory), "Win")]
	[HarmonyPrefix]
	private static bool Win(BoardVictory __instance)
	{
        if(Session.Active && Board.Instance?.boardVicotry!=null && __instance.Pointer!=Board.Instance.boardVicotry.Pointer)return false;
        if(Party.InParty&&!Party.IsHost)return Sync.ApplyingHostResult;
        if(Session.Active&&!Session.Started)return false;
        if(Session.Active && Sync.ResultSent)return false;
		if (PvP.Enabled && !Session.Allow)
		{
			return false;
		}
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.SendResult(win: true);
		return true;
	}

	[HarmonyPatch(typeof(GameLose), "HandleGameLose")]
	[HarmonyPrefix]
	private static bool Lose()
	{
        if(Party.InParty&&!Party.IsHost)return Sync.ApplyingHostResult;
        if(Session.Active&&!Session.Started)return false;
		if (PvP.Enabled && !Session.Allow)
		{
			if (Sync.Running && Party.IsHost)
			{
				PvP.Finish(PvP.Team.Attack);
			}
			return false;
		}
		if (!Sync.Running || Session.Allow)
		{
			return true;
		}
		if (Sync.IsClient)
		{
			return false;
		}
		Sync.SendResult(win: false);
		return true;
	}

    [HarmonyPatch(typeof(CardUI),"OnMouseDown")]
    [HarmonyPrefix]
    private static bool DeckCardClick(CardUI __instance)=>DeckSharing.BeforeClick(__instance);
    [HarmonyPatch(typeof(CardUI),"OnMouseDown")]
    [HarmonyPostfix]
    private static void DeckCardClicked(CardUI __instance)=>DeckSharing.AfterClick(__instance);

    [HarmonyPatch(typeof(CardSlotManager),"HandleExtraCardSibling")]
    [HarmonyPrefix]
    private static bool ExtraSibling(CardUI __0)=>DeckSharing.KeepNativeSibling(__0);
    [HarmonyPatch(typeof(InGameUI),"AddCardToBank")]
    [HarmonyPrefix]
    private static bool AddDeckCard(CardUI __0,ref bool __result){if(DeckSharing.BeforeAdd(__0))return true;__result=false;return false;}
    [HarmonyPatch(typeof(InGameUI),"AddCardToBank")]
    [HarmonyPostfix]
    private static void AddedDeckCard(CardUI __0,bool __result){if(__result){if(DeckSharing.Active&&__0!=null)__0.isSelected=true;DeckSharing.Changed();}}
    [HarmonyPatch(typeof(InGameUI),"RemoveCardFromBank")]
    [HarmonyPostfix]
    private static void RemovedDeckCard(CardUI __0,bool __result){if(__result){if(DeckSharing.Active&&__0!=null)__0.isSelected=false;DeckSharing.Changed();}}

	[HarmonyPatch(typeof(InGameBtn), "OnMouseUpAsButton")]
	[HarmonyPrefix]
	private static bool InGameBtnClick(InGameBtn __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUpAsButton();
		}, "InGameBtn");
	}

	[HarmonyPatch(typeof(UIButton), "OnMouseUpAsButton")]
	[HarmonyPrefix]
	private static bool UIButtonClick(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUpAsButton();
		}, "UIButton");
	}

	[HarmonyPatch(typeof(UIButton), "OnMouseUp")]
	[HarmonyPrefix]
	private static bool UIButtonUp(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnMouseUp();
		}, "UIButton.OnMouseUp");
	}

	[HarmonyPatch(typeof(UIButton), "OnClicked")]
	[HarmonyPrefix]
	private static bool UIButtonClicked(UIButton __instance)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnClicked();
		}, "UIButton.OnClicked");
	}

	[HarmonyPatch(typeof(TheButton), "OnPointerClick")]
	[HarmonyPrefix]
	private static bool TheButtonClick(TheButton __instance, PointerEventData eventData)
	{
		return Session.OnStartClicked(__instance.gameObject, delegate
		{
			__instance.OnPointerClick(eventData);
		}, "TheButton");
	}
}

